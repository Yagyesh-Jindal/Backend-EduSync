namespace EduSyncAPI.Models
{
    public class Question
    {
        public Guid QuestionId { get; set; }
        public string Text { get; set; }
        public List<Answer> Answers { get; set; }
        public Guid CorrectAnswerId { get; set; }
        public int Points { get; set; }
    }
} 