using System.Security.Claims;
using EduSyncAPI.DTOs;
using EduSyncAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduSyncAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CoursesController : ControllerBase
    {
        private readonly ICourseService _courseService;

        public CoursesController(ICourseService courseService)
        {
            _courseService = courseService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll() =>
            Ok(await _courseService.GetAllAsync());

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id) =>
            Ok(await _courseService.GetByIdAsync(id));

        // Get courses for the logged-in instructor
        [HttpGet("instructor")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> GetInstructorCourses()
        {
            var instructorIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (instructorIdClaim == null)
                return Unauthorized("Invalid token — user ID missing");

            var instructorId = Guid.Parse(instructorIdClaim.Value);
            var courses = await _courseService.GetAllAsync();
            
            // Filter courses by instructor ID
            var instructorCourses = courses.Where(c => c.InstructorId == instructorId).ToList();
            return Ok(instructorCourses);
        }

        // Get courses for the logged-in student
        [HttpGet("student")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetStudentCourses()
        {
            var studentIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (studentIdClaim == null)
                return Unauthorized("Invalid token — user ID missing");

            var studentId = Guid.Parse(studentIdClaim.Value);
            
            // Use the service to get enrolled courses from the database
            var enrolledCourses = await _courseService.GetEnrolledCoursesAsync(studentId);
            
            return Ok(enrolledCourses);
        }

        // Enroll in a course
        [HttpPost("{courseId}/enroll")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> EnrollInCourse(Guid courseId)
        {
            try
            {
                Console.WriteLine($"Enrollment request received for courseId: {courseId}");
                
                var studentIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (studentIdClaim == null)
                {
                    Console.WriteLine("Unauthorized: Invalid token - user ID missing");
                    return Unauthorized("Invalid token — user ID missing");
                }

                var studentId = Guid.Parse(studentIdClaim.Value);
                Console.WriteLine($"Student ID from token: {studentId}");

                // Check if the course exists
                var course = await _courseService.GetByIdAsync(courseId);
                if (course == null)
                {
                    Console.WriteLine($"Course not found with ID: {courseId}");
                    return NotFound("Course not found");
                }
                
                Console.WriteLine($"Course found: {course.Title} (ID: {course.CourseId})");

                // Use service/database for enrollment
                var enrolled = await _courseService.EnrollStudentAsync(studentId, courseId);
                if (!enrolled)
                {
                    Console.WriteLine($"Student {studentId} is already enrolled in course {courseId}");
                    return BadRequest("Already enrolled in this course");
                }

                Console.WriteLine($"Successfully enrolled student {studentId} in course {courseId}");
                return Ok(new { message = "Successfully enrolled in course" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in EnrollInCourse: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, $"Error enrolling in course: {ex.Message}");
            }
        }

        // Get enrollment count for a course (Instructor only)
        [HttpGet("{courseId}/enrollments")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> GetEnrollmentCount(Guid courseId)
        {
            var course = await _courseService.GetByIdAsync(courseId);
            if (course == null)
                return NotFound("Course not found");
            var count = await _courseService.GetEnrollmentCountAsync(courseId);
            return Ok(new { courseId, enrolledStudents = count });
        }

        // 🔐 Require Instructor role
        [HttpPost]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> Create([FromBody] CourseDto dto)
        {
            try
            {
                // Validate the model
                if (!ModelState.IsValid)
                {
                    Console.WriteLine("Invalid course data: " + string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)));
                    return BadRequest(ModelState);
                }
                
                // ✅ Extract instructor ID from token
                var instructorIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (instructorIdClaim == null)
                    return Unauthorized("Invalid token — user ID missing");

                var instructorId = Guid.Parse(instructorIdClaim.Value);
                Console.WriteLine($"Creating course for instructor: {instructorId}");

                var course = await _courseService.CreateCourseAsync(dto, instructorId);
                Console.WriteLine($"Course created successfully: {course.CourseId}");
                return CreatedAtAction(nameof(GetById), new { id = course.CourseId }, course);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating course: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, $"Error creating course: {ex.Message}");
            }
        }
        
        // Update a course
        [HttpPut("{id}")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> Update(Guid id, [FromBody] CourseDto dto)
        {
            try
            {
                // Get the current user ID from the token
                var instructorIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (instructorIdClaim == null)
                    return Unauthorized("Invalid token — user ID missing");
                
                var instructorId = Guid.Parse(instructorIdClaim.Value);
                
                // Check if the course exists and belongs to this instructor
                var existingCourse = await _courseService.GetByIdAsync(id);
                if (existingCourse == null)
                    return NotFound("Course not found");
                    
                if (existingCourse.InstructorId != instructorId)
                    return Forbid("You can only update your own courses");
                
                // Update the course
                var updatedCourse = await _courseService.UpdateCourseAsync(id, dto);
                return Ok(updatedCourse);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error updating course: {ex.Message}");
            }
        }
    }
}
