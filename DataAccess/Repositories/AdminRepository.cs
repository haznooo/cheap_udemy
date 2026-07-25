using DataAccess.Data;
using DataAccess.Dto;
using DataAccess.Entities.json;
using DataAccess.Interfaces;
using Microsoft.EntityFrameworkCore;
using static DataAccess.Common.clsPageResult;

namespace DataAccess.Repositories
{
    // Admin-exclusive data access (see IAdminRepository for the "why one admin repo").
    // Spans several tables on purpose — it's organized by ACTOR (the admin), not by
    // table, because every method here is used only by AdminService. Shared queries
    // stay in their per-table repo.
    public class AdminRepository(AppDbContext context) : IAdminRepository
    {
        // Paged list of accounts for the admin user-management view
        public async Task<PageResult<UserListItemDto>> GetUsersAsync(int pageNumber, int pageSize, string? status = null, string? search = null)
        {
            var query = context.Users.AsNoTracking(); // Better performance for Read-Only

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(u => u.status == status);

            if (!string.IsNullOrWhiteSpace(search))
            {
                // Substring match across username, display name (LEFT-joined, null-safe:
                // a null column just doesn't match) and email. Wildcards are escaped so
                // input like "50%" or "a_b" is treated literally, not as a LIKE pattern.
                // Plain ILIKE (no trigram index) — the admin user table is small and this
                // is an admin-only path, so a sequential scan is fine.
                var escaped = search.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
                var pattern = $"%{escaped}%";
                query = query.Where(u =>
                    EF.Functions.ILike(u.username, pattern, @"\")
                    || EF.Functions.ILike(u.UserProfile.display_name, pattern, @"\")
                    || EF.Functions.ILike(u.email, pattern, @"\"));
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(u => u.create_date)
                .ThenByDescending(u => u.user_id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new UserListItemDto
                {
                    UserId = u.user_id,
                    Username = u.username,
                    Email = u.email,
                    Role = u.role,
                    Status = u.status,
                    CreateDate = u.create_date,
                    DisplayName = u.UserProfile.display_name,
                    ImageUrl = u.UserProfile.image_url
                })
                .ToListAsync();

            return new PageResult<UserListItemDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        // Admin read: deliberately NOT filtered to active — an admin must see
        // banned/suspended accounts too (the service maps deleted → 404). Unlike the
        // self read (GetUserByIdAsync), Profile is null when the user has no profile
        // row (signup no longer creates one).
        public async Task<UserAndProfileDto?> GetUserWithProfileForAdminAsync(int userId)
        {
            var R = await context.Users.Where(u => u.user_id == userId)
             .Select(u => new
             {
                 UserId = u.user_id,
                 u.username,
                 u.email,
                 u.role,
                 u.status,

                 HasProfile = u.UserProfile != null,
                 u.UserProfile.bio,
                 u.UserProfile.image_url,
                 u.UserProfile.display_name
             }).FirstOrDefaultAsync();

            if (R == null) return null;

            return new UserAndProfileDto
            {
                UserId = R.UserId,
                Username = R.username,
                Email = R.email,
                Role = R.role,
                Status = R.status,
                Profile = R.HasProfile
                    ? new UserProfileDto
                    {
                        DisplayName = R.display_name,
                        Bio = R.bio,
                        ImageUrl = R.image_url
                    }
                    : null
            };
        }

        public async Task<bool> DoesUserExistByIdAsync(int userId)
        {
            return await context.Users.AnyAsync(e => e.user_id == userId && e.status != "deleted");
        }

        // Status + role in one query, deliberately NOT filtered by status — the
        // ban/suspend/unban flow needs to see banned/suspended targets. Returns null
        // if the user doesn't exist.
        public async Task<UserStatusRoleDto?> GetUserStatusAndRoleAsync(int userId)
        {
            try
            {
                return await context.Users
                    .Where(u => u.user_id == userId)
                    .Select(u => new UserStatusRoleDto { Status = u.status, Role = u.role })
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return null;
            }
        }

        public async Task<bool> UpdateUserStatusAsync(int userId, string status)
        {
            try
            {
                var user = await context.Users.FirstOrDefaultAsync(u => u.user_id == userId);
                if (user == null) return false;

                user.status = status;
                await context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return false;
            }
        }

        // Admin moderation list: every course regardless of status, INCLUDING
        // soft-deleted/tombstoned ones (unlike the public GetAllCourses, which is
        // published + non-deleted only). Newest-first by created_date. Optional
        // substring search over title + instructor username, and an optional status
        // filter. Plain ILIKE (admin-only path).
        //
        // The status filter: a courses.status value (published/draft/retired/suspended)
        // matches only NON-tombstoned rows — a taken-down course keeps its pre-takedown
        // status value, so without this a "suspended" filter would hand back dead courses.
        // The pseudo-value "deleted" selects tombstones instead (deleted_at != null),
        // since soft-delete is a separate column, not a status value. Unfiltered (null)
        // still returns every course, tombstoned included.
        public async Task<PageResult<AdminCourseDto>> GetAllCoursesForAdminAsync(
            int pageNumber, int pageSize, string? status = null, string? search = null)
        {
            try
            {
                var query = context.Courses.AsQueryable();

                if (!string.IsNullOrWhiteSpace(status))
                {
                    if (status == "deleted")
                        query = query.Where(c => c.deleted_at != null);
                    else
                        query = query.Where(c => c.status == status && c.deleted_at == null);
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var escaped = search.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
                    var pattern = $"%{escaped}%";
                    query = query.Where(c =>
                        EF.Functions.ILike(c.title, pattern, @"\")
                        || EF.Functions.ILike(c.instructor.username, pattern, @"\"));
                }

                var totalCount = await query.CountAsync();

                var items = await query
                    .OrderByDescending(c => c.created_date)
                    .ThenByDescending(c => c.course_id)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .AsNoTracking()
                    .Select(c => new AdminCourseDto
                    {
                        CourseId = c.course_id,
                        Title = c.title,
                        CategoryId = c.category_id,
                        CategoryName = c.category.name,
                        InstructorId = c.instructor_id,
                        InstructorName = c.instructor.username,
                        code = c.code,
                        description = c.description,
                        thumbnail_url = c.thumbnail_url,
                        price = c.price,
                        status = c.status,
                        level = c.level,
                        estimated_duration_minutes = c.estimated_duration_minutes,
                        avg_rating = c.avg_rating,
                        reviews_count = c.reviews_count,
                        published_date = c.published_date,
                        deleted_at = c.deleted_at,
                        removal_reason = c.removal_reason,
                    })
                    .ToListAsync();

                return new PageResult<AdminCourseDto>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
        }

        // Every lesson's content blocks across the whole course — so a caller (the admin
        // takedown) can gather the referenced media file names BEFORE the rows are gone.
        public async Task<List<List<ContentBlock>>> GetCourseLessonContentBlocksAsync(int courseId)
        {
            try
            {
                // Materialize the lesson rows (content_blocks deserializes as part of
                // normal entity materialization — the proven jsonb read path here) and
                // pull the blocks in memory, rather than projecting the jsonb column.
                var lessons = await context.Lessons
                    .Where(l => l.section.course_id == courseId)
                    .AsNoTracking()
                    .ToListAsync();

                return lessons
                    .Select(l => l.content_blocks)
                    .Where(b => b != null)
                    .ToList()!;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return new List<List<ContentBlock>>();
            }
        }

        // Admin takedown (permanent content purge): hard-deletes the course's sections —
        // the DB CASCADE (fk_lessons_sections / fk_user_lesson_progress_lessons) removes
        // their lessons and progress rows, and being a real SQL DELETE the sync triggers
        // still fire — then tombstones the course row via deleted_at so it 404s everywhere
        // while its FK-protected payment/enrollment records survive. thumbnail_url is
        // nulled here; the caller best-effort deletes the actual bucket object.
        public async Task<bool> PurgeCourseContentAsync(int courseId, string? removalReason)
        {
            try
            {
                var course = await context.Courses.FirstOrDefaultAsync(c => c.course_id == courseId && c.deleted_at == null);
                if (course == null) return false;

                await context.Sections.Where(s => s.course_id == courseId).ExecuteDeleteAsync();

                course.deleted_at = DateTime.UtcNow;
                course.removal_reason = removalReason;
                course.thumbnail_url = null;
                course.updated_at = DateTime.UtcNow;

                var results = await context.SaveChangesAsync();
                return results > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return false;
            }
        }

        // Wipes every review for a course — used by the admin takedown, where the course
        // is gone for good (unlike suspend, which keeps reviews for a possible restore).
        public async Task<bool> DeleteAllReviewsForCourseAsync(int courseId)
        {
            try
            {
                await context.Reviews.Where(r => r.course_id == courseId).ExecuteDeleteAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return false;
            }
        }
    }
}
