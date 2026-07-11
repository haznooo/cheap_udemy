using Business.Common;
using Business.Dto.Request;
using Business.Dto.Rsponse;
using Business.Interfaces;
using DataAccess.Common;
using DataAccess.Dto;
using DataAccess.Entities;
using DataAccess.Entities.json;
using DataAccess.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using static DataAccess.Common.clsPageResult;

namespace Business.Services
{
    public class CourseService(ICoursesRepository coursesRepository, IEnrollmentRepository enrollmentRepository, IUserAndProfileRepository userAndProfileRepository) : ICourseService
    {

        public async Task<MyResult<PageResult<CourseDto>>> GetAllCourses(GetCoursesRequest request)
        {

            if(request.PageNumber <= 0 || request.PageSize <= 0)
            {
                return MyResult<PageResult<CourseDto>>.Failure(ErrorType.BadRequest, "Invalid page number or page size.");
            }

            var R = await coursesRepository.GetAllCourses(
                request.PageNumber, request.PageSize,
                request.SearchTerm, request.CategoryId, request.Level,
                request.MinPrice, request.MaxPrice, request.SortBy);

            if(R == null || R.Items == null)
            {
                return MyResult<PageResult<CourseDto>>.Failure(ErrorType.NotFound, "No courses found.");
            }

            return MyResult<PageResult<CourseDto>>.Success(R);

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
            bool categoryExists = await coursesRepository.DoesCategoryExistAsync(request.CategoryId);
            if (!categoryExists)
            {
                return MyResult<CourseDto>.Failure(ErrorType.BadRequest, "Category does not exist.");
            }
            string[] validLevels = { "beginner", "intermediate", "advanced" };
            if (!validLevels.Contains(request.level))
            {
                return MyResult<CourseDto>.Failure(ErrorType.BadRequest, "Invalid level. Must be beginner, intermediate, or advanced.");
            }

            // Creating a course is what turns a student into an instructor — there's no
            // separate "become an instructor" action. The DB trigger trg_verify_instructor
            // rejects the insert below unless instructor_id already has role instructor/admin,
            // so a first-time student caller must be promoted before the insert.
            var callerRole = await userAndProfileRepository.GetUserRoleAsync(instructorId);
            if (callerRole == null)
            {
                return MyResult<CourseDto>.Failure(ErrorType.Unauthorized, "User not found or inactive.");
            }
            if (callerRole == "student" && !await userAndProfileRepository.PromoteUserToInstructorAsync(instructorId))
            {
                return MyResult<CourseDto>.Failure(ErrorType.Failure, "Failed to promote user to instructor.");
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

            var result = await coursesRepository.AddNewCourse(courseEntity);

            if(result == null)
            {
                return MyResult<CourseDto>.Failure(ErrorType.Failure, "Failed to add new course.");
            }

            return MyResult<CourseDto>.Success(result);

        }

        public async Task<MyResult<CourseDto>> GetCourseById(int courseId, int? callerId = null, bool isAdmin = false)
        {
            if (courseId <= 0)
            {
                return MyResult<CourseDto>.Failure(ErrorType.BadRequest, "Invalid course ID.");
            }

            var course = await coursesRepository.GetCourseById(courseId, callerId, isAdmin);

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

            var ownerId = await coursesRepository.GetCourseInstructorId(courseId);

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
        // On success the value is the REPLACED file name (null if the course had no
        // thumbnail yet) so the controller can remove the stale file from storage.
        public async Task<MyResult<string?>> SetThumbnail(int courseId, int callerId, bool isAdmin, string fileName)
        {
            // Defensive re-check; controllers should also verify before uploading.
            var permission = await CheckCourseEditPermission(courseId, callerId, isAdmin);
            if (!permission.IsSuccess)
            {
                return MyResult<string?>.Failure(permission.FailureType, permission.Errors.Select(e => e.Message).ToArray());
            }

            var (ok, oldFileName) = await coursesRepository.UpdateThumbnail(courseId, fileName);
            if (!ok)
            {
                return MyResult<string?>.Failure(ErrorType.Failure, "Failed to update thumbnail.");
            }

            return MyResult<string?>.Success(oldFileName);
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

            if (!await enrollmentRepository.CanViewCourseContentAsync(courseId, callerId, isAdmin))
            {
                return MyResult<PageResult<LessonDto>>.Failure(ErrorType.NotFound, "Course not found.");
            }

            var lessons = await coursesRepository.GetCourseLessons(courseId, pageNumber, pageSize, callerId, isAdmin);

            if (lessons == null)
            {
                return MyResult<PageResult<LessonDto>>.Failure(ErrorType.NotFound, "Failed to retrieve lessons.");
            }

            return MyResult<PageResult<LessonDto>>.Success(lessons);
        }

        // Section list uses the same enrollment gate as GetCourseLessons: owning instructor,
        // admin, or an active/completed enrollment. Everyone else gets 404 (curriculum hidden).
        public async Task<MyResult<PageResult<SectionResponse>>> GetCourseSections(int courseId, int callerId, bool isAdmin, int pageNumber, int pageSize)
        {
            if (courseId <= 0)
            {
                return MyResult<PageResult<SectionResponse>>.Failure(ErrorType.BadRequest, "Invalid course ID.");
            }

            if (pageNumber <= 0 || pageSize <= 0)
            {
                return MyResult<PageResult<SectionResponse>>.Failure(ErrorType.BadRequest, "Invalid page number or page size.");
            }

            if (!await enrollmentRepository.CanViewCourseContentAsync(courseId, callerId, isAdmin))
            {
                return MyResult<PageResult<SectionResponse>>.Failure(ErrorType.NotFound, "Course not found.");
            }

            var sections = await coursesRepository.GetCourseSections(courseId, pageNumber, pageSize);

            if (sections == null)
            {
                return MyResult<PageResult<SectionResponse>>.Failure(ErrorType.NotFound, "Failed to retrieve sections.");
            }

            var mapped = new PageResult<SectionResponse>
            {
                Items = sections.Items.Select(s => new SectionResponse
                {
                    SectionId = s.SectionId,
                    CourseId = s.CourseId,
                    Title = s.Title,
                    SortOrder = s.SortOrder
                }).ToList(),
                TotalCount = sections.TotalCount,
                PageNumber = sections.PageNumber,
                PageSize = sections.PageSize
            };

            return MyResult<PageResult<SectionResponse>>.Success(mapped);
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

            var r = await coursesRepository.GetCoursesByInstructorIdAsync(instructorId, pageNumber, pageSize);
            if (r == null)
                return MyResult<PageResult<CourseDto>>.Failure(ErrorType.Failure, "Failed to retrieve courses.");

            return MyResult<PageResult<CourseDto>>.Success(r);
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
                bool categoryExists = await coursesRepository.DoesCategoryExistAsync(request.CategoryId.Value);
                if (!categoryExists)
                    return MyResult<CourseDto>.Failure(ErrorType.BadRequest, "Category does not exist.");
            }

            if (request.Price.HasValue && request.Price.Value < 0)
                return MyResult<CourseDto>.Failure(ErrorType.BadRequest, "Price cannot be negative.");

            var result = await coursesRepository.UpdateCourseAsync(courseId, request.Title, request.Description, request.Code, request.Price, request.Level, request.CategoryId);
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

            var course = await coursesRepository.GetRawCourseAsync(courseId);
            if (course == null)
                return MyResult<CourseDto>.Failure(ErrorType.NotFound, "Course not found.");

            if (course.status == "published")
                return MyResult<CourseDto>.Failure(ErrorType.Conflict, "Course is already published.");

            var result = await coursesRepository.UpdateCourseStatusAsync(courseId, "published");
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

            var course = await coursesRepository.GetRawCourseAsync(courseId);
            if (course == null)
                return MyResult<CourseDto>.Failure(ErrorType.NotFound, "Course not found.");

            if (course.status != "published")
                return MyResult<CourseDto>.Failure(ErrorType.Conflict, "Course is not published.");

            var result = await coursesRepository.UpdateCourseStatusAsync(courseId, "draft");
            if (result == null)
                return MyResult<CourseDto>.Failure(ErrorType.Failure, "Failed to unpublish course.");

            return MyResult<CourseDto>.Success(result);
        }

        public async Task<MyResult<SectionResponse>> AddNewSection(AddSectionRequest request)
        {
            if (request.CourseId <= 0)
            {
                return MyResult<SectionResponse>.Failure(ErrorType.BadRequest, "Invalid course ID.");
            }
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return MyResult<SectionResponse>.Failure(ErrorType.BadRequest, "Section title is required.");
            }

            SectionEntitiy sectionEntity = new SectionEntitiy
            {
                title = request.Title,
                sort_order = request.SortOrder,
                course_id = request.CourseId
            };

            var result = await coursesRepository.AddNewSection(sectionEntity);

            if (result == null)
            {
                return MyResult<SectionResponse>.Failure(ErrorType.Failure, "Failed to add new section.");
            }

            return MyResult<SectionResponse>.Success(new SectionResponse
            {
                SectionId = result.section_id,
                Title = result.title,
                SortOrder = result.sort_order,
                CourseId = result.course_id
            });
        }

