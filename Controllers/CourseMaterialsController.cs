using System.Security.Claims;
using EduSyncAPI.Models;
using EduSyncAPI.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EduSyncAPI.Controllers
{
    [ApiController]
    [Route("api/courses/{courseId}/materials")]
    public class CourseMaterialsController : ControllerBase
    {
        private readonly EduSyncDbContext _context;
        private readonly ILogger<CourseMaterialsController> _logger;

        public CourseMaterialsController(EduSyncDbContext context, ILogger<CourseMaterialsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetMaterials(string courseId)
        {
            try
            {
                // Try to parse the courseId as Guid
                if (!Guid.TryParse(courseId, out Guid courseGuid))
                {
                    _logger.LogWarning("Invalid course ID format: {CourseId}", courseId);
                    return BadRequest($"Invalid course ID format: {courseId}. Expected a valid GUID.");
                }

                // Check if course exists
                var course = await _context.Courses.FindAsync(courseGuid);
                if (course == null)
                {
                    return NotFound($"Course with ID {courseId} not found");
                }
                
                // If user is a student, verify enrollment
                if (User.Identity.IsAuthenticated && User.IsInRole("Student"))
                {
                    var studentIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                    if (studentIdClaim == null)
                        return Unauthorized("Invalid token — user ID missing");

                    var studentId = Guid.Parse(studentIdClaim.Value);
                    
                    // Check if student is enrolled in the course
                    var isEnrolled = await _context.Enrollments.AnyAsync(e => 
                        e.StudentId == studentId && e.CourseId == courseGuid);
                        
                    if (!isEnrolled)
                    {
                        _logger.LogWarning("Student {StudentId} tried to access materials for course {CourseId} without being enrolled", 
                            studentId, courseId);
                        return Ok(new List<CourseMaterialDto>()); // Return empty list instead of error for security
                    }
                }
                
                // Fetch materials from DB first
                var materials = await _context.CourseMaterial
                    .Where(m => m.CourseId == courseGuid)
                    .ToListAsync();
                
                // Map to DTOs in memory
                var materialDtos = materials.Select(m => new CourseMaterialDto
                {
                    Id = m.Id,
                    CourseId = m.CourseId,
                    Title = m.Title,
                    Type = m.Type,
                    Url = m.Url,
                    UploadedAt = m.UploadedAt,
                    FileName = ExtractFileNameFromUrl(m.Url),
                    IsStorageBlob = m.Url.Contains("blob.core.windows.net")
                }).ToList();
                
                return Ok(materialDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving materials for course {CourseId}", courseId);
                return StatusCode(500, $"Error retrieving materials: {ex.Message}");
            }
        }

        [HttpPost]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> AddMaterial(string courseId, [FromBody] CourseMaterial material)
        {
            try
            {
                // Try to parse the courseId as Guid
                if (!Guid.TryParse(courseId, out Guid courseGuid))
                {
                    _logger.LogWarning("Invalid course ID format: {CourseId}", courseId);
                    return BadRequest($"Invalid course ID format: {courseId}. Expected a valid GUID.");
                }

                // Validate instructor ownership
                var instructorIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (instructorIdClaim == null)
                    return Unauthorized("Invalid token — user ID missing");

                var instructorId = Guid.Parse(instructorIdClaim.Value);
                
                // Check if course exists and belongs to the instructor
                var course = await _context.Courses.FindAsync(courseGuid);
                if (course == null)
                    return NotFound("Course not found");
                    
                if (course.InstructorId != instructorId)
                    return Forbid("You don't have permission to add materials to this course");

                // Set material properties
                material.Id = Guid.NewGuid();
                material.CourseId = courseGuid;
                material.UploadedAt = DateTime.UtcNow;

                // Ensure required fields are provided
                if (string.IsNullOrWhiteSpace(material.Title))
                {
                    return BadRequest("Material title is required");
                }
                
                if (string.IsNullOrWhiteSpace(material.Type))
                {
                    material.Type = "Document"; // Default type
                }
                
                if (string.IsNullOrWhiteSpace(material.Url))
                {
                    return BadRequest("Material URL is required");
                }

                _context.CourseMaterial.Add(material);
                await _context.SaveChangesAsync();
                
                // Return DTO with additional properties
                var materialDto = new CourseMaterialDto
                {
                    Id = material.Id,
                    CourseId = material.CourseId,
                    Title = material.Title,
                    Type = material.Type,
                    Url = material.Url,
                    UploadedAt = material.UploadedAt,
                    FileName = ExtractFileNameFromUrl(material.Url),
                    IsStorageBlob = material.Url.Contains("blob.core.windows.net")
                };
                
                return CreatedAtAction(nameof(GetMaterials), new { courseId }, materialDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding material to course {CourseId}", courseId);
                return StatusCode(500, $"Error adding material: {ex.Message}");
            }
        }

        [HttpDelete("{materialId}")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> DeleteMaterial(string courseId, string materialId)
        {
            try
            {
                // Try to parse the courseId and materialId as Guids
                if (!Guid.TryParse(courseId, out Guid courseGuid))
                {
                    _logger.LogWarning("Invalid course ID format: {CourseId}", courseId);
                    return BadRequest($"Invalid course ID format: {courseId}. Expected a valid GUID.");
                }

                if (!Guid.TryParse(materialId, out Guid materialGuid))
                {
                    _logger.LogWarning("Invalid material ID format: {MaterialId}", materialId);
                    return BadRequest($"Invalid material ID format: {materialId}. Expected a valid GUID.");
                }

                // Validate instructor ownership
                var instructorIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (instructorIdClaim == null)
                    return Unauthorized("Invalid token — user ID missing");

                var instructorId = Guid.Parse(instructorIdClaim.Value);
                
                // Check if course exists and belongs to the instructor
                var course = await _context.Courses.FindAsync(courseGuid);
                if (course == null)
                    return NotFound("Course not found");
                    
                if (course.InstructorId != instructorId)
                    return Forbid("You don't have permission to delete materials from this course");
                    
                // Find the material
                var material = await _context.CourseMaterial
                    .FirstOrDefaultAsync(m => m.Id == materialGuid && m.CourseId == courseGuid);
                    
                if (material == null)
                    return NotFound("Material not found");

                _context.CourseMaterial.Remove(material);
                await _context.SaveChangesAsync();
                
                // Note: If we wanted to delete the blob from Azure storage as well,
                // we would need to inject the BlobService and delete it here.
                
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting material {MaterialId} from course {CourseId}", materialId, courseId);
                return StatusCode(500, $"Error deleting material: {ex.Message}");
            }
        }
        
        private string ExtractFileNameFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return string.Empty;
                
            try
            {
                // For Azure blob storage URLs
                if (url.Contains("blob.core.windows.net"))
                {
                    var uri = new Uri(url);
                    var segments = uri.Segments;
                    return segments[segments.Length - 1];
                }
                
                // For other URLs, just return the last segment
                return Path.GetFileName(url);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
} 