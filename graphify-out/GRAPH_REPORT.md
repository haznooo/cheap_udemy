# Graph Report - .  (2026-06-28)

## Corpus Check
- Corpus is ~26,173 words - fits in a single context window. You may not need a graph.

## Summary
- 628 nodes · 948 edges · 38 communities (24 shown, 14 thin omitted)
- Extraction: 99% EXTRACTED · 1% INFERRED · 0% AMBIGUOUS · INFERRED: 8 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- [[_COMMUNITY_Enrollment & Pagination DTOs|Enrollment & Pagination DTOs]]
- [[_COMMUNITY_Review DTOs|Review DTOs]]
- [[_COMMUNITY_Business Layer Dependencies|Business Layer Dependencies]]
- [[_COMMUNITY_Result Pattern & Error Types|Result Pattern & Error Types]]
- [[_COMMUNITY_Course & Section DTOs|Course & Section DTOs]]
- [[_COMMUNITY_Lesson & Content Block DTOs|Lesson & Content Block DTOs]]
- [[_COMMUNITY_User Profile Data DTOs|User Profile Data DTOs]]
- [[_COMMUNITY_API Controllers & Reference Data|API Controllers & Reference Data]]
- [[_COMMUNITY_Refresh Token Service|Refresh Token Service]]
- [[_COMMUNITY_Course Entity & Pagination|Course Entity & Pagination]]
- [[_COMMUNITY_Dev Docs & Architecture Concepts|Dev Docs & Architecture Concepts]]
- [[_COMMUNITY_User Account Management DTOs|User Account Management DTOs]]
- [[_COMMUNITY_Auth Request DTOs|Auth Request DTOs]]
- [[_COMMUNITY_Lesson Entity & JSONB|Lesson Entity & JSONB]]
- [[_COMMUNITY_App Launch Configuration|App Launch Configuration]]
- [[_COMMUNITY_DataAccess DI & Config|DataAccess DI & Config]]
- [[_COMMUNITY_Authorization Handler|Authorization Handler]]
- [[_COMMUNITY_Media Service|Media Service]]
- [[_COMMUNITY_EF Core AppDbContext|EF Core AppDbContext]]
- [[_COMMUNITY_Content Block Format (JSONB)|Content Block Format (JSONB)]]
- [[_COMMUNITY_Admin Action Service|Admin Action Service]]
- [[_COMMUNITY_Login Log Service|Login Log Service]]
- [[_COMMUNITY_Admin Action Repository|Admin Action Repository]]
- [[_COMMUNITY_Login Log Repository|Login Log Repository]]
- [[_COMMUNITY_Logout Request DTO|Logout Request DTO]]
- [[_COMMUNITY_Refresh Token Response DTO|Refresh Token Response DTO]]
- [[_COMMUNITY_Content Block DTO|Content Block DTO]]
- [[_COMMUNITY_Login DTO|Login DTO]]
- [[_COMMUNITY_Admin Action Entity|Admin Action Entity]]
- [[_COMMUNITY_Category Entity|Category Entity]]
- [[_COMMUNITY_Country Entity|Country Entity]]
- [[_COMMUNITY_Course Metadata JSONB|Course Metadata JSONB]]
- [[_COMMUNITY_Lesson Metadata JSONB|Lesson Metadata JSONB]]
- [[_COMMUNITY_Login Log Entity|Login Log Entity]]
- [[_COMMUNITY_Payment Entity|Payment Entity]]
- [[_COMMUNITY_User Lesson Progress Entity|User Lesson Progress Entity]]
- [[_COMMUNITY_OpenCode LSP Config|OpenCode LSP Config]]
- [[_COMMUNITY_JSONB Columns Concept|JSONB Columns Concept]]

## God Nodes (most connected - your core abstractions)
1. `MyResult` - 38 edges
2. `CourseDto` - 19 edges
3. `UserAndProfileRepository` - 18 edges
4. `EnrollmentDto` - 14 edges
5. `CourseController` - 14 edges
6. `CoursesRepository` - 13 edges
7. `EnrollmentRepository` - 13 edges
8. `CourseService` - 12 edges
9. `LessonDto` - 11 edges
10. `ReviewDto` - 11 edges

## Surprising Connections (you probably didn't know these)
- `Learning Platform API README` --references--> `cheap-udemy Project Documentation`  [INFERRED]
  README.md → CLAUDE.md
