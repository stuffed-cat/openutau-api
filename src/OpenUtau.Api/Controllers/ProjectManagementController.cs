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
using System.Reflection;
using System.Text.Json;

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
                DocManager.Inst.StartUndoGroup("api", true);
                
                // Clone old time axis for remapping
                var oldTimeAxis = project.timeAxis.Clone();
                
                // Keep the first tempo (index 0) and change its BPM, remove others
                DocManager.Inst.ExecuteCmd(new BpmCommand(project, bpm));
                project.ValidateFull();
                
                // Optionally clear other tempos if they exist
                foreach (var tempo in project.tempos.Skip(1))
                {
                    DocManager.Inst.ExecuteCmd(new DelTempoChangeCommand(project, tempo.position));
                }

                project.ValidateFull();

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

                DocManager.Inst.StartUndoGroup("api", true);
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

        [HttpPost("session/saveAs")]
        public IActionResult SessionSaveAs([FromQuery] string targetPath)
        {
            if (DocManager.Inst.Project == null) return BadRequest("No project in session");
            if (string.IsNullOrWhiteSpace(targetPath)) return BadRequest("targetPath required");

            try
            {
                targetPath = Path.GetFullPath(targetPath);

                var directory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (!Path.GetExtension(targetPath).Equals(".ustx", StringComparison.OrdinalIgnoreCase))
                {
                    targetPath = Path.ChangeExtension(targetPath, ".ustx");
                }

                Ustx.Save(targetPath, DocManager.Inst.Project);

                return Ok(new
                {
                    message = "Session project saved successfully.",
                    path = targetPath
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }


        public class CommandRequest
        {
            public string CommandType { get; set; }
            public System.Text.Json.JsonElement Args { get; set; }
        }

        [HttpPost("session/executeCommand")]
        public IActionResult SessionExecute([FromBody] CommandRequest request)
        {
            if (DocManager.Inst.Project == null) return BadRequest("No project in session");
            if (request == null || string.IsNullOrWhiteSpace(request.CommandType)) {
                return BadRequest("Missing command type.");
            }
            try
            {
                var cmdType = typeof(UCommand).Assembly.GetTypes()
                    .FirstOrDefault(t => t.IsSubclassOf(typeof(UCommand)) && t.Name == request.CommandType);

                if (cmdType == null)
                {
                    return BadRequest($"Command '{request.CommandType}' not found.");
                }

                var constructors = cmdType.GetConstructors();
                if (constructors.Length == 0) return BadRequest("No valid constructors found.");
                var constructor = constructors
                    .Select(ctor => new { Ctor = ctor, Args = TryResolveConstructorArgs(DocManager.Inst.Project, request.Args, ctor) })
                    .FirstOrDefault(x => x.Args.success);

                if (constructor == null) {
                    return BadRequest("Unable to resolve command arguments.");
                }

                var cmdObj = constructor.Ctor.Invoke(constructor.Args.args);
                
                if (cmdObj is UCommand command)
                {
                    DocManager.Inst.StartUndoGroup($"API Session Execute {request.CommandType}", true);
                    DocManager.Inst.ExecuteCmd(command);
                    DocManager.Inst.EndUndoGroup();
                    return Ok(new { message = $"Command {request.CommandType} executed successfully." });
                }
                
                return BadRequest("Not a valid UCommand.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
            }
        }

        private static (bool success, object[] args, string error) TryResolveConstructorArgs(UProject project, JsonElement requestArgs, ConstructorInfo constructor) {
            var parameters = constructor.GetParameters();
            var argsList = new object[parameters.Length];

            for (var i = 0; i < parameters.Length; i++) {
                if (!TryResolveArgument(project, requestArgs, parameters[i], out var value, out var error)) {
                    return (false, Array.Empty<object>(), error);
                }
                argsList[i] = value;
            }

            return (true, argsList, string.Empty);
        }

        private static bool TryResolveArgument(UProject project, JsonElement requestArgs, ParameterInfo param, out object value, out string error) {
            value = null;
            error = string.Empty;

            if (param.ParameterType == typeof(UProject)) {
                value = project;
                return true;
            }

            if (param.ParameterType == typeof(UTrack)) {
                if (TryResolveTrack(project, requestArgs, param.Name, out var track, out error)) {
                    value = track;
                    return true;
                }
                return false;
            }

            if (param.ParameterType == typeof(UVoicePart) || param.ParameterType == typeof(UPart)) {
                if (TryResolvePart(project, requestArgs, param.Name, out var part, out error)) {
                    if (param.ParameterType.IsInstanceOfType(part)) {
                        value = part;
                        return true;
                    }
                    error = $"Part resolved for '{param.Name}' is not of type {param.ParameterType.Name}.";
                }
                return false;
            }

            if (param.ParameterType == typeof(UNote)) {
                if (TryResolveNote(project, requestArgs, param.Name, out var note, out error)) {
                    value = note;
                    return true;
                }
                return false;
            }

            if (param.ParameterType == typeof(UNote[])) {
                if (TryResolveNotes(project, requestArgs, out var notes, out error)) {
                    value = notes;
                    return true;
                }
                return false;
            }

            if (param.ParameterType == typeof(List<UNote>)) {
                if (TryResolveNotes(project, requestArgs, out var notes, out error)) {
                    value = notes.ToList();
                    return true;
                }
                return false;
            }

            if (TryGetRequestValue(requestArgs, param.Name, out var prop)) {
                try {
                    value = JsonSerializer.Deserialize(prop.GetRawText(), param.ParameterType);
                    return true;
                } catch (Exception ex) {
                    error = $"Missing or invalid argument '{param.Name}': {ex.Message}";
                    return false;
                }
            }

            if (param.HasDefaultValue) {
                value = param.DefaultValue;
                return true;
            }

            error = $"Missing required argument: {param.Name}";
            return false;
        }

        private static bool TryResolveTrack(UProject project, JsonElement requestArgs, string paramName, out UTrack track, out string error) {
            error = string.Empty;
            track = null;

            if (TryGetInt(requestArgs, "trackIndex", out var trackIndex) || TryGetInt(requestArgs, "trackNo", out trackIndex)) {
                if (trackIndex < 0 || trackIndex >= project.tracks.Count) {
                    error = $"Invalid track index: {trackIndex}";
                    return false;
                }
                track = project.tracks[trackIndex];
                return true;
            }

            if (TryGetRequestValue(requestArgs, paramName, out var nested) && nested.ValueKind == JsonValueKind.Object) {
                if (TryGetInt(nested, "trackIndex", out trackIndex) || TryGetInt(nested, "trackNo", out trackIndex)) {
                    if (trackIndex < 0 || trackIndex >= project.tracks.Count) {
                        error = $"Invalid track index: {trackIndex}";
                        return false;
                    }
                    track = project.tracks[trackIndex];
                    return true;
                }
            }

            error = $"Missing required track reference: {paramName}";
            return false;
        }

        private static bool TryResolvePart(UProject project, JsonElement requestArgs, string paramName, out UVoicePart part, out string error) {
            error = string.Empty;
            part = null;

            if (TryGetInt(requestArgs, "partIndex", out var partIndex) || TryGetInt(requestArgs, "partNo", out partIndex)) {
                if (partIndex < 0 || partIndex >= project.parts.Count) {
                    error = $"Invalid part index: {partIndex}";
                    return false;
                }

                if (project.parts[partIndex] is not UVoicePart voicePart) {
                    error = $"Part {partIndex} is not a voice part.";
                    return false;
                }

                part = voicePart;
                return true;
            }

            if (TryGetRequestValue(requestArgs, paramName, out var nested) && nested.ValueKind == JsonValueKind.Object) {
                if (TryGetInt(nested, "partIndex", out partIndex) || TryGetInt(nested, "partNo", out partIndex)) {
                    if (partIndex < 0 || partIndex >= project.parts.Count) {
                        error = $"Invalid part index: {partIndex}";
                        return false;
                    }

                    if (project.parts[partIndex] is not UVoicePart voicePart) {
                        error = $"Part {partIndex} is not a voice part.";
                        return false;
                    }

                    part = voicePart;
                    return true;
                }
            }

            error = $"Missing required part reference: {paramName}";
            return false;
        }

        private static bool TryResolveNote(UProject project, JsonElement requestArgs, string paramName, out UNote note, out string error) {
            error = string.Empty;
            note = null;

            if (TryGetRequestValue(requestArgs, paramName, out var nested) && nested.ValueKind == JsonValueKind.Object) {
                if (!TryHasReferenceFields(nested) && TryDeserializeJson(nested, out UNote createdNote, out error)) {
                    note = createdNote;
                    return true;
                }
            }

            if (TryGetRequestValue(requestArgs, "note", out var noteObject) && noteObject.ValueKind == JsonValueKind.Object && paramName != "note") {
                if (!TryHasReferenceFields(noteObject) && TryDeserializeJson(noteObject, out UNote createdNote, out error)) {
                    note = createdNote;
                    return true;
                }
            }

            if (!TryResolvePart(project, requestArgs, paramName, out var part, out error)) {
                return false;
            }

            if (!TryGetInt(requestArgs, "noteIndex", out var noteIndex)) {
                if (TryGetRequestValue(requestArgs, paramName, out var nestedNote) && nestedNote.ValueKind == JsonValueKind.Object) {
                    if (!TryGetInt(nestedNote, "noteIndex", out noteIndex)) {
                        error = $"Missing required note index for '{paramName}'";
                        return false;
                    }
                } else {
                    error = $"Missing required note index for '{paramName}'";
                    return false;
                }
            }

            if (noteIndex < 0 || noteIndex >= part.notes.Count) {
                error = $"Invalid note index: {noteIndex}";
                return false;
            }

            note = part.notes.ElementAt(noteIndex);
            return true;
        }

        private static bool TryResolveNotes(UProject project, JsonElement requestArgs, out UNote[] notes, out string error) {
            error = string.Empty;
            notes = Array.Empty<UNote>();

            if (!TryResolvePart(project, requestArgs, "part", out var part, out error)) {
                return false;
            }

            if (TryGetRequestValue(requestArgs, "notes", out var noteObjects) && noteObjects.ValueKind == JsonValueKind.Array) {
                var createdNotes = new List<UNote>();
                foreach (var item in noteObjects.EnumerateArray()) {
                    if (item.ValueKind == JsonValueKind.Object && !TryHasReferenceFields(item)) {
                        if (!TryDeserializeJson(item, out UNote createdNote, out error)) {
                            return false;
                        }
                        createdNotes.Add(createdNote);
                    } else if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var noteIndex)) {
                        if (noteIndex < 0 || noteIndex >= part.notes.Count) {
                            error = $"Invalid note index: {noteIndex}";
                            return false;
                        }
                        createdNotes.Add(part.notes.ElementAt(noteIndex));
                    } else {
                        error = "Invalid notes array entry.";
                        return false;
                    }
                }

                notes = createdNotes.ToArray();
                return true;
            }

            if (!TryGetIntArray(requestArgs, "noteIndices", out var noteIndices)) {
                error = "Missing required noteIndices array.";
                return false;
            }

            var resolved = new List<UNote>();
            foreach (var noteIndex in noteIndices) {
                if (noteIndex < 0 || noteIndex >= part.notes.Count) {
                    error = $"Invalid note index: {noteIndex}";
                    return false;
                }
                resolved.Add(part.notes.ElementAt(noteIndex));
            }

            notes = resolved.ToArray();
            return true;
        }

        private static bool TryDeserializeJson<T>(JsonElement source, out T value, out string error) {
            error = string.Empty;
            value = default;

            try {
                value = JsonSerializer.Deserialize<T>(source.GetRawText(), new JsonSerializerOptions {
                    IncludeFields = true,
                    PropertyNameCaseInsensitive = true,
                });
                if (value is UNote note) {
                    note.pitch ??= new UPitch();
                    if (note.pitch.data.Count == 0) {
                        int start = NotePresets.Default.DefaultPortamento.PortamentoStart;
                        int length = NotePresets.Default.DefaultPortamento.PortamentoLength;
                        var shape = NotePresets.Default.DefaultPitchShape;
                        note.pitch.AddPoint(new PitchPoint(start, 0, shape));
                        note.pitch.AddPoint(new PitchPoint(start + length, 0, shape));
                    }
                    note.vibrato ??= new UVibrato();
                }
                return true;
            } catch (Exception ex) {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryHasReferenceFields(JsonElement source) {
            return source.ValueKind == JsonValueKind.Object && (
                source.TryGetProperty("noteIndex", out _) ||
                source.TryGetProperty("partIndex", out _) ||
                source.TryGetProperty("partNo", out _) ||
                source.TryGetProperty("trackIndex", out _) ||
                source.TryGetProperty("trackNo", out _)
            );
        }

        private static bool TryGetRequestValue(JsonElement source, string name, out JsonElement value) {
            if (source.ValueKind == JsonValueKind.Object && source.TryGetProperty(name, out value)) {
                return true;
            }

            value = default;
            return false;
        }

        private static bool TryGetInt(JsonElement source, string name, out int value) {
            value = 0;
            if (!TryGetRequestValue(source, name, out var prop)) {
                return false;
            }

            switch (prop.ValueKind) {
                case JsonValueKind.Number:
                    return prop.TryGetInt32(out value);
                case JsonValueKind.String:
                    return int.TryParse(prop.GetString(), out value);
                default:
                    return false;
            }
        }

        private static bool TryGetIntArray(JsonElement source, string name, out int[] values) {
            values = Array.Empty<int>();
            if (!TryGetRequestValue(source, name, out var prop) || prop.ValueKind != JsonValueKind.Array) {
                return false;
            }

            var list = new List<int>();
            foreach (var item in prop.EnumerateArray()) {
                if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var value)) {
                    list.Add(value);
                } else if (item.ValueKind == JsonValueKind.String && int.TryParse(item.GetString(), out value)) {
                    list.Add(value);
                } else {
                    return false;
                }
            }

            values = list.ToArray();
            return true;
        }


    }
}