        public async Task<MyResult<SectionResponse>> UpdateSection(int sectionId, UpdateSectionRequest request, int callerId, bool isAdmin)
        {
            if (sectionId <= 0)
                return MyResult<SectionResponse>.Failure(ErrorType.BadRequest, "Invalid section ID.");

            var courseId = await coursesRepository.GetCourseIdBySection(sectionId);
            if (courseId == null)
                return MyResult<SectionResponse>.Failure(ErrorType.NotFound, "Section not found.");

            // Sections/lessons stay editable by the owner/admin regardless of enrollment —
            // only course-level hard delete is blocked once anyone has ever enrolled.
            var permission = await CheckCourseEditPermission(courseId.Value, callerId, isAdmin);
            if (!permission.IsSuccess)
                return MyResult<SectionResponse>.Failure(permission.FailureType, permission.Errors.Select(e => e.Message).ToArray());

            if (request.SortOrder.HasValue && request.SortOrder.Value <= 0)
                return MyResult<SectionResponse>.Failure(ErrorType.BadRequest, "Sort order must be positive.");

            var result = await coursesRepository.UpdateSectionAsync(sectionId, request.Title, request.SortOrder);
            if (result == null)
                return MyResult<SectionResponse>.Failure(ErrorType.Failure, "Failed to update section.");

            return MyResult<SectionResponse>.Success(new SectionResponse
            {
                SectionId = result.SectionId,
                CourseId = result.CourseId,
                Title = result.Title,
                SortOrder = result.SortOrder
            });
        }

