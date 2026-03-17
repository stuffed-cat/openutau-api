using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core.Format;
using System.IO;
using System.Linq;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/project/[controller]")]
    public class ProjectInfoController : ControllerBase
    {
        [HttpPost]
        public IActionResult GetInfo(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            try
            {
                var tempFile = Path.GetTempFileName();
                using (var stream = new FileStream(tempFile, FileMode.Create))
                {
                    file.CopyTo(stream);
                }

                Formats.LoadProject(new string[] { tempFile });
                var project = OpenUtau.Core.DocManager.Inst.Project;
                
                if (project == null)
                {
                    System.IO.File.Delete(tempFile);
                    return BadRequest("Failed to load project.");
                }

                var info = new
                {
                    Tracks = project.tracks.Select(t => new
                    {
                        Singer = t.Singer?.Id,
                        Phonemizer = t.Phonemizer?.GetType().Name,
                        Renderer = t.RendererSettings?.renderer
                    }),
                    Parts = project.parts.Select(p => new
                    {
                        Name = p.name,
                        TrackNo = p.trackNo,
                        Duration = p.Duration
                    })
                };

                System.IO.File.Delete(tempFile);
                return Ok(info);
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
