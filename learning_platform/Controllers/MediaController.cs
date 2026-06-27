using Business.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/media")]
    [Authorize]
    public class MediaController(IMediaService mediaService) : ControllerBase
    {
        // 50 MB limit for this example
        private const long MaxFileSize = 50 * 1024 * 1024;

        // Allowed file types
        private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".mp4", ".mov" };

        [HttpPost("upload")]
        public async Task<IActionResult> UploadMedia(IFormFile file)
        {
            // 1. Basic Validation
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file was uploaded.");
            }

            // 2. Size Validation
            if (file.Length > MaxFileSize)
            {
                return BadRequest($"File exceeds the maximum limit of {MaxFileSize / (1024 * 1024)}MB.");
            }

            // 3. Extension Validation
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !_allowedExtensions.Contains(extension))
            {
                return BadRequest("Invalid file type. Only JPG, PNG, MP4, and MOV are allowed.");
            }

            try
            {
                // 4. Send to the service layer (which talks to Cloudinary/Supabase)
                string fileUrl = await mediaService.UploadFileAsync(file);

                // 5. Return the URL in a standard JSON format
                return Ok(new { url = fileUrl });
            }
            catch (Exception ex)
            {
                // Log the exception in a real app
                return StatusCode(500, "An error occurred while uploading the file.");
            }
        }
    }
}
