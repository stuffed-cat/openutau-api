using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Format;
using System.IO;
using System;
using System.Linq;
using OpenUtau.Core;
using OpenUtau.Core.Analysis;
using System.Collections.Generic;
using System.Threading.Tasks;



namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/project/parts")]
    public class PartsController : ControllerBase
    {
        [HttpGet("/api/project/part/{partNo}")]
        public IActionResult GetPartProperties(int partNo) {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");

            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest("Invalid part index");
            var part = project.parts[partNo];
            if (part == null) return NotFound("Part not found");

            if (part is UVoicePart voicePart) {
                return Ok(new {
                    partNo = partNo,
                    trackNo = voicePart.trackNo,
                    position = voicePart.position,
                    duration = voicePart.Duration,
                    name = voicePart.name,
                    notes = voicePart.notes.Select((n, index) => new {
                        noteIndex = index,
                        position = n.position,
                        duration = n.duration,
                        lyric = n.lyric,
                        tone = n.tone
                    }).ToList(),
                    curves = voicePart.curves.Select(c => new {
                        abbr = c.abbr,
                        name = c.descriptor?.name,
                        xs = c.xs,
                        ys = c.ys
                    }).ToList()
                });
            } else if (part is UWavePart wavePart) {
                return Ok(new {
                    partNo = partNo,
                    trackNo = wavePart.trackNo,
                    position = wavePart.position,
                    duration = wavePart.Duration,
                    name = wavePart.name,
                    filePath = wavePart.FilePath,
                    fileDurationMs = wavePart.fileDurationMs
                });
            }

            return Ok(new {
                partNo = partNo,
                trackNo = part.trackNo,
                position = part.position,
                name = part.name
            });
        }

        public class CurveUpdateData {
            public int[] xs { get; set; }
            public int[] ys { get; set; }
        }

        [HttpPost("{partNo}/curves/{abbr}")]
        public IActionResult UpdateCurve(int partNo, string abbr, [FromBody] CurveUpdateData request) {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");

            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest("Invalid part index");
            var partBase = project.parts[partNo];
            if (partBase == null) return NotFound("Part not found");

            if (!(partBase is UVoicePart part)) return BadRequest("Not a voice part");

            if (request == null || request.xs == null || request.ys == null || request.xs.Length != request.ys.Length) {
                return BadRequest("Invalid curve data");
            }

            var curve = part.curves.FirstOrDefault(c => c.abbr == abbr);
            int[] oldXs = curve?.xs.ToArray() ?? new int[0];
            int[] oldYs = curve?.ys.ToArray() ?? new int[0];

            if (curve == null) {
                if (!project.expressions.ContainsKey(abbr)) {
                    project.expressions.Add(abbr, new UExpressionDescriptor(abbr, abbr, -1000, 1000, 0) { type = UExpressionType.Curve });
                }
            }

            DocManager.Inst.StartUndoGroup("api");
            DocManager.Inst.ExecuteCmd(new MergedSetCurveCommand(project, part, abbr, oldXs, oldYs, request.xs, request.ys));
            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = $"Curve {abbr} updated with {request.xs.Length} points" });
        }

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
    
        [HttpPost("{partIndex}/rename")]
        public IActionResult RenamePart(int partIndex, [FromQuery] string name)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project is currently loaded.");
            if (partIndex < 0 || partIndex >= project.parts.Count) return NotFound("Part not found.");
            
            var part = project.parts[partIndex];
            DocManager.Inst.StartUndoGroup("command.part.rename");
            DocManager.Inst.ExecuteCmd(new RenamePartCommand(project, part, name));
            DocManager.Inst.EndUndoGroup();
            return Ok(new { message = $"Part renamed to {name}" });
        }

        [HttpPost("{partIndex}/replaceaudio")]
        public async Task<IActionResult> ReplaceAudio(int partIndex, IFormFile audioFile)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project is currently loaded.");
            if (partIndex < 0 || partIndex >= project.parts.Count) return NotFound("Part not found.");
            
            var part = project.parts[partIndex];
            if (!(part is UWavePart)) return BadRequest("Target part is not a wave part.");

            if (audioFile == null || audioFile.Length == 0) return BadRequest("No audio file provided.");

            try
            {
                var audioPath = Path.Combine(Path.GetTempPath(), audioFile.FileName);
                using (var stream = new FileStream(audioPath, FileMode.Create))
                {
                    await audioFile.CopyToAsync(stream);
                }

                UWavePart newPart = new UWavePart() {
                    FilePath = audioPath,
                    trackNo = part.trackNo,
                    position = part.position
                };
                newPart.Load(project);

                DocManager.Inst.StartUndoGroup("command.import.audio");
                DocManager.Inst.ExecuteCmd(new ReplacePartCommand(project, part, newPart));
                DocManager.Inst.EndUndoGroup();

                return Ok(new { message = "Audio replaced successfully.", filePath = audioPath });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("{partIndex}/transcribe")]
        public async Task<IActionResult> TranscribePart(int partIndex)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project is currently loaded.");
            if (partIndex < 0 || partIndex >= project.parts.Count) return NotFound("Part not found.");
            
            var part = project.parts[partIndex];
            if (!(part is UWavePart wavePart)) return BadRequest("Target part is not a wave part.");

            try
            {
                UVoicePart? voicePart = null;
                using (var game = new Game())
                {
                    var options = new GameOptions();
                    var batching = new MidiExtractor<GameOptions>.BatchingStrategy();
                    
                    voicePart = await Task.Run(() => {
                        return game.Transcribe(project, wavePart,
                            options, batching,
                            () => true, 
                            (processedS, totalS) => { }
                        );
                    });
                }
                
                if (voicePart != null)
                {
                    var track = new UTrack(project);
                    track.TrackNo = project.tracks.Count;
                    voicePart.trackNo = track.TrackNo;
                    
                    DocManager.Inst.StartUndoGroup("command.part.transcribe");
                    DocManager.Inst.ExecuteCmd(new AddTrackCommand(project, track));
                    DocManager.Inst.ExecuteCmd(new AddPartCommand(project, voicePart));
                    DocManager.Inst.EndUndoGroup();
                    
                    return Ok(new { message = "Transcription completed successfully.", newTrackIndex = track.TrackNo, voicePartName = voicePart.name });
                }
                return BadRequest("Transcription returned null result.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("{partIndex}/merge")]
        public IActionResult MergeParts(int partIndex, [FromBody] int[] otherPartIndices)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project is currently loaded.");
            
            var partsToMerge = new List<UPart>();
            
            if (partIndex < 0 || partIndex >= project.parts.Count) return NotFound($"Base part at index {partIndex} not found.");
            partsToMerge.Add(project.parts[partIndex]);

            if (otherPartIndices != null)
            {
                foreach (int idx in otherPartIndices)
                {
                    if (idx < 0 || idx >= project.parts.Count) return NotFound($"Part at index {idx} not found.");
                    if (!partsToMerge.Contains(project.parts[idx]))
                        partsToMerge.Add(project.parts[idx]);
                }
            }

            if (partsToMerge.Count <= 1) return BadRequest("Need at least two parts to merge.");

            int trackNo = partsToMerge.First().trackNo;
            if (!partsToMerge.All(p => p.trackNo == trackNo))
                return BadRequest("All parts to be merged must belong to the same track.");

            var voiceParts = new List<UVoicePart>();
            foreach (var p in partsToMerge)
            {
                if (p is UVoicePart vp) voiceParts.Add(vp);
                else return BadRequest("Can only merge voice parts.");
            }

            try
            {
                UVoicePart mergedPart = voiceParts.Aggregate((merging, nextup) => {
                    string newComment = merging.comment + nextup.comment;
                    var (leftPart, rightPart) = (merging.position < nextup.position) ? (merging, nextup) : (nextup, merging);
                    int newPosition = leftPart.position;
                    int newDuration = Math.Max(leftPart.End, rightPart.End) - newPosition;
                    int deltaPos = rightPart.position - leftPart.position;
                    
                    UVoicePart shiftPart = new UVoicePart();
                    foreach (var note in rightPart.notes) {
                        UNote shiftNote = note.Clone();
                        shiftNote.position += deltaPos;
                        shiftPart.notes.Add(shiftNote);
                    }
                    foreach (var curve in rightPart.curves) {
                        UCurve shiftCurve = curve.Clone();
                        for (var i = 0; i < shiftCurve.xs.Count; i++) {
                            shiftCurve.xs[i] += deltaPos;
                        }
                        shiftPart.curves.Add(shiftCurve);
                    }
                    
                    SortedSet<UNote> newNotes = new SortedSet<UNote>(leftPart.notes.Concat(shiftPart.notes));
                    List<UCurve> newCurves = UCurve.MergeCurves(leftPart.curves, shiftPart.curves);
                    
                    return new UVoicePart() {
                        name = partsToMerge[0].name,
                        comment = newComment,
                        trackNo = trackNo,
                        position = newPosition,
                        notes = newNotes,
                        curves = newCurves,
                        Duration = newDuration,
                    };
                });

                ValidateOptions options = new ValidateOptions() {
                    SkipTiming = true,
                    Part = mergedPart,
                    SkipPhoneme = false,
                    SkipPhonemizer = false
                };
                mergedPart.Validate(options, project, project.tracks[trackNo]);

                DocManager.Inst.StartUndoGroup("command.part.edit");
                var partsToRemove = partsToMerge.OrderByDescending(p => project.parts.IndexOf(p)).ToList();
                foreach (var pToRemove in partsToRemove)
                {
                    DocManager.Inst.ExecuteCmd(new RemovePartCommand(project, pToRemove));
                }
                DocManager.Inst.ExecuteCmd(new AddPartCommand(project, mergedPart));
                DocManager.Inst.EndUndoGroup();

                return Ok(new { message = "Parts merged successfully." });
            }
            catch (Exception ex)
            {
                DocManager.Inst.EndUndoGroup(); 
                return StatusCode(500, new { error = ex.Message });
            }
        }


    }
}
