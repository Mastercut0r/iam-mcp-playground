# IAM Mock API + MCP Server

Eine **.NET 10** Mock-API für ein einfaches IAM-System (Tenants, User, Rollen, Lizenzen)
mit Fake-Daten, plus einem lokalen **MCP-Server** und einem **Konsolen-Client** zum Testen.

> Authentifizierung ist bewusst weggelassen — Annahme: Hosting in einem privaten VNet.

## Architektur

```
┌──────────────────┐   stdio (MCP)   ┌──────────────────┐   HTTP/JSON   ┌──────────────────┐
│  IamMock.        │ ──────────────► │  IamMock.        │ ────────────► │  IamMock.Api     │
│  McpClient       │ ◄────────────── │  McpServer       │ ◄──────────── │  (REST, Bogus)   │
│  (Konsole)       │                 │  (9 Tools)       │               │  :5080           │
└──────────────────┘                 └──────────────────┘               └──────────────────┘
```

Der MCP-Server ist eine dünne Schicht **über** der REST-API (kein eingebettetes Datenmodell).
Das entspricht dem realistischen Szenario "interne API im VNet, MCP-Server greift darauf zu".

## Projektstruktur

```
playground-mcp/
├── IamMock.slnx                     # Solution (neues XML-Format von .NET 10)
├── .mcp.json                        # MCP-Server-Eintrag für Claude Code
├── Directory.Build.props            # gemeinsame Build-Settings (net10.0, nullable, …)
└── src/
    ├── IamMock.Contracts/           # geteilte Domain-Modelle (DTOs)
    │   └── Models/                  # Tenant, User, Role, License
    ├── IamMock.Api/                 # ASP.NET Core Minimal API
    │   ├── Data/MockDataStore.cs    # deterministische Fake-Daten via Bogus
    │   ├── Endpoints/               # je Ressource eine Endpoint-Klasse
    │   └── IamMock.Api.http         # Beispiel-Requests
    ├── IamMock.McpServer/           # MCP-Server (stdio)
    │   ├── IamApiClient.cs          # typed HttpClient über die REST-API
    │   └── Tools/                   # MCP-Tools (Tenant/User/Role/License)
    └── IamMock.McpClient/           # Konsolen-Client (Demo + interaktiv)
```

## Datenmodell

Bewusst flach gehalten:

| Entität     | Felder (Auszug)                                                        |
|-------------|------------------------------------------------------------------------|
| **Tenant**  | `Id, Name, Domain, IsActive, CreatedAt`                                |
| **User**    | `Id, TenantId, Email, DisplayName, Department, IsActive, RoleIds[], LicenseIds[]` |
| **Role**    | `Id, Name, Description, Permissions[]` (global)                         |
| **License** | `Id, TenantId, SkuName, TotalSeats, AssignedSeats, AvailableSeats, ExpiresAt` |

Die Daten werden mit festem Seed generiert → bei jedem Start identisch
(3 Tenants: Contoso/Fabrikam/Globex, 6 Rollen, ~8–20 User pro Tenant).
`AssignedSeats` wird aus den tatsächlichen User-Zuweisungen abgeleitet.

## Voraussetzungen

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

```bash
dotnet build IamMock.slnx
```

## Schnellstart

### 1. REST-API starten

```bash
dotnet run --project src/IamMock.Api
```

- API:        http://localhost:5080
- API-Doku:   http://localhost:5080/scalar  (interaktive OpenAPI-UI)
- OpenAPI:    http://localhost:5080/openapi/v1.json

> Port überschreiben: `ASPNETCORE_URLS=http://localhost:6000 dotnet run --project src/IamMock.Api`

### 2. MCP-Client starten (in einem zweiten Terminal)

Der Client startet den MCP-Server selbst als Kindprozess, listet alle Tools auf,
fährt einen kleinen Demo-Ablauf und geht dann in einen interaktiven Modus.

```bash
dotnet run --project src/IamMock.McpClient
```

Interaktiv lassen sich Tools direkt aufrufen:

