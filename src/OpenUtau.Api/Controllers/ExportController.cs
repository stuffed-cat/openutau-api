using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using System.IO.Compression;
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
        public async Task<IActionResult> ExportTrack(IFormFile file, [FromQuery] int trackIndex, [FromQuery] string format = "wav")
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

                var wavFile = baseFile;
                System.IO.File.Delete(tempFile);

                if (!System.IO.File.Exists(wavFile))
                    return StatusCode(500, "Failed to render wav.");

                wavFile = OpenUtau.Api.AudioExporter.ConvertFormat(wavFile, format);
                var finalFileName = $"track_{trackIndex}.wav";
                if (!string.IsNullOrEmpty(format) && format != "wav") {
                    var ext = format.ToLowerInvariant().TrimStart('.');
                    finalFileName = System.IO.Path.ChangeExtension(finalFileName, "." + ext);
                }
                var streamRetExp = new FileStream(wavFile, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);
                return File(streamRetExp, OpenUtau.Api.AudioExporter.GetContentType(format), finalFileName);
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        
        [HttpPost("tracks")]
        public async Task<IActionResult> ExportTracks(IFormFile? file = null, [FromQuery] string format = "wav")
        {
            try
            {
                UProject project;
                string tempFile = null;

                if (file != null && file.Length > 0)
                {
                    tempFile = Path.GetTempFileName();
                    using (var stream = new FileStream(tempFile, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                    Formats.LoadProject(new string[] { tempFile });
                    project = DocManager.Inst.Project;
                }
                else
                {
                    project = DocManager.Inst.Project;
                }

                if (project == null)
                {
                    if (tempFile != null) System.IO.File.Delete(tempFile);
                    return BadRequest("No project loaded or uploaded.");
                }

                var tempDir = Path.Combine(Path.GetTempPath(), "tracks_export_" + pathHelper());
                Directory.CreateDirectory(tempDir);
                
                var baseFile = Path.Combine(tempDir, "export.wav");
                await PlaybackManager.Inst.RenderToFiles(project, baseFile);

                if (tempFile != null) System.IO.File.Delete(tempFile);

                var zipFilePath = Path.Combine(Path.GetTempPath(), "tracks_export_" + pathHelper() + ".zip");
                
                using (var zipStream = new FileStream(zipFilePath, FileMode.Create))
                using (var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Create, true))
                {
                    foreach (var exportedFile in Directory.GetFiles(tempDir))
                    {
                        var finalFile = exportedFile;
                        if (!string.IsNullOrEmpty(format) && format != "wav")
                        {
                            finalFile = OpenUtau.Api.AudioExporter.ConvertFormat(exportedFile, format);
                        }
                        
                        var entryName = Path.GetFileName(finalFile);
                        archive.CreateEntryFromFile(finalFile, entryName);
                        
                        // Clean up temp formats if we generated them
                        if (finalFile != exportedFile) {
                            System.IO.File.Delete(finalFile);
                        }
                    }
                }
                
                Directory.Delete(tempDir, true);

                var streamRetExp = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);
                return File(streamRetExp, "application/zip", "tracks.zip");
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private string pathHelper() => System.Guid.NewGuid().ToString("N");
    }
}
