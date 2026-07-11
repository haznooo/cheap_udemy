using DataAccess.Common;
using DataAccess.Data;
using DataAccess.Dto;
using DataAccess.Entities;
using DataAccess.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using static DataAccess.Common.clsPageResult;

namespace DataAccess.Repositories
{
    public class CoursesRepository(AppDbContext context) : ICoursesRepository
    {


        public async Task<PageResult<CourseDto>> GetAllCourses(
            int pageNumber, int pageSize,
            string? search = null, int? categoryId = null, string? level = null,
            decimal? minPrice = null, decimal? maxPrice = null, string? sortBy = null)
        {

            try
            {
                // The public catalog only ever shows published, non-deleted courses.
                var query = context.Courses
                    .Where(c => c.status == "published" && c.deleted_at == null);

                if (!string.IsNullOrWhiteSpace(search))
                {
                    // Escape LIKE wildcards so user input like "50%" or "c_sharp" is treated
                    // as a literal substring, not a SQL pattern. ILIKE '%term%' is accelerated
                    // by the gin_trgm_ops GIN index on title.
                    var escapedSearch = search.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
                    query = query.Where(c => EF.Functions.ILike(c.title, $"%{escapedSearch}%", @"\"));
                }
                if (categoryId.HasValue)
                {
                    query = query.Where(c => c.category_id == categoryId.Value);
                }
                if (!string.IsNullOrWhiteSpace(level))
                {
                    query = query.Where(c => c.level == level);
                }
                if (minPrice.HasValue)
                {
                    query = query.Where(c => c.price >= minPrice.Value);
                }
                if (maxPrice.HasValue)
                {
                    query = query.Where(c => c.price <= maxPrice.Value);
                }

                var totalCount = await query.CountAsync();

                query = sortBy switch
                {
                    "price_asc" => query.OrderBy(c => c.price),
                    "price_desc" => query.OrderByDescending(c => c.price),
                    "rating" => query.OrderByDescending(c => c.avg_rating),
                    "newest" => query.OrderByDescending(c => c.published_date),
                    // No explicit sort + a search term: order by trigram relevance.
                    _ when !string.IsNullOrWhiteSpace(search)
                        => query.OrderByDescending(c => EF.Functions.TrigramsSimilarity(c.title, search!)),
                    _ => query.OrderByDescending(c => c.published_date)
                };

                var items = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .AsNoTracking()
                    .Select(c => new CourseDto
                    {
                        CourseId = c.course_id,
                        Title = c.title,
                        CategoryId = c.category_id,
                        // EF Core handles the join automatically behind the scenes here:
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
                    })
                    .ToListAsync();

                return new PageResult<CourseDto>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
            }
            catch (Exception ex)
            {
                // Consider logging via ILogger instead of Console.WriteLine in production!
                Console.WriteLine(ex.ToString());
                return null;
            }
        }

        // Single course with full detail. Returns null if it does not exist, has been
        // soft-deleted, or (for a draft/retired course) the caller is neither the
        // owning instructor nor an admin — anonymous/other callers only see published.
        public async Task<CourseDto> GetCourseById(int courseId, int? callerId = null, bool isAdmin = false)
        {
            try
            {
                var c = await context.Courses
                    .Where(c => c.course_id == courseId && c.deleted_at == null
                        && (c.status == "published" || isAdmin || c.instructor_id == callerId))
                    .AsNoTracking()
                    .FirstOrDefaultAsync();

                if (c == null) return null;

                string categoryName = await context.Categories
                    .Where(cat => cat.category_id == c.category_id)
                    .Select(cat => cat.name)
                    .FirstOrDefaultAsync() ?? "Unknown Category";

                string instructorName = await context.Users
                    .Where(u => u.user_id == c.instructor_id)
                    .Select(u => u.username)
                    .FirstOrDefaultAsync();

                return new CourseDto
                {
                    CourseId = c.course_id,
                    Title = c.title,
                    CategoryId = c.category_id,
                    CategoryName = categoryName,
                    InstructorId = c.instructor_id,
                    InstructorName = instructorName,
                    code = c.code,
                    description = c.description,
                    thumbnail_url = c.thumbnail_url,
                    price = c.price,
                    status = c.status,
                    level = c.level,
                    estimated_duration_minutes = c.estimated_duration_minutes,
                    avg_rating = c.avg_rating,
                    reviews_count = c.reviews_count,
                    course_metadata = c.course_metadata == null ? null : new Entities.json.course_metadata
                    {
                        lessons_count = c.course_metadata.lessons_count,
                        enrollments_count = c.course_metadata.enrollments_count
                    },
                    published_date = c.published_date,
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
        }

        // Returns the owning instructor id, or null if the course does not exist.
        // Used for ownership checks (e.g. setting a thumbnail).
        public async Task<int?> GetCourseInstructorId(int courseId)
        {
            return await context.Courses
                .Where(c => c.course_id == courseId && c.deleted_at == null)
                .Select(c => (int?)c.instructor_id)
                .FirstOrDefaultAsync();
        }

        // Resolves the course a section belongs to, or null if the section
        // does not exist. Used for ownership checks on lessons (lesson -> section -> course).
        public async Task<int?> GetCourseIdBySection(int sectionId)
        {
            return await context.Sections
                .Where(s => s.section_id == sectionId)
                .Select(s => (int?)s.course_id)
                .FirstOrDefaultAsync();
        }

        // Returns the replaced file name so the caller can remove it from storage
        // once the new one is safely persisted.
        public async Task<(bool Success, string? OldFileName)> UpdateThumbnail(int courseId, string fileName)
        {
            try
            {
                var course = await context.Courses.FirstOrDefaultAsync(c => c.course_id == courseId);
                if (course == null) return (false, null);

                var oldFileName = course.thumbnail_url;
                course.thumbnail_url = fileName;
                course.updated_at = DateTime.UtcNow;
                await context.SaveChangesAsync();
                return (true, oldFileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return (false, null);
            }
        }

        public async Task<CourseDto> AddNewCourse(CourseEntitiy CourseE) 
        {
        
            context.Courses.Add(CourseE);

            try 
            {
                var results = await context.SaveChangesAsync();
                if (results <= 0)
                {
                    return null;
                }
                string CategoryName = await context.Categories.Where(c => c.category_id == CourseE.category_id)
                    .Select(c => c.name)
                    .FirstOrDefaultAsync() ?? "Unknown Category";
      ;

                return new CourseDto
                {
                    CourseId = CourseE.course_id,
                    Title = CourseE.title,
                    CategoryId = CourseE.category_id,
                    CategoryName = CategoryName,
                    InstructorId = CourseE.instructor_id,
                    code = CourseE.code,
                    description = CourseE.description,
                    price = CourseE.price,
                    status = CourseE.status,
                    level = CourseE.level,
                    estimated_duration_minutes = CourseE.estimated_duration_minutes,
                    course_metadata = new Entities.json.course_metadata
                    {
                        lessons_count = CourseE.course_metadata.lessons_count,
                        enrollments_count = CourseE.course_metadata.enrollments_count

                    },
                    published_date = CourseE.published_date

                };
            }
            catch (Exception ex)
            {
                // Consider logging via ILogger instead of Console.WriteLine in production!
                Console.WriteLine(ex.ToString());
                return null;
            }
          

            

            
        }

        public async Task<PageResult<LessonDto>> GetCourseLessons(int courseId, int pageNumber, int pageSize, int? callerId = null, bool isAdmin = false)
        {
            try
            {
                // Non-deleted courses only; published-only unless the caller is the
                // owning instructor or an admin (matches GetCourseById's owner bypass).
                var query = context.Lessons
                    .Where(l => l.section.course_id == courseId
                        && l.section.course.deleted_at == null
                        && (l.section.course.status == "published" || isAdmin || l.section.course.instructor_id == callerId))
                    .AsNoTracking();

                var totalCount = await query.CountAsync();

                var items = await query
                    .OrderBy(l => l.section.sort_order)
                    .ThenBy(l => l.sort_order)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(l => new LessonDto
                    {
                        LessonId = l.lesson_id,
                        SectionId = l.section_id,
                        Title = l.title,
                        SortOrder = l.sort_order,
                        EstimatedDurationMinutes = l.estimated_duration_minutes,
                    })
                    .ToListAsync();

                return new PageResult<LessonDto>
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

        // Section list itself carries no published/deleted filter — the caller (service layer)
        // must authorize access via CanViewCourseContentAsync before calling this, same as lessons.
        public async Task<PageResult<SectionDto>> GetCourseSections(int courseId, int pageNumber, int pageSize)
        {
            try
            {
                var query = context.Sections
                    .Where(s => s.course_id == courseId)
                    .AsNoTracking();

                var totalCount = await query.CountAsync();

                var items = await query
                    .OrderBy(s => s.sort_order)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(s => new SectionDto
                    {
                        SectionId = s.section_id,
                        CourseId = s.course_id,
                        Title = s.title,
                        SortOrder = s.sort_order
                    })
                    .ToListAsync();

                return new PageResult<SectionDto>
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

        public async Task<SectionEntitiy> AddNewSection(SectionEntitiy section)
        {
            context.Sections.Add(section);

            try
            {
                var results = await context.SaveChangesAsync();
                if (results <= 0)
                {
                    return null;
                }

                return section;
            }
            catch (Exception ex)
            {
                // Consider logging via ILogger instead of Console.WriteLine in production!
                Console.WriteLine(ex.ToString());
                return null;
            }
        }

        public async Task<SectionDto?> UpdateSectionAsync(int sectionId, string? title, int? sortOrder)
        {
            try
            {
                var section = await context.Sections.FirstOrDefaultAsync(s => s.section_id == sectionId);
                if (section == null) return null;

                if (!string.IsNullOrWhiteSpace(title)) section.title = title;
                if (sortOrder.HasValue) section.sort_order = sortOrder.Value;

                await context.SaveChangesAsync();

                return new SectionDto
                {
                    SectionId = section.section_id,
                    CourseId = section.course_id,
                    Title = section.title,
                    SortOrder = section.sort_order
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
        }

        // Hard delete — sections have no soft-delete column. Cascades to the section's
        // lessons at the DB level (fk_lessons_sections ON DELETE CASCADE); any lesson media
        // in storage is not cleaned up by this path (accepted leak, same as the other
        // accepted leaks documented in CLAUDE.md's media-cleanup section).
        public async Task<bool> DeleteSectionAsync(int sectionId)
        {
            try
            {
                var section = await context.Sections.FirstOrDefaultAsync(s => s.section_id == sectionId);
                if (section == null) return false;

                context.Sections.Remove(section);
                var results = await context.SaveChangesAsync();
                return results > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return false;
            }
        }

        // Soft-delete: sets deleted_at + removal_reason (columns already existed, unused
        // until now). Same anonymize-not-erase convention as user delete elsewhere in this
        // app — avoids FK issues with reviews/payments referencing the course, and makes the
        // course fully invisible/gone from every read path without an actual row DELETE.
        public async Task<bool> SoftDeleteCourseAsync(int courseId, string? removalReason)
        {
            try
            {
                var course = await context.Courses.FirstOrDefaultAsync(c => c.course_id == courseId && c.deleted_at == null);
                if (course == null) return false;

                course.deleted_at = DateTime.UtcNow;
                course.removal_reason = removalReason;
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

        public async Task<CourseDto?> UpdateCourseAsync(int courseId, string? title, string? description, string? code, decimal? price, string? level, int? categoryId)
        {
            try
            {
                var course = await context.Courses.FirstOrDefaultAsync(c => c.course_id == courseId && c.deleted_at == null);
                if (course == null) return null;

                if (!string.IsNullOrWhiteSpace(title)) course.title = title;
                if (description != null) course.description = description;
                if (!string.IsNullOrWhiteSpace(code)) course.code = code;
                if (price.HasValue) course.price = price.Value;
                if (!string.IsNullOrWhiteSpace(level)) course.level = level;
                if (categoryId.HasValue) course.category_id = categoryId.Value;
                course.updated_at = DateTime.UtcNow;

                await context.SaveChangesAsync();

                string categoryName = await context.Categories
                    .Where(c => c.category_id == course.category_id)
                    .Select(c => c.name)
                    .FirstOrDefaultAsync() ?? "Unknown Category";

                string instructorName = await context.Users
                    .Where(u => u.user_id == course.instructor_id)
                    .Select(u => u.username)
                    .FirstOrDefaultAsync() ?? "";

                return new CourseDto
                {
                    CourseId = course.course_id,
                    Title = course.title,
                    CategoryId = course.category_id,
                    CategoryName = categoryName,
                    InstructorId = course.instructor_id,
                    InstructorName = instructorName,
                    code = course.code,
                    description = course.description,
                    thumbnail_url = course.thumbnail_url,
                    price = course.price,
                    status = course.status,
                    level = course.level,
                    estimated_duration_minutes = course.estimated_duration_minutes,
                    avg_rating = course.avg_rating,
                    reviews_count = course.reviews_count,
                    course_metadata = course.course_metadata,
                    published_date = course.published_date
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
        }

        public async Task<PageResult<CourseDto>> GetCoursesByInstructorIdAsync(int instructorId, int pageNumber, int pageSize)
        {
            try
            {
                var query = context.Courses
                    .Where(c => c.instructor_id == instructorId && c.deleted_at == null)
                    .AsNoTracking();

                var totalCount = await query.CountAsync();

                var items = await query
                    .OrderByDescending(c => c.created_date)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(c => new CourseDto
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
                    })
                    .ToListAsync();

                return new PageResult<CourseDto>
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

        // Returns any non-deleted course entity regardless of status (for owner/admin operations).
        public async Task<CourseEntitiy?> GetRawCourseAsync(int courseId)
        {
            try
            {
                return await context.Courses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.course_id == courseId && c.deleted_at == null);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
        }

        // Updates course status and returns the updated CourseDto, or null on failure.
        // Sets published_date on the first publish.
        public async Task<CourseDto?> UpdateCourseStatusAsync(int courseId, string newStatus)
        {
            try
            {
                var course = await context.Courses.FirstOrDefaultAsync(c => c.course_id == courseId);
                if (course == null) return null;

                course.status = newStatus;
                course.updated_at = DateTime.UtcNow;
                if (newStatus == "published" && course.published_date == null)
                    course.published_date = DateTime.UtcNow;

                await context.SaveChangesAsync();

                string categoryName = await context.Categories
                    .Where(c => c.category_id == course.category_id)
                    .Select(c => c.name)
                    .FirstOrDefaultAsync() ?? "Unknown Category";

                string instructorName = await context.Users
                    .Where(u => u.user_id == course.instructor_id)
                    .Select(u => u.username)
                    .FirstOrDefaultAsync() ?? "";

                return new CourseDto
                {
                    CourseId = course.course_id,
                    Title = course.title,
                    CategoryId = course.category_id,
                    CategoryName = categoryName,
                    InstructorId = course.instructor_id,
                    InstructorName = instructorName,
                    code = course.code,
                    description = course.description,
                    thumbnail_url = course.thumbnail_url,
                    price = course.price,
                    status = course.status,
                    level = course.level,
                    estimated_duration_minutes = course.estimated_duration_minutes,
                    avg_rating = course.avg_rating,
                    reviews_count = course.reviews_count,
                    course_metadata = course.course_metadata,
                    published_date = course.published_date
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
        }

        public async Task<bool> DoesCategoryExistAsync(int categoryId)
        {
            return await context.Categories.AnyAsync(c => c.category_id == categoryId);
        }

    }
}
