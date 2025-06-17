using EduSyncAPI.DTOs;
using EduSyncAPI.Models;

namespace EduSyncAPI.Interfaces
{
    public interface IResultService
    {
        Task<IEnumerable<Result>> GetAllAsync();
        Task<Result> GetByIdAsync(Guid id);
        Task<Result> CreateAsync(ResultDto dto);
        Task<Result> CalculateResultAsync(ResultDto dto);
    }
} 