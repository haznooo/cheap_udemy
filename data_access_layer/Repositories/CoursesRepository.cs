using DataAccess.Common;
using DataAccess.Data;
using DataAccess.Dto;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using static DataAccess.Common.clsPageResult;

namespace DataAccess.Repositories
{
    public class CoursesRepository(AppDbContext context)
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
                    // ILIKE '%term%' is accelerated by the gin_trgm_ops GIN index on title.
                    query = query.Where(c => EF.Functions.ILike(c.title, $"%{search}%"));
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

        public async Task<List<LessonDto>> GetCourseLessons(int courseId)
        {
            try
            {
                return await context.Lessons
                    .Where(l => l.section.course_id == courseId)
                    .OrderBy(l => l.section.sort_order)
                    .ThenBy(l => l.sort_order)
                    .AsNoTracking()
                    .Select(l => new LessonDto
                    {
                        LessonId = l.lesson_id,
                        SectionId = l.section_id,
                        Title = l.title,
                        SortOrder = l.sort_order,
                        EstimatedDurationMinutes = l.estimated_duration_minutes,
                    })
                    .ToListAsync();
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




    }
}
