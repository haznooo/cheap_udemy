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

| Prefix                          | Controller                                   |
| ------------------------------- | -------------------------------------------- |
| `api/User`                      | Authentication (`signUp`, `login`)           |
| `api/user`                      | User profile (delete, update password/profile) |
| `api/Courses`                   | Courses                                      |
| `api/Courses/{courseId}/reviews`| Reviews                                      |
| `api/Lessons`                   | Lessons                                      |
| `api/Enrollments`               | Enrollments                                  |
| `api/media`                     | Media (Supabase)                             |

> Note: `api/User` (auth) and `api/user` (profile) differ only by case — both are real, distinct controllers.

## Key conventions (what's actually in use)

- **Result pattern**: Services return `MyResult<T>` (`Business.Common`). Controllers unpack `.IsSuccess` / `.FailureType` into HTTP status codes via a switch expression (`Unauthorized` → 401, `NotFound` → 404, `BadRequest` → 400, `Conflict` → 409).
- **Validation**: done **manually** with inline `if` checks inside services (e.g. `if (request.UserId <= 0) return ...Failure(BadRequest, ...)`). FluentValidation is **not** wired up despite being referenced.
- **Mapping**: done **manually by hand** (e.g. `CoursesRepository.AddNewCourse` builds the DTO field-by-field). No source-generated mapper is actually in use.
- **Object creation**: services and repositories are created with `new` at the call site (controllers `new` services; services `new` repositories). They are **not** registered in DI and have **no interfaces** — *except* `IMediaService`/`SupabaseMediaService` and `LessonService`, which **are** registered (see `Program.cs`) and injected via constructor. Most controllers receive `AppDbContext` directly; `MediaController` takes `IMediaService`, `LessonsController` takes `LessonService`.
- **Pagination**: `clsPageResult.PageResult<T>` exists in both `Business.Common` and `DataAccess.Common`; the service copies from one to the other.
- **Error handling in repos**: repositories `try/catch (Exception)`, `Console.WriteLine` the error, and `return null`/`false`. So "DB failed" and "no rows" look identical to the caller. Repos still do **not** use `ILogger` (see Logging below for where `ILogger` *is* used).
- **JSONB columns**: `LessonEntity.content_blocks`, `LessonEntity.lesson_metadata`, `CourseEntitiy.course_metadata`. Npgsql dynamic JSON serialization enabled in `data_access_layer/DependencyInjection.cs` (`NpgsqlConnection.GlobalTypeMapper.EnableDynamicJson()`).
- **Auth**: JWT bearer with a custom `UserOwnerOrAdmin` policy + handler (checks user ID match or admin role). Issuer `CheapUdemyApi`, audience `CheapUdemyApiUsers`, expiry 20 min. Refresh tokens rotated via `TokenService` (BCrypt-hashed, stored in `user_refresh_tokens`). Controllers enforce ownership inline: `await authorizationService.AuthorizeAsync(User, userId, "UserOwnerOrAdmin")` then `Forbid()` on failure.
- **Rate limiting** (in use): configured in `Program.cs` via `AddRateLimiter`. A single **global limiter** — fixed window, **20 requests/min per IP** (partitioned by `RemoteIpAddress`), `QueueLimit = 0`, rejection status `429`. `app.UseRateLimiter()` runs *before* auth, followed by middleware that writes a friendly "too many attempts" message on 429 (no exact limits exposed). No per-endpoint `[EnableRateLimiting]` policies — everything is covered uniformly by the global limiter. Relies on `UseForwardedHeaders` for the client IP behind a proxy (trusted-proxy restriction is still a TODO, so `X-Forwarded-For` is currently spoofable).
- **Logging (`ILogger`)**: used in **controllers** for security events only — failed logins (`LogWarning`, IP only, never credentials) in `AuthenticationController`, forbidden/403 attempts (`LogWarning`) on every `Forbid()` path in `UserController`, and successful admin deletes (`LogInformation`). `ILogger<T>` is injected via the controllers' primary constructors. Repositories still use `Console.WriteLine` (not migrated).
- **Login logging** (DB): `LoginLogService` → `LoginLogRepository` write a row to `login_logs` (`success`/`failed`) on signup and login attempts.
- **Admin-action auditing** (DB): `AdminActionService` → `AdminActionRepository` write an immutable row to `admin_actions` (mirrors the login-log `new`d pattern). Currently called from `UserController.DeleteUser` when an admin deletes *another* user (`action_type: "delete"`, `target_table: "users"`). `old_value`/`new_value` are JSONB and must stay small & non-sensitive (no passwords/tokens). `action_type` is constrained to `'create','update','delete','ban','unban'`.
- **Media**: `SupabaseMediaService` uploads to the `course-media` Supabase bucket and returns the stored file name.
- **DB triggers**: Course publish date, instructor role verification, enrollment progress sync, admin-action verification (`trg_verify_admin_action` rejects inserts whose `admin_id` isn't an admin) + immutability (`trg_lock_admin_actions` blocks UPDATE/DELETE), user anonymization on delete — all at the PostgreSQL level (see `db/01-create-schema.sql`).

## Honest state of the code (read before assuming a pattern exists)

**Referenced in `.csproj` but NOT used anywhere** (leftovers from the abandoned clean-architecture attempt — don't treat them as active patterns, and feel free to remove them):

- `MediatR` — no `IRequest` / handlers exist. No CQRS.
- `FluentValidation` — no `AbstractValidator`. Validation is manual.
- `Riok.Mapperly` — no `[Mapper]` class. Mapping is manual.
- `Mapster` — unused.
- `HtmlSanitizer` — installed but not yet wired in (input sanitizing is a planned addition).
- `CloudinaryDotNet` — only a stray `using` in `MediaService.cs`; the real implementation is Supabase-only.

**Known rough edges / planned work:**
- No DI / interfaces for most services & repositories (everything is `new`d). Biggest refactor target.
- Repos swallow exceptions and return `null`/`false`, still via `Console.WriteLine` (not migrated to `ILogger`). `ILogger` is only used in controllers for security events.
- No global exception-handling middleware.
- Done since this list was written: ✅ rate limiting, ✅ security logging (`ILogger`), ✅ login logging, ✅ admin-action auditing.
- Still planned by the owner: input sanitizing (HtmlSanitizer), Docker.

## Database setup

- `db/01-create-schema.sql` – full schema with tables, constraints, indexes, triggers, RLS
- `db/02-seed.sql` – seed data
- EF Core uses `Npgsql`. **No EF migrations** — schema is managed via the raw SQL scripts above.
- **Naming**: despite `EFCore.NamingConventions` being referenced, `UseSnakeCaseNamingConvention` is **not** actually configured. Mapping works only because entity **property names are written literally in snake_case** (e.g. `user_id`, `target_table`) so they match the DB columns 1:1. **Gotcha:** a typo'd property name therefore maps to a non-existent column and fails at runtime (this happened with `admin_actions.trget_table` → fixed to `target_table` + explicit `HasColumnName`). When adding columns, match the SQL column name exactly or add `.HasColumnName(...)`.

## No tests / how to verify manually

There is no test project. All verification is manual via the HTTP API (see `learning_platform.http` or the Scalar UI at `/scalar/v1`). Useful checks for the recently-added features:

- **Rate limiting**: hammer any endpoint >20×/min from the same IP → expect `429` + "Too many attempts" body. Counter is per-IP, fixed 1-min window.
- **Failed-login logging**: POST `api/User/login` with a wrong password → `LogWarning` in console (IP only) **and** a `failed` row in `login_logs`.
- **403 logging**: call an `api/user/...` endpoint with a JWT for a *different* user (non-admin) → `403` + `LogWarning`.
- **Admin-action auditing**: log in as an `admin`, delete *another* user via `api/user/Delete/{userId}` → a `delete` row appears in `admin_actions` (immutable; FK/trigger requires the caller's `admin_id` to actually have role `admin` in the DB, or the audit insert is silently swallowed by the repo's try/catch). Self-deletion does **not** create an audit row.

## CI / infra

`.github/workflows/` is empty. No CI or deployment pipeline configured.
