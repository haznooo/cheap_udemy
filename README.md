# Learning Platform API

A backend API for a Udemy-style e-learning platform. It manages user authentication,
user course libraries, and the process of posting, reviewing, and enrolling in courses.

> Learning / CV project — not production-ready. The goal is practice and something to
> show, so it favours simple solutions over heavy "correct" ones.

## Prerequisites

* ASP.NET Core 10 SDK
* PostgreSQL (the setup scripts live in [`db/`](db/))
* A running Supabase instance (used for media storage)

## Environment Variables

Every setting other than `Supabase:Url` and logging is read from environment variables
(not user-secrets or `appsettings.json`).

| Variable           | Purpose                       |
| ------------------ | ----------------------------- |
| `ConnectionString` | PostgreSQL connection string  |
| `JWT_SECRET_KEY`   | Symmetric signing key for JWT |
| `StupidKey`        | Supabase service role key     |

On Windows: search → *Edit the system environment variables* → *Advanced* →
*Environment Variables* → under the user or system section click *New* to add each one.

## Packages

3 projects, 3 `.csproj` files (`Api`, `Business`, `DataAccess`). Verified against actual code usage, not just what's referenced.

### Actively used

| Package | Purpose |
| --- | --- |
| `BCrypt.Net-Next` | Password hashing (signup/login/password change) |
| `supabase-csharp` | Supabase client — storage uploads (avatar/thumbnail/course media), signed URLs for private lesson media |
| `System.IdentityModel.Tokens.Jwt` | JWT creation/validation in `AuthenticationController` |
| `Microsoft.IdentityModel.Tokens` | Token validation parameters |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | JWT bearer auth middleware |
| `Microsoft.AspNetCore.OpenApi` | OpenAPI document generation, paired with Scalar |
| `Scalar.AspNetCore` | `/scalar/v1` API explorer UI |
| `Microsoft.EntityFrameworkCore` / `Npgsql.EntityFrameworkCore.PostgreSQL` | ORM + Postgres provider for `AppDbContext` |
| `Microsoft.Extensions.Configuration` / `.Binder` / `.Json` | Env-var/config binding (connection string, JWT key, Supabase key) |
| `Microsoft.Extensions.DependencyInjection` | `AddDataAccessDI`/`AddBusinessDI` extension methods |

### Tooling only (not exercised by app code)

| Package | Why it's there |
| --- | --- |
| `Microsoft.EntityFrameworkCore.Design` | Enables `dotnet ef` CLI tooling — but no migrations exist; schema is raw SQL (see below) |
| `Microsoft.EntityFrameworkCore.Tools` | Same, CLI-only |
| `Microsoft.Extensions.ApiDescription.Server` | Build-time OpenAPI JSON generation, no direct code reference |

### Removed (were referenced but unused)

`MediatR`, `Riok.Mapperly`, `Mapster`, `CloudinaryDotNet`, `LinqKit.Microsoft.EntityFrameworkCore`, `Swashbuckle.AspNetCore`, `FluentValidation` + `FluentValidation.DependencyInjectionExtensions`, `EFCore.NamingConventions` — no code anywhere used them (no CQRS, no `[Mapper]` class, manual mapping/validation throughout, `UseSnakeCaseNamingConvention()` never called, Swagger superseded by `Microsoft.AspNetCore.OpenApi` + Scalar). `HtmlSanitizer` is kept — it's referenced ahead of planned input-sanitizing work (review/lesson/profile free-text fields), not dead.

Removing `Swashbuckle.AspNetCore` from `DataAccess.csproj` had a side effect: `Business.csproj` (a plain class library, not the Web SDK) was getting `Microsoft.AspNetCore.Http.IFormFile` transitively through Swashbuckle's own dependency chain via the `DataAccess` project reference. Fixed with an explicit `<FrameworkReference Include="Microsoft.AspNetCore.App" />` in `Business.csproj` — the standard way for a non-Web-SDK library to access ASP.NET Core types.

## Database setup

There are **no EF Core migrations** — the schema is managed by the raw SQL scripts in
[`db/`](db/). Run them in order (see [`db/README.md`](db/README.md) for details):

```bash
psql "<your-connection-string>" -f db/01-create-schema.sql   # schema: tables, indexes, triggers
psql "<your-connection-string>" -f db/02-seed.sql            # seed data
```

> `01-create-schema.sql` begins with `DROP SCHEMA public CASCADE` — it resets the
> `public` schema, so only run it on a database you are happy to wipe.

## Run the project

```bash
dotnet restore
dotnet run --project learning_platform
```

Dev URLs:

* HTTP:  `http://localhost:5297`
* HTTPS: `https://localhost:7038`
* API explorer (Scalar UI): `/scalar/v1`
