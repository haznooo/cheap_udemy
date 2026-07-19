using DataAccess.Entities;
using DataAccess.Entities.json;

namespace DataAccess.Interfaces
{
    public interface ILessonsRepository
    {
        Task<LessonEntity> AddLessonAsync(LessonEntity lesson);
        Task<LessonEntity?> GetAnyLessonByIdAsync(int lessonId);
        Task<(LessonEntity? Result, bool Conflict)> UpdateLessonAsync(int lessonId, string? title, int? estimatedDurationMinutes, List<ContentBlock>? contentBlocks, int? sortOrder);
        Task<LessonEntity?> UpdateLessonStatusAsync(int lessonId, string newStatus);
        Task<bool> DeleteLessonAsync(int lessonId);
        Task<bool> IsMediaReferencedByOtherLessonsAsync(int lessonId, string fileName);
        Task<int> GetMaxSortOrderForSectionAsync(int sectionId);
    }
}