        public async Task<MyResult<bool>> DeleteSection(int sectionId, int callerId, bool isAdmin)
        {
            if (sectionId <= 0)
                return MyResult<bool>.Failure(ErrorType.BadRequest, "Invalid section ID.");

            var courseId = await coursesRepository.GetCourseIdBySection(sectionId);
            if (courseId == null)
                return MyResult<bool>.Failure(ErrorType.NotFound, "Section not found.");

            // Same as UpdateSection: allowed regardless of enrollment, owner/admin only.
            var permission = await CheckCourseEditPermission(courseId.Value, callerId, isAdmin);
            if (!permission.IsSuccess)
                return MyResult<bool>.Failure(permission.FailureType, permission.Errors.Select(e => e.Message).ToArray());

            var success = await coursesRepository.DeleteSectionAsync(sectionId);
            if (!success)
                return MyResult<bool>.Failure(ErrorType.Failure, "Failed to delete section.");

            return MyResult<bool>.Success(true);
        }

        // Hard-delete is only allowed if no one has ever enrolled (any status, including
        // dropped) — otherwise the instructor can only unpublish. Deletion is a soft-delete
        // (deleted_at + removal_reason), never an actual row DELETE.
        public async Task<MyResult<bool>> DeleteCourse(int courseId, int callerId, bool isAdmin, string? removalReason)
        {
            if (courseId <= 0)
                return MyResult<bool>.Failure(ErrorType.BadRequest, "Invalid course ID.");

            var permission = await CheckCourseEditPermission(courseId, callerId, isAdmin);
            if (!permission.IsSuccess)
                return MyResult<bool>.Failure(permission.FailureType, permission.Errors.Select(e => e.Message).ToArray());

            var hasEnrollments = await enrollmentRepository.HasAnyEnrollmentAsync(courseId);
            if (hasEnrollments)
                return MyResult<bool>.Failure(ErrorType.Conflict, "Cannot delete a course that has enrollment history; unpublish it instead.");

            var success = await coursesRepository.SoftDeleteCourseAsync(courseId, removalReason);
            if (!success)
                return MyResult<bool>.Failure(ErrorType.Failure, "Failed to delete course.");

            return MyResult<bool>.Success(true);
        }
    }
}
