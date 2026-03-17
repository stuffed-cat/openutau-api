using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Format;
using OpenUtau.Core.Ustx;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
        [HttpGet("open")]
        public IActionResult OpenProject([FromQuery] string path)
        {
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
                return BadRequest("Invalid path or file does not exist.");
            
            try
            {
                var project = Ustx.Load(path);
                if (project == null) return BadRequest("Failed to load project.");
                
                // Return the loaded project JSON representation or just success
                return Ok(new { message = "Project loaded successfully", name = project.name, tracksCount = project.tracks.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("saveAs")]
        public IActionResult SaveAsProject(IFormFile file, [FromQuery] string path)
        {
            if (file == null || file.Length == 0) return BadRequest("No file uploaded");
            if (string.IsNullOrEmpty(path)) return BadRequest("Save path is required.");

            try
            {
                using (var stream = new FileStream(path, FileMode.Create))
                {
                    file.CopyTo(stream);
                }

                return Ok(new { message = $"Project saved to {path}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

    [Route("api/[controller]")]
    public class ProjectManagementController : ControllerBase
    {
        private IActionResult ExecuteEdit(IFormFile file, Action<UProject> modifier)
        {
            if (file == null || file.Length == 0) return BadRequest("No file uploaded");

            try
            {
                var tempFile = Path.GetTempFileName();
                using (var stream = new FileStream(tempFile, FileMode.Create)) { file.CopyTo(stream); }

                var project = Ustx.Load(tempFile);
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
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("export/midi")]
        public IActionResult ExportMidi(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("No file uploaded");
            
            try
            {
                var tempFile = Path.GetTempFileName();
                using (var stream = new FileStream(tempFile, FileMode.Create)) { file.CopyTo(stream); }

                var project = Ustx.Load(tempFile);
                System.IO.File.Delete(tempFile);

                if (project == null) return BadRequest("Failed to load project.");

                var midiPath = Path.GetTempFileName() + ".mid";
                MidiWriter.Save(midiPath, project);

                var streamRet = new FileStream(midiPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);
                return File(streamRet, "audio/midi", "project.mid");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("export/audio")]
        public async Task<IActionResult> ExportAudio(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("No file uploaded");

            var tempFilePath = Path.GetTempFileName();
            using (var stream = new FileStream(tempFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            try
            {
                var project = Ustx.Load(tempFilePath);
                var outputPath = Path.GetTempFileName() + ".wav";
                
                await PlaybackManager.Inst.RenderMixdown(project, outputPath);
                
                if (!System.IO.File.Exists(outputPath)) return StatusCode(500, "Render failed.");
                
                var streamRet = new FileStream(outputPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);
                System.IO.File.Delete(tempFilePath);
                return File(streamRet, "audio/wav", "project.wav");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("importAudio")]
        public IActionResult ImportAudio(IFormFile projectFile, IFormFile audioFile, [FromQuery] int trackIndex)
        {
            if (projectFile == null || audioFile == null) return BadRequest("Both project and audio files are required.");

            try
            {
                var tempProject = Path.GetTempFileName();
                using (var stream = new FileStream(tempProject, FileMode.Create)) { projectFile.CopyTo(stream); }

                var tempAudio = Path.Combine(Path.GetTempPath(), audioFile.FileName);
                using (var stream = new FileStream(tempAudio, FileMode.Create)) { audioFile.CopyTo(stream); }

                var project = Ustx.Load(tempProject);
                if (project == null) return BadRequest("Failed to load project.");

                if (trackIndex < 0 || trackIndex >= project.tracks.Count) 
                {
                    // Create new track if trackIndex is invalid
                    var newTrack = new UTrack(project) { TrackName = "Audio Track" };
                    project.tracks.Add(newTrack);
                    trackIndex = project.tracks.Count - 1;
                }

                var wavePart = new UWavePart()
                {
                    name = audioFile.FileName,
                    FilePath = tempAudio,
                    position = 0,
                    trackNo = trackIndex
                };
                
                project.parts.Add(wavePart);

                var outTemp = Path.GetTempFileName() + ".ustx";
                Ustx.Save(outTemp, project);
                System.IO.File.Delete(tempProject);

                var streamRet = new FileStream(outTemp, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);
                return File(streamRet, "application/json", "edited.ustx");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("tempo")]
        public IActionResult SetTempo(IFormFile file, [FromQuery] double bpm)
        {
            return ExecuteEdit(file, project =>
            {
                if (bpm <= 0) throw new Exception("BPM must be positive.");
                project.tempos = new List<UTempo> { new UTempo(0, bpm) };
            });
        }

        [HttpPost("transpose")]
        public IActionResult Transpose(IFormFile file, [FromQuery] int steps, [FromQuery] int? partNo)
        {
            return ExecuteEdit(file, project =>
            {
                if (partNo.HasValue)
                {
                    if (partNo.Value >= 0 && partNo.Value < project.parts.Count && project.parts[partNo.Value] is UVoicePart part)
                    {
                        foreach (var note in part.notes)
                        {
                            note.tone += steps;
                            if (note.tone < 0) note.tone = 0;
                            if (note.tone > 127) note.tone = 127;
                        }
                    }
                }
                else
                {
                    foreach (var partBase in project.parts)
                    {
                        if (partBase is UVoicePart part)
                        {
                            foreach (var note in part.notes)
                            {
                                note.tone += steps;
                                if (note.tone < 0) note.tone = 0;
                                if (note.tone > 127) note.tone = 127;
                            }
                        }
                    }
                }
            });
        }
    }
}
