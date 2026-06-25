# cheap-udemy

## Quick start

```powershell
# restore & run (dev)
dotnet restore
dotnet run --project learning_platform

# dev URLs: http://localhost:5297 / https://localhost:7038
```

## Required env vars

All read from environment variables (not user secrets or appsettings):

| Variable        | Purpose                            |
|-----------------|------------------------------------|
| `ConnectionString` | PostgreSQL connection string    |
| `JWT_SECRET_KEY`   | Symmetric signing key for JWT   |
| `StupidKey`        | Supabase service role key       |

appsettings.json only holds `Supabase:Url` and logging config. Every other setting comes from env vars.

## Solution map

3 projects in `cheap_udemy.slnx`:

```
Api (learning_platform/Api.csproj)
  → Business (Application/Business.csproj)
    → DataAccess (data_access_layer/DataAccess.csproj)
```

- **Api** – ASP.NET Core Web API entrypoint (`Program.cs`), controllers, auth handlers, DI wiring
- **Business** – Services, CQRS request/response DTOs, validation (`FluentValidation`), `MyResult<T>` result pattern, `MediatR`
- **DataAccess** – EF Core `AppDbContext`, entities, repositories, `clsPageResult.PageResult<T>` pagination, DB-level DTOs

## API routes

| Prefix            | Controller             |
|-------------------|------------------------|
| `api/User`        | Authentication         |
| `api/user`        | User profile           |
| `api/Courses`     | Courses                |
| `api/Lessons`     | Lessons                |
| `api/media`       | Media (Supabase)       |

## Key conventions

- **Result pattern**: Services return `MyResult<T>` (`Business.Common`). Controllers unpack `.IsSuccess` / `.FailureType` into HTTP status codes via switch expression.
- **Pagination**: `clsPageResult.PageResult<T>` in both `Business.Common` and `DataAccess.Common`.
- **JSONB columns**: `LessonEntity.content_blocks`, `LessonEntity.lesson_metadata`, `CourseEntitiy.course_metadata`. Npgsql dynamic JSON serialization enabled in `DependencyInjection.cs` (`NpgsqlConnection.GlobalTypeMapper.EnableDynamicJson()`).
- **Mapper**: `Riok.Mapperly` (source-generated) used for entity ↔ DTO mapping.
- **Auth**: JWT bearer with custom `UserOwnerOrAdmin` policy (handler checks user ID match or admin role). JWT issuer: `CheapUdemyApi`, audience: `CheapUdemyApiUsers`, expiry: 20 min. Refresh tokens rotated via `TokenService` (BCrypt-hashed, stored in `user_refresh_tokens`).
- **DB triggers**: Course publish date, instructor role verification, enrollment progress sync, admin action immutability, user anonymization on delete – all at the PostgreSQL level (see `1-Create_Full_Databse.sql`).

## Database setup

- `1-Create_Full_Databse.sql` – full schema with tables, constraints, indexes, triggers, RLS
- `2-Seed_database.sql` – seed data
- EF Core uses `Npgsql` with `EFCore.NamingConventions`. No EF migrations are used; schema is managed via raw SQL scripts.

## No tests

There is no test project. All verification is manual via the HTTP API (see `learning_platform.http` or tools like the Scalar UI at `/scalar/v1`).

## CI / infra

`.github/workflows/` is empty. No CI or deployment pipeline is configured.
