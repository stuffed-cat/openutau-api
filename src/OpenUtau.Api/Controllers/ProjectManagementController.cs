using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Format;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Util;
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
        [HttpPost("/api/project/remaptimeaxis")]
        public IActionResult RemapTimeAxis([FromQuery] double bpm)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project in session");
            if (bpm <= 0) return BadRequest("BPM must be greater than 0");

            try
            {
                DocManager.Inst.StartUndoGroup();
                
                // Clone old time axis for remapping
                var oldTimeAxis = project.timeAxis.Clone();
                
                // Keep the first tempo (index 0) and change its BPM, remove others
                DocManager.Inst.ExecuteCmd(new BpmCommand(project, bpm));
                
                // Optionally clear other tempos if they exist
                foreach (var tempo in project.tempos.Skip(1))
                {
                    DocManager.Inst.ExecuteCmd(new DelTempoChangeCommand(project, tempo.position));
                }

                // Execute remap using new time axis logic similar to MainWindowViewModel
                var newTimeAxis = project.timeAxis.Clone();
                
                int RemapTickPos(int tickPos, TimeAxis oldAxis, TimeAxis newAxis)
                {
                    double msPos = oldAxis.TickPosToMsPos(tickPos);
                    return newAxis.MsPosToTickPos(msPos);
                }

                foreach (var part in project.parts)
                {
                    var partOldStartTick = part.position;
                    var partNewStartTick = RemapTickPos(part.position, oldTimeAxis, newTimeAxis);
                    if (partNewStartTick != partOldStartTick)
                    {
                        DocManager.Inst.ExecuteCmd(new MovePartCommand(project, part, partNewStartTick, part.trackNo));
                    }

                    if (part is UVoicePart voicePart)
                    {
                        var partOldDuration = voicePart.Duration;
                        var partNewDuration = RemapTickPos(partOldStartTick + voicePart.duration, oldTimeAxis, newTimeAxis) - partNewStartTick;
                        if (partNewDuration != partOldDuration)
                        {
                            DocManager.Inst.ExecuteCmd(new ResizePartCommand(project, voicePart, partNewDuration - partOldDuration, false));
                        }

                        var noteCommands = new List<UCommand>();
                        foreach (var note in voicePart.notes)
                        {
                            var noteOldStartTick = note.position + partOldStartTick;
                            var noteOldEndTick = note.End + partOldStartTick;
                            var noteOldDuration = note.duration;
                            
                            var noteNewStartTick = RemapTickPos(noteOldStartTick, oldTimeAxis, newTimeAxis);
                            var noteNewEndTick = RemapTickPos(noteOldEndTick, oldTimeAxis, newTimeAxis);
                            var deltaPosTickInPart = (noteNewStartTick - partNewStartTick) - (noteOldStartTick - partOldStartTick);
                            
                            if (deltaPosTickInPart != 0)
                            {
                                noteCommands.Add(new MoveNoteCommand(voicePart, note, deltaPosTickInPart, 0));
                            }
                            
                            var noteNewDuration = noteNewEndTick - noteNewStartTick;
                            var deltaDur = noteNewDuration - noteOldDuration;
                            
                            if (deltaDur != 0)
                            {
                                noteCommands.Add(new ResizeNoteCommand(voicePart, note, deltaDur));
                            }
                        }
                        
                        foreach (var command in noteCommands)
                        {
                            DocManager.Inst.ExecuteCmd(command);
                        }
                    }
                }

                DocManager.Inst.EndUndoGroup();
                return Ok(new { message = "Time axis remapped successfully" });
            }
            catch (Exception ex)
            {
                if (DocManager.Inst.HasOpenUndoGroup) DocManager.Inst.EndUndoGroup();
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("/api/project/new")]
        public IActionResult NewProject()
        {
            try
            {
                var project = new UProject();
                Ustx.AddDefaultExpressions(project);
                DocManager.Inst.ExecuteCmd(new LoadProjectNotification(project));
                return Ok(new { message = "New project created in session" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("/api/project/savetemplate")]
        public IActionResult SaveTemplate([FromQuery] string name)
        {
            if (string.IsNullOrEmpty(name)) return BadRequest("Template name required");
            if (DocManager.Inst.Project == null) return BadRequest("No project in session");
            try
            {
                var tempPath = PathManager.Inst.TemplatesPath;
                if (!Directory.Exists(tempPath)) Directory.CreateDirectory(tempPath);
                
                var file = Path.Combine(tempPath, name + ".ustx");
                Ustx.Save(file, DocManager.Inst.Project);
                
                return Ok(new { message = "Template saved", path = file });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("/api/project/loadtemplate")]
        public IActionResult LoadTemplate([FromQuery] string name)
        {
            if (string.IsNullOrEmpty(name)) return BadRequest(new { error = "Template name required" });
            
            try
            {
                var tempPath = PathManager.Inst.TemplatesPath;
                var file = Path.Combine(tempPath, name + ".ustx");
                if (!System.IO.File.Exists(file)) return NotFound(new { error = $"Template {name} not found" });

                var project = Ustx.Load(file);
                if (project == null) return StatusCode(500, new { error = "Failed to load project from template" });
                
                // Prevent accidental overwrite of the template
                project.FilePath = null;
                project.Saved = false;

                DocManager.Inst.ExecuteCmd(new LoadProjectNotification(project));
                
                return Ok(new { message = $"Template {name} loaded into session" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("/api/project/locations/projectdir")]
        public IActionResult GetProjectDir()
        {
            try
            {
                var p = DocManager.Inst.Project;
                string dir = p != null && !string.IsNullOrEmpty(p.FilePath) 
                    ? Path.GetDirectoryName(p.FilePath) 
                    : PathManager.Inst.DataPath;
                return Ok(new { projectDir = dir ?? "" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("/api/project/locations/exportdir")]
        public IActionResult GetExportDir()
        {
            try
            {
                var p = DocManager.Inst.Project;
                string dir = p != null && !string.IsNullOrEmpty(p.FilePath) 
                    ? Path.GetDirectoryName(p.FilePath) 
                    : PathManager.Inst.DataPath;
                return Ok(new { exportDir = Path.Combine(dir ?? "", "Export") }); // Often export dir is a subdirectory or same dir
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("/api/project/importtracks")]
        public IActionResult ImportTracks(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("No file uploaded");
            if (DocManager.Inst.Project == null) return BadRequest("No project in session");
            try
            {
                var ext = Path.GetExtension(file.FileName);
                if (string.IsNullOrEmpty(ext)) ext = ".ustx";
                var tempFile = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString() + ext);
                using (var stream = new FileStream(tempFile, FileMode.Create)) { file.CopyTo(stream); }
                
                var projects = Formats.ReadProjects(new string[] { tempFile });
                System.IO.File.Delete(tempFile);

                if (projects == null || projects.Length == 0) return BadRequest("Invalid project file");
                var incomingProject = projects[0];

                DocManager.Inst.StartUndoGroup();
                foreach (var track in incomingProject.tracks)
                {
                    DocManager.Inst.ExecuteCmd(new AddTrackCommand(DocManager.Inst.Project, new UTrack(DocManager.Inst.Project)
                    {
                        TrackName = track.TrackName,
                        Singer = track.Singer,
                        Phonemizer = track.Phonemizer,
                        RendererSettings = track.RendererSettings
                    }));
                    int newTrackNo = DocManager.Inst.Project.tracks.Count - 1;

                    foreach (var part in incomingProject.parts.Where(p => p.trackNo == track.TrackNo))
                    {
                        if (part is UVoicePart vp)
                        {
                            var newPart = new UVoicePart()
                            {
                                name = vp.name,
                                position = vp.position,
                                trackNo = newTrackNo,
                                Duration = vp.Duration
                            };
                            DocManager.Inst.ExecuteCmd(new AddPartCommand(DocManager.Inst.Project, newPart));
                            foreach (var note in vp.notes)
                            {
                                var n = note.Clone();
                                DocManager.Inst.ExecuteCmd(new AddNoteCommand(newPart, n));
                            }
                        }
                        else if (part is UWavePart wp)
                        {
                            var newPart = new UWavePart()
                            {
                                name = wp.name,
                                position = wp.position,
                                trackNo = newTrackNo,
                                FilePath = wp.FilePath
                            };
                            DocManager.Inst.ExecuteCmd(new AddPartCommand(DocManager.Inst.Project, newPart));
                        }
                    }
                }
                DocManager.Inst.EndUndoGroup();

                return Ok(new { message = "Tracks imported successfully" });
            }
            catch (Exception ex)
            {
                if (DocManager.Inst.HasOpenUndoGroup) DocManager.Inst.EndUndoGroup(); // Safety
                return StatusCode(500, new { error = ex.Message });
            }
        }

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
                var ext = Path.GetExtension(projectFile.FileName);
                if (string.IsNullOrEmpty(ext)) ext = ".ustx";
                var tempFile = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString() + ext);
                using (var stream = new FileStream(tempFile, FileMode.Create)) { projectFile.CopyTo(stream); }
                
                var projects = Formats.ReadProjects(new string[] { tempFile });
                System.IO.File.Delete(tempFile);
                if (projects == null || projects.Length == 0) return BadRequest("Invalid project file");
                var project = projects[0];

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
                var ext = Path.GetExtension(file.FileName);
                if (string.IsNullOrEmpty(ext)) ext = ".ustx";
                var tempFile = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString() + ext);
                using (var stream = new FileStream(tempFile, FileMode.Create)) { file.CopyTo(stream); }
                
                Formats.LoadProject(new string[] { tempFile });
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
