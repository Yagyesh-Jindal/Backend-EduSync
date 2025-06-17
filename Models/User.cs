namespace EduSyncAPI.Models
{
    public class User
    {
        public Guid UserId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Role { get; set; } // Student or Instructor
        public string PasswordHash { get; set; }
    }

}
