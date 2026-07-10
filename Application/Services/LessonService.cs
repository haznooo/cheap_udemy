using Business.Common;
using Business.Dto.Request;
using Business.Interfaces;
using DataAccess.Dto;
using DataAccess.Entities;
using DataAccess.Entities.json;
using DataAccess.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Supabase;

namespace Business.Services
{
    public class LessonService(
        ILessonsRepository lessonsRepository,
        ICoursesRepository coursesRepository,
        IEnrollmentRepository enrollmentRepository,
        ICourseService courseService,
        Client supabaseClient,
        IMediaService mediaService) : ILessonService
    {
        private const string BucketName = MediaBuckets.CourseMedia;

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
            var courseId = await coursesRepository.GetCourseIdBySection(request.SectionId);
            if (courseId == null)
            {
                return MyResult<LessonDto>.Failure(ErrorType.NotFound, "Section not found.");
            }

            var permission = await courseService.CheckCourseEditPermission(courseId.Value, callerId, isAdmin);
            if (!permission.IsSuccess)
            {
                return MyResult<LessonDto>.Failure(permission.FailureType, permission.Errors.Select(e => e.Message).ToArray());
            }

            var validationErrors = ValidateBlockData(request);
            if (validationErrors.Count > 0)
            {
                return MyResult<LessonDto>.Failure(ErrorType.BadRequest, [.. validationErrors]);
            }

            var nextSortOrder = (await lessonsRepository.GetMaxSortOrderForSectionAsync(request.SectionId)) + 1;

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

            var savedEntity = await lessonsRepository.AddLessonAsync(entity);

            var dto = await PrepareLessonResponseAsync(savedEntity);
            return MyResult<LessonDto>.Success(dto);
        }

        // Lesson content (incl. signed video URLs) is enrollment-gated: only the owning
        // instructor, an admin, or a student with an active/completed enrollment may view it.
        // Anyone else gets 404 so lesson existence/content can't be browsed without enrolling.
        public async Task<MyResult<LessonDto>> GetLessonAsync(int lessonId, int callerId, bool isAdmin)
        {
            int? courseId = await enrollmentRepository.GetCourseIdByLessonAsync(lessonId);
            if (courseId == null)
            {
                return MyResult<LessonDto>.Failure(ErrorType.NotFound, $"Lesson with ID {lessonId} not found.");
            }

            if (!await enrollmentRepository.CanViewCourseContentAsync(courseId.Value, callerId, isAdmin))
            {
                return MyResult<LessonDto>.Failure(ErrorType.NotFound, $"Lesson with ID {lessonId} not found.");
            }

            var entity = await lessonsRepository.GetLessonByIdAsync(lessonId);
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

            var lesson = await lessonsRepository.GetAnyLessonByIdAsync(lessonId);
            if (lesson == null)
                return MyResult<LessonDto>.Failure(ErrorType.NotFound, $"Lesson with ID {lessonId} not found.");

            var courseId = await coursesRepository.GetCourseIdBySection(lesson.section_id);
            if (courseId == null)
                return MyResult<LessonDto>.Failure(ErrorType.NotFound, "Section not found.");

            var permission = await courseService.CheckCourseEditPermission(courseId.Value, callerId, isAdmin);
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

            var updated = await lessonsRepository.UpdateLessonAsync(lessonId, request.Title, request.EstimatedDurationMinutes, newBlocks);
            if (updated == null)
                return MyResult<LessonDto>.Failure(ErrorType.Failure, "Failed to update lesson.");

            // The old blocks are replaced; media files only they referenced are now
            // orphaned in the bucket — remove them (after the save, best-effort).
            if (newBlocks != null)
            {
                var removedFiles = ExtractMediaFileNames(lesson.content_blocks);
                var keptNames = ExtractMediaFileNames(request.ContentBlocks);
                // Match by substring, not equality: a client that echoes back the
                // signed URL of an unchanged block (instead of the raw stored file
                // name it should send) still embeds the file name in that URL — the
                // file is kept, not deleted out from under the lesson.
                removedFiles.RemoveWhere(old => keptNames.Any(kept => kept.Contains(old, StringComparison.Ordinal)));
                await DeleteUnreferencedMediaAsync(lessonId, removedFiles);
            }

            var dto = await PrepareLessonResponseAsync(updated);
            return MyResult<LessonDto>.Success(dto);
        }

