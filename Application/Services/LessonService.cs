using Business.Common;
using Business.Dto.Request;
using DataAccess.Data;
using DataAccess.Dto;
using DataAccess.Entities;
using DataAccess.Entities.json;
using DataAccess.Repositories;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Supabase;

namespace Business.Services
{
    public class LessonService(AppDbContext context, Client supabaseClient)
    {
        private const string BucketName = "course-media";

        public async Task<MyResult<LessonDto>> CreateLessonAsync(LessonRequest request, int callerId, bool isAdmin)
        {
            if (request.SectionId <= 0)
            {
                return MyResult<LessonDto>.Failure(ErrorType.BadRequest, "Invalid section ID.");
            }
            // A null body element would NRE the validation/mapping loops below.
            request.ContentBlocks ??= new();

            // A lesson is owned transitively: lesson -> section -> course -> instructor.
            // Resolve the section's course and verify the caller may edit it before inserting.
            CoursesRepository coursesRepo = new CoursesRepository(context);
            var courseId = await coursesRepo.GetCourseIdBySection(request.SectionId);
            if (courseId == null)
            {
                return MyResult<LessonDto>.Failure(ErrorType.NotFound, "Section not found.");
            }

            var permission = await new CourseService(context).CheckCourseEditPermission(courseId.Value, callerId, isAdmin);
            if (!permission.IsSuccess)
            {
                return MyResult<LessonDto>.Failure(permission.FailureType, permission.Errors.Select(e => e.Message).ToArray());
            }

            var validationErrors = ValidateBlockData(request);
            if (validationErrors.Count > 0)
            {
                return MyResult<LessonDto>.Failure(ErrorType.BadRequest, [.. validationErrors]);
            }

            LessonsRepository repository = new LessonsRepository(context);
            var nextSortOrder = (await repository.GetMaxSortOrderForSectionAsync(request.SectionId)) + 1;

            var entity = new LessonEntity
            {
                section_id = request.SectionId,
                title = request.Title,
                sort_order = nextSortOrder,
                content_blocks = request.ContentBlocks.Select(b => new ContentBlock
                {
                    BlockId = Guid.NewGuid().ToString("N")[..8],
                    Type = b.Type,
                    Data = JsonSerializer.SerializeToElement(b.Data)
                }).ToList()
            };

            var savedEntity = await repository.AddLessonAsync(entity);

            var dto = await PrepareLessonResponseAsync(savedEntity);
            return MyResult<LessonDto>.Success(dto);
        }

        // Lesson content (incl. signed video URLs) is enrollment-gated: only the owning
        // instructor, an admin, or a student with an active/completed enrollment may view it.
        // Anyone else gets 404 so lesson existence/content can't be browsed without enrolling.
        public async Task<MyResult<LessonDto>> GetLessonAsync(int lessonId, int callerId, bool isAdmin)
        {
            var enrollmentRepo = new EnrollmentRepository(context);
            int? courseId = await enrollmentRepo.GetCourseIdByLessonAsync(lessonId);
            if (courseId == null)
            {
                return MyResult<LessonDto>.Failure(ErrorType.NotFound, $"Lesson with ID {lessonId} not found.");
            }

            if (!await enrollmentRepo.CanViewCourseContentAsync(courseId.Value, callerId, isAdmin))
            {
                return MyResult<LessonDto>.Failure(ErrorType.NotFound, $"Lesson with ID {lessonId} not found.");
            }

            LessonsRepository repository = new LessonsRepository(context);
            var entity = await repository.GetLessonByIdAsync(lessonId);
            if (entity == null)
            {
                return MyResult<LessonDto>.Failure(ErrorType.NotFound, $"Lesson with ID {lessonId} not found.");
            }

            var dto = await PrepareLessonResponseAsync(entity);
            return MyResult<LessonDto>.Success(dto);
        }

        // Not a pure entity->DTO map: media blocks store only the bucket file name, which is
        // useless to the client, so short-lived signed URLs must be minted here on every read.
        private async Task<LessonDto> PrepareLessonResponseAsync(LessonEntity entity)
        {
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowOutOfOrderMetadataProperties = true
            };

            var dto = new LessonDto
            {
                LessonId = entity.lesson_id,
                SectionId = entity.section_id,
                Title = entity.title,
                SortOrder = entity.sort_order,
                ContentBlocks = new List<ContentBlockDto>()
            };

            if (entity.content_blocks == null) return dto;

