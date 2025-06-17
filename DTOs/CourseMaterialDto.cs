namespace EduSyncAPI.DTOs
{
    public class CourseMaterialDto
    {
        public Guid Id { get; set; }
        public Guid CourseId { get; set; }
        public string Title { get; set; }
        public string Type { get; set; }
        public string Url { get; set; }
        public DateTime UploadedAt { get; set; }
        public string FileName { get; set; }
        public bool IsStorageBlob { get; set; }
    }
} 