- `Code Reviewer Agent` --references--> `Manual Field-by-Field Mapping`  [EXTRACTED]
  .claude/agents/code-reviewer.md → CLAUDE.md
- `Code Reviewer Agent` --references--> `Repository Exception Swallowing (null/false returns)`  [EXTRACTED]
  .claude/agents/code-reviewer.md → CLAUDE.md
- `Code Reviewer Agent` --references--> `MyResult<T> Result Pattern`  [EXTRACTED]
  .claude/agents/code-reviewer.md → CLAUDE.md
- `Code Reviewer Agent` --references--> `Snake Case Property Naming Gotcha`  [EXTRACTED]
  .claude/agents/code-reviewer.md → CLAUDE.md

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Refresh Token Security System** — claude_md_refreshtokenrotation, claude_md_chainbreachdetection, claude_md_absoluteexpiry [EXTRACTED 1.00]
- **Agent-Based Development Review Workflow** — _claude_agents_code_reviewer_md_agent, _claude_agents_planner_md_agent, _claude_agents_security_audit_reviewer_md_agent [INFERRED 0.85]
- **Audit and Observability Layer** — claude_md_loginlogging, claude_md_adminactionauditing, claude_md_dbtriggers [INFERRED 0.85]

## Communities (38 total, 14 thin omitted)

### Community 0 - "Enrollment & Pagination DTOs"
Cohesion: 0.05
Nodes (31): Business.Common, clsPageResult, PageResult, Business.Dto.Request, DropEnrollmentRequest, Business.Dto.Request, EnrollRequest, Business.Dto.Request (+23 more)

### Community 1 - "Review DTOs"
Cohesion: 0.06
Nodes (26): AddReviewRequest, Business.Dto.Request, Business.Dto.Request, UpdateReviewRequest, Business.Services, List, Task, ReviewService (+18 more)

### Community 2 - "Business Layer Dependencies"
Cohesion: 0.04
Nodes (43): net10.0, BCrypt.Net-Next (4.2.0), CloudinaryDotNet (1.29.2), Riok.Mapperly (4.3.1), supabase-csharp (0.16.2), System.IdentityModel.Tokens.Jwt (8.19.1), Microsoft.NET.Sdk, net10.0 (+35 more)

### Community 3 - "Result Pattern & Error Types"
Cohesion: 0.09
Nodes (19): Business.Common, Error, ErrorType, Business.Common, MyResult, Business.Dto.Rsponse, UserProfileResponse, Business.Services (+11 more)

### Community 4 - "Course & Section DTOs"
Cohesion: 0.10
Nodes (23): AddCourseRequest, Business.Dto.Request, AddSectionRequest, Business.Dto.Request, Business.Dto.Request, GetCoursesRequest, Business.Dto.Request, UpdateCourseRequest (+15 more)

### Community 5 - "Lesson & Content Block DTOs"
Cohesion: 0.09
Nodes (21): Business.Dto.Request, ContentBlockRequest, Business.Dto.Request, LessonRequest, Business.Dto.Request, UpdateLessonRequest, Business.Services, List (+13 more)

### Community 6 - "User Profile Data DTOs"
Cohesion: 0.10
Nodes (12): DataAccess.Dto, UserAndProfileDto, DataAccess.Dto, UserProfileDto, DataAccess.Entities, UserEntity, DataAccess.Entities, UserProfileEntity (+4 more)

### Community 7 - "API Controllers & Reference Data"
Cohesion: 0.06
Nodes (25): ControllerBase, CategoryDto, DataAccess.Dto, CountryDto, DataAccess.Dto, Api.Controllers, CategoriesController, ActionResult (+17 more)

### Community 8 - "Refresh Token Service"
Cohesion: 0.11
Nodes (12): Business.Services, Task, RefreshTokenService, DataAccess.Dto, RefreshTokenDto, string, DataAccess.Entities, RefreshTokenEntity (+4 more)

### Community 9 - "Course Entity & Pagination"
Cohesion: 0.11
Nodes (11): clsPageResult, DataAccess.Common, PageResult, CourseEntitiy, DataAccess.Entities, DataAccess.Entities, SectionEntitiy, CoursesRepository (+3 more)

### Community 10 - "Dev Docs & Architecture Concepts"
Cohesion: 0.12
Nodes (26): Code Reviewer Agent, Planner Agent, Security Audit Reviewer Agent, Absolute Token Expiry (no sliding reset), Admin Action Auditing to DB (immutable rows), Layered Architecture (Api → Business → DataAccess), Refresh Token Reuse Detection (Chain Breach), cheap-udemy Project Documentation (+18 more)

