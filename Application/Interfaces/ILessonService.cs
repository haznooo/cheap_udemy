using Business.Common;
using Business.Dto.Request;
using DataAccess.Dto;

namespace Business.Interfaces
{
    public interface ILessonService
    {
        Task<MyResult<LessonDto>> CreateLessonAsync(LessonRequest request, int callerId, bool isAdmin);
        Task<MyResult<LessonDto>> GetLessonAsync(int lessonId, int callerId, bool isAdmin);
        Task<MyResult<LessonDto>> UpdateLessonAsync(int lessonId, UpdateLessonRequest request, int callerId, bool isAdmin);
        Task<MyResult<bool>> DeleteLessonAsync(int lessonId, int callerId, bool isAdmin);
    }
}