        // Hard-deletes a lesson (owner instructor or admin only) and cleans up the
        // media files its content blocks referenced. user_lesson_progress rows go
        // with it via ON DELETE CASCADE.
        public async Task<MyResult<bool>> DeleteLessonAsync(int lessonId, int callerId, bool isAdmin)
        {
            if (lessonId <= 0)
                return MyResult<bool>.Failure(ErrorType.BadRequest, "Invalid lesson ID.");

            var lesson = await lessonsRepository.GetAnyLessonByIdAsync(lessonId);
            if (lesson == null)
                return MyResult<bool>.Failure(ErrorType.NotFound, $"Lesson with ID {lessonId} not found.");

            var courseId = await coursesRepository.GetCourseIdBySection(lesson.section_id);
            if (courseId == null)
                return MyResult<bool>.Failure(ErrorType.NotFound, "Section not found.");

            var permission = await courseService.CheckCourseEditPermission(courseId.Value, callerId, isAdmin);
            if (!permission.IsSuccess)
                return MyResult<bool>.Failure(permission.FailureType, permission.Errors.Select(e => e.Message).ToArray());

            // Collect the file names BEFORE the row (and with it the block data) is gone.
            var mediaFiles = ExtractMediaFileNames(lesson.content_blocks);

            var deleted = await lessonsRepository.DeleteLessonAsync(lessonId);
            if (!deleted)
                return MyResult<bool>.Failure(ErrorType.Failure, "Failed to delete lesson.");

            await DeleteUnreferencedMediaAsync(lessonId, mediaFiles);

            return MyResult<bool>.Success(true);
        }

        // Runs after the DB change is saved. Best-effort: a failed storage delete only
        // leaks a file, so it never fails the request. A file still referenced by
        // another lesson (instructor reused an upload) is kept.
        private async Task DeleteUnreferencedMediaAsync(int lessonId, HashSet<string> fileNames)
        {
            foreach (var fileName in fileNames)
            {
                if (!await lessonsRepository.IsMediaReferencedByOtherLessonsAsync(lessonId, fileName))
                {
                    await mediaService.DeleteCourseMediaAsync(fileName);
                }
            }
        }

        // File names of supabase-hosted media referenced by a lesson's stored blocks.
        private static HashSet<string> ExtractMediaFileNames(List<ContentBlock>? blocks)
        {
            var names = new HashSet<string>();
            if (blocks == null) return names;

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowOutOfOrderMetadataProperties = true
            };

            foreach (var b in blocks)
            {
                try
                {
                    if (b.Data.ValueKind == JsonValueKind.Undefined || b.Data.ValueKind == JsonValueKind.Null) continue;
                    AddMediaFileName(names, b.Data.Deserialize<BlockData>(jsonOptions));
                }
                catch (JsonException)
                {
                    // Unreadable block — nothing to clean up for it.
                }
            }

            return names;
        }

        private static HashSet<string> ExtractMediaFileNames(List<ContentBlockRequest>? blocks)
        {
            var names = new HashSet<string>();
            if (blocks == null) return names;

            foreach (var b in blocks) AddMediaFileName(names, b.Data);

            return names;
        }

        private static void AddMediaFileName(HashSet<string> names, BlockData? data)
        {
            // Only supabase-hosted media maps to a bucket object; external providers
            // (e.g. a youtube video id) are not ours to delete.
            if (data is ImageBlockData img && !string.IsNullOrWhiteSpace(img.Url))
                names.Add(img.Url);
            else if (data is VideoBlockData vid && vid.Provider == "supabase" && !string.IsNullOrWhiteSpace(vid.VideoId))
                names.Add(vid.VideoId);
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
