# WMS API

Backend for a warehouse management system that runs a PDA-driven warehouse
floor end to end — from truck unload through putaway, picking, packing,
sorting, and final dispatch. Built as an ASP.NET Core Web API consumed by a
companion Flutter app ([wms-app](https://github.com/Warayuth014/wms-app)).

## Stack

- **ASP.NET Core 8** (C# 12) Web API, controller + service layered
- **Entity Framework Core 8** on **SQL Server**
- **SignalR** for real-time putaway/sorting station updates
- **Swagger / OpenAPI** for interactive API docs

## Modules

Each module is a controller + service pair, backed by its own set of EF Core
models:

| Module | Covers |
|---|---|
| Receiving | Scan parts against a PO, resolve condition/lot/serial, assign to pallet |
| Putaway | Route a received pallet to ASRS or a prework station |
| Unload | Unload a replenishment pallet back onto the floor, lot- and serial-aware |
| Basket | Load unloaded stock into baskets for picking, S/N-gated where required |
| Picking | Allocate stock to pick orders, station-based pick flow |
| Packing | Pack picked items, close out pick orders |
| Sorting | Sort packed cartons into outbound batches |
| Check-In | Stage and dispatch outbound shipments |
| Health | `GET /api/health` — lets clients confirm they've reached this API |

## Architecture

```text
Controllers/   → thin HTTP layer, one action per endpoint
Services/      → business logic, one interface + implementation per module
DTOs/          → request/response shapes, decoupled from EF models
Models/        → EF Core entities, grouped by module
Data/          → DbContext + per-entity Fluent API configuration
Migrations/    → EF Core migration history
Hubs/          → SignalR hubs (Putaway, Sorting)
```

Every endpoint returns a `ServiceResult` from the service layer, which
`ToActionResult()` maps to the right HTTP status — controllers stay free of
business logic.

## Getting started

**Prerequisites:** .NET 8 SDK, a SQL Server instance (LocalDB/Express is
fine), the `dotnet-ef` global tool (`dotnet tool install --global
dotnet-ef`).

1. Clone the repo.
2. Point the app at your database. `appsettings.json` ships with a
   placeholder connection string on purpose — **never put real credentials
   there**, since it's tracked by git. Instead, create
   `WmsApi/WmsApi/appsettings.Development.json` (already gitignored) with:

   ```json
   {
     "ConnectionStrings": {
       "Default": "Server=.\\SQLEXPRESS;Database=WmsDB;User Id=...;Password=...;TrustServerCertificate=True;"
     }
   }
   ```

   ASP.NET Core loads this automatically on top of `appsettings.json` when
   running locally (`ASPNETCORE_ENVIRONMENT=Development`).
3. Apply migrations:

   ```bash
   cd WmsApi/WmsApi
   dotnet ef database update
   ```

4. Run it:

   ```bash
   dotnet run
   ```

   The API listens on `http://0.0.0.0:5000`. Swagger UI is at
   `http://localhost:5000/swagger`.

## Project layout

```text
WmsApi/
└── WmsApi/
    ├── Controllers/
    ├── Services/
    ├── DTOs/
    ├── Models/
    ├── Data/
    │   └── Configurations/
    ├── Migrations/
    └── Hubs/
```

## License

MIT — see [LICENSE](LICENSE).
