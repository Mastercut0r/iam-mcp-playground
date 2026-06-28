# IAM Mock API + MCP Server

A **.NET 10** mock API for a simple IAM system (tenants, users, roles, licenses)
with fake data, plus a local **MCP server** and a **console client** for testing.

> Authentication is intentionally omitted — assumption: hosted inside a private VNet.

## Architecture

```
┌──────────────────┐   stdio (MCP)   ┌──────────────────┐   HTTP/JSON   ┌──────────────────┐
│  IamMock.        │ ──────────────► │  IamMock.        │ ────────────► │  IamMock.Api     │
│  McpClient       │ ◄────────────── │  McpServer       │ ◄──────────── │  (REST, Bogus)   │
│  (console)       │                 │  (16 tools)      │               │  :5080           │
└──────────────────┘                 └──────────────────┘               └──────────────────┘
```

The MCP server is a thin layer **on top of** the REST API (no embedded data model).
This mirrors the realistic scenario of an "internal API in a VNet, accessed by an MCP server".

## Project structure

```
playground-mcp/
├── IamMock.slnx                     # Solution (new .NET 10 XML format)
├── .mcp.json                        # MCP server entry for Claude Code
├── Directory.Build.props            # shared build settings (net10.0, nullable, …)
└── src/
    ├── IamMock.Contracts/           # shared domain models (DTOs)
    │   └── Models/                  # Tenant, User, Role, License
    ├── IamMock.Api/                 # ASP.NET Core Minimal API
    │   ├── Data/MockDataStore.cs    # deterministic fake data via Bogus
    │   ├── Endpoints/               # one endpoint class per resource
    │   └── IamMock.Api.http         # example requests
    ├── IamMock.McpServer/           # MCP server (stdio)
    │   ├── IamApiClient.cs          # typed HttpClient over the REST API
    │   └── Tools/                   # MCP tools (Tenant/User/Role/License)
    └── IamMock.McpClient/           # console client (demo + interactive)
```

## Data model

Deliberately flat:

| Entity      | Fields (excerpt)                                                       |
|-------------|------------------------------------------------------------------------|
| **Tenant**  | `Id, Name, Domain, IsActive, CreatedAt`                                |
| **User**    | `Id, TenantId, Email, DisplayName, Department, IsActive, RoleIds[], LicenseIds[]` |
| **Role**    | `Id, Name, Description, Permissions[]` (global)                         |
| **License** | `Id, TenantId, SkuName, TotalSeats, AssignedSeats, AvailableSeats, ExpiresAt` |

Data is generated with a fixed seed → identical on every start
(3 tenants: Contoso/Fabrikam/Globex, 6 roles, ~8–20 users per tenant).
`AssignedSeats` is derived from the actual user assignments.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

```bash
dotnet build IamMock.slnx
```

## Quick start

### 1. Start the REST API

```bash
dotnet run --project src/IamMock.Api
```

- API:       http://localhost:5080
- API docs:  http://localhost:5080/scalar  (interactive OpenAPI UI)
- OpenAPI:   http://localhost:5080/openapi/v1.json

> Override the port: `ASPNETCORE_URLS=http://localhost:6000 dotnet run --project src/IamMock.Api`

### 2. Start the MCP client (in a second terminal)

The client launches the MCP server itself as a child process, lists all tools,
runs a small demo flow and then drops into an interactive mode.

```bash
dotnet run --project src/IamMock.McpClient
```

Tools can be invoked directly in interactive mode:

```
> list_tenants
> list_users {"search": "mar"}
> license_summary {"tenantId": "e586e2cf-6eb7-468d-ba2b-7d29b84302b9"}
> exit
```

## REST endpoints

**Read**

| Method & path                   | Description                                   |
|---------------------------------|-----------------------------------------------|
| `GET /health`                   | Liveness probe                                |
| `GET /tenants`                  | All tenants                                   |
| `GET /tenants/{id}`             | A single tenant                               |
| `GET /tenants/{id}/users`       | Users of a tenant                             |
| `GET /tenants/{id}/licenses`    | Licenses of a tenant                          |
| `GET /users?tenantId=&search=`  | Users, optionally filtered                    |
| `GET /users/{id}`               | A single user                                 |
| `GET /roles`                    | All roles                                     |
| `GET /roles/{id}`               | A single role                                 |
| `GET /licenses?tenantId=`       | Licenses, optionally filtered                 |
| `GET /licenses/{id}`            | A single license                              |

**Write** (in-memory, reset on restart)

| Method & path                               | Description                           |
|---------------------------------------------|---------------------------------------|
| `POST /users`                               | Create a user (201)                   |
| `PATCH /users/{id}`                         | Partially update a user               |
| `DELETE /users/{id}`                        | Delete a user (204)                   |
| `POST /users/{id}/roles/{roleId}`           | Assign a role                         |
| `DELETE /users/{id}/roles/{roleId}`         | Remove a role                         |
| `POST /users/{id}/licenses/{licenseId}`     | Assign a license seat (checks seats)  |
| `DELETE /users/{id}/licenses/{licenseId}`   | Revoke a license seat                 |

Errors are reported as JSON `{ "error": "..." }` with an appropriate status:
`404` (not found), `409` (conflict, e.g. duplicate email / no free seats),
`400` (validation).

## MCP tools

**Read:** `list_tenants`, `get_tenant`, `list_users`, `get_user`, `list_roles`,
`get_role`, `list_licenses`, `get_license`, `license_summary`

**Write:** `create_user`, `update_user`, `delete_user`, `assign_role`,
`unassign_role`, `assign_license`, `revoke_license`

Tool errors (e.g. "no free seats") are propagated to the client as an
`McpException` with a meaningful message.

## Integration with Claude Code / Claude Desktop

The [`.mcp.json`](.mcp.json) file registers the server project-wide for **Claude Code**
(path relative to the project root, so run `dotnet build` first).

For **Claude Desktop**, add the following block to `claude_desktop_config.json`
(use absolute paths):

```json
{
  "mcpServers": {
    "iam-mock": {
      "command": "dotnet",
      "args": ["F:\\Repos\\playground-mcp\\src\\IamMock.McpServer\\bin\\Debug\\net10.0\\IamMock.McpServer.dll"],
      "env": { "IamApi__BaseUrl": "http://localhost:5080" }
    }
  }
}
```

> The REST API (`dotnet run --project src/IamMock.Api`) must be running for the
> tools to return data.

## Configuration

| Setting               | Where                                | Default                  |
|-----------------------|--------------------------------------|--------------------------|
| API port              | `ASPNETCORE_URLS`                    | `http://localhost:5080`  |
| Server's API URL      | `IamApi__BaseUrl` (env / appsettings)| `http://localhost:5080`  |
| Client's server DLL   | `IAM_MCP_SERVER_DLL` (env)           | resolved from build output |
| Client's API URL      | `IAM_API_BASEURL` (env)              | `http://localhost:5080`  |

## Notes

- **No authentication** — intended only for local testing / private networks.
- Seed data is **deterministic** (fixed seed). Write operations are applied
  **in-memory** and are lost when the API restarts.
- The MCP SDK (`ModelContextProtocol`) is currently referenced as a preview.
