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
    // Remember to update your CreateLessonAsync method to handle mapping async if you return it there too!
    public class LessonService(AppDbContext context, Client supabaseClient)
    {
        private const string BucketName = "course-media";

        public async Task<LessonDto> CreateLessonAsync(LessonDto dto)
        {

            validateBlockData(dto);

            LessonsRepository repository = new LessonsRepository(context);
            // 1. Map DTO to Entity
            var entity = new LessonEntity
            {
                section_id = dto.SectionId,
                title = dto.Title,
                sort_order = dto.SortOrder,
                content_blocks = dto.ContentBlocks.Select(b => new ContentBlock
                {
                    BlockId = b.BlockId,
                    Type = b.Type,
                    // Serialize the specific BlockData into a JsonElement for EF Core
                    Data = JsonSerializer.SerializeToElement(b.Data)
                }).ToList()
            };

            // 2. Save using Repository
            var savedEntity = await repository.AddLessonAsync(entity);

            // 3. Map back to DTO
            return await MapToDtoAsync(savedEntity);
        }

  

        public async Task<LessonDto?> GetLessonAsync(int lessonId)
        {
            LessonsRepository repository = new LessonsRepository(context);
            var entity = await repository.GetLessonByIdAsync(lessonId);
            if (entity == null) return null;

            // We make the mapping asynchronous now because generating signed URLs requires network requests
            return await MapToDtoAsync(entity);
        }


        // Helper method for clean mapping
        private async Task<LessonDto> MapToDtoAsync(LessonEntity entity)
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

                        // --- SECURITY LAYER: Generate Temporary Signed Links ---
                        // If the block contains media, exchange the stored file name for a secure 1-hour URL
                        if (blockData is ImageBlockData img && !string.IsNullOrWhiteSpace(img.Url))
                        {
                            // img.Url currently contains just the "uniqueFileName.png" from the database
                            img.Url = await supabaseClient.Storage.From(BucketName).CreateSignedUrl(img.Url, 3600);
                        }
                        else if (blockData is VideoBlockData vid && !string.IsNullOrWhiteSpace(vid.VideoId) && vid.Provider == "supabase")
                        {
                            // vid.VideoId holds our unique file name for the video
                            vid.VideoId = await supabaseClient.Storage.From(BucketName).CreateSignedUrl(vid.VideoId, 3600);
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


        private void validateBlockData(LessonDto dto)
        {
            foreach (var block in dto.ContentBlocks)
            {
                if (block.Data == null)
                {
                    throw new ArgumentException($"Content block {block.BlockId} is missing data.");
                }
            }
            foreach (var block in dto.ContentBlocks)
            {
                // 1. Ensure the outer type matches the inner C# class type
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
                    throw new ArgumentException($"Block type '{block.Type}' does not match the provided data structure.");
                }

                // 2. You can also add specific checks here
                if (block.Data is ImageBlockData img && string.IsNullOrWhiteSpace(img.Url))
                {
                    throw new ArgumentException("Image blocks must contain a valid URL.");
                }
            }
        }
    }
}






   



 
  

