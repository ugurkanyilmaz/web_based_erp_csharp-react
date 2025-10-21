# KetenErp

Minimal .NET ERP scaffold with three projects:

- KetenErp.Api - ASP.NET Core Web API (controllers, DI)
- KetenErp.Core - Domain entities and repository interfaces
- KetenErp.Infrastructure - EF Core DbContext and repository implementations

Quick start (Windows PowerShell):

```powershell
cd "c:\Users\uur\OneDrive\Masaüstü\keten_work\erp"
# build
dotnet build
# run API
dotnet run --project KetenErp.Api\KetenErp.Api.csproj
```

Default connection string uses LocalDB. Update `appsettings.json` in the API project with your SQL Server connection string under `ConnectionStrings:DefaultConnection`.

API endpoints:
- GET /api/products
- GET /api/products/{id}

Next steps:
- Add migrations: `dotnet ef migrations add Initial -p KetenErp.Infrastructure -s KetenErp.Api -o Migrations`
- Apply migrations: `dotnet ef database update -p KetenErp.Infrastructure -s KetenErp.Api`
- Add DTOs and validation
- Add write operations (create/update/delete)
