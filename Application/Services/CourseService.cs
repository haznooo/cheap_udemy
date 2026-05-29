using AngleSharp.Io;
using Business.Common;
using Business.Dto.Request;
using Business.Dto.Rsponse;
using DataAccess.Common;
using DataAccess.Data;
using DataAccess.Dto;
using DataAccess.Entities;
using DataAccess.Entities.json;
using DataAccess.Repositories;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;
using static Business.Common.clsPageResult;

namespace Business.Services
{
    public class CourseService(AppDbContext context)
    {

        public async Task<MyResult<PageResult<CourseDto>>> GetAllCourses(int pageNumber, int pageSize)
        {

            if(pageNumber <= 0 || pageSize <= 0)
            {
                return MyResult<PageResult<CourseDto>>.Failure(ErrorType.BadRequest, "Invalid page number or page size.");
            }

            CoursesRepository repo = new CoursesRepository(context);


            var R = await repo.GetAllCourses(pageNumber, pageSize);

            if(R == null || R.Items == null)
            {
                return MyResult<PageResult<CourseDto>>.Failure(ErrorType.NotFound, "No courses found.");
            }

            Business.Common.clsPageResult.PageResult<CourseDto> pageResult = new Business.Common.clsPageResult.PageResult<CourseDto>
            {
                Items = R.Items,
                PageNumber = R.PageNumber,
                PageSize = R.PageSize,
                TotalCount = R.TotalCount,
            };

            return MyResult<PageResult<CourseDto>>.Success(pageResult);

        }

        public async Task<MyResult<CourseDto>> AddNewCourse(AddCourseRequest request)
        {

            if(request.InstructorId <= 0)
            {
                return MyResult<CourseDto>.Failure(ErrorType.BadRequest, "Invalid instructor ID.");
            }
            if(request.CategoryId <= 0 || request.CategoryId > 5)
            {
                return MyResult<CourseDto>.Failure(ErrorType.BadRequest, "Invalid category ID.");
            }

            CourseEntitiy courseEntity = new CourseEntitiy
            {
                instructor_id = request.InstructorId,
                title = request.Title,
                category_id = request.CategoryId,
                description = request.Description,
                code = request.Code,
                price = request.Price,
                status = request.Status,
                level = request.level,
                estimated_duration_minutes = 0,
                created_date = DateTime.UtcNow,
                course_metadata = new course_metadata
                {
                    lessons_count = 0,
                    enrollments_count = 0
             
                }
            };
            var courseService = new CourseService(context);


            CoursesRepository repo = new CoursesRepository(context);
            var result = await repo.AddNewCourse(courseEntity);

            if(result == null)
            {
                return MyResult<CourseDto>.Failure(ErrorType.NotFound, "Failed to add new course.");
            }

            return MyResult<CourseDto>.Success(result);

        }

        public async Task<MyResult<SectionEntitiy>> AddNewSection(AddSectionRequest request)
        {

            SectionEntitiy sectionEntity = new SectionEntitiy
            {
                title = request.Title,
                sort_order = request.SortOrder,
                course_id = request.CourseId
            };

            CoursesRepository repo = new CoursesRepository(context);
            var result = await repo.AddNewSection(sectionEntity);

            if (result == null)
            {
                return MyResult<SectionEntitiy>.Failure(ErrorType.NotFound, "Failed to add new section.");
            }

            return MyResult<SectionEntitiy>.Success(new SectionEntitiy
            {
                section_id = result.section_id,
                title = result.title,
                sort_order = result.sort_order,
                course_id = result.course_id
            });
        }
    }
}
