using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace namHub_FastFood.Controller.Images
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImagesController : ControllerBase
    {
        private readonly string _uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");

        public ImagesController()
        {
            if (!Directory.Exists(_uploadFolder))
            {
                Directory.CreateDirectory(_uploadFolder);
            }
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }

            var fileName = Path.GetFileName(file.FileName);
            var filePath = Path.Combine(_uploadFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var imageUrl = $"/images/{fileName}"; // URL to access the image
            return Ok(new { url = imageUrl });
        }
    }
}
