namespace EduSyncAPI.DTOs
{
    public class LoginDto
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class AuthResponseDto
    {
        public string Token { get; set; }
        public string Role { get; set; }
        public string Name { get; set; }
    }
}
