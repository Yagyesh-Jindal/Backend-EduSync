using EduSyncAPI.DTOs;
using EduSyncAPI.Models;

namespace EduSyncAPI.Interfaces
{
    public interface ICourseService
    {
        Task<IEnumerable<Course>> GetAllAsync();
        Task<Course> GetByIdAsync(Guid id);

        // ✅ Updated to take instructorId from the controller
        Task<Course> CreateCourseAsync(CourseDto dto, Guid instructorId);
        
        // Add update method
        Task<Course> UpdateCourseAsync(Guid id, CourseDto dto);

        Task<bool> EnrollStudentAsync(Guid studentId, Guid courseId);
        Task<int> GetEnrollmentCountAsync(Guid courseId);

        Task<IEnumerable<Course>> GetEnrolledCoursesAsync(Guid studentId);

        Task<bool> EnrollmentExistsAsync(Guid studentId, Guid courseId);
    }
}