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
            string[] validLevels = { "beginner", "intermediate", "advanced" };
            if (!validLevels.Contains(request.level))
            {
                return MyResult<CourseDto>.Failure(ErrorType.BadRequest, "Invalid level. Must be beginner, intermediate, or advanced.");
            }

            CourseEntitiy courseEntity = new CourseEntitiy
            {
                instructor_id = instructorId,
                title = request.Title,
                category_id = request.CategoryId,
                description = request.Description,
                code = request.Code,
                price = request.Price,
                // Always start as draft; publishing is a separate explicit action.
                // Ignoring request.Status prevents a caller from bypassing the draft workflow.
                status = "draft",
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
                return MyResult<CourseDto>.Failure(ErrorType.Failure, "Failed to add new course.");
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

        // Verifies the caller may edit a course (owning instructor or admin).
        // Returns NotFound if the course doesn't exist, Unauthorized if not permitted.
        // Call this BEFORE uploading any media so non-owners can't write to storage.
        public async Task<MyResult<bool>> CheckCourseEditPermission(int courseId, int callerId, bool isAdmin)
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

            return MyResult<bool>.Success(true);
        }

        // Persists an already-uploaded thumbnail file name onto a course.
        // Only the owning instructor (or an admin) may change it.
        public async Task<MyResult<bool>> SetThumbnail(int courseId, int callerId, bool isAdmin, string fileName)
        {
            // Defensive re-check; controllers should also verify before uploading.
            var permission = await CheckCourseEditPermission(courseId, callerId, isAdmin);
            if (!permission.IsSuccess)
            {
                return permission;
            }

            CoursesRepository repo = new CoursesRepository(context);
            var ok = await repo.UpdateThumbnail(courseId, fileName);
            if (!ok)
            {
                return MyResult<bool>.Failure(ErrorType.Failure, "Failed to update thumbnail.");
            }

            return MyResult<bool>.Success(true);
        }

        // Lesson curriculum is enrollment-gated: only the owning instructor, an admin,
        // or a student with an active/completed enrollment may see it. Everyone else gets 404
        // (hide the curriculum so it can't be browsed without enrolling).
        public async Task<MyResult<PageResult<LessonDto>>> GetCourseLessons(int courseId, int callerId, bool isAdmin, int pageNumber, int pageSize)
        {
            if (courseId <= 0)
            {
                return MyResult<PageResult<LessonDto>>.Failure(ErrorType.BadRequest, "Invalid course ID.");
            }

            if (pageNumber <= 0 || pageSize <= 0)
            {
                return MyResult<PageResult<LessonDto>>.Failure(ErrorType.BadRequest, "Invalid page number or page size.");
            }

            var enrollmentRepo = new EnrollmentRepository(context);
            if (!await enrollmentRepo.CanViewCourseContentAsync(courseId, callerId, isAdmin))
            {
                return MyResult<PageResult<LessonDto>>.Failure(ErrorType.NotFound, "Course not found.");
            }

            CoursesRepository repo = new CoursesRepository(context);
            var lessons = await repo.GetCourseLessons(courseId, pageNumber, pageSize);

            if (lessons == null)
            {
                return MyResult<PageResult<LessonDto>>.Failure(ErrorType.NotFound, "Failed to retrieve lessons.");
            }

            return MyResult<PageResult<LessonDto>>.Success(new PageResult<LessonDto>
            {
                Items = lessons.Items,
                TotalCount = lessons.TotalCount,
                PageNumber = lessons.PageNumber,
                PageSize = lessons.PageSize
            });
        }

        public async Task<MyResult<PageResult<CourseDto>>> GetInstructorCourses(int instructorId, int callerId, string callerRole, int pageNumber, int pageSize)
        {
            if (instructorId <= 0)
                return MyResult<PageResult<CourseDto>>.Failure(ErrorType.BadRequest, "Invalid instructor ID.");

            if (pageNumber <= 0 || pageSize <= 0)
                return MyResult<PageResult<CourseDto>>.Failure(ErrorType.BadRequest, "Invalid page number or page size.");

            bool isAdmin = callerRole == "admin";
            if (!isAdmin && callerId != instructorId)
                return MyResult<PageResult<CourseDto>>.Failure(ErrorType.Unauthorized, "Access denied.");

            CoursesRepository repo = new CoursesRepository(context);
            var r = await repo.GetCoursesByInstructorIdAsync(instructorId, pageNumber, pageSize);
            if (r == null)
                return MyResult<PageResult<CourseDto>>.Failure(ErrorType.Failure, "Failed to retrieve courses.");

            return MyResult<PageResult<CourseDto>>.Success(new PageResult<CourseDto>
            {
                Items = r.Items,
                TotalCount = r.TotalCount,
                PageNumber = r.PageNumber,
                PageSize = r.PageSize
            });
        }

        public async Task<MyResult<CourseDto>> UpdateCourse(int courseId, UpdateCourseRequest request, int callerId, bool isAdmin)
        {
            if (courseId <= 0)
                return MyResult<CourseDto>.Failure(ErrorType.BadRequest, "Invalid course ID.");

            var permission = await CheckCourseEditPermission(courseId, callerId, isAdmin);
            if (!permission.IsSuccess)
                return MyResult<CourseDto>.Failure(permission.FailureType, permission.Errors.Select(e => e.Message).ToArray());

            string[] validLevels = { "beginner", "intermediate", "advanced" };
            if (!string.IsNullOrWhiteSpace(request.Level) && !validLevels.Contains(request.Level))
                return MyResult<CourseDto>.Failure(ErrorType.BadRequest, "Invalid level. Must be beginner, intermediate, or advanced.");

            if (request.CategoryId.HasValue)
            {
                bool categoryExists = await context.Categories.AnyAsync(c => c.category_id == request.CategoryId.Value);
                if (!categoryExists)
                    return MyResult<CourseDto>.Failure(ErrorType.BadRequest, "Category does not exist.");
            }

            if (request.Price.HasValue && request.Price.Value < 0)
                return MyResult<CourseDto>.Failure(ErrorType.BadRequest, "Price cannot be negative.");

            CoursesRepository repo = new CoursesRepository(context);
            var result = await repo.UpdateCourseAsync(courseId, request.Title, request.Description, request.Code, request.Price, request.Level, request.CategoryId);
            if (result == null)
                return MyResult<CourseDto>.Failure(ErrorType.Failure, "Failed to update course.");

            return MyResult<CourseDto>.Success(result);
        }

        public async Task<MyResult<CourseDto>> PublishCourse(int courseId, int callerId, bool isAdmin)
        {
            if (courseId <= 0)
                return MyResult<CourseDto>.Failure(ErrorType.BadRequest, "Invalid course ID.");

            var permission = await CheckCourseEditPermission(courseId, callerId, isAdmin);
            if (!permission.IsSuccess)
                return MyResult<CourseDto>.Failure(permission.FailureType, permission.Errors.Select(e => e.Message).ToArray());

            CoursesRepository repo = new CoursesRepository(context);
            var course = await repo.GetRawCourseAsync(courseId);
            if (course == null)
                return MyResult<CourseDto>.Failure(ErrorType.NotFound, "Course not found.");

            if (course.status == "published")
                return MyResult<CourseDto>.Failure(ErrorType.Conflict, "Course is already published.");

            var result = await repo.UpdateCourseStatusAsync(courseId, "published");
            if (result == null)
                return MyResult<CourseDto>.Failure(ErrorType.Failure, "Failed to publish course.");

            return MyResult<CourseDto>.Success(result);
        }

        public async Task<MyResult<CourseDto>> UnpublishCourse(int courseId, int callerId, bool isAdmin)
        {
            if (courseId <= 0)
                return MyResult<CourseDto>.Failure(ErrorType.BadRequest, "Invalid course ID.");

            var permission = await CheckCourseEditPermission(courseId, callerId, isAdmin);
            if (!permission.IsSuccess)
                return MyResult<CourseDto>.Failure(permission.FailureType, permission.Errors.Select(e => e.Message).ToArray());

            CoursesRepository repo = new CoursesRepository(context);
            var course = await repo.GetRawCourseAsync(courseId);
            if (course == null)
                return MyResult<CourseDto>.Failure(ErrorType.NotFound, "Course not found.");

            if (course.status != "published")
                return MyResult<CourseDto>.Failure(ErrorType.Conflict, "Course is not published.");

            var result = await repo.UpdateCourseStatusAsync(courseId, "draft");
            if (result == null)
                return MyResult<CourseDto>.Failure(ErrorType.Failure, "Failed to unpublish course.");

            return MyResult<CourseDto>.Success(result);
        }

        public async Task<MyResult<SectionEntitiy>> AddNewSection(AddSectionRequest request)
        {
            if (request.CourseId <= 0)
            {
                return MyResult<SectionEntitiy>.Failure(ErrorType.BadRequest, "Invalid course ID.");
            }
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return MyResult<SectionEntitiy>.Failure(ErrorType.BadRequest, "Section title is required.");
            }

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
                return MyResult<SectionEntitiy>.Failure(ErrorType.Failure, "Failed to add new section.");
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
