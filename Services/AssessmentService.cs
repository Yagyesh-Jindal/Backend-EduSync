using EduSyncAPI.DTOs;
using EduSyncAPI.Interfaces;
using EduSyncAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EduSyncAPI.Services
{
    public class AssessmentService : IAssessmentService
    {
        private readonly EduSyncDbContext _context;

        public AssessmentService(EduSyncDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Assessment>> GetAllAsync() =>
            await _context.Assessments.ToListAsync();

        public async Task<Assessment> GetByIdAsync(Guid id) =>
            await _context.Assessments.FindAsync(id);

        public async Task<IEnumerable<Assessment>> GetByCourseIdAsync(Guid courseId) =>
            await _context.Assessments
                .Where(a => a.CourseId == courseId)
                .ToListAsync();

        public async Task<Assessment> CreateAsync(AssessmentDto dto)
        {
            var assessment = new Assessment
            {
                AssessmentId = Guid.NewGuid(),
                CourseId = dto.CourseId,
                Title = dto.Title,
                Questions = dto.Questions,
                MaxScore = dto.MaxScore
            };
            _context.Assessments.Add(assessment);
            await _context.SaveChangesAsync();
            return assessment;
        }

        public async Task<Assessment> UpdateAsync(Guid id, AssessmentDto dto)
        {
            var assessment = await _context.Assessments.FindAsync(id);
            if (assessment == null)
                throw new InvalidOperationException($"Assessment with ID {id} not found");

            // Update properties
            assessment.Title = dto.Title;
            assessment.CourseId = dto.CourseId;
            assessment.Questions = dto.Questions;
            assessment.MaxScore = dto.MaxScore;

            _context.Assessments.Update(assessment);
            await _context.SaveChangesAsync();
            return assessment;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var assessment = await _context.Assessments.FindAsync(id);
            if (assessment == null)
                return false;

            _context.Assessments.Remove(assessment);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
