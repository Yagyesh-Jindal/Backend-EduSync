using System;

namespace EduSyncAPI.Models
{
    public class Enrollment
    {
        public Guid EnrollmentId { get; set; } = Guid.NewGuid();
        public Guid StudentId { get; set; }
        public Guid CourseId { get; set; }
        // Navigation properties (optional)
        // public User Student { get; set; }
        // public Course Course { get; set; }
    }
} 