```
> list_tenants
> list_users {"search": "mar"}
> license_summary {"tenantId": "e586e2cf-6eb7-468d-ba2b-7d29b84302b9"}
> exit
```

## REST-Endpoints

**Lesen**

| Methode & Pfad                  | Beschreibung                                  |
|---------------------------------|-----------------------------------------------|
| `GET /health`                   | Liveness-Probe                                |
| `GET /tenants`                  | Alle Tenants                                  |
| `GET /tenants/{id}`             | Einzelner Tenant                              |
| `GET /tenants/{id}/users`       | User eines Tenants                            |
| `GET /tenants/{id}/licenses`    | Lizenzen eines Tenants                        |
| `GET /users?tenantId=&search=`  | User, optional gefiltert                      |
| `GET /users/{id}`               | Einzelner User                                |
| `GET /roles`                    | Alle Rollen                                   |
| `GET /roles/{id}`               | Einzelne Rolle                                |
| `GET /licenses?tenantId=`       | Lizenzen, optional gefiltert                  |
| `GET /licenses/{id}`            | Einzelne Lizenz                               |

**Schreiben** (in-memory, bei Neustart zurückgesetzt)

| Methode & Pfad                              | Beschreibung                          |
|---------------------------------------------|---------------------------------------|
| `POST /users`                               | User anlegen (201)                    |
| `PATCH /users/{id}`                         | User teil-aktualisieren               |
| `DELETE /users/{id}`                        | User löschen (204)                    |
| `POST /users/{id}/roles/{roleId}`           | Rolle zuweisen                        |
| `DELETE /users/{id}/roles/{roleId}`         | Rolle entziehen                       |
| `POST /users/{id}/licenses/{licenseId}`     | Lizenz-Seat zuweisen (prüft Seats)    |
| `DELETE /users/{id}/licenses/{licenseId}`   | Lizenz-Seat entziehen                 |

Fehler werden als JSON `{ "error": "..." }` mit passendem Status gemeldet:
`404` (nicht gefunden), `409` (Konflikt, z. B. doppelte Email / keine freien Seats),
`400` (Validierung).

## MCP-Tools

**Lesen:** `list_tenants`, `get_tenant`, `list_users`, `get_user`, `list_roles`,
`get_role`, `list_licenses`, `get_license`, `license_summary`

**Schreiben:** `create_user`, `update_user`, `delete_user`, `assign_role`,
`unassign_role`, `assign_license`, `revoke_license`

Tool-Fehler (z. B. „keine freien Seats") werden als `McpException` mit
aussagekräftiger Meldung an den Client weitergereicht.

## Einbindung in Claude Code / Claude Desktop

Die Datei [`.mcp.json`](.mcp.json) registriert den Server projektweit für **Claude Code**
(Pfad relativ zum Projekt-Root, daher vorher `dotnet build` ausführen).

Für **Claude Desktop** den folgenden Block in die `claude_desktop_config.json` einfügen
(absolute Pfade verwenden):

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

> Die REST-API (`dotnet run --project src/IamMock.Api`) muss laufen, damit die Tools
> Daten liefern.

## Konfiguration

| Einstellung           | Wo                                   | Default                  |
|-----------------------|--------------------------------------|--------------------------|
| API-Port              | `ASPNETCORE_URLS`                    | `http://localhost:5080`  |
| API-URL des Servers   | `IamApi__BaseUrl` (env / appsettings)| `http://localhost:5080`  |
| Server-DLL des Clients| `IAM_MCP_SERVER_DLL` (env)           | aus Build-Output ermittelt |
| API-URL des Clients   | `IAM_API_BASEURL` (env)              | `http://localhost:5080`  |

## Hinweise

- **Keine Authentifizierung** — nur für lokale Tests / private Netze gedacht.
- Seed-Daten sind **deterministisch** (fester Seed). Schreib-Operationen werden
  **in-memory** angewandt und gehen bei einem Neustart der API verloren.
- Das MCP-SDK (`ModelContextProtocol`) ist aktuell als Preview eingebunden.
