using Bogus;
using IamMock.Contracts.Models;
using IamMock.Contracts.Requests;

namespace IamMock.Api.Data;

/// <summary>
/// In-memory store of deterministically generated fake IAM data. The seed data is built
/// once with a fixed seed; mutations (create/update/delete, role/license assignment) are
/// applied in-memory and are lost on restart. All access is guarded by a simple lock.
/// </summary>
public sealed class MockDataStore
{
    private const int Seed = 8675309;
    private readonly object _lock = new();

    private readonly List<Tenant> _tenants;
    private readonly List<Role> _roles;
    private readonly List<User> _users;
    private readonly List<License> _licenses;

    public MockDataStore()
    {
        // Make Bogus deterministic across runs.
        Randomizer.Seed = new Random(Seed);
        var faker = new Faker("en");

        _roles = BuildRoles();
        _tenants = BuildTenants(faker);
        _licenses = [];
        _users = [];

        foreach (var tenant in _tenants)
        {
            var tenantLicenses = BuildLicenses(faker, tenant);
            _licenses.AddRange(tenantLicenses);
            _users.AddRange(BuildUsers(faker, tenant, tenantLicenses, _roles));
        }

        RecomputeAssignedSeats();
    }

    // ---- Reads (return snapshots) -----------------------------------------

    public IReadOnlyList<Tenant> GetTenants()
    {
        lock (_lock) return _tenants.ToList();
    }

    public Tenant? GetTenant(Guid id)
    {
        lock (_lock) return _tenants.FirstOrDefault(t => t.Id == id);
    }

    public IReadOnlyList<Role> GetRoles()
    {
        lock (_lock) return _roles.ToList();
    }

    public Role? GetRole(Guid id)
    {
        lock (_lock) return _roles.FirstOrDefault(r => r.Id == id);
    }

    public User? GetUser(Guid id)
    {
        lock (_lock) return _users.FirstOrDefault(u => u.Id == id);
    }

    public IReadOnlyList<User> GetUsers(Guid? tenantId = null)
    {
        lock (_lock)
            return (tenantId is null ? _users : _users.Where(u => u.TenantId == tenantId)).ToList();
    }

    public License? GetLicense(Guid id)
    {
        lock (_lock) return _licenses.FirstOrDefault(l => l.Id == id);
    }

    public IReadOnlyList<License> GetLicenses(Guid? tenantId = null)
    {
        lock (_lock)
            return (tenantId is null ? _licenses : _licenses.Where(l => l.TenantId == tenantId)).ToList();
    }

    // ---- Writes ------------------------------------------------------------

