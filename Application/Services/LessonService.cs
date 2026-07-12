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
using System.Text.RegularExpressions;
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

            // Authorization (incl. the owner/admin bypass for a draft course) was already
            // decided above by CanViewCourseContentAsync — fetch unconditionally here
            // instead of re-filtering to published-only and undoing that bypass.
            var entity = await lessonsRepository.GetAnyLessonByIdAsync(lessonId);
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

            // Same rule as section reorder: positive only, uniqueness enforced per
            // section by the DB (uq_lesson_order_per_section).
            if (request.SortOrder.HasValue && request.SortOrder.Value <= 0)
                return MyResult<LessonDto>.Failure(ErrorType.BadRequest, "Sort order must be positive.");

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

            var (updated, conflict) = await lessonsRepository.UpdateLessonAsync(lessonId, request.Title, request.EstimatedDurationMinutes, newBlocks, request.SortOrder);
            if (conflict)
                return MyResult<LessonDto>.Failure(ErrorType.Conflict, "Another lesson in this section already has that sort order.");
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
                else if (block.Data is TextBlockData text)
                {
                    var richTextError = ValidateTextBlockContent(text.Content);
                    if (richTextError != null)
                        errors.Add(richTextError);
                }
            }

            return errors;
        }

        // --- Rich-text (ProseMirror/TipTap subset) validation for text blocks ---------
        //
        // A text block's Content is still a plain `string`. It now holds one of two things:
        //   1. Legacy content: a genuine plain-text string (every lesson written before the
        //      rich-text feature). This MUST keep working untouched.
        //   2. New content: JSON.stringify(doc) of a constrained ProseMirror-style document
        //      { "type": "doc", "content": [...] } produced by the frontend editor.
        //
        // We only strictly validate content we can positively identify as case (2) — a JSON
        // object whose root `type` is "doc". Anything else (plain prose, JSON that isn't a
        // doc, or a string that merely starts with '{' but doesn't parse) is accepted as
        // legacy plain text. Trade-off: a *malformed* doc that fails JSON parsing is accepted
        // rather than rejected, because it's indistinguishable from a legacy plain string that
        // happens to start with '{' — and the hard requirement is that old plain strings never
        // break. Storing it is harmless (it's just a string). Residual gap: a doc nested deeper
        // than System.Text.Json's default 64-level parser limit throws on parse and is treated
        // as legacy; our own depth cap (20) is far shallower, so any *recognized* doc is fully
        // bounded — same category of accepted gap as the missing request-size limit.

        private const int MaxRichTextDepth = 20;      // nesting depth of the doc tree
        private const int MaxRichTextNodeCount = 5000;  // total nodes (blocks + text leaves)
        private const int MaxRichTextTextLength = 50000; // total leaf-text characters

        // Text color / highlight color: a plain hex value like #7048e8 or #fff.
        private static readonly Regex HexColorRegex =
            new("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$", RegexOptions.Compiled);

        private sealed class RichTextState
        {
            public int NodeCount;
            public int TextLength;
        }

        /// <summary>
        /// Returns an error message if <paramref name="content"/> is a recognized rich-text
        /// document that violates the allowed subset or the size caps; otherwise null (valid
        /// doc, or legacy plain text that is passed through untouched).
        /// </summary>
        private static string? ValidateTextBlockContent(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return null;

            // Fast path: real prose almost never starts with '{'. Only brace-leading content
            // is a candidate for being our serialized doc; everything else is legacy plain text.
            var trimmed = content.TrimStart();
            if (trimmed.Length == 0 || trimmed[0] != '{')
                return null;

            try
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                // Not the doc shape we own -> treat as legacy plain text, accept as-is.
                if (root.ValueKind != JsonValueKind.Object)
                    return null;
                if (!root.TryGetProperty("type", out var typeEl)
                    || typeEl.ValueKind != JsonValueKind.String
                    || typeEl.GetString() != "doc")
                    return null;

                return ValidateRichTextDocument(root);
            }
            catch (JsonException)
            {
                // Starts with '{' but isn't valid JSON -> legacy plain text, keep it.
                return null;
            }
        }

        private static string? ValidateRichTextDocument(JsonElement docRoot)
        {
            var state = new RichTextState();

            if (!docRoot.TryGetProperty("content", out var content))
                return null; // an empty doc with no content is harmless.
            if (content.ValueKind != JsonValueKind.Array)
                return "Rich text document 'content' must be an array.";

            foreach (var node in content.EnumerateArray())
            {
                var error = ValidateRichTextNode(node, parentType: "doc", depth: 1, state);
                if (error != null)
                    return error;
            }

            return null;
        }

        private static string? ValidateRichTextNode(JsonElement node, string parentType, int depth, RichTextState state)
        {
            if (depth > MaxRichTextDepth)
                return "Rich text content is nested too deeply.";
            if (++state.NodeCount > MaxRichTextNodeCount)
                return "Rich text content has too many nodes.";

            if (node.ValueKind != JsonValueKind.Object)
                return "Rich text node must be a JSON object.";
            if (!node.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String)
                return "Rich text node is missing a string 'type'.";
            var type = typeEl.GetString()!;

            // Allowlist which node types may appear inside which parent.
            bool allowed = parentType switch
            {
                "doc" => type is "paragraph" or "codeBlock",
                "paragraph" => type is "text",
                "codeBlock" => type is "text",
                _ => false
            };
            if (!allowed)
                return $"Rich text node '{type}' is not allowed inside '{parentType}'.";

            if (type == "text")
                return ValidateRichTextLeaf(node, state);

            if (type == "codeBlock")
            {
                var attrError = ValidateCodeBlockAttrs(node);
                if (attrError != null)
                    return attrError;
            }

            if (node.TryGetProperty("content", out var childContent))
            {
                if (childContent.ValueKind != JsonValueKind.Array)
                    return $"Rich text '{type}' content must be an array.";
                foreach (var child in childContent.EnumerateArray())
                {
                    var error = ValidateRichTextNode(child, type, depth + 1, state);
                    if (error != null)
                        return error;
                }
            }

            return null;
        }

        private static string? ValidateRichTextLeaf(JsonElement node, RichTextState state)
        {
            if (!node.TryGetProperty("text", out var textEl) || textEl.ValueKind != JsonValueKind.String)
                return "Rich text 'text' node must have a string 'text' value.";

            var text = textEl.GetString() ?? string.Empty;
            if (text.Length == 0)
                return "Rich text 'text' node must not be empty.";

            state.TextLength += text.Length;
            if (state.TextLength > MaxRichTextTextLength)
                return "Rich text content is too long.";

            if (node.TryGetProperty("marks", out var marks))
            {
                if (marks.ValueKind != JsonValueKind.Array)
                    return "Rich text 'marks' must be an array.";
                foreach (var mark in marks.EnumerateArray())
                {
                    var error = ValidateRichTextMark(mark);
                    if (error != null)
                        return error;
                }
            }

            return null;
        }

        private static string? ValidateRichTextMark(JsonElement mark)
        {
            if (mark.ValueKind != JsonValueKind.Object)
                return "Rich text mark must be a JSON object.";
            if (!mark.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String)
                return "Rich text mark is missing a string 'type'.";

            var type = typeEl.GetString()!;
            return type switch
            {
                "bold" or "italic" => null,
                "textStyle" => ValidateColorMark(mark, "textStyle", colorRequired: true),
                "highlight" => ValidateColorMark(mark, "highlight", colorRequired: false),
                _ => $"Rich text mark '{type}' is not allowed."
            };
        }

        private static string? ValidateColorMark(JsonElement mark, string markType, bool colorRequired)
        {
            if (!mark.TryGetProperty("attrs", out var attrs) || attrs.ValueKind != JsonValueKind.Object)
                return colorRequired ? $"Rich text '{markType}' mark requires a color." : null;

            if (!attrs.TryGetProperty("color", out var color) || color.ValueKind == JsonValueKind.Null)
                return colorRequired ? $"Rich text '{markType}' mark requires a color." : null;

            if (color.ValueKind != JsonValueKind.String || !HexColorRegex.IsMatch(color.GetString()!))
                return $"Rich text '{markType}' color must be a hex value like #7048e8.";

            return null;
        }

        private static string? ValidateCodeBlockAttrs(JsonElement node)
        {
            if (!node.TryGetProperty("attrs", out var attrs) || attrs.ValueKind == JsonValueKind.Null)
                return null;
            if (attrs.ValueKind != JsonValueKind.Object)
                return "Rich text 'codeBlock' attrs must be a JSON object.";

            if (!attrs.TryGetProperty("language", out var lang) || lang.ValueKind == JsonValueKind.Null)
                return null;
            if (lang.ValueKind != JsonValueKind.String)
                return "Rich text 'codeBlock' language must be a string.";

            var language = lang.GetString()!;
            if (language.Length > 50)
                return "Rich text 'codeBlock' language name is too long.";
            foreach (var c in language)
            {
                // Enough for real language slugs: c++, c#, f#, objective-c, plaintext, etc.
                if (!char.IsLetterOrDigit(c) && c is not ('-' or '+' or '#' or '.'))
                    return "Rich text 'codeBlock' language contains invalid characters.";
            }

            return null;
        }
    }
}
