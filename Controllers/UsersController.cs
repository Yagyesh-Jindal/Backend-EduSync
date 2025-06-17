using EduSyncAPI.DTOs;
using EduSyncAPI.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace EduSyncAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public async Task<IActionResult> Get() => Ok(await _userService.GetAllAsync());

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id) => Ok(await _userService.GetByIdAsync(id));

        [HttpGet("profile")]
        [Authorize]
        public async Task<IActionResult> GetProfile()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return Unauthorized("Invalid token — user ID missing");

            var userId = Guid.Parse(userIdClaim.Value);
            var user = await _userService.GetByIdAsync(userId);
            
            if (user == null)
                return NotFound("User not found");
                
            return Ok(new {
                name = user.Name,
                email = user.Email,
                role = user.Role
            });
        }

        [HttpGet("progress")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetStudentProgress()
        {
            var studentIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (studentIdClaim == null)
                return Unauthorized("Invalid token — user ID missing");

            var studentId = Guid.Parse(studentIdClaim.Value);
            
            // For now, return mock progress data
            // In a real implementation, you would query the database for actual progress
            return Ok(new {
                completedAssessments = 0,
                averageScore = 0,
                studyHours = 0,
                lastActivity = DateTime.UtcNow
            });
        }

        [HttpGet("instructor-stats")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> GetInstructorStats()
        {
            var instructorIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (instructorIdClaim == null)
                return Unauthorized("Invalid token — user ID missing");

            var instructorId = Guid.Parse(instructorIdClaim.Value);
            
            // For now, return basic stats
            // In a real implementation, you would query the database for actual statistics
            return Ok(new {
                totalStudents = 0,
                totalAssessments = 0,
                pendingGrading = 0,
                completionRate = 0
            });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] UserDto dto)
        {
            var result = await _userService.CreateAsync(dto);
            return CreatedAtAction(nameof(Get), new { id = result.UserId }, result);
        }
    }
}