    public User CreateUser(CreateUserRequest request)
    {
        lock (_lock)
        {
            if (_tenants.All(t => t.Id != request.TenantId))
                throw DomainException.NotFound($"Tenant '{request.TenantId}' was not found.");
            if (string.IsNullOrWhiteSpace(request.Email))
                throw DomainException.Validation("Email is required.");
            if (string.IsNullOrWhiteSpace(request.DisplayName))
                throw DomainException.Validation("DisplayName is required.");
            if (_users.Any(u => u.TenantId == request.TenantId &&
                                u.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase)))
                throw DomainException.Conflict($"A user with email '{request.Email}' already exists in this tenant.");

            var roleIds = (request.RoleIds ?? []).Distinct().ToList();
            foreach (var roleId in roleIds)
                if (_roles.All(r => r.Id != roleId))
                    throw DomainException.Validation($"Role '{roleId}' does not exist.");

            var licenseIds = (request.LicenseIds ?? []).Distinct().ToList();
            foreach (var licenseId in licenseIds)
            {
                var license = _licenses.FirstOrDefault(l => l.Id == licenseId)
                    ?? throw DomainException.Validation($"License '{licenseId}' does not exist.");
                if (license.TenantId != request.TenantId)
                    throw DomainException.Validation($"License '{licenseId}' belongs to a different tenant.");
                if (license.AvailableSeats < 1)
                    throw DomainException.Conflict($"License '{license.SkuName}' has no available seats.");
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                TenantId = request.TenantId,
                Email = request.Email,
                DisplayName = request.DisplayName,
                Department = string.IsNullOrWhiteSpace(request.Department) ? "Unassigned" : request.Department,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                RoleIds = roleIds,
                LicenseIds = licenseIds,
            };

            _users.Add(user);
            RecomputeAssignedSeats();
            return user;
        }
    }

    public User UpdateUser(Guid id, UpdateUserRequest request)
    {
        lock (_lock)
        {
            var user = _users.FirstOrDefault(u => u.Id == id)
                ?? throw DomainException.NotFound($"User '{id}' was not found.");

            if (request.DisplayName is { } displayName)
            {
                if (string.IsNullOrWhiteSpace(displayName))
                    throw DomainException.Validation("DisplayName cannot be empty.");
                user.DisplayName = displayName;
            }

            if (request.Department is { } department)
                user.Department = department;

            if (request.IsActive is { } isActive)
                user.IsActive = isActive;

            return user;
        }
    }

    public void DeleteUser(Guid id)
    {
        lock (_lock)
        {
            var user = _users.FirstOrDefault(u => u.Id == id)
                ?? throw DomainException.NotFound($"User '{id}' was not found.");
            _users.Remove(user);
            RecomputeAssignedSeats();
        }
    }

    public User AssignRole(Guid userId, Guid roleId)
    {
        lock (_lock)
        {
            var user = RequireUser(userId);
            if (_roles.All(r => r.Id != roleId))
                throw DomainException.NotFound($"Role '{roleId}' was not found.");
            if (user.RoleIds.Contains(roleId))
                throw DomainException.Conflict("Role is already assigned to the user.");

            user.RoleIds = [.. user.RoleIds, roleId];
            return user;
        }
    }

    public User UnassignRole(Guid userId, Guid roleId)
    {
        lock (_lock)
        {
            var user = RequireUser(userId);
            if (!user.RoleIds.Contains(roleId))
                throw DomainException.Conflict("Role is not assigned to the user.");

            user.RoleIds = user.RoleIds.Where(r => r != roleId).ToList();
            return user;
        }
    }

    public User AssignLicense(Guid userId, Guid licenseId)
    {
        lock (_lock)
        {
            var user = RequireUser(userId);
            var license = _licenses.FirstOrDefault(l => l.Id == licenseId)
                ?? throw DomainException.NotFound($"License '{licenseId}' was not found.");

            if (license.TenantId != user.TenantId)
                throw DomainException.Validation("License belongs to a different tenant than the user.");
            if (user.LicenseIds.Contains(licenseId))
                throw DomainException.Conflict("License is already assigned to the user.");
            if (license.AvailableSeats < 1)
                throw DomainException.Conflict($"License '{license.SkuName}' has no available seats.");

            user.LicenseIds = [.. user.LicenseIds, licenseId];
            RecomputeAssignedSeats();
            return user;
        }
    }

    public User RevokeLicense(Guid userId, Guid licenseId)
    {
        lock (_lock)
        {
            var user = RequireUser(userId);
            if (!user.LicenseIds.Contains(licenseId))
                throw DomainException.Conflict("License is not assigned to the user.");

            user.LicenseIds = user.LicenseIds.Where(l => l != licenseId).ToList();
            RecomputeAssignedSeats();
            return user;
        }
    }

    // ---- Internals ---------------------------------------------------------

    private User RequireUser(Guid id) =>
        _users.FirstOrDefault(u => u.Id == id)
        ?? throw DomainException.NotFound($"User '{id}' was not found.");

    private void RecomputeAssignedSeats()
    {
        foreach (var license in _licenses)
            license.AssignedSeats = _users.Count(u => u.LicenseIds.Contains(license.Id));
    }

    // ---- Seed builders -----------------------------------------------------

    private static List<Role> BuildRoles() =>
    [
        new()
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Global Administrator",
            Description = "Full, unrestricted access across all tenants.",
            Permissions = ["*"],
        },
        new()
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Name = "Tenant Administrator",
            Description = "Manage users, roles and licenses within a tenant.",
            Permissions = ["tenant.read", "user.read", "user.write", "role.read", "license.read", "license.assign"],
        },
        new()
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Name = "User Manager",
            Description = "Create and update users.",
            Permissions = ["user.read", "user.write"],
        },
        new()
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Name = "License Manager",
            Description = "Assign and revoke licenses.",
            Permissions = ["license.read", "license.assign"],
        },
        new()
        {
            Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
            Name = "Auditor",
            Description = "Read-only access to all resources.",
            Permissions = ["tenant.read", "user.read", "role.read", "license.read"],
        },
        new()
        {
            Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
            Name = "Member",
            Description = "Standard user with access to their own profile only.",
            Permissions = ["self.read"],
        },
    ];

    private static List<Tenant> BuildTenants(Faker faker)
    {
        (string Name, string Domain)[] seeds =
        [
            ("Contoso", "contoso.com"),
            ("Fabrikam", "fabrikam.io"),
            ("Globex", "globex.net"),
        ];

        return seeds.Select(s => new Tenant
        {
            Id = Guid.NewGuid(),
            Name = s.Name,
            Domain = s.Domain,
            IsActive = faker.Random.Bool(0.9f),
            CreatedAt = faker.Date.PastOffset(4),
        }).ToList();
    }

    private static List<License> BuildLicenses(Faker faker, Tenant tenant)
    {
        string[] skus = ["IAM-FREE", "IAM-PRO", "IAM-ENTERPRISE"];

        // Two distinct SKUs per tenant.
        return faker.PickRandom(skus, 2).Select(sku => new License
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            SkuName = sku,
            TotalSeats = faker.Random.Int(25, 250),
            AssignedSeats = 0, // derived from assignments in RecomputeAssignedSeats
            ExpiresAt = faker.Date.FutureOffset(2),
        }).ToList();
    }

    private static List<User> BuildUsers(Faker faker, Tenant tenant, List<License> tenantLicenses, IReadOnlyList<Role> roles)
    {
        var count = faker.Random.Int(8, 20);
        var users = new List<User>(count);

        for (var i = 0; i < count; i++)
        {
            var first = faker.Name.FirstName();
            var last = faker.Name.LastName();
            var email = $"{first}.{last}@{tenant.Domain}".ToLowerInvariant().Replace(" ", "");

            users.Add(new User
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Email = email,
                DisplayName = $"{first} {last}",
                Department = faker.Commerce.Department(1),
                IsActive = faker.Random.Bool(0.9f),
                CreatedAt = faker.Date.PastOffset(3),
                RoleIds = faker.PickRandom(roles, faker.Random.Int(1, 3)).Select(r => r.Id).Distinct().ToList(),
                LicenseIds = faker.Random.Bool(0.8f)
                    ? faker.PickRandom(tenantLicenses, faker.Random.Int(1, tenantLicenses.Count)).Select(l => l.Id).Distinct().ToList()
                    : [],
            });
        }

        return users;
    }
}
