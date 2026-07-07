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
| `api/User`                       | Authentication (`signUp`, `login`, `refresh`, `logout`). Signup is **account-only** (`Username`/`Email`/`Password` — no nested profile; `LoginResponse.Profile` is `null` on signup). The profile is created afterwards via `POST api/user/me/profile/add`, the avatar via `POST api/user/me/avatar`. |
| `api/user`                       | User profile (`me/*` self routes + admin-only `{userId}` routes)   |
| `api/Courses`                    | Courses (+ owner-only `{courseId}/media` upload)                   |
| `api/Courses/{courseId}/reviews` | Reviews                                                            |
| `api/Lessons`                    | Lessons (`POST add`, `PUT update/{id}`, `GET get/{id}`, `DELETE {id}` — delete is hard-delete, owner/admin via section→course→instructor, cleans up the lesson's bucket media) |
| `api/Enrollments`                | Enrollments                                                        |
| `api/categories`                 | Lookup list (authenticated — `[Authorize]`, no anonymous access)   |

> Note: `api/User` (auth) and `api/user` (profile) differ only by case — both are real, distinct controllers.
> There is **no longer** an `api/media` controller — generic media upload was replaced by owner-scoped `POST api/Courses/{courseId}/media`.

**Course endpoints** (`api/Courses`):

| Method & path              | Auth          | Notes                                                                                                                                                                                                                                                                                                                                |
| -------------------------- | ------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `POST get`                 | anonymous     | Paged catalog. **Only published, non-deleted** courses. Body (`GetCoursesRequest`) supports optional `SearchTerm` (trigram `ILIKE`, uses the `gin_trgm_ops` index), `CategoryId`, `Level`, `MinPrice`, `MaxPrice`, `SortBy` (`newest`/`price_asc`/`price_desc`/`rating`; defaults to trigram relevance when searching, else newest). |
| `GET {courseId}`           | anonymous     | Single **published** course, full detail incl. `course_metadata`. 404 if draft/retired/deleted/missing.                                                                                                                                                                                                                              |
| `POST add`                 | authenticated | `instructor_id` is taken from the JWT (`NameIdentifier`), **never** the body. Category validated against the `categories` table.                                                                                                                                                                                                     |
| `PUT {courseId}/thumbnail` | owner / admin | Multipart `file` (JPG/PNG ≤5 MB) → `IMediaService.UploadCourseThumbnailAsync` into the **public `course-thumbnail` bucket**, file name persisted to `courses.thumbnail_url`; the replaced file is then best-effort deleted from storage. Ownership = the course's instructor or an admin; otherwise 403.                                |
| `POST section/add`         | owner / admin | Adds a section. Ownership verified via `CheckCourseEditPermission` (course's instructor or an admin); `CourseId`/`Title` validated. Otherwise 403.                                                                                                                                                                                    |
| `POST {courseId}/media`    | owner / admin | Multipart `file` (JPG/PNG/MP4/MOV ≤5 MB) → `IMediaService.UploadCourseMediaAsync`, returns `{ url }` (the stored file name for embedding in lesson `content_blocks`). Ownership checked via `CheckCourseEditPermission` **before** upload. Replaces the old `POST api/media/upload`.                                                    |
| `GET {courseId}/lessons`   | **enrolled** / owner / admin | Curriculum outline (lesson titles, no content), **paged** (`pageNumber`/`pageSize` query params, default 1/10). **Enrollment-gated**: owner instructor or admin always; otherwise requires an **active/completed enrollment** in a published, non-deleted course. Everyone else → `404` (curriculum is hidden, not browsable without enrolling). |
| `GET instructor/me`        | authenticated | The caller's own courses as an instructor (id from the JWT).                                                                                                                                                                                                                                                                         |
| `GET instructor/{id}`      | admin         | Another instructor's courses. Admin-only (`[Authorize(Roles="admin")]`).                                                                                                                                                                                                                                                             |

> Also on `api/Courses` (not expanded here): `PATCH {courseId}`, `POST {courseId}/publish`, `POST {courseId}/unpublish` — all owner/admin via `CheckCourseEditPermission`.

**User-profile endpoints** (`api/user`): self-actions are `me/*` (target id comes from the **access token**, never the URL — anti-IDOR); cross-user access is **admin-only** id routes.

| Method & path           | Auth          | Notes                                                                                                              |
| ----------------------- | ------------- | ----------------------------------------------------------------------------------------------------------------- |
| `GET me`                | authenticated | Read your own profile (`UserProfileResponse`).                                                                     |
| `POST me/avatar`        | authenticated | Multipart `file` (JPG/PNG ≤**3 MB**, extension **and** `Content-Type` both validated) → `IMediaService.UploadAvatarAsync` into the **public `avatar` bucket**, file name persisted to `users_profile.image_url` (**upsert** — creates the profile row if the user hasn't made one yet, since signup no longer does); the replaced file is then best-effort deleted from storage. The **only** way to set an avatar (no request DTO carries an image name). |
| `POST me/password`      | authenticated | Change your own password (revokes refresh tokens).                                                                 |
| `POST me/profile`       | authenticated | Create your own profile (409 if one already exists).                                                               |
| `PUT me/profile`        | authenticated | Update your own profile fields (404 if none exists yet).                                                           |
| `POST me/delete`        | authenticated | Self-delete (anonymize). **Never** audited (not an admin action). Best-effort deletes the avatar file from storage (name captured **before** the anonymize trigger wipes the profile row). |
| `GET {userId}`          | admin         | Read another user's profile.                                                                                       |
| `POST {userId}/delete`  | admin         | Admin delete another user (anonymize). **Always** audited (`admin_actions` + `LogInformation`). Also best-effort deletes the avatar file from storage. |

> The `me/*` routes take no id and need no `UserOwnerOrAdmin` policy — identity is the token. The id routes are gated by `[Authorize(Roles="admin")]` (the codebase's first pure role gate; admin power elsewhere is still inline "owner-or-admin").

## Key conventions (what's actually in use)

- **Result pattern**: Services return `MyResult<T>` (`Business.Common`) — internal-only, for passing errors between layers. Controllers never serialize it; they call `MapFailure(result)` on the shared base class.
- **Error contract (RFC 7807 ProblemDetails)**: **every** non-2xx response body is a ProblemDetails JSON (`{ type, title, status, detail, traceId }`) — the frontend reads `detail`. Produced in two places: (1) `ApiControllerBase.MapFailure` (`learning_platform/Controllers/ApiControllerBase.cs`) translates `MyResult` failures via `Problem(...)` — `NotFound` → 404, `BadRequest` → 400, `Conflict` → 409, and `Unauthorized` → **403** (service-level Unauthorized means "authenticated but not allowed", e.g. ownership/enrollment checks; only `AuthenticationController` passes 401 explicitly because failed login/refresh IS a credential problem — 401 tells the SPA "re-login", 403 tells it "not yours"); (2) middleware in `Program.cs` for everything that never reaches a controller: `AddProblemDetails()` + `UseExceptionHandler()` (unhandled exceptions → 500, no stack trace) + `UseStatusCodePages()` (empty-body 401/403 from the JWT middleware) + the rate limiter's `OnRejected` (429). Model-binding 400s are the framework's `ValidationProblemDetails` (same family; field errors under `errors` instead of `detail`). All controllers inherit `ApiControllerBase`, which also provides `CallerId`/`CallerRole` (from the JWT) and the `MissingIdentity()` 401 guard.
- **Validation**: done **manually** with inline `if` checks inside services (e.g. `if (request.UserId <= 0) return ...Failure(BadRequest, ...)`). FluentValidation is **not** wired up despite being referenced.
- **Mapping**: done **manually by hand** (e.g. `CoursesRepository.AddNewCourse` builds the DTO field-by-field). No source-generated mapper is actually in use.
- **Object creation**: services and repositories are created with `new` at the call site (controllers `new` services; services `new` repositories). They are **not** registered in DI and have **no interfaces** — _except_ `IMediaService`/`SupabaseMediaService` which **is** registered (see `Program.cs`) and injected via constructor. Most controllers receive `AppDbContext` directly; `UserController`/`CourseController` also take `IMediaService`, `LessonsController` takes `LessonService` (whose ctor takes `AppDbContext` + Supabase `Client` + `IMediaService`).
- **Pagination**: `clsPageResult.PageResult<T>` is a **single shared type** in `DataAccess.Common` (the old duplicate in `Business.Common` was deleted; services/controllers now `using (static) DataAccess.Common.clsPageResult` and just pass the repo's `PageResult` straight through — no more field-by-field copy across the layer boundary). **Why DataAccess and not Business:** it's the lowest layer (references nothing; everything above sees it), so repos can *return* the type directly and it flows up to the API with **zero mapping** — and it sits next to the `DataAccess.Dto` payloads it wraps (which already serialize straight to the client). Putting it in `Business` would forbid repos from returning it (DataAccess can't reference Business — backward dependency) and force the mapping copy back. `PageResult<T>` is a pure, dependency-free container (no EF/domain logic), so the lowest layer acts as the de-facto shared kernel. Trade-off accepted: the public API response shape is coupled to a DataAccess type; only worth revisiting if the API contract ever needs to stay stable while the DB layer changes. Applied consistently across **every** list endpoint now, including per-course sub-lists that used to return a bare `List<T>` — course lessons (`GET api/Courses/{courseId}/lessons`), enrollment progress (`GET api/Enrollments/progress/{courseId}`), and course reviews (`GET api/Courses/{courseId}/reviews/get`) all take `pageNumber`/`pageSize` query params (default 1/10) and return `PageResult<T>`, same as the catalog/enrollment-list endpoints. Owner's call: even though those sub-lists are bounded per-course, paging them anyway keeps every list endpoint uniform for API consumers (e.g. a future mobile client) and avoids unbounded response size if a course/review count grows large.
- **Error handling in repos**: repositories `try/catch (Exception)`, `Console.WriteLine` the error, and `return null`/`false`. So "DB failed" and "no rows" look identical to the caller. Repos still do **not** use `ILogger` (see Logging below for where `ILogger` _is_ used).
- **JSONB columns**: `LessonEntity.content_blocks`, `LessonEntity.lesson_metadata`, `CourseEntitiy.course_metadata`. Npgsql dynamic JSON serialization enabled in `data_access_layer/DependencyInjection.cs` (`NpgsqlConnection.GlobalTypeMapper.EnableDynamicJson()`).
- **Auth**: JWT bearer with a custom `UserOwnerOrAdmin` policy + handler (checks user ID match or admin role). Issuer `CheapUdemyApi`, audience `CheapUdemyApiUsers`, expiry 20 min. Refresh tokens rotated via `RefreshTokenService`, stored in `user_refresh_tokens` as a **deterministic SHA-256 hash** (`RefreshTokenService.HashRefreshToken`; high-entropy random tokens don't need a salt — **only passwords use BCrypt**). Self-actions take the user id from the **access token** (`me/*` routes), not the URL — so there's no per-request ownership check to get wrong (anti-IDOR). Cross-user/admin access uses `[Authorize(Roles="admin")]`. The `UserOwnerOrAdmin` policy + handler are now **unused** (the `me/*` refactor removed the last callers) and can be deleted in a follow-up. See **"Refresh-token rotation"** below for the full `/refresh` flow (reuse detection + absolute expiry).
- **Rate limiting** (in use): configured in `Program.cs` via `AddRateLimiter`. A single **global limiter** — fixed window, **20 requests/min per IP** (partitioned by `RemoteIpAddress`), `QueueLimit = 0`, rejection status `429`. `app.UseRateLimiter()` runs _before_ auth; the limiter's `OnRejected` writes a friendly ProblemDetails body on 429 (no exact limits exposed). No per-endpoint `[EnableRateLimiting]` policies — everything is covered uniformly by the global limiter. Relies on `UseForwardedHeaders` for the client IP behind a proxy (trusted-proxy restriction is still a TODO, so `X-Forwarded-For` is currently spoofable).
- **Logging (`ILogger`)**: used in **controllers** for security events only — failed logins (`LogWarning`, IP only, never credentials) and failed refresh attempts in `AuthenticationController`, and successful admin deletes (`LogInformation`) in `UserController`. `ILogger<T>` is injected via the controllers' primary constructors. Repositories still use `Console.WriteLine` (not migrated).
- **Login logging** (DB): `LoginLogService` → `LoginLogRepository` write a row to `login_logs` (`success`/`failed`) on signup and login attempts.
- **Admin-action auditing** (DB): `AdminActionService` → `AdminActionRepository` write an immutable row to `admin_actions` (mirrors the login-log `new`d pattern). Currently called from `UserController.DeleteUser` when an admin deletes _another_ user (`action_type: "delete"`, `target_table: "users"`). `old_value`/`new_value` are JSONB and must stay small & non-sensitive (no passwords/tokens). `action_type` is constrained to `'create','update','delete','ban','unban'`.
- **Media**: `IMediaService` (`Application/Services/MediaService.cs`) exposes **purpose-named methods, one per bucket — callers never pass a bucket name**: `UploadAvatarAsync` / `UploadCourseThumbnailAsync` / `UploadCourseMediaAsync` plus matching best-effort `DeleteXxxAsync` methods (never throw; a storage hiccup during cleanup must never fail the request). Uploads return the stored file name (a GUID + extension, always server-generated — **no request DTO anywhere accepts a client-supplied file name**). Bucket names are constants in `MediaBuckets`, used only inside the service. Three buckets: **`course-media` (private)** — paid lesson content, read back via 1-hour signed URLs minted in `LessonService`; **`course-thumbnail` (public, jpeg/png ≤5 MB bucket-enforced)** — course thumbnails (they appear on the anonymous catalog, so they must be publicly readable); **`avatar` (public, jpeg/png ≤3 MB bucket-enforced)** — user avatars. Public buckets are read back via the permanent public object URL `{Supabase:Url}/storage/v1/object/public/{bucket}/{fileName}`. The upload sets `ContentType = file.ContentType` explicitly — the supabase-csharp default is `text/plain`, which MIME-restricted buckets reject. There is **no generic upload endpoint**. Three ownership-scoped upload paths: `PUT api/Courses/{courseId}/thumbnail` (JPG/PNG ≤5 MB) → `courses.thumbnail_url`, `POST api/user/me/avatar` (JPG/PNG ≤3 MB, self) → `users_profile.image_url`, and `POST api/Courses/{courseId}/media` (JPG/PNG/MP4/MOV ≤5 MB) → returns the file name for embedding in lesson `content_blocks` (owner/admin, permission checked **before** upload). **Profile add/update never touch `image_url`**; the avatar changes only via the avatar endpoint. **API read-back note:** responses return the raw stored file name (`ImageUrl`, `thumbnail_url`); the frontend builds the public URL itself.
- **Media cleanup (orphaned-file policy)**: storage deletes always happen **after** the DB change is committed, and are **best-effort** (failure is logged and swallowed — a leaked file is harmless, a broken DB reference is not). Cleanup paths: avatar replace + account delete (self & admin; the file name is captured **before** `DeleteUserAsync_Anonymize`, because the DB trigger deletes the profile row), thumbnail replace, lesson update (old vs new `content_blocks` file-name diff), and lesson delete. Lesson media is only deleted if **no other lesson still references it** — `LessonsRepository.IsMediaReferencedByOtherLessonsAsync` does a raw-SQL `content_blocks::text LIKE '%name%'` check (GUID names ⇒ no false positives; on query error it reports "referenced" so the file is kept). Only supabase-hosted block media counts (`ImageBlockData.Url`, `VideoBlockData` with `Provider == "supabase"`); external URLs/ids are never deleted, and `DeleteFromBucketAsync` refuses names containing path separators. **Accepted leaks (deliberate, don't “fix” without asking):** files uploaded via `POST {courseId}/media` but never saved into a lesson; old thumbnails uploaded before the `course-thumbnail` bucket existed (they sit in `course-media`); partially-failed cleanup. A background orphan-sweep job would cover these but is out of scope for this project.
- **DB triggers**: Course publish date, instructor role verification, enrollment progress sync, admin-action verification (`trg_verify_admin_action` rejects inserts whose `admin_id` isn't an admin) + immutability (`trg_lock_admin_actions` blocks UPDATE/DELETE), user anonymization on delete — all at the PostgreSQL level (see `db/01-create-schema.sql`).

## Honest state of the code (read before assuming a pattern exists)

**Referenced in `.csproj` but NOT used anywhere** (leftovers from the abandoned clean-architecture attempt — don't treat them as active patterns, and feel free to remove them):

- `MediatR` — no `IRequest` / handlers exist. No CQRS.
- `FluentValidation` — no `AbstractValidator`. Validation is manual.
- `Riok.Mapperly` — no `[Mapper]` class. Mapping is manual.
- `Mapster` — unused.
- `HtmlSanitizer` — installed but not yet wired in (input sanitizing is a planned addition).
- `CloudinaryDotNet` — package still referenced but no longer used anywhere (the stray `using` in `MediaService.cs` was removed); the real implementation is Supabase-only.

**Known rough edges / planned work:**

- No DI / interfaces for most services & repositories (everything is `new`d). Biggest refactor target.
- Repos swallow exceptions and return `null`/`false`, still via `Console.WriteLine` (not migrated to `ILogger`). `ILogger` is only used in controllers for security events.
- Done since this list was written: ✅ rate limiting, ✅ security logging (`ILogger`), ✅ login logging, ✅ admin-action auditing, ✅ media upload now persisted (course thumbnail + user avatar), ✅ course catalog filtered to published/non-deleted with search/filter/sort over the `pg_trgm` index, ✅ course `instructor_id` taken from the JWT (not the body), ✅ GET course-detail + GET profile read endpoints, ✅ **refresh-token hardening** (SHA-256 hashing, user id from the access token, absolute expiry, rotation reuse detection — see "Refresh-token rotation" below), ✅ **authorization holes closed on lessons/sections/media** (`POST api/Lessons/add` now `[Authorize]` + section→course→instructor owner-or-admin check; `POST api/Courses/section/add` now ownership-checked; `POST api/media/upload` now `[Authorize]`; anonymous lesson reads — `GET api/Lessons/{id}` and `GET api/Courses/{courseId}/lessons` — restricted to published, non-deleted courses), ✅ **authenticated-by-default fallback policy** (`Program.cs` sets `FallbackPolicy = RequireAuthenticatedUser()`; anonymous endpoints — the whole `AuthenticationController`, plus the public Course/Lesson reads — are explicitly `[AllowAnonymous]`), ✅ **enrollment authorization** (`EnrollmentController` now `[Authorize]`; user id taken from the JWT not the body — `UserId` removed from the enroll/drop/mark-progress DTOs; enrollment-list reads scoped to the owner, course-roster reads to the owning instructor/admin), ✅ **login no longer leaks which emails exist** (unknown email and wrong password both return 401/"invalid credentials"; unknown-email path runs a dummy BCrypt verify to equalize timing — `AuthenticationService.DummyPasswordHash`), ✅ **signup/login split out of `UserService` into `AuthenticationService`** (`UserSignUp`/`LoginUser` now live in `Application/Services/AuthenticationService.cs`, used only by `AuthenticationController`; `UserService` is profile/account-only — `DeleteUser`, `UpdatePassword`, `GetUserProfile`, `SetAvatar`, `AddUpdateUserProfile`), ✅ **password change revokes refresh tokens** (`UserService.UpdatePassword` → `RefreshTokenService.RevokeAllForUser` → `RefreshTokenRepository.RevokeAllRefreshTokensAsync`, bulk `ExecuteUpdate` marking used+revoked), ✅ **enrollment business rules** (`EnrollmentService.EnrollStudent` now fetches `CourseEnrollmentInfoDto` via `EnrollmentRepository.GetCourseEnrollmentInfoAsync` and rejects: draft/retired/deleted courses → `404 "Course not found."`, instructor self-enroll → `400`, and paid courses `price > 0` → `400 "requires payment"` since there's no payment flow), ✅ **access-control hardening (frontend prep)** — **supersedes** the two earlier clauses about anonymous lesson reads and `POST api/media/upload [Authorize]`: (a) **lessons are now fully enrollment-gated** — both `GET api/Courses/{courseId}/lessons` (outline) and `GET api/Lessons/{id}` (content) require owner/admin or an **active/completed enrollment**; anonymous → 401, non-enrolled → 404 (logic in `EnrollmentRepository.CanViewCourseContentAsync`); (b) **`/me` self-routes + admin-only id routes** on `api/user` (and `GET api/Enrollments/me`, `GET api/Courses/instructor/me`) — self-actions take the id from the token, cross-user routes are `[Authorize(Roles="admin")]` (first pure role gates); self-delete vs admin-delete split (only admin-delete audits); (c) **generic `MediaController` deleted**, replaced by owner-scoped `POST api/Courses/{courseId}/media`; (d) **reviews**: reading now requires login (`GET …/reviews` no longer `[AllowAnonymous]`), `DeleteReview` is **author-or-admin only** (instructors can't censor reviews), `AddReview` now checks the course is published/non-deleted and the enrollment is active/completed), ✅ **ProblemDetails error contract** (see "Error contract" in Key conventions: shared `ApiControllerBase` with `MapFailure`/`CallerId`/`MissingIdentity`, global exception handler + status-code pages + 429 `OnRejected` — every non-2xx body is now RFC 7807; the ~20 copy-pasted failure switch expressions and the per-controller `CallerId` copies were deleted; **behavior change:** ownership/enrollment failures that used to be 401 — and Enrollment/Review's empty-body `Forbid()` — are now **403 with a body**; only auth endpoints return 401), ✅ **pagination extended to the remaining list endpoints** (`CoursesRepository.GetCourseLessons`, `EnrollmentRepository.GetUserCourseProgressAsync`, `ReviewRepository.GetReviewsByCourseIdAsync` — and their service/controller callers — switched from a bare `List<T>` to `PageResult<T>` with `pageNumber`/`pageSize` query params, matching every other list endpoint), ✅ **media lifecycle** (`IMediaService` split into purpose-named upload/delete methods — no bucket parameter; thumbnails moved to the new **public `course-thumbnail` bucket** (they were unviewable before: raw file names from the private bucket, never signed); replaced/deleted avatars, thumbnails, and lesson media are now best-effort deleted from storage after the DB commit — see "Media cleanup" in Key conventions; new `DELETE api/Lessons/{id}` hard-delete, owner/admin, cleans up the lesson's media).
**Still planned / NOT yet done (owner will do later):**

- ⬜ **Input sanitizing (HtmlSanitizer)** — referenced but unwired. Free-text fields that need it before they're a real stored-XSS surface: review `comment`, lesson `content_blocks`, profile `bio`/`display_name`, course `description`.
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

- **Rate limiting**: hammer any endpoint >20×/min from the same IP → expect `429` + a ProblemDetails body ("Too many requests. Please try again later."). Counter is per-IP, fixed 1-min window.
- **Error contract**: any failing request returns `application/problem+json`; quick checks — no token on `GET api/user/me` → 401 ProblemDetails (from `UseStatusCodePages`), wrong password on login → 401 with `detail: "invalid credentials"`, `GET api/Courses/999999` → 404 with `detail: "Course not found."`, malformed JSON body → 400 `ValidationProblemDetails`.
- **Failed-login logging**: POST `api/User/login` with a wrong password → `LogWarning` in console (IP only) **and** a `failed` row in `login_logs`.
- **403 logging**: call an `api/user/...` endpoint with a JWT for a _different_ user (non-admin) → `403` + `LogWarning`.
- **Admin-action auditing**: log in as an `admin`, delete _another_ user via `api/user/Delete/{userId}` → a `delete` row appears in `admin_actions` (immutable; FK/trigger requires the caller's `admin_id` to actually have role `admin` in the DB, or the audit insert is silently swallowed by the repo's try/catch). Self-deletion does **not** create an audit row.

## Custom agents (`.claude/agents/`)

Before starting non-trivial work, check whether one of this project's custom
subagents fits — names are self-explanatory, no need to open/scan their
definitions first, just judge from the name+trigger below whether one is
useful for the task at hand:

- `code-reviewer` — after writing/changing code, before calling it done.
- `security-audit-reviewer` — after touching auth, tokens, login, user data,
  or anything security-sensitive.
- `planner` — only when the user explicitly asks to plan first; not automatic.
- `motivational-agent` — when the user wants encouragement or perspective on
  progress; grounds it in real commits/CLAUDE.md state, not generic hype.

When spawning any of them, hand over enough concrete project context (what
changed, relevant `file:line`s, current state, and pointers to the relevant
CLAUDE.md sections) so they can reason from the actual project structure
instead of assuming or guessing — each spawn starts cold with no memory of
this conversation.

## CI / infra

`.github/workflows/` is empty. No CI or deployment pipeline configured.

**Commit workflow:** this is a solo learning repo — most commits are made (and pushed) **directly to `master`**, no feature branch / PR. Don't branch unless explicitly asked.

## Important Note
please update this file by removing or adding anything after any major change happen or when a certain info is not longer useful.
