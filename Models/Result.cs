namespace EduSyncAPI.Models
{
    public class Result
    {
        public Guid ResultId { get; set; }
        public Guid AssessmentId { get; set; }
        public Guid UserId { get; set; }
        public int Score { get; set; }
        public double Percentage { get; set; }
        public bool IsPassed { get; set; }
        public DateTime AttemptDate { get; set; }
    }

}
