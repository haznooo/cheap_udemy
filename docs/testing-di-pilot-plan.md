# Plan: First unit-testing pilot on `UserService` (deferred)

> **Status: NOT implemented yet.** Documents *what we will do later*. Nothing in the repo
> changes until this is deliberately picked up. Scope is intentionally **one service only**.

## Context

Goal: a real, referenceable unit-testing example in this repo. Today no service can be
unit-tested because every service **`new`s its repository inline**, hard-wiring the real
database in:

```csharp
// Application/Services/UserService.cs — repeated in every method
UserAndProfileRepository UserRepository = new UserAndProfileRepository(context);
```

With the repo constructed *inside* the method, a test can never substitute a fake — there's
no seam. This pilot is the **smallest change that makes exactly one service (`UserService`)
unit-testable**, plus a test project and a handful of real tests. It is deliberately a
**one-service slice** of the repository pattern; the full app-wide DI/interface rollout stays
a separate, later effort (see "Deferred").

### Concept recap (why the shape below)

- To unit-test `UserService` you fake its **dependency** (the repository), not the service itself.
  So the **repository** gets an interface; `UserService` gets it **injected** instead of `new`-ing it.
  `UserService` does **not** need its own interface yet — that's only needed the day we unit-test
  the controller.
- The interface holds the repo's **full public surface** (standard "Extract Interface"), not just
  the methods a test touches. A test mocks whatever subset a given path calls; that never shrinks
  the interface.
- Test the code that makes **decisions** (validation, 404/409 mapping, wrong-password branches),
  **not** the repository's EF queries (those belong in future integration tests).

## The change (when picked up)

### 1. Extract `IUserAndProfileRepository`
- New file `data_access_layer/Repositories/IUserAndProfileRepository.cs`.
- Contains **every public method** of `UserAndProfileRepository` (all ~20: `AddUserAsync`,
  `DeleteUserAsync_Anonymize` (both overloads), `UpdateUserPasswordAsync`, `GetUsersAsync`,
  `GetUserByCredentialsAsync`, `GetUserByEmailAsync`, `GetUserByIdAsync`, `GetUserProfileByIdAsync`,
  `UpdateUserAvatarAsync`, `AddUserProfileAsync`, `UpdateUserProfileByUserIdAsync`, `IsEmailUsedAsync`,
  `IsUserActiveAsync`, `DoesUserExistByIdAsync`, `DoesUserProfileExistAsync`,
  `GetHashedPasswordByEmailAsync`, `PromotUserToInstructorAsync`, `GetHashedPasswordByIdAsync`,
  `GetUserIdByEmail`).
- `class UserAndProfileRepository(...) : IUserAndProfileRepository` — no body changes.

### 2. Inject the repo into `UserService`
- Change the primary constructor from `UserService(AppDbContext context)` to
  `UserService(IUserAndProfileRepository userRepo, AppDbContext context)`.
  (Keep `context` for now — `UpdatePassword` still needs it to `new RefreshTokenService(context)`;
  see wrinkle below.)
- Delete the seven inline `new UserAndProfileRepository(context)` lines; use the injected `userRepo`.
- **Keep the caller (`UserController`) `new`-ing `UserService`** — the "UserService only" boundary.
  The controller constructs it as `new UserService(new UserAndProfileRepository(context), context)`.
  **No DI registration, no other service/repo/controller touched.**

### 3. Add the test project
- `tests/Application.Tests/Application.Tests.csproj`, added to `cheap_udemy.slnx`, referencing
  `Application/Business.csproj`.
- Packages: `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `Moq`.
- One test class: `UserServiceTests.cs`.

### 4. First tests (the actual evidence)
Construct `new UserService(mockRepo.Object, null!)` and assert on `MyResult<T>` outcomes. Good
first cases (all pure, none reach `RefreshTokenService`):

| Method | Case | Setup → Expected |
| --- | --- | --- |
| `GetUserProfile` | not found | repo returns `null` → `Failure(NotFound)` |
| `GetUserProfile` | bad id | `userId <= 0` → `Failure(BadRequest)` (no repo call) |
| `AddUserProfile` | 409 | `DoesUserExistByIdAsync`→true, `DoesUserProfileExistAsync`→true → `Failure(Conflict)` |
| `AddUserProfile` | user missing | `DoesUserExistByIdAsync`→false → `Failure(NotFound)` |
| `UpdatePassword` | short new pwd | `NewPassword` len < 5 → `Failure(BadRequest)` (no repo call) |
| `UpdatePassword` | wrong old pwd | repo returns `BCrypt.HashPassword("right")`, request sends `"wrong"` → `Failure(Unauthorized)` |
| `DeleteUser` | wrong pwd | same BCrypt trick → `Failure(Unauthorized)` |
| `SetAvatar` | not found | `UpdateUserAvatarAsync`→false → `Failure(NotFound)` |

Note on BCrypt: `BCrypt.Verify` is a **static pure function**, not a dependency — tests seed the
mock with a real hash from `BCrypt.HashPassword(...)`; no need to fake BCrypt.

## Known wrinkles (documented, not fixed in this pilot)

- **`UpdatePassword` success path is not cleanly isolatable yet.** After a successful update it does
  `new RefreshTokenService(context).RevokeAllForUser(userId)` — a second hard dependency. Under the
  "UserService only" scope we test its **failure branches only** (short password / bad id /
  user-not-found / wrong old password). Fully testing the success path needs `IRefreshTokenService`
  injected too — **deferred**.

## Deferred (out of scope now — the "later" list)

- Full repository-pattern rollout across all services/repos + **real DI registration** (drop the
  inline `new`s app-wide). CLAUDE.md's "biggest refactor target."
- `IUserService` + injecting the service into `UserController` (needed only to unit-test controllers).
- Integration tests against a throwaway Postgres (**Testcontainers**).
- **CI**: a GitHub Actions job running `dotnet test` (`.github/workflows/` is currently empty).
- Docker isolated environment; practicing **TDD** (test-first) as a workflow.
- When this pilot is implemented, update **CLAUDE.md** (add a "Testing" section, tick the DI/interface
  progress).

## Verification (when implemented)

1. `dotnet build` — solution still compiles (repo implements the interface; `UserController`
   constructs `UserService` with the repo).
2. `dotnet test` — the new `UserServiceTests` pass.
3. Smoke-run the API (`dotnet run --project learning_platform`) and hit a couple of `api/user/me/*`
   routes to confirm the injection change didn't break runtime behavior.
