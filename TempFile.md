# untie testing

I want to add unit tests to this CV project. Read this whole brief before responding.

## Context about me

I've spent the last several weeks learning unit testing in C#/.NET from scratch (i kinda forget the nich details a little but it is fine since i still remember all the concepts) ,
lesson by lesson, and I want to now apply it to a real-ish codebase (this project).
I'm NOT new to programming — I'm comfortable with async/await, EF Core & ADO.NET,
dependency injection, and interfaces. Don't re-teach those; teach only their
_testing implications_ when relevant.

What I've already learned and can use confidently:

- xUnit: [Fact], [Theory] + [InlineData], Assert.Equal / Throws / All / Contains
- AAA structure (Arrange-Act-Assert), one behaviour = one test method,
  test naming ClassName_Method_Scenario, test independence
- Equivalence partitions + boundary values
- Complex return types (assert properties; reference vs value equality)
- Collections & FluentAssertions (.Should().BeEquivalentTo(...))
- Stateful objects (sequences of calls, invariants)
- Dependencies: hand-written fakes, then Moq (Setup/Returns), then
  interaction verification (Verify / Times / It.IsAny / It.Is)
- The "false green" family of traps (swallowed exceptions, always-true
  predicates, vacuous asserts over empty collections, missing test data)

What I have NOT done yet: EF Core
integration tests, Testcontainers, TDD, and most improtnatly is _where_ should exaclty i add tests and _why_ which is something i will learn right now alongside implementing. I'll do integration testing in a later
since i did not learn it — keep THIS round to pure unit tests only.

## Ground rules — different from my lessons

- This time **I** set up the environment myself: I'll install the NuGet packages
  and create the test project. Do NOT scaffold it for me. Walk me through what to
  install / create if I ask, but let me do the hands-on setup.
- Keep teaching me the way I like it: front-load the _why_ and a short concrete
  syntax example before I write the real test, then let me implement it myself and
  review what I wrote. Don't just hand me finished tests.
- Verify claims by actually building/running (dotnet test), not by guessing.
- start from the easier unite tests (if avaialbe) to the hardest.

## The goal (important — this is a CV project, not a real product)

I do NOT want tests for everything. I want a **small, curated set of tests where
each one showcases a different, well-chosen unit-testing technique** — variety over
coverage. Think of it as a portfolio of "here's me demonstrating I understand X".
Each test should be a clean, textbook example of a specific technique worth showing.

