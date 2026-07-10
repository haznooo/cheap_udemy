using DataAccess.Entities;
using DataAccess.Entities.json;

namespace DataAccess.Interfaces
{
    public interface ILessonsRepository
    {
        Task<LessonEntity> AddLessonAsync(LessonEntity lesson);
        Task<LessonEntity?> GetAnyLessonByIdAsync(int lessonId);
        Task<LessonEntity?> UpdateLessonAsync(int lessonId, string? title, int? estimatedDurationMinutes, List<ContentBlock>? contentBlocks);
        Task<bool> DeleteLessonAsync(int lessonId);
        Task<bool> IsMediaReferencedByOtherLessonsAsync(int lessonId, string fileName);
        Task<int> GetMaxSortOrderForSectionAsync(int sectionId);
    }
}
