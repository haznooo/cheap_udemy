# cheap-udemy

## What this project is

A learning / CV project — a Udemy-style course platform API. **Not production-ready and never will be.** The goal is to practice and to have something to show on a CV. Prefer standered and common solutions over complex nich ones, even if it was a little limited.

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

| Prefix                           | Controller                                                         |
| -------------------------------- | ------------------------------------------------------------------ |
| `api/User`                       | Authentication (`signUp`, `login`)                                 |
| `api/user`                       | User profile (get, delete, update password/profile, avatar upload) |
| `api/Courses`                    | Courses                                                            |
| `api/Courses/{courseId}/reviews` | Reviews                                                            |
| `api/Lessons`                    | Lessons                                                            |
| `api/Enrollments`                | Enrollments                                                        |
| `api/media`                      | Media (Supabase)                                                   |

> Note: `api/User` (auth) and `api/user` (profile) differ only by case — both are real, distinct controllers.

**Course endpoints** (`api/Courses`):

| Method & path              | Auth          | Notes                                                                                                                                                                                                                                                                                                                                |
| -------------------------- | ------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `POST get`                 | anonymous     | Paged catalog. **Only published, non-deleted** courses. Body (`GetCoursesRequest`) supports optional `SearchTerm` (trigram `ILIKE`, uses the `gin_trgm_ops` index), `CategoryId`, `Level`, `MinPrice`, `MaxPrice`, `SortBy` (`newest`/`price_asc`/`price_desc`/`rating`; defaults to trigram relevance when searching, else newest). |
| `GET {courseId}`           | anonymous     | Single **published** course, full detail incl. `course_metadata`. 404 if draft/retired/deleted/missing.                                                                                                                                                                                                                              |
| `POST add`                 | authenticated | `instructor_id` is taken from the JWT (`NameIdentifier`), **never** the body. Category validated against the `categories` table.                                                                                                                                                                                                     |
| `PUT {courseId}/thumbnail` | owner / admin | Multipart `file` (JPG/PNG ≤5 MB) → uploaded via `IMediaService`, file name persisted to `courses.thumbnail_url`. Ownership = the course's instructor or an admin; otherwise 401.                                                                                                                                                     |
| `POST section/add`         | owner / admin | Adds a section. Ownership verified via `CheckCourseEditPermission` (course's instructor or an admin); `CourseId`/`Title` validated. Otherwise 401.                                                                                                                                                                                    |
| `GET {courseId}/lessons`   | anonymous     | Lessons for a course. **Only published, non-deleted** courses (draft/retired/deleted → empty).                                                                                                                                                                                                                                       |

**User-profile endpoints** (`api/user`, all owner-or-admin via `UserOwnerOrAdmin`):

| Method & path                  | Notes                                                                                                               |
| ------------------------------ | ------------------------------------------------------------------------------------------------------------------- |
| `GET {userId}`                 | Read a profile (`UserProfileResponse`). `Forbid()` + `LogWarning` on cross-user access.                             |
| `POST {userId}/avatar`         | Multipart `file` (JPG/PNG ≤5 MB) → uploaded via `IMediaService`, file name persisted to `users_profile.avatar_url`. |
| `POST Delete/{userId}`         | Delete (anonymize). Admin-deleting-another-user is audited.                                                         |
| `POST UpdatePassword/{userId}` | Change password.                                                                                                    |
| `PUT UpdateProfile/{userId}`   | Update profile fields.                                                                                              |

## Key conventions (what's actually in use)

- **Result pattern**: Services return `MyResult<T>` (`Business.Common`). Controllers unpack `.IsSuccess` / `.FailureType` into HTTP status codes via a switch expression (`Unauthorized` → 401, `NotFound` → 404, `BadRequest` → 400, `Conflict` → 409).
- **Validation**: done **manually** with inline `if` checks inside services (e.g. `if (request.UserId <= 0) return ...Failure(BadRequest, ...)`). FluentValidation is **not** wired up despite being referenced.
- **Mapping**: done **manually by hand** (e.g. `CoursesRepository.AddNewCourse` builds the DTO field-by-field). No source-generated mapper is actually in use.
- **Object creation**: services and repositories are created with `new` at the call site (controllers `new` services; services `new` repositories). They are **not** registered in DI and have **no interfaces** — _except_ `IMediaService`/`SupabaseMediaService` which **is** registered (see `Program.cs`) and injected via constructor. Most controllers receive `AppDbContext` directly; `MediaController` takes `IMediaService`, `LessonsController` takes `LessonService`.
- **Pagination**: `clsPageResult.PageResult<T>` exists in both `Business.Common` and `DataAccess.Common`; the service copies from one to the other.
- **Error handling in repos**: repositories `try/catch (Exception)`, `Console.WriteLine` the error, and `return null`/`false`. So "DB failed" and "no rows" look identical to the caller. Repos still do **not** use `ILogger` (see Logging below for where `ILogger` _is_ used).
- **JSONB columns**: `LessonEntity.content_blocks`, `LessonEntity.lesson_metadata`, `CourseEntitiy.course_metadata`. Npgsql dynamic JSON serialization enabled in `data_access_layer/DependencyInjection.cs` (`NpgsqlConnection.GlobalTypeMapper.EnableDynamicJson()`).
- **Auth**: JWT bearer with a custom `UserOwnerOrAdmin` policy + handler (checks user ID match or admin role). Issuer `CheapUdemyApi`, audience `CheapUdemyApiUsers`, expiry 20 min. Refresh tokens rotated via `RefreshTokenService`, stored in `user_refresh_tokens` as a **deterministic SHA-256 hash** (`RefreshTokenService.HashRefreshToken`; high-entropy random tokens don't need a salt — **only passwords use BCrypt**). Controllers enforce ownership inline: `await authorizationService.AuthorizeAsync(User, userId, "UserOwnerOrAdmin")` then `Forbid()` on failure. See **"Refresh-token rotation"** below for the full `/refresh` flow (reuse detection + absolute expiry).
- **Rate limiting** (in use): configured in `Program.cs` via `AddRateLimiter`. A single **global limiter** — fixed window, **20 requests/min per IP** (partitioned by `RemoteIpAddress`), `QueueLimit = 0`, rejection status `429`. `app.UseRateLimiter()` runs _before_ auth, followed by middleware that writes a friendly "too many attempts" message on 429 (no exact limits exposed). No per-endpoint `[EnableRateLimiting]` policies — everything is covered uniformly by the global limiter. Relies on `UseForwardedHeaders` for the client IP behind a proxy (trusted-proxy restriction is still a TODO, so `X-Forwarded-For` is currently spoofable).
- **Logging (`ILogger`)**: used in **controllers** for security events only — failed logins (`LogWarning`, IP only, never credentials) in `AuthenticationController`, forbidden/403 attempts (`LogWarning`) on every `Forbid()` path in `UserController`, and successful admin deletes (`LogInformation`). `ILogger<T>` is injected via the controllers' primary constructors. Repositories still use `Console.WriteLine` (not migrated).
- **Login logging** (DB): `LoginLogService` → `LoginLogRepository` write a row to `login_logs` (`success`/`failed`) on signup and login attempts.
- **Admin-action auditing** (DB): `AdminActionService` → `AdminActionRepository` write an immutable row to `admin_actions` (mirrors the login-log `new`d pattern). Currently called from `UserController.DeleteUser` when an admin deletes _another_ user (`action_type: "delete"`, `target_table: "users"`). `old_value`/`new_value` are JSONB and must stay small & non-sensitive (no passwords/tokens). `action_type` is constrained to `'create','update','delete','ban','unban'`.
- **Media**: `SupabaseMediaService` uploads to the `course-media` Supabase bucket and returns the stored file name. The raw `POST api/media/upload` endpoint (now `[Authorize]` — authenticated only, no longer anonymous) still just returns that name, but two endpoints now persist it: `PUT api/Courses/{courseId}/thumbnail` → `courses.thumbnail_url` and `POST api/user/{userId}/avatar` → `users_profile.avatar_url` (both inject `IMediaService`; JPG/PNG ≤5 MB; ownership-checked).
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
- Done since this list was written: ✅ rate limiting, ✅ security logging (`ILogger`), ✅ login logging, ✅ admin-action auditing, ✅ media upload now persisted (course thumbnail + user avatar), ✅ course catalog filtered to published/non-deleted with search/filter/sort over the `pg_trgm` index, ✅ course `instructor_id` taken from the JWT (not the body), ✅ GET course-detail + GET profile read endpoints, ✅ **refresh-token hardening** (SHA-256 hashing, user id from the access token, absolute expiry, rotation reuse detection — see "Refresh-token rotation" below), ✅ **authorization holes closed on lessons/sections/media** (`POST api/Lessons/add` now `[Authorize]` + section→course→instructor owner-or-admin check; `POST api/Courses/section/add` now ownership-checked; `POST api/media/upload` now `[Authorize]`; anonymous lesson reads — `GET api/Lessons/{id}` and `GET api/Courses/{courseId}/lessons` — restricted to published, non-deleted courses), ✅ **authenticated-by-default fallback policy** (`Program.cs` sets `FallbackPolicy = RequireAuthenticatedUser()`; anonymous endpoints — the whole `AuthenticationController`, plus the public Course/Lesson reads — are explicitly `[AllowAnonymous]`), ✅ **enrollment authorization** (`EnrollmentController` now `[Authorize]`; user id taken from the JWT not the body — `UserId` removed from the enroll/drop/mark-progress DTOs; enrollment-list reads scoped to the owner, course-roster reads to the owning instructor/admin), ✅ **login no longer leaks which emails exist** (unknown email and wrong password both return 401/"invalid credentials"; unknown-email path runs a dummy BCrypt verify to equalize timing — `UserService.DummyPasswordHash`), ✅ **password change revokes refresh tokens** (`UserService.UpdatePassword` → `RefreshTokenService.RevokeAllForUser` → `RefreshTokenRepository.RevokeAllRefreshTokensAsync`, bulk `ExecuteUpdate` marking used+revoked).
**Still planned / NOT yet done (owner will do later):**

- ⬜ **Input sanitizing (HtmlSanitizer)** — referenced but unwired. Free-text fields that need it before they're a real stored-XSS surface: review `comment`, lesson `content_blocks`, profile `bio`/`display_name`, course `description`.
- ⬜ **Enrollment business rules** — `EnrollmentService.EnrollStudent` only checks `CourseId > 0`. Still missing: published/non-deleted course check (can currently enroll in a draft/retired/deleted course), instructor self-enroll guard (instructor can enroll in their own course), and any payment/price check (paid courses can be enrolled for free).
- ⬜ **Dedicated auth rate limit** — login/refresh/signup still share the single global 20/min-per-IP limiter; no stricter per-endpoint limit. (`X-Forwarded-For` also still spoofable — trusted-proxy restriction is a TODO.)
- ⬜ **Docker.**

## Refresh-token rotation (implemented)

The `/refresh` flow (`api/User/refresh` → `RefreshTokenService.RefreshAccessToken`) hardens a *stolen* refresh token so it's actually containable, not just time-limited. `/refresh` and `/logout` both take a `RefreshTokenRequest` of **`{ RefreshToken, AccessToken }`** — no client-supplied user id.

### User id comes from the access token, never the body

The controller (`AuthenticationController.GetUserIdFromExpiredToken`) validates the access token's **signature/issuer/audience** but sets `ValidateLifetime = false`, so an *expired* access token still works as the trustworthy source of the user id (`NameIdentifier`). A tampered/garbage token → `null` → `401`. The client therefore sends **both** tokens; the user id scopes the refresh-token lookup.

### Lookup & state branching

Tokens are stored as deterministic SHA-256 hashes, so `RefreshTokenRepository.GetRefreshTokenByHashAsync(userId, tokenHash)` does a **direct indexed lookup across ALL states** (used/revoked/expired included — a valid-only filter would hide a replayed token). `RefreshAccessToken` then branches on the found row:

| State | Outcome |
| ----- | ------- |
| not found | `401` invalid, no breach |
| `replaced_by_id != null` (superseded, replayed) | **BREACH** → revoke whole chain → `401 "reuse detected"` |
| `is_used` / `revoked_at` set (e.g. logged out), or expired | `401`, plain re-login, no breach |
| valid | rotate |

**The breach trigger is `replaced_by_id != null`, NOT `is_used`** — deliberate: logout sets `is_used = true` but leaves `replaced_by_id == null`, so a logged-out token falls through to a plain `401` instead of false-flagging as theft. Only a token actually rotated to a child can trip the breach.

### Reuse detection (the anti-theft measure)

`RevokeBreachedChainAsync` walks `replaced_by_id` **forward** from the replayed token, setting `chain_breached + is_used + revoked_at` on every link — including the thief's currently-valid token. Scoped to the chain via forward links, **never by `user_id`**, so the user's other devices (separate chains) stay logged in. Forward-only (each child has a higher id) ⇒ no cycles. Net: a thief forces the user to re-login exactly once per theft; the new chain is one the thief doesn't hold.

### Absolute expiration (no sliding reset)

`AddNewRefreshTokenFirstTime(userId, deviceInfo, ipAddress, DateTime? expiresAt = null)` is the shared minter. Login/signup pass no expiry → fresh `AddDays(7)`. Rotation passes `currentToken.expires_at` → the child **inherits the parent's deadline**, so the original-login deadline propagates down the chain and a session (legit or stolen) dies ≤7 days after the original login, full stop.

### Bugs fixed in the same method

- **Bug 1:** the user is now fetched + validated **before** any mint/revoke (a deleted/banned user fails with a clean `401` instead of burning the old token + orphaning a new one). `UserAndProfileRepository.GetUserByIdAsync` got a null guard (the `status == "active"` filter returns no row → previously NRE → 500).
- **Bug 2:** the revoke (`UpdateRefreshTokenAsync`) result is checked, so a silent failure can't leave two valid tokens.

### Explicitly OUT OF SCOPE (owner's decision — this is a CV project)

- **Benign double-refresh grace window.** Strict reuse detection can false-positive when an honest client fires `/refresh` twice near-simultaneously (two tabs, retries) and the second call sees the now-used token. **Deliberately NOT handled.** Do not add it.
- **No transaction** around the mint+revoke pair or the breach-revoke loop. Each `UpdateRefreshTokenAsync` saves independently, so a mid-loop failure can leave a chain *partially* revoked (risk = under-revoking, e.g. the thief's link survives) — accepted as-is.

## Database setup

- `db/01-create-schema.sql` – full schema with tables, constraints, indexes, triggers, RLS
- `db/02-seed.sql` – seed data
- EF Core uses `Npgsql`. **No EF migrations** — schema is managed via the raw SQL scripts above.
- **Naming**: despite `EFCore.NamingConventions` being referenced, `UseSnakeCaseNamingConvention` is **not** actually configured. Mapping works only because entity **property names are written literally in snake_case** (e.g. `user_id`, `target_table`) so they match the DB columns 1:1. **Gotcha:** a typo'd property name therefore maps to a non-existent column and fails at runtime (this happened with `admin_actions.trget_table` → fixed to `target_table` + explicit `HasColumnName`). When adding columns, match the SQL column name exactly or add `.HasColumnName(...)`.

## No tests / how to verify manually

There is no test project. All verification is manual via the HTTP API (see `learning_platform.http` or the Scalar UI at `/scalar/v1`). Useful checks for the recently-added features:

- **Rate limiting**: hammer any endpoint >20×/min from the same IP → expect `429` + "Too many attempts" body. Counter is per-IP, fixed 1-min window.
- **Failed-login logging**: POST `api/User/login` with a wrong password → `LogWarning` in console (IP only) **and** a `failed` row in `login_logs`.
- **403 logging**: call an `api/user/...` endpoint with a JWT for a _different_ user (non-admin) → `403` + `LogWarning`.
- **Admin-action auditing**: log in as an `admin`, delete _another_ user via `api/user/Delete/{userId}` → a `delete` row appears in `admin_actions` (immutable; FK/trigger requires the caller's `admin_id` to actually have role `admin` in the DB, or the audit insert is silently swallowed by the repo's try/catch). Self-deletion does **not** create an audit row.

## CI / infra

`.github/workflows/` is empty. No CI or deployment pipeline configured.

**Commit workflow:** this is a solo learning repo — most commits are made (and pushed) **directly to `master`**, no feature branch / PR. Don't branch unless explicitly asked.

## Important Note
please update this file by removing or adding anything after any major change happen or when a certain info is not longer useful.
