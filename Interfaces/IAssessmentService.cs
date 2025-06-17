using EduSyncAPI.DTOs;
using EduSyncAPI.Models;

namespace EduSyncAPI.Interfaces
{
    public interface IAssessmentService
    {
        Task<IEnumerable<Assessment>> GetAllAsync();
        Task<Assessment> GetByIdAsync(Guid id);
        Task<IEnumerable<Assessment>> GetByCourseIdAsync(Guid courseId);
        Task<Assessment> CreateAsync(AssessmentDto dto);
        Task<Assessment> UpdateAsync(Guid id, AssessmentDto dto);
        Task<bool> DeleteAsync(Guid id);
    }
}
