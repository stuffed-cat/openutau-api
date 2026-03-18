using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Format;
using OpenUtau.Core.Ustx;
using System;
using System.IO;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/project/[controller]")]
    public class TrackEffectsController : ControllerBase
    {
        public class TrackEffectParams
        {
            public int TrackIndex { get; set; }
            public double? Volume { get; set; }
            public double? Pan { get; set; }
            public bool? Mute { get; set; }
            public bool? Solo { get; set; }
        }

        [HttpPost("apply")]
        public IActionResult ApplyEffects([FromForm] IFormFile file, [FromForm] string effectsJson)
        {
            if (file == null || file.Length == 0) return BadRequest("No file uploaded");

            try
            {
                var effects = System.Text.Json.JsonSerializer.Deserialize<TrackEffectParams>(effectsJson);
                if (effects == null) return BadRequest("Invalid effects JSON");

                var tempFile = Path.GetTempFileName();
                using (var stream = new FileStream(tempFile, FileMode.Create)) { file.CopyTo(stream); }

                Formats.LoadProject(new string[] { tempFile });
                var project = DocManager.Inst.Project;
                if (project == null) {
                    System.IO.File.Delete(tempFile);
                    return BadRequest("Failed to load project.");
                }

                if (effects.TrackIndex < 0 || effects.TrackIndex >= project.tracks.Count)
                {
                    System.IO.File.Delete(tempFile);
                    return BadRequest("Invalid track index");
                }

                var track = project.tracks[effects.TrackIndex];

                if (effects.Volume.HasValue) track.Volume = effects.Volume.Value;
                if (effects.Pan.HasValue) track.Pan = effects.Pan.Value;
                if (effects.Mute.HasValue) track.Mute = effects.Mute.Value;
                if (effects.Solo.HasValue) track.Solo = effects.Solo.Value;

                var outTemp = Path.GetTempFileName() + ".ustx";
                Ustx.Save(outTemp, project);
                System.IO.File.Delete(tempFile);

                var streamRet = new FileStream(outTemp, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);
                return File(streamRet, "application/json", "edited.ustx");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
