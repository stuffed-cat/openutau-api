using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Format;
using System.IO;
using System.Threading.Tasks;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProjectController : ControllerBase
    {
        [HttpPost("render")]
        public async Task<IActionResult> RenderProject(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No ustx file provided");

            var tempFilePath = Path.GetTempFileName();
            using (var stream = new FileStream(tempFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            try
            {
                // Load Project
                var project = Ustx.Load(tempFilePath);
                
                // Define output path
                var outputPath = Path.GetTempFileName() + ".wav";
                
                // Render using PlaybackManager (writes to outputPath)
                await PlaybackManager.Inst.RenderMixdown(project, outputPath);
                
                if (!System.IO.File.Exists(outputPath))
                {
                    return StatusCode(500, "Render failed, output file not found.");
                }
                
                // Read and Return Audio
                var memoryStream = new MemoryStream(await System.IO.File.ReadAllBytesAsync(outputPath));
                
                // Cleanup temp files
                System.IO.File.Delete(tempFilePath);
                System.IO.File.Delete(outputPath);
                
                return File(memoryStream, "audio/wav", "rendered.wav");
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, ex.Message + "\n" + ex.StackTrace);
            }
        }
    }
}
