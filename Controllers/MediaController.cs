using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Microsoft.AspNetCore.Http;
using EduSyncAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace EduSyncAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MediaController : ControllerBase
    {
        private readonly IBlobService _blobService;

        public MediaController(IBlobService blobService)
        {
            _blobService = blobService;
        }

        public class FileUploadModel
        {
            [SwaggerSchema("The file to upload")]
            public IFormFile File { get; set; }
        }

        /// <summary>
        /// Uploads a media file to blob storage.
        /// </summary>
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [SwaggerOperation(Summary = "Upload a media file", Description = "Uploads a media file to Azure Blob Storage.")]
        [SwaggerResponse(StatusCodes.Status200OK, "File uploaded successfully", typeof(object))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "No file provided")]
        public async Task<IActionResult> Upload([FromForm] FileUploadModel model)
        {
            if (model.File == null || model.File.Length == 0)
                return BadRequest("No file provided.");

            var url = await _blobService.UploadFileAsync(model.File, "courses-media");
            return Ok(new { Url = url });
        }
    }
}
