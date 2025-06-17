namespace EduSyncAPI.Models
{
    public class Assessment
    {
        public Guid AssessmentId { get; set; }
        public Guid CourseId { get; set; }
        public string Title { get; set; }
        public string Questions { get; set; } // JSON string
        public int MaxScore { get; set; }
    }

}
