namespace EduSyncAPI.Models
{
    public class Course
    {
        public Guid CourseId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public Guid InstructorId { get; set; }
        // [MaxLength(2048)]
        public string MediaUrl { get; set; }
        public List<CourseMaterial> Materials { get; set; } = new List<CourseMaterial>();
        public string InstructorName { get; set; }
        public int EnrolledStudents { get; set; }
    }

    public class CourseMaterial
    {
        public Guid Id { get; set; }
        public Guid CourseId { get; set; }
        public string Title { get; set; }
        public string Type { get; set; }
        public string Url { get; set; }
        public DateTime UploadedAt { get; set; }
    }
}
