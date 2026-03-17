using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Format;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/project/[controller]")]
    public class ExportController : ControllerBase
    {
        [HttpPost("track")]
        public async Task<IActionResult> ExportTrack(IFormFile file, [FromQuery] int trackIndex)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            try
            {
                var tempFile = Path.GetTempFileName();
                using (var stream = new FileStream(tempFile, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                Formats.LoadProject(new string[] { tempFile });
                var project = DocManager.Inst.Project;

                if (project == null || project.tracks.Count <= trackIndex)
                {
                    System.IO.File.Delete(tempFile);
                    return BadRequest("Failed to load project or track index out of bounds.");
                }

                // Render only specific track
                // Actually PlaybackManager doesn't natively expose single track rendering easily without muting.
                // We'll mute all other tracks.
                for (int i = 0; i < project.tracks.Count; i++)
                {
                    project.tracks[i].Mute = (i != trackIndex);
                }

                var baseFile = Path.Combine(Path.GetTempPath(), "track_export_" + pathHelper());
                await PlaybackManager.Inst.RenderMixdown(project, baseFile);

                var wavFile = baseFile + ".wav";
                System.IO.File.Delete(tempFile);

                if (!System.IO.File.Exists(wavFile))
                    return StatusCode(500, "Failed to render wav.");

                var streamRet = new FileStream(wavFile, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);
                return File(streamRet, "audio/wav", $"track_{trackIndex}.wav");
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private string pathHelper() => System.Guid.NewGuid().ToString("N");
    }
}
