using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Format;
using OpenUtau.Core.Ustx;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/project/management")]
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

        [HttpPost("saveAs")]
        public IActionResult SaveAsProject(IFormFile file, [FromQuery] string targetPath)
        {
            if (file == null || file.Length == 0) return BadRequest("No file uploaded");
            if (string.IsNullOrEmpty(targetPath)) return BadRequest("targetPath required");
            try
            {
                
                
                var tempFile = Path.GetTempFileName();
                using (var stream = new FileStream(tempFile, FileMode.Create)) { file.CopyTo(stream); }
                var p = Ustx.Load(tempFile);
                
                Ustx.Save(targetPath, p);
                System.IO.File.Delete(tempFile);
                
                return Ok(new { message = "Project saved", path = targetPath });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("importAudio")]
        public IActionResult ImportAudio(IFormFile projectFile, IFormFile audioFile, [FromQuery] int trackIndex, [FromQuery] int position = 0)
        {
            if (projectFile == null || audioFile == null) return BadRequest("Need both projectFile and audioFile");
            try
            {
                var tempFile = Path.GetTempFileName();
                using (var stream = new FileStream(tempFile, FileMode.Create)) { projectFile.CopyTo(stream); }
                var project = Ustx.Load(tempFile);
                System.IO.File.Delete(tempFile);

                var audioPath = Path.Combine(Path.GetTempPath(), audioFile.FileName);
                using (var stream = new FileStream(audioPath, FileMode.Create)) { audioFile.CopyTo(stream); }

                var wavePart = new UWavePart()
                {
                    name = audioFile.FileName,
                    FilePath = audioPath,
                    position = position,
                    trackNo = trackIndex
                };
                
                try { wavePart.Load(project); } catch { } // Read duration

                if (trackIndex >= project.tracks.Count) {
                    project.tracks.Add(new UTrack(project) { TrackName = "Audio Track" });
                }
                project.parts.Add(wavePart);

                var outTemp = Path.GetTempFileName() + ".ustx";
                Ustx.Save(outTemp, project);

                var streamRet = new FileStream(outTemp, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);
                return File(streamRet, "application/json", "edited.ustx");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("exportMidi")]
        public IActionResult ExportMidi(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("No file uploaded");
            try
            {
                var tempFile = Path.GetTempFileName();
                using (var stream = new FileStream(tempFile, FileMode.Create)) { file.CopyTo(stream); }
                var project = Ustx.Load(tempFile);
                System.IO.File.Delete(tempFile);

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

        [HttpPost("timeStretch")]
        public IActionResult TimeStretch(IFormFile file, [FromQuery] double scale)
        {
            if (scale <= 0) return BadRequest("Scale must be positive");
            return ExecuteEdit(file, project =>
            {
                foreach (var tempo in project.tempos)
                {
                    tempo.bpm /= scale;
                }
                foreach (var part in project.parts)
                {
                    part.position = (int)Math.Round(part.position * scale);
                    if (part is UVoicePart vp)
                    {
                        foreach (var note in vp.notes)
                        {
                            note.position = (int)Math.Round(note.position * scale);
                            note.duration = (int)Math.Round(note.duration * scale);
                        }
                    }
                }
                project.ValidateFull();
            });
        }

        [HttpPost("transpose")]
        public IActionResult Transpose(IFormFile file, [FromQuery] int shiftSemitones, [FromQuery] int? trackIndex = null)
        {
            return ExecuteEdit(file, project =>
            {
                foreach (var part in project.parts)
                {
                    if (part is UVoicePart vp && (!trackIndex.HasValue || vp.trackNo == trackIndex.Value))
                    {
                        foreach (var note in vp.notes)
                        {
                            int newTone = note.tone + shiftSemitones;
                            if (newTone >= 0 && newTone <= 127)
                            {
                                note.tone = newTone;
                            }
                        }
                    }
                }
                project.ValidateFull();
            });
        }

        [HttpPost("session/open")]
        public IActionResult SessionOpen([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("No file uploaded");
            try
            {
                var tempFile = Path.GetTempFileName() + ".ustx";
                using (var stream = new FileStream(tempFile, FileMode.Create)) { file.CopyTo(stream); }
                
                Console.WriteLine($"Parts count from load: {Ustx.Load(tempFile).parts.Count}"); Formats.LoadProject(new string[] { tempFile });
                System.IO.File.Delete(tempFile);
                return Ok(new { message = "Project loaded into memory session." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("session/download")]
        public IActionResult SessionDownload()
        {
            if (DocManager.Inst.Project == null) return BadRequest("No project in session");
            try
            {
                var outTemp = Path.GetTempFileName() + ".ustx";
                Ustx.Save(outTemp, DocManager.Inst.Project);
                var streamRet = new FileStream(outTemp, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);
                return File(streamRet, "application/json", "session_exported.ustx");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("session/executeCommand")]
        public IActionResult SessionExecute([FromBody] string commandType)
        {
            // Provided as an example of making a history-tracked stateful change
            if (DocManager.Inst.Project == null) return BadRequest("No project in session");
            
            // Note: True command integration would map 'commandType' to specific UCommands (e.g. AddNoteCommand)
            // Here we just trigger a mock command or rely on the frontend to know the schema.
            // For now, returning success.
            return Ok(new { message = "Ready for command architecture (use native UCommands for history scaling)." });
        }

    }
}
