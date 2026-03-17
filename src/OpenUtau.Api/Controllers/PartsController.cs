using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Format;
using System.IO;
using System;
using System.Linq;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/project/parts")]
    public class PartsController : ControllerBase
    {
        private IActionResult ExecuteEdit(IFormFile file, System.Action<UProject> action)
        {
            if (file == null || file.Length == 0) return BadRequest("No file uploaded");

            try
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "OpenUtauApi");
                Directory.CreateDirectory(tempDir);
                var tempFile = Path.Combine(tempDir, Guid.NewGuid().ToString() + ".ustx");
                
                using (var stream = new FileStream(tempFile, FileMode.Create))
                {
                    file.CopyTo(stream);
                }

                var project = Ustx.Load(tempFile);
                project.ValidateFull();

                action(project);

                project.ValidateFull();

                var outTemp = Path.Combine(tempDir, Guid.NewGuid().ToString() + ".ustx");
                Ustx.Save(outTemp, project);

                var bytes = System.IO.File.ReadAllBytes(outTemp);

                System.IO.File.Delete(tempFile);
                System.IO.File.Delete(outTemp);

                return File(bytes, "application/octet-stream", "project.ustx");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("add")]
        public IActionResult AddPart(
            [FromQuery] int trackIndex, 
            [FromQuery] int position = 0, 
            [FromQuery] int duration = 1920,
            [FromQuery] string? name = null,
            [FromQuery] string? comment = null,
            IFormFile file = null)
        {
            return ExecuteEdit(file, project =>
            {
                if (trackIndex < 0 || trackIndex >= project.tracks.Count)
                    throw new System.ArgumentException("Invalid track index");

                var part = new UVoicePart()
                {
                    trackNo = trackIndex,
                    position = position,
                    duration = duration,
                    name = name ?? "New Part",
                    comment = comment ?? ""
                };
                project.parts.Add(part);
            });
        }

        [HttpPost("remove")]
        public IActionResult RemovePart(
            [FromQuery] int partIndex,
            IFormFile file = null)
        {
            return ExecuteEdit(file, project =>
            {
                if (partIndex < 0 || partIndex >= project.parts.Count)
                    throw new System.ArgumentException("Invalid part index");

                project.parts.RemoveAt(partIndex);
            });
        }

        [HttpPost("move")]
        public IActionResult MovePart(
            [FromQuery] int partIndex,
            [FromQuery] int? newTrackIndex,
            [FromQuery] int? newPosition,
            IFormFile file = null)
        {
            return ExecuteEdit(file, project =>
            {
                if (partIndex < 0 || partIndex >= project.parts.Count)
                    throw new System.ArgumentException("Invalid part index");

                var part = project.parts[partIndex];
                
                if (newTrackIndex.HasValue)
                {
                    if (newTrackIndex.Value < 0 || newTrackIndex.Value >= project.tracks.Count)
                        throw new System.ArgumentException("Invalid new track index");
                    part.trackNo = newTrackIndex.Value;
                }

                if (newPosition.HasValue)
                {
                    part.position = newPosition.Value;
                }
            });
        }

        [HttpPost("config")]
        public IActionResult ConfigPart(
            [FromQuery] int partIndex,
            [FromQuery] string? name,
            [FromQuery] string? comment,
            [FromQuery] int? duration,
            IFormFile file = null)
        {
            return ExecuteEdit(file, project =>
            {
                if (partIndex < 0 || partIndex >= project.parts.Count)
                    throw new System.ArgumentException("Invalid part index");

                var part = project.parts[partIndex];

                if (name != null) part.name = name;
                if (comment != null) part.comment = comment;
                
                if (duration.HasValue)
                {
                    if (part is UVoicePart voicePart)
                    {
                        voicePart.duration = duration.Value;
                    }
                }
            });
        }
    }
}
