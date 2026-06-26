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
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using static Business.Common.clsPageResult;

namespace Business.Services
{
    public class CourseService(AppDbContext context)
    {

        public async Task<MyResult<PageResult<CourseDto>>> GetAllCourses(GetCoursesRequest request)
        {

            if(request.PageNumber <= 0 || request.PageSize <= 0)
            {
                return MyResult<PageResult<CourseDto>>.Failure(ErrorType.BadRequest, "Invalid page number or page size.");
            }

            CoursesRepository repo = new CoursesRepository(context);


            var R = await repo.GetAllCourses(
                request.PageNumber, request.PageSize,
                request.SearchTerm, request.CategoryId, request.Level,
                request.MinPrice, request.MaxPrice, request.SortBy);

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

        public async Task<MyResult<CourseDto>> AddNewCourse(AddCourseRequest request, int instructorId)
        {

            if(instructorId <= 0)
            {
                return MyResult<CourseDto>.Failure(ErrorType.BadRequest, "Invalid instructor ID.");
            }
            if(request.CategoryId <= 0)
            {
                return MyResult<CourseDto>.Failure(ErrorType.BadRequest, "Invalid category ID.");
            }
            // Validate against the categories table instead of a hardcoded upper bound.
            bool categoryExists = await context.Categories.AnyAsync(c => c.category_id == request.CategoryId);
            if (!categoryExists)
            {
                return MyResult<CourseDto>.Failure(ErrorType.BadRequest, "Category does not exist.");
            }

            CourseEntitiy courseEntity = new CourseEntitiy
            {
                instructor_id = instructorId,
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

            CoursesRepository repo = new CoursesRepository(context);
            var result = await repo.AddNewCourse(courseEntity);

            if(result == null)
            {
                return MyResult<CourseDto>.Failure(ErrorType.NotFound, "Failed to add new course.");
            }

            return MyResult<CourseDto>.Success(result);

        }

        public async Task<MyResult<CourseDto>> GetCourseById(int courseId)
        {
            if (courseId <= 0)
            {
                return MyResult<CourseDto>.Failure(ErrorType.BadRequest, "Invalid course ID.");
            }

            CoursesRepository repo = new CoursesRepository(context);
            var course = await repo.GetCourseById(courseId);

            if (course == null)
            {
                return MyResult<CourseDto>.Failure(ErrorType.NotFound, "Course not found.");
            }

            return MyResult<CourseDto>.Success(course);
        }

        // Persists an already-uploaded thumbnail file name onto a course.
        // Only the owning instructor (or an admin) may change it.
        public async Task<MyResult<bool>> SetThumbnail(int courseId, int callerId, bool isAdmin, string fileName)
        {
            if (courseId <= 0)
            {
                return MyResult<bool>.Failure(ErrorType.BadRequest, "Invalid course ID.");
            }

            CoursesRepository repo = new CoursesRepository(context);
            var ownerId = await repo.GetCourseInstructorId(courseId);

            if (ownerId == null)
            {
                return MyResult<bool>.Failure(ErrorType.NotFound, "Course not found.");
            }
            if (!isAdmin && ownerId != callerId)
            {
                return MyResult<bool>.Failure(ErrorType.Unauthorized, "You do not own this course.");
            }

            var ok = await repo.UpdateThumbnail(courseId, fileName);
            if (!ok)
            {
                return MyResult<bool>.Failure(ErrorType.Failure, "Failed to update thumbnail.");
            }

            return MyResult<bool>.Success(true);
        }

        public async Task<MyResult<List<LessonDto>>> GetCourseLessons(int courseId)
        {
            if (courseId <= 0)
            {
                return MyResult<List<LessonDto>>.Failure(ErrorType.BadRequest, "Invalid course ID.");
            }

            CoursesRepository repo = new CoursesRepository(context);
            var lessons = await repo.GetCourseLessons(courseId);

            if (lessons == null)
            {
                return MyResult<List<LessonDto>>.Failure(ErrorType.NotFound, "Failed to retrieve lessons.");
            }

            return MyResult<List<LessonDto>>.Success(lessons);
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
