using System.ComponentModel.DataAnnotations;

namespace EduSyncAPI.DTOs
{
    public class CourseDto
    {
        [Required(ErrorMessage = "Title is required")]
        public string Title { get; set; }
        
        [Required(ErrorMessage = "Description is required")]
        public string Description { get; set; }
        
        // Optional field
        [MaxLength(2048)]
        public string MediaUrl { get; set; }
    }
}
