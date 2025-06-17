using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EduSyncAPI.Interfaces;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using EduSyncAPI.Models;
using EduSyncAPI.DTOs;
using Azure.Storage.Blobs;
using Swashbuckle.AspNetCore.Annotations;
using System.Net.Mime;

namespace EduSyncAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FilesController : ControllerBase
    {
        private readonly IBlobService _blobService;
        private readonly EduSyncDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ICourseService _courseService;

        public FilesController(IBlobService blobService, EduSyncDbContext context, IConfiguration configuration, ICourseService courseService)
        {
            _blobService = blobService;
            _context = context;
            _configuration = configuration;
            _courseService = courseService;
        }

        /// <summary>
        /// Uploads a file to Azure Blob Storage
        /// </summary>
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [Authorize(Roles = "Instructor")]
        [SwaggerOperation(Summary = "Upload a file to Azure Blob Storage")]
        public async Task<IActionResult> UploadFile(IFormFile file, [FromForm] string type = "course")
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file provided");

            try
            {
                string containerName = "course-materials";
                
                // Get user ID from claims
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized("Invalid token - user ID missing");

                var userId = Guid.Parse(userIdClaim.Value);

                // Generate a unique file name
                string fileExtension = Path.GetExtension(file.FileName);
                string fileName = $"{Guid.NewGuid()}{fileExtension}";
                
                // Upload to Azure Blob Storage
                var blobUrl = await _blobService.UploadFileAsync(file, containerName);
                
                return Ok(new { url = blobUrl, fileName = fileName, originalName = file.FileName });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uploading file: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Downloads a file from Azure Blob Storage
        /// </summary>
        [HttpGet("download")]
        [Authorize]
        [SwaggerOperation(Summary = "Download a file from Azure Blob Storage")]
        public async Task<IActionResult> DownloadFile([FromQuery] string fileUrl, [FromQuery] string? fileName = null)
        {
            try
            {
                if (string.IsNullOrEmpty(fileUrl))
                {
                    return BadRequest("File URL is required");
                }

                // Get the file from Azure Blob Storage
                var fileStream = await _blobService.DownloadFileAsync(fileUrl);
                
                // Determine content type based on file extension
                string contentType = "application/octet-stream"; // Default
                string extension = Path.GetExtension(fileUrl).ToLowerInvariant();
                
                switch (extension)
                {
                    case ".pdf":
                        contentType = "application/pdf";
                        break;
                    case ".jpg":
                    case ".jpeg":
                        contentType = "image/jpeg";
                        break;
                    case ".png":
                        contentType = "image/png";
                        break;
                    case ".gif":
                        contentType = "image/gif";
                        break;
                    case ".doc":
                    case ".docx":
                        contentType = "application/msword";
                        break;
                    case ".xls":
                    case ".xlsx":
                        contentType = "application/vnd.ms-excel";
                        break;
                    case ".ppt":
                    case ".pptx":
                        contentType = "application/vnd.ms-powerpoint";
                        break;
                    case ".mp4":
                        contentType = "video/mp4";
                        break;
                    case ".mp3":
                        contentType = "audio/mpeg";
                        break;
                }

                // Use provided filename or extract from URL
                string downloadFileName = fileName;
                if (string.IsNullOrEmpty(downloadFileName))
                {
                    // Extract filename from URL
                    var uri = new Uri(fileUrl);
                    var segments = uri.AbsolutePath.Split('/');
                    downloadFileName = segments[segments.Length - 1];
                }

                return File(fileStream, contentType, downloadFileName);
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine($"File not found: {ex.Message}");
                return NotFound($"File not found: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading file: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets all files for a specific course
        /// </summary>
        [HttpGet("course/{courseId}")]
        [Authorize]
        [SwaggerOperation(Summary = "Get all files for a course")]
        public async Task<IActionResult> GetCourseFiles(Guid courseId)
        {
            try
            {
                // Check if course exists
                var course = await _context.Courses.FindAsync(courseId);
                if (course == null)
                    return NotFound("Course not found");

                // Check if user is instructor or enrolled in the course
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized("Invalid token - user ID missing");

                var userId = Guid.Parse(userIdClaim.Value);
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

                // Only allow access to course owner or enrolled students
                if (userRole == "Instructor" && course.InstructorId != userId)
                {
                    return Forbidden("You don't have permission to access files for this course");
                }
                else if (userRole == "Student")
                {
                    // Check if student is enrolled using the database
                    var isEnrolled = await _courseService.EnrollmentExistsAsync(userId, courseId);
                    
                    if (!isEnrolled)
                        return Forbidden("You are not enrolled in this course");
                }

                // Get all materials for the course (which should have blob URLs)
                var materials = await _context.CourseMaterial
                    .Where(m => m.CourseId == courseId)
                    .Select(m => new CourseMaterialDto
                    {
                        Id = m.Id,
                        Title = m.Title,
                        Type = m.Type,
                        Url = m.Url,
                        UploadedAt = m.UploadedAt
                    })
                    .ToListAsync();

                return Ok(materials);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting course files: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes a file from Azure Blob Storage and removes its reference
        /// </summary>
        [HttpDelete("{materialId}")]
        [Authorize(Roles = "Instructor")]
        [SwaggerOperation(Summary = "Delete a file")]
        public async Task<IActionResult> DeleteFile(Guid materialId)
        {
            try
            {
                // Find the material
                var material = await _context.CourseMaterial.FindAsync(materialId);
                if (material == null)
                    return NotFound("Material not found");

                // Check if user is the course owner
                var course = await _context.Courses.FindAsync(material.CourseId);
                if (course == null)
                    return NotFound("Course not found");

                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized("Invalid token - user ID missing");

                var userId = Guid.Parse(userIdClaim.Value);

                if (course.InstructorId != userId)
                    return Forbidden("You don't have permission to delete this file");

                // Delete the blob from Azure storage
                if (!string.IsNullOrEmpty(material.Url) && material.Url.Contains("blob.core.windows.net"))
                {
                    try
                    {
                        await _blobService.DeleteFileAsync(material.Url);
                        Console.WriteLine($"Deleted blob at URL: {material.Url}");
                    }
                    catch (Exception ex)
                    {
                        // Log but continue - we still want to remove the database reference
                        Console.WriteLine($"Error deleting blob: {ex.Message}. Continuing with database cleanup.");
                    }
                }

                // Delete material from database
                _context.CourseMaterial.Remove(material);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting file: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [NonAction]
        private IActionResult Forbidden(string message)
        {
            return StatusCode(403, message);
        }
    }
} 