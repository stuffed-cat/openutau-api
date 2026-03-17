using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core.Format;
using System.IO;
using System.Threading.Tasks;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FormatsController : ControllerBase
    {
        [HttpPost("convert")]
        public async Task<IActionResult> ConvertToUstx(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file provided");

            var ext = Path.GetExtension(file.FileName).ToLower();
            var tempFilePath = Path.GetTempFileName() + ext;
            
            using (var stream = new FileStream(tempFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            try
            {
                var project = Formats.ReadProject(new string[] { tempFilePath });
                if (project == null)
                    return BadRequest("Failed to read project from the provided file.");

                var outputPath = Path.GetTempFileName() + ".ustx";
                Ustx.Save(outputPath, project);

                var memoryStream = new MemoryStream(await System.IO.File.ReadAllBytesAsync(outputPath));

                System.IO.File.Delete(tempFilePath);
                System.IO.File.Delete(outputPath);

                return File(memoryStream, "application/json", Path.GetFileNameWithoutExtension(file.FileName) + ".ustx");
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}