Techniques I want represented across the set (pick the real class/method in THIS
project that best fits each — skip any that genuinely don't fit the codebase):

- Async testing with Moq (async Task test, ReturnsAsync, await Assert.ThrowsAsync)
  — this is the priority since my API is async everywhere; START HERE.
- Mocking a dependency + interaction verification (Verify / Times / It.IsAny)
- Exception / edge-case testing (guard clauses, invalid input)
- Data-driven testing with [Theory] + [InlineData]
- Readable structural assertions with FluentAssertions (BeEquivalentTo)
- try to not force all kinds , if one kind does not really fit the current api then there is not need to force it , it actually worth noting why this test does not fit here

## What I want you to do first

1. Explore this codebase and tell me what's actually here — the classes, the
   dependencies, what's testable and what isn't and _why_. Since it's an API, point out the
   async service/repository methods that are the natural first targets.
2. Propose an ordered plan starting with an ASYNC test first, then progressing
   through the technique list above. For each planned test, label it with the
   technique it demonstrates and the exact real class/method it targets.
3. Wait for me to pick / adjust before we start. Then we go one test at a time:
   you teach the concept + show a tiny generic syntax example, I write the real
   test against this project, you review.

---

# PROGRESS LOG (Claude: read this on resume — everything above is the original brief, everything below is current state)

_Last updated: 2026-07-19_

## Environment: READY ✅

- `tests/Business.Tests` created by the owner (xUnit + Moq 4.20.72 + FluentAssertions **8.10.0**, `Microsoft.NET.Test.Sdk`), references **only** `Business/Business.csproj`. Registered in `cheap_udemy.slnx` (4 projects now). Template's `UnitTest1.cs` deleted. `<Using Include="Xunit" />` is in the csproj — test files don't need `using Xunit;`.
- Incident during setup: `Business.Tests.csproj` got accidentally deleted from disk (symptom: no syntax colors, test explorer empty, "just a class in a folder"). Claude recreated it verbatim + re-added to the sln. Lesson noted: no colors/IntelliSense = file not in a loaded project.
- 2 known build warnings, documented in CLAUDE.md → "Tests / how to verify": `NU1902` AngleSharp (transitive via unwired HtmlSanitizer — ignore for now) and `MSB3277` EF Relational 10.0.4 vs 10.0.9 — fix is pinning `Microsoft.EntityFrameworkCore.Relational 10.0.9` in `DataAccess.csproj`, **not yet applied** (owner's to-do, optional).

## The agreed 5-test plan & status — ALL DONE ✅ (2026-07-19)

**Format change:** on 2026-07-19 the owner asked Claude to write tests 2-6 directly (overriding the "owner writes, Claude reviews" rule) and commit each one separately. All 6 techniques are now implemented, verified green (`dotnet test`: **18 passed** — theories expand), and committed one commit per test on `master`.

1. ✅ **Async + ReturnsAsync** — `EnrollmentService.EnrollStudent`, course-not-found → `NotFound`. `tests/Business.Tests/Services/EnrollmentServiceTests.cs`.
2. ✅ **Same technique, object-steered branch** — paid-course branch → `BadRequest` (DTO built to pass every earlier guard). Same file.
3. ✅ **[Theory] + [InlineData] boundary values** — `UserSignUp` username guard: 8 invalid cases → `BadRequest` (no setups — guard precedes all repo calls) + 2 boundary-valid cases (1/20 chars) walked through to full success. `AuthenticationServiceTests.cs`.
4. ✅ **Verify / Times / It.Is interaction testing** — `AdminService.SetUserStatus`: ban → `RevokeAllForUser` `Times.Once`; unban → `Times.Never`; unban-of-suspended → audit `LogAsync` with `It.Is` on `actionType == "unsuspend"`. Shared-arrange `CreateSut` helper. `AdminServiceTests.cs`.
5. ✅ **FluentAssertions BeEquivalentTo** — `LoginUser` happy path, whole `LoginResponse` incl. nested `Profile` record; real BCrypt hash in arrange; mixed-case email in the request proves normalization via the exact-arg lowercase setup. `AuthenticationServiceTests.cs`.
6. ✅ **Capstone: state branching + interactions** — `RefreshAccessToken`: replayed token (`replaced_by_id != null`) → `RevokeBreachedChainAsync` `Times.Once` + Unauthorized; logged-out token (`is_used`, no child) → same 401 but `Times.Never` on both revoke-chain and mint — the Verifys are what distinguish the branches. `RefreshTokenServiceTests.cs`.

**Deliberately skipped (note the why in the repo eventually):** `Assert.ThrowsAsync` — nothing in the Business layer throws; failures are `MyResult` values, asserted via `FailureType`. Stateful-sequence tests — services are stateless per call.

## Conventions established in the model test (keep applying)

- `Setup` with **exact argument values**, not `It.IsAny` — the setup then doubles as an implicit argument check (`It.IsAny` belongs in `Verify`, test 4).
- Assert the **`ErrorType` enum**, never the error-message string (message = UI copy).
- Name the real object `sut`; mocks named after their role (`enrollmentRepository`).
- No setups for repo methods the tested path never reaches (loose-mock defaults cover them).
- Naming: `Method_Scenario_ExpectedOutcome` (e.g. `EnrollStudent_CourseDoesNotExist_ReturnsNotFound`).

## Working format

Original format (teach → owner writes → Claude reviews) was used for the plan/teaching phase; the owner then switched to Claude-writes for tests 2-6 (explicit request, 2026-07-19). If a future round adds tests (e.g. integration tests), re-ask which format the owner wants.
