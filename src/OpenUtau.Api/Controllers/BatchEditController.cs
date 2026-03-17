using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Editing;
using OpenUtau.Core.Format;
using System.IO;
using System.Linq;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/project/[controller]")]
    public class BatchEditController : ControllerBase
    {
        [HttpPost("quantize")]
        public IActionResult Quantize(IFormFile file, [FromQuery] int quantizeToTick = 15)
        {
            return ExecuteEdit(file, (project) => 
            {
                foreach(var part in project.parts.OfType<OpenUtau.Core.Ustx.UVoicePart>()) {
                    foreach(var note in part.notes) {
                        note.position = (note.position / quantizeToTick) * quantizeToTick;
                    }
                }
            });
        }

        [HttpPost("clear-pitch")]
        public IActionResult ClearPitch(IFormFile file)
        {
            return ExecuteEdit(file, (project) => 
            {
                foreach(var part in project.parts.OfType<OpenUtau.Core.Ustx.UVoicePart>()) {
                    foreach(var note in part.notes) {
                        note.pitch.data.Clear();
                    }
                }
            });
        }

        private IActionResult ExecuteEdit(IFormFile file, System.Action<OpenUtau.Core.Ustx.UProject> modifier)
        {
            if (file == null || file.Length == 0) return BadRequest("No file uploaded");

            try
            {
                var tempFile = Path.GetTempFileName();
                using (var stream = new FileStream(tempFile, FileMode.Create)) { file.CopyTo(stream); }

                Formats.LoadProject(new string[] { tempFile });
                var project = DocManager.Inst.Project;
                if (project == null) {
                    System.IO.File.Delete(tempFile);
                    return BadRequest("Failed to load project.");
                }

                modifier(project);

                var outTemp = Path.GetTempFileName() + ".ustx";
                Ustx.Save(outTemp, project);
                System.IO.File.Delete(tempFile);

                var streamRet = new FileStream(outTemp, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);
                return File(streamRet, "application/json", "edited.ustx");
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