### Community 11 - "User Account Management DTOs"
Cohesion: 0.12
Nodes (17): Business.Dto.Request, DeleteUserRequest, Business.Dto.Request, UpdatePasswordRequest, Business.Dto.Request, UserProfileRequest, IAuthorizationService, Api.Controllers (+9 more)

### Community 12 - "Auth Request DTOs"
Cohesion: 0.14
Nodes (14): Business.Dto.Request, LoginRequest, Business.Dto.Request, RefreshTokenRequest, Business.Dto.Request, SignUpRequest, Business.Dto.Rsponse, LoginResponse (+6 more)

### Community 13 - "Lesson Entity & JSONB"
Cohesion: 0.18
Nodes (8): ContentBlock, DataAccess.Entities.json, DataAccess.Entities, LessonEntity, List, Task, DataAccess.Repositories, LessonsRepository

### Community 14 - "App Launch Configuration"
Cohesion: 0.13
Nodes (15): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, applicationUrl, commandName (+7 more)

### Community 15 - "DataAccess DI & Config"
Cohesion: 0.15
Nodes (9): IConfiguration, IServiceCollection, DataAccess.DependencyInjection, DependencyInjection, Api, IConfiguration, IServiceCollection, DependencyInjection (+1 more)

### Community 16 - "Authorization Handler"
Cohesion: 0.20
Nodes (8): AuthorizationHandler, AuthorizationHandlerContext, IAuthorizationRequirement, Api.Authorization, Task, UserOwnerOrAdminHandler, Api.Authorization, UserOwnerOrAdminRequirement

### Community 17 - "Media Service"
Cohesion: 0.27
Nodes (7): Business.Services, IFormFile, string, Task, IMediaService, SupabaseMediaService, Client

### Community 18 - "EF Core AppDbContext"
Cohesion: 0.28
Nodes (5): AppDbContext, DataAccess.Data, DbContext, DbContextOptionsBuilder, ModelBuilder

### Community 19 - "Content Block Format (JSONB)"
Cohesion: 0.48
Nodes (6): BlockData, DataAccess.Entities.json, ImageBlockData, QuizBlockData, TextBlockData, VideoBlockData

### Community 20 - "Admin Action Service"
Cohesion: 0.40
Nodes (3): AdminActionService, Business.Services, Task

### Community 21 - "Login Log Service"
Cohesion: 0.40
Nodes (3): Business.Services, Task, LoginLogService

### Community 22 - "Admin Action Repository"
Cohesion: 0.40
Nodes (3): AdminActionRepository, Task, DataAccess.Repositories

### Community 23 - "Login Log Repository"
Cohesion: 0.40
Nodes (3): Task, DataAccess.Repositories, LoginLogRepository

## Knowledge Gaps
- **163 isolated node(s):** `net10.0`, `BCrypt.Net-Next (4.2.0)`, `CloudinaryDotNet (1.29.2)`, `FluentValidation.DependencyInjectionExtensions (12.1.1)`, `HtmlSanitizer (9.0.892)` (+158 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **14 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `MyResult` connect `Result Pattern & Error Types` to `Enrollment & Pagination DTOs`, `Refresh Token Service`, `Lesson & Content Block DTOs`, `Review DTOs`?**
  _High betweenness centrality (0.212) - this node is a cross-community bridge._
- **Why does `CourseDto` connect `Result Pattern & Error Types` to `Course Entity & Pagination`, `Course & Section DTOs`?**
  _High betweenness centrality (0.053) - this node is a cross-community bridge._
- **What connects `net10.0`, `BCrypt.Net-Next (4.2.0)`, `CloudinaryDotNet (1.29.2)` to the rest of the system?**
  _163 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Enrollment & Pagination DTOs` be split into smaller, more focused modules?**
  _Cohesion score 0.05432692307692308 - nodes in this community are weakly interconnected._
- **Should `Review DTOs` be split into smaller, more focused modules?**
  _Cohesion score 0.0636734693877551 - nodes in this community are weakly interconnected._
- **Should `Business Layer Dependencies` be split into smaller, more focused modules?**
  _Cohesion score 0.04336734693877551 - nodes in this community are weakly interconnected._
- **Should `Result Pattern & Error Types` be split into smaller, more focused modules?**
  _Cohesion score 0.08792270531400966 - nodes in this community are weakly interconnected._