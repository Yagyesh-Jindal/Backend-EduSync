using EduSyncAPI.DTOs;
using EduSyncAPI.Interfaces;
using EduSyncAPI.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace EduSyncAPI.Services
{
    public class CourseService : ICourseService
    {
        private readonly EduSyncDbContext _context;

        public CourseService(EduSyncDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Course>> GetAllAsync()
        {
            var courses = await _context.Courses.ToListAsync();
            
            // Get all instructor IDs
            var instructorIds = courses.Select(c => c.InstructorId).Distinct().ToList();
            
            // Get all instructors in a single query
            var instructors = await _context.Users
                .Where(u => instructorIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.Name);
                
            // Add instructor name to each course
            foreach (var course in courses)
            {
                if (instructors.TryGetValue(course.InstructorId, out string instructorName))
                {
                    course.InstructorName = instructorName;
                }
                
                // Add enrollment count
                course.EnrolledStudents = await _context.Enrollments.CountAsync(e => e.CourseId == course.CourseId);
            }
            
            return courses;
        }

        public async Task<Course> GetByIdAsync(Guid id)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course != null)
            {
                var instructor = await _context.Users.FindAsync(course.InstructorId);
                if (instructor != null)
                {
                    course.InstructorName = instructor.Name;
                }
                
                // Add enrollment count
                course.EnrolledStudents = await _context.Enrollments.CountAsync(e => e.CourseId == course.CourseId);
            }
            return course;
        }

        // ✅ Uses instructorId passed from controller (extracted from JWT)
        public async Task<Course> CreateCourseAsync(CourseDto dto, Guid instructorId)
        {
            // Get instructor name first
            var instructor = await _context.Users.FindAsync(instructorId);
            string instructorName = instructor?.Name ?? "Unknown Instructor";

            var course = new Course
            {
                CourseId = Guid.NewGuid(),
                Title = dto.Title,
                Description = dto.Description,
                MediaUrl = dto.MediaUrl,
                InstructorId = instructorId,
                InstructorName = instructorName, // Set instructor name
                EnrolledStudents = 0 // Initialize to 0
            };

            _context.Courses.Add(course);
            await _context.SaveChangesAsync();

            return course;
        }
        
        public async Task<Course> UpdateCourseAsync(Guid id, CourseDto dto)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course == null)
            {
                throw new KeyNotFoundException($"Course with ID {id} not found");
            }
            
            // Update course properties
            course.Title = dto.Title;
            course.Description = dto.Description;
            course.MediaUrl = dto.MediaUrl;
            // EnrolledStudents is not updated here as it's calculated separately
            
            // Save changes
            await _context.SaveChangesAsync();
            
            // Get instructor name for the updated course
            var instructor = await _context.Users.FindAsync(course.InstructorId);
            if (instructor != null)
            {
                course.InstructorName = instructor.Name;
            }
            
            // Get current enrollment count
            course.EnrolledStudents = await _context.Enrollments.CountAsync(e => e.CourseId == course.CourseId);
            
            return course;
        }

        public async Task<bool> EnrollStudentAsync(Guid studentId, Guid courseId)
        {
            // Check if already enrolled
            var exists = await _context.Enrollments.AnyAsync(e => e.StudentId == studentId && e.CourseId == courseId);
            if (exists) return false;
            var enrollment = new Enrollment { StudentId = studentId, CourseId = courseId };
            _context.Enrollments.Add(enrollment);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<int> GetEnrollmentCountAsync(Guid courseId)
        {
            return await _context.Enrollments.CountAsync(e => e.CourseId == courseId);
        }

        public async Task<IEnumerable<Course>> GetEnrolledCoursesAsync(Guid studentId)
        {
            // Get enrolled course IDs from the database
            var enrolledCourseIds = await _context.Enrollments
                .Where(e => e.StudentId == studentId)
                .Select(e => e.CourseId)
                .ToListAsync();

            // Get the courses
            var courses = await _context.Courses
                .Where(c => enrolledCourseIds.Contains(c.CourseId))
                .ToListAsync();

            // Get instructor names
            var instructorIds = courses.Select(c => c.InstructorId).Distinct().ToList();
            var instructors = await _context.Users
                .Where(u => instructorIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.Name);

            // Add instructor names
            foreach (var course in courses)
            {
                if (instructors.TryGetValue(course.InstructorId, out string instructorName))
                {
                    course.InstructorName = instructorName;
                }
            }

            return courses;
        }

        public async Task<bool> EnrollmentExistsAsync(Guid studentId, Guid courseId)
        {
            return await _context.Enrollments.AnyAsync(e => e.StudentId == studentId && e.CourseId == courseId);
        }
    }
}
