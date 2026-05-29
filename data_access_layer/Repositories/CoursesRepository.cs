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


        public async Task<PageResult<CourseDto>> GetAllCourses(int pageNumber, int pageSize)
        {

            try
            {
                var totalCount = await context.Courses.CountAsync();

                var items = await context.Courses
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
