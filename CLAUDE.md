# cheap-udemy

## What this project is

A learning / CV project — a Udemy-style course platform API. **Not production-ready and never will be.** The goal is practice and something to show on a CV. Prefer simple solutions over "correct but heavy" ones, even if a little limited.

> History: clean architecture was attempted early on, then abandoned because it added too much ceremony for the project's size. Some packages from that attempt are still referenced but unused — see **Honest state of the code** below. Don't assume a pattern is in use just because its package is installed; grep first.

## Quick start

```powershell
# restore & run (dev)
dotnet restore
dotnet run --project learning_platform

# dev URLs: http://localhost:5297 / https://localhost:7038
```

## Required env vars

All read from environment variables (not user secrets or appsettings):

| Variable           | Purpose                       |
| ------------------ | ----------------------------- |
| `ConnectionString` | PostgreSQL connection string  |
| `JWT_SECRET_KEY`   | Symmetric signing key for JWT |
| `StupidKey`        | Supabase service role key     |

`appsettings.json` only holds `Supabase:Url` and logging config. Every other setting comes from env vars.

## Solution map

3 projects in `cheap_udemy.slnx`:

```
Api (learning_platform/Api.csproj)
  → Business (Application/Business.csproj)
    → DataAccess (data_access_layer/DataAccess.csproj)
```

- **Api** – ASP.NET Core Web API entrypoint (`Program.cs`), controllers, auth handlers, DI wiring
- **Business** – Services, request/response DTOs, the `MyResult<T>` result pattern (`Business.Common`)
- **DataAccess** – EF Core `AppDbContext`, entities, repositories, `clsPageResult.PageResult<T>` pagination, DB-level DTOs

## API routes

| Prefix        | Controller       |
| ------------- | ---------------- |
| `api/User`    | Authentication   |
| `api/user`    | User profile     |
| `api/Courses` | Courses          |
| `api/Lessons` | Lessons          |
| `api/media`   | Media (Supabase) |

## Key conventions (what's actually in use)

- **Result pattern**: Services return `MyResult<T>` (`Business.Common`). Controllers unpack `.IsSuccess` / `.FailureType` into HTTP status codes via a switch expression. (Note: the switch currently maps `Unauthorized` → `Conflict`/409 — that's a bug, not intent.)
- **Validation**: done **manually** with inline `if` checks inside services (e.g. `if (request.UserId <= 0) return ...Failure(BadRequest, ...)`). FluentValidation is **not** wired up despite being referenced.
- **Mapping**: done **manually by hand** (e.g. `CoursesRepository.AddNewCourse` builds the DTO field-by-field). No source-generated mapper is actually in use.
- **Object creation**: services and repositories are created with `new` at the call site (controllers `new` services; services `new` repositories). They are **not** registered in DI and have **no interfaces** (except `IMediaService`). Controllers receive `AppDbContext` directly.
- **Pagination**: `clsPageResult.PageResult<T>` exists in both `Business.Common` and `DataAccess.Common`; the service copies from one to the other.
- **Error handling in repos**: repositories `try/catch (Exception)`, `Console.WriteLine` the error, and `return null`. So "DB failed" and "no rows" look identical to the caller. No `ILogger` is used.
- **JSONB columns**: `LessonEntity.content_blocks`, `LessonEntity.lesson_metadata`, `CourseEntitiy.course_metadata`. Npgsql dynamic JSON serialization enabled in `data_access_layer/DependencyInjection.cs` (`NpgsqlConnection.GlobalTypeMapper.EnableDynamicJson()`).
- **Auth**: JWT bearer with a custom `UserOwnerOrAdmin` policy + handler (checks user ID match or admin role). Issuer `CheapUdemyApi`, audience `CheapUdemyApiUsers`, expiry 20 min. Refresh tokens rotated via `TokenService` (BCrypt-hashed, stored in `user_refresh_tokens`).
- **Media**: `SupabaseMediaService` uploads to the `course-media` Supabase bucket and returns the stored file name.
- **DB triggers**: Course publish date, instructor role verification, enrollment progress sync, admin action immutability, user anonymization on delete — all at the PostgreSQL level (see `1-Create_Full_Databse.sql`).

## Honest state of the code (read before assuming a pattern exists)

**Referenced in `.csproj` but NOT used anywhere** (leftovers from the abandoned clean-architecture attempt — don't treat them as active patterns, and feel free to remove them):

- `MediatR` — no `IRequest` / handlers exist. No CQRS.
- `FluentValidation` — no `AbstractValidator`. Validation is manual.
- `Riok.Mapperly` — no `[Mapper]` class. Mapping is manual.
- `Mapster` — unused.
- `HtmlSanitizer` — installed but not yet wired in (input sanitizing is a planned addition).
- `CloudinaryDotNet` — only a stray `using` in `MediaService.cs`; the real implementation is Supabase-only.

**Known rough edges / planned work:**
- No DI / interfaces for services & repositories (everything is `new`d). Biggest refactor target.
- No `ILogger`; repos swallow exceptions and return `null`.
- No global exception-handling middleware.
- Planned by the owner: rate limiting, input sanitizing (HtmlSanitizer), Docker.

## Database setup

- `1-Create_Full_Databse.sql` – full schema with tables, constraints, indexes, triggers, RLS
- `2-Seed_database.sql` – seed data
- EF Core uses `Npgsql` with `EFCore.NamingConventions`. **No EF migrations** — schema is managed via the raw SQL scripts above. (Entity/column names are snake_case to match the SQL.)

## No tests

There is no test project. All verification is manual via the HTTP API (see `learning_platform.http` or the Scalar UI at `/scalar/v1`).

## CI / infra

`.github/workflows/` is empty. No CI or deployment pipeline configured.
