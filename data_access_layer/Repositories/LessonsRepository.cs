using DataAccess.Data;
using DataAccess.Dto;
using DataAccess.Entities;
using DataAccess.Entities.json;
using DataAccess.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataAccess.Repositories
{
    public class LessonsRepository(AppDbContext context) : ILessonsRepository
    {

        public async Task<LessonEntity> AddLessonAsync(LessonEntity lesson)
        {
            context.Lessons.Add(lesson);
            await context.SaveChangesAsync();
            return lesson;
        }

        // Returns any lesson by ID without filtering by course status (for owner/admin operations).
        public async Task<LessonEntity?> GetAnyLessonByIdAsync(int lessonId)
        {
            return await context.Lessons
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.lesson_id == lessonId);
        }

        public async Task<LessonEntity?> UpdateLessonAsync(int lessonId, string? title, int? estimatedDurationMinutes, List<ContentBlock>? contentBlocks)
        {
            try
            {
                var lesson = await context.Lessons.FirstOrDefaultAsync(l => l.lesson_id == lessonId);
                if (lesson == null) return null;

                if (!string.IsNullOrWhiteSpace(title)) lesson.title = title;
                if (estimatedDurationMinutes.HasValue) lesson.estimated_duration_minutes = estimatedDurationMinutes.Value;
                if (contentBlocks != null) lesson.content_blocks = contentBlocks;
                lesson.updated_at = DateTime.UtcNow;

                await context.SaveChangesAsync();
                return lesson;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
        }

        public async Task<bool> DeleteLessonAsync(int lessonId)
        {
            try
            {
                var affected = await context.Lessons
                    .Where(l => l.lesson_id == lessonId)
                    .ExecuteDeleteAsync();
                return affected > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return false;
            }
        }

        // True if any OTHER lesson's content blocks mention the file name. Checked before
        // removing a media file from storage, so a file reused across lessons survives.
        // content_blocks is jsonb, so this is a text LIKE over the column — crude but safe:
        // stored names are server-generated GUIDs, so false positives are practically impossible.
        public async Task<bool> IsMediaReferencedByOtherLessonsAsync(int lessonId, string fileName)
        {
            try
            {
                var pattern = $"%{fileName}%";
                return await context.Lessons
                    .FromSqlInterpolated($"SELECT * FROM lessons WHERE lesson_id <> {lessonId} AND content_blocks::text LIKE {pattern}")
                    .AnyAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                // If the check itself fails, claim the file is referenced so the
                // caller keeps it — leaking a file beats breaking another lesson.
                return true;
            }
        }

        // Returns the maximum sort_order for lessons in a given section so we can append a new lesson at the end of the list.
        public async Task<int> GetMaxSortOrderForSectionAsync(int sectionId)
        {
            var max = await context.Lessons
                .Where(l => l.section_id == sectionId)
                .MaxAsync(l => (int?)l.sort_order);

            return max ?? -1;
        }
    }
}