            foreach (var b in entity.content_blocks)
            {
                BlockData blockData = null;
                try
                {
                    if (b.Data.ValueKind != JsonValueKind.Undefined && b.Data.ValueKind != JsonValueKind.Null)
                    {
                        blockData = b.Data.Deserialize<BlockData>(jsonOptions);

                        if (blockData is ImageBlockData img && !string.IsNullOrWhiteSpace(img.Url))
                        {
                            try
                            {
                                img.Url = await supabaseClient.Storage.From(BucketName).CreateSignedUrl(img.Url, 3600);
                            }
                            catch
                            {
                                img.Url = null;
                            }
                        }
                        else if (blockData is VideoBlockData vid && !string.IsNullOrWhiteSpace(vid.VideoId) && vid.Provider == "supabase")
                        {
                            try
                            {
                                vid.VideoId = await supabaseClient.Storage.From(BucketName).CreateSignedUrl(vid.VideoId, 3600);
                            }
                            catch
                            {
                                vid.VideoId = null;
                            }
                        }
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Failed to deserialize block {b.BlockId}: {ex.Message}");
                   
                }

                dto.ContentBlocks.Add(new ContentBlockDto
                {
                    BlockId = b.BlockId,
                    Type = b.Type,
                    Data = blockData
                });
            }

            return dto;
        }

        public async Task<MyResult<LessonDto>> UpdateLessonAsync(int lessonId, UpdateLessonRequest request, int callerId, bool isAdmin)
        {
            if (lessonId <= 0)
                return MyResult<LessonDto>.Failure(ErrorType.BadRequest, "Invalid lesson ID.");

            LessonsRepository repository = new LessonsRepository(context);
            var lesson = await repository.GetAnyLessonByIdAsync(lessonId);
            if (lesson == null)
                return MyResult<LessonDto>.Failure(ErrorType.NotFound, $"Lesson with ID {lessonId} not found.");

            CoursesRepository coursesRepo = new CoursesRepository(context);
            var courseId = await coursesRepo.GetCourseIdBySection(lesson.section_id);
            if (courseId == null)
                return MyResult<LessonDto>.Failure(ErrorType.NotFound, "Section not found.");

            var permission = await new CourseService(context).CheckCourseEditPermission(courseId.Value, callerId, isAdmin);
            if (!permission.IsSuccess)
                return MyResult<LessonDto>.Failure(permission.FailureType, permission.Errors.Select(e => e.Message).ToArray());

            List<ContentBlock>? newBlocks = null;
            if (request.ContentBlocks != null)
            {
                var validationErrors = ValidateBlockData(request.ContentBlocks);
                if (validationErrors.Count > 0)
                    return MyResult<LessonDto>.Failure(ErrorType.BadRequest, [.. validationErrors]);

                newBlocks = request.ContentBlocks.Select(b => new ContentBlock
                {
                    BlockId = Guid.NewGuid().ToString("N")[..8],
                    Type = b.Type,
                    Data = JsonSerializer.SerializeToElement(b.Data)
                }).ToList();
            }

            var updated = await repository.UpdateLessonAsync(lessonId, request.Title, request.EstimatedDurationMinutes, newBlocks);
            if (updated == null)
                return MyResult<LessonDto>.Failure(ErrorType.Failure, "Failed to update lesson.");

            var dto = await PrepareLessonResponseAsync(updated);
            return MyResult<LessonDto>.Success(dto);
        }

        private List<string> ValidateBlockData(LessonRequest request)
        {
            return ValidateBlockData(request.ContentBlocks);
        }
        private List<string> ValidateBlockData(List<ContentBlockRequest> blocks)
        {
            var errors = new List<string>();

            foreach (var block in blocks)
            {
                if (block.Data == null)
                {
                    errors.Add("A content block is missing data.");
                    continue;
                }

                bool isMatch = block.Data switch
                {
                    TextBlockData => block.Type == "text",
                    ImageBlockData => block.Type == "image",
                    VideoBlockData => block.Type == "video",
                    QuizBlockData => block.Type == "quiz",
                    _ => false
                };

                if (!isMatch)
                {
                    errors.Add($"Block type '{block.Type}' does not match the provided data structure.");
                    continue;
                }

                if (block.Data is ImageBlockData img && string.IsNullOrWhiteSpace(img.Url))
                {
                    errors.Add("Image blocks must contain a valid URL.");
                }
            }

            return errors;
        }
    }
}
