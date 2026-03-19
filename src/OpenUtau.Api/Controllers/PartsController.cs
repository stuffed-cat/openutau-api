using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Format;
using System.IO;
using System;
using System.Linq;
using OpenUtau.Core;
using OpenUtau.Core.Analysis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
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

            DocManager.Inst.StartUndoGroup("api", true);
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
            DocManager.Inst.StartUndoGroup("command.part.rename", true);
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

                DocManager.Inst.StartUndoGroup("command.import.audio", true);
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
                    
                    DocManager.Inst.StartUndoGroup("command.part.transcribe", true);
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

        public class WavePartCropRequest
        {
            public double StartMs { get; set; }    // Start position in milliseconds
            public double DurationMs { get; set; } // Duration to keep in milliseconds
        }

        [HttpPost("{partIndex}/crop")]
        public IActionResult CropWavePart(int partIndex, [FromBody] WavePartCropRequest request)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project is currently loaded.");
            if (partIndex < 0 || partIndex >= project.parts.Count) return NotFound("Part not found.");

            var part = project.parts[partIndex] as UWavePart;
            if (part == null) return BadRequest("Target part is not a wave part.");

            try
            {
                if (request.StartMs < 0 || request.DurationMs <= 0)
                    return BadRequest("Invalid crop parameters: StartMs must be >= 0, DurationMs must be > 0");

                if (request.StartMs + request.DurationMs > part.fileDurationMs)
                    return BadRequest($"Crop range exceeds file duration ({part.fileDurationMs}ms)");

                DocManager.Inst.StartUndoGroup("command.part.crop", true);
                
                // Create a modified clone with the new skip and trim values
                var newPart = new UWavePart()
                {
                    FilePath = part.FilePath,
                    trackNo = part.trackNo,
                    position = part.position,
                    skipMs = request.StartMs,
                    trimMs = request.DurationMs
                };
                newPart.Load(project);

                DocManager.Inst.ExecuteCmd(new ReplacePartCommand(project, part, newPart));
                DocManager.Inst.EndUndoGroup();

                return Ok(new { 
                    message = "Wave part cropped successfully.",
                    skipMs = newPart.skipMs,
                    trimMs = newPart.trimMs,
                    newDurationMs = newPart.Duration
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        public class TimeStretchRequest
        {
            public double StretchRatio { get; set; } // 1.0 = normal, 0.5 = half speed, 2.0 = double speed
        }

        [HttpPost("{partIndex}/timestretch")]
        public IActionResult TimeStretchWavePart(int partIndex, [FromBody] TimeStretchRequest request)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project is currently loaded.");
            if (partIndex < 0 || partIndex >= project.parts.Count) return NotFound("Part not found.");

            var part = project.parts[partIndex] as UWavePart;
            if (part == null) return BadRequest("Target part is not a wave part.");

            try
            {
                if (request.StretchRatio <= 0)
                    return BadRequest("Stretch ratio must be > 0");

                DocManager.Inst.StartUndoGroup("command.part.timestretch", true);

                // Time stretching is achieved by modifying the duration
                // The actual audio processing would be done by external tools
                int newDuration = (int)System.Math.Round(part.Duration / request.StretchRatio);
                part.Duration = newDuration;

                DocManager.Inst.EndUndoGroup();

                return Ok(new { 
                    message = "Time stretch applied successfully.",
                    stretchRatio = request.StretchRatio,
                    newDuration = newDuration
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        public class FadeRequest
        {
            public double FadeDurationMs { get; set; } // Duration of fade in/out in milliseconds
            public string Type { get; set; } = "both";  // "in", "out", or "both"
        }

        [HttpPost("{partIndex}/fade")]
        public async Task<IActionResult> ApplyFadeInOut(int partIndex, [FromBody] FadeRequest request)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project is currently loaded.");
            if (partIndex < 0 || partIndex >= project.parts.Count) return NotFound("Part not found.");

            var part = project.parts[partIndex] as UWavePart;
            if (part == null) return BadRequest("Target part is not a wave part.");

            try
            {
                if (request.FadeDurationMs <= 0)
                    return BadRequest("Fade duration must be > 0");

                if (!System.IO.File.Exists(part.FilePath))
                    return BadRequest("Source audio file not found");

                var outputPath = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(part.FilePath),
                    System.IO.Path.GetFileNameWithoutExtension(part.FilePath) + "_faded.wav"
                );

                // Apply fade using NAudio
                await Task.Run(() =>
                {
                    using (var reader = new NAudio.Wave.WaveFileReader(part.FilePath))
                    {
                        var sampleProvider = reader.ToSampleProvider();
                        var fadeProvider = new FadeSampleProvider(
                            sampleProvider,
                            request.Type == "in" || request.Type == "both" ? request.FadeDurationMs / 1000.0 : 0,
                            request.Type == "out" || request.Type == "both" ? request.FadeDurationMs / 1000.0 : 0);

                        NAudio.Wave.WaveFileWriter.CreateWaveFile16(outputPath, fadeProvider);
                    }
                });

                DocManager.Inst.StartUndoGroup("command.part.fade", true);

                var newPart = new UWavePart()
                {
                    FilePath = outputPath,
                    trackNo = part.trackNo,
                    position = part.position
                };
                newPart.Load(project);

                DocManager.Inst.ExecuteCmd(new ReplacePartCommand(project, part, newPart));
                DocManager.Inst.EndUndoGroup();

                return Ok(new { 
                    message = "Fade applied successfully.",
                    outputPath = outputPath,
                    type = request.Type,
                    fadeDurationMs = request.FadeDurationMs
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        public class VolumePoint
        {
            public double TimeMs { get; set; }  // Time position in milliseconds
            public double Volume { get; set; }  // Volume level (0.0 = silent, 1.0 = full)
        }

        public class VolumeEnvelopeRequest
        {
            public VolumePoint[] Points { get; set; } // Array of volume control points
        }

        [HttpPost("{partIndex}/volume-envelope")]
        public IActionResult EditVolumeEnvelope(int partIndex, [FromBody] VolumeEnvelopeRequest request)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project is currently loaded.");
            if (partIndex < 0 || partIndex >= project.parts.Count) return NotFound("Part not found.");

            var part = project.parts[partIndex] as UWavePart;
            if (part == null) return BadRequest("Target part is not a wave part.");

            try
            {
                if (request.Points == null || request.Points.Length == 0)
                    return BadRequest("Volume envelope requires at least one control point");

                // Validate points
                foreach (var point in request.Points)
                {
                    if (point.TimeMs < 0 || point.TimeMs > part.fileDurationMs)
                        return BadRequest($"Point time {point.TimeMs}ms is outside file duration");
                    if (point.Volume < 0 || point.Volume > 1)
                        return BadRequest("Volume must be between 0 and 1");
                }

                // Store volume envelope metadata as a note in comment
                string envelopeData = System.Text.Json.JsonSerializer.Serialize(request.Points);
                string newComment = part.comment + (string.IsNullOrEmpty(part.comment) ? "" : "\n") 
                    + "VOLUME_ENVELOPE:" + envelopeData;

                DocManager.Inst.StartUndoGroup("command.part.volume-envelope", true);
                part.comment = newComment;
                DocManager.Inst.EndUndoGroup();

                return Ok(new { 
                    message = "Volume envelope updated successfully.",
                    pointCount = request.Points.Length,
                    envelopeData = request.Points
                });
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

                DocManager.Inst.StartUndoGroup("command.part.edit", true);
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

        // ==================== 表情曲线绘制工具 ====================

        public class LinearCurveRequest
        {
            public int StartX { get; set; }      // 起始时间位置
            public int EndX { get; set; }        // 结束时间位置
            public int StartY { get; set; }      // 起始值
            public int EndY { get; set; }        // 结束值
            public int Interval { get; set; } = 5;  // x轴间隔（默认5）
        }

        [HttpPost("{partNo}/curves/{abbr}/linear")]
        public IActionResult DrawLinearCurve(int partNo, string abbr, [FromBody] LinearCurveRequest request)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");
            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest("Invalid part index");

            var partBase = project.parts[partNo];
            if (!(partBase is UVoicePart part)) return BadRequest("Not a voice part");

            try
            {
                var xs = new List<int>();
                var ys = new List<int>();

                // 线性插值从 StartX 到 EndX，间隔为 Interval
                if (request.StartX > request.EndX)
                    (request.StartX, request.EndX) = (request.EndX, request.StartX);

                int steps = (request.EndX - request.StartX) / request.Interval + 1;
                for (int i = 0; i < steps; i++)
                {
                    int x = request.StartX + i * request.Interval;
                    if (x > request.EndX) x = request.EndX;

                    // 线性插值公式
                    double t = (request.EndX > request.StartX) 
                        ? (double)(x - request.StartX) / (request.EndX - request.StartX) 
                        : 0;
                    int y = (int)System.Math.Round(request.StartY + (request.EndY - request.StartY) * t);

                    xs.Add(x);
                    ys.Add(y);
                }

                return ApplyCurveCommand(project, part, abbr, xs.ToArray(), ys.ToArray(), "linear");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        public class BezierCurveRequest
        {
            public int StartX { get; set; }
            public int EndX { get; set; }
            public int StartY { get; set; }
            public int EndY { get; set; }
            public int ControlY1 { get; set; }   // 第一个控制点Y值
            public int ControlY2 { get; set; }   // 第二个控制点Y值
            public int Interval { get; set; } = 5;
        }

        [HttpPost("{partNo}/curves/{abbr}/bezier")]
        public IActionResult DrawBezierCurve(int partNo, string abbr, [FromBody] BezierCurveRequest request)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");
            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest("Invalid part index");

            var partBase = project.parts[partNo];
            if (!(partBase is UVoicePart part)) return BadRequest("Not a voice part");

            try
            {
                var xs = new List<int>();
                var ys = new List<int>();

                if (request.StartX > request.EndX)
                    (request.StartX, request.EndX) = (request.EndX, request.StartX);

                int steps = (request.EndX - request.StartX) / request.Interval + 1;
                for (int i = 0; i < steps; i++)
                {
                    int x = request.StartX + i * request.Interval;
                    if (x > request.EndX) x = request.EndX;

                    // 三次贝塞尔曲线插值
                    double t = (request.EndX > request.StartX)
                        ? (double)(x - request.StartX) / (request.EndX - request.StartX)
                        : 0;

                    double mt = 1 - t;
                    double y = mt * mt * mt * request.StartY
                        + 3 * mt * mt * t * request.ControlY1
                        + 3 * mt * t * t * request.ControlY2
                        + t * t * t * request.EndY;

                    xs.Add(x);
                    ys.Add((int)System.Math.Round(y));
                }

                return ApplyCurveCommand(project, part, abbr, xs.ToArray(), ys.ToArray(), "bezier");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        public class RampCurveRequest
        {
            public int StartX { get; set; }
            public int EndX { get; set; }
            public int StartValue { get; set; }
            public int EndValue { get; set; }
            public string Shape { get; set; } = "linear";  // "linear", "exponential", "logarithmic"
            public int Interval { get; set; } = 5;
        }

        [HttpPost("{partNo}/curves/{abbr}/ramp")]
        public IActionResult DrawRampCurve(int partNo, string abbr, [FromBody] RampCurveRequest request)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");
            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest("Invalid part index");

            var partBase = project.parts[partNo];
            if (!(partBase is UVoicePart part)) return BadRequest("Not a voice part");

            try
            {
                var xs = new List<int>();
                var ys = new List<int>();

                if (request.StartX > request.EndX)
                    (request.StartX, request.EndX) = (request.EndX, request.StartX);

                int steps = (request.EndX - request.StartX) / request.Interval + 1;
                for (int i = 0; i < steps; i++)
                {
                    int x = request.StartX + i * request.Interval;
                    if (x > request.EndX) x = request.EndX;

                    double t = (request.EndX > request.StartX)
                        ? (double)(x - request.StartX) / (request.EndX - request.StartX)
                        : 0;

                    double y = request.Shape switch
                    {
                        "exponential" => request.StartValue + (request.EndValue - request.StartValue) * (System.Math.Exp(t) - 1) / (System.Math.E - 1),
                        "logarithmic" => request.StartValue + (request.EndValue - request.StartValue) * System.Math.Log(1 + t * (System.Math.E - 1)) / System.Math.Log(System.Math.E),
                        _ => request.StartValue + (request.EndValue - request.StartValue) * t // linear
                    };

                    xs.Add(x);
                    ys.Add((int)System.Math.Round(y));
                }

                return ApplyCurveCommand(project, part, abbr, xs.ToArray(), ys.ToArray(), "ramp");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        public class NoiseRequest
        {
            public int StartX { get; set; }
            public int EndX { get; set; }
            public int BaseValue { get; set; }     // 基础值
            public int NoiseAmount { get; set; }   // 噪声幅度
            public int Interval { get; set; } = 5;
            public int RandomSeed { get; set; } = -1;  // -1表示使用随机种子
        }

        [HttpPost("{partNo}/curves/{abbr}/noise")]
        public IActionResult DrawNoiseCurve(int partNo, string abbr, [FromBody] NoiseRequest request)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");
            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest("Invalid part index");

            var partBase = project.parts[partNo];
            if (!(partBase is UVoicePart part)) return BadRequest("Not a voice part");

            try
            {
                var xs = new List<int>();
                var ys = new List<int>();

                if (request.StartX > request.EndX)
                    (request.StartX, request.EndX) = (request.EndX, request.StartX);

                var random = request.RandomSeed >= 0 
                    ? new System.Random(request.RandomSeed) 
                    : new System.Random();

                int steps = (request.EndX - request.StartX) / request.Interval + 1;
                for (int i = 0; i < steps; i++)
                {
                    int x = request.StartX + i * request.Interval;
                    if (x > request.EndX) x = request.EndX;

                    // 添加随机噪声
                    int noise = random.Next(-request.NoiseAmount, request.NoiseAmount + 1);
                    int y = request.BaseValue + noise;

                    xs.Add(x);
                    ys.Add(y);
                }

                return ApplyCurveCommand(project, part, abbr, xs.ToArray(), ys.ToArray(), "noise");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        public class SmoothCurveRequest
        {
            public int WindowSize { get; set; } = 3;  // 平滑窗口大小（必须为奇数）
        }

        [HttpPost("{partNo}/curves/{abbr}/smooth")]
        public IActionResult SmoothCurve(int partNo, string abbr, [FromBody] SmoothCurveRequest request)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");
            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest("Invalid part index");

            var partBase = project.parts[partNo];
            if (!(partBase is UVoicePart part)) return BadRequest("Not a voice part");

            try
            {
                var curve = part.curves.FirstOrDefault(c => c.abbr == abbr);
                if (curve == null || curve.xs.Count == 0)
                    return BadRequest("Curve not found or is empty");

                if (request.WindowSize < 1 || request.WindowSize % 2 == 0)
                    return BadRequest("Window size must be a positive odd number");

                var xs = curve.xs.ToList();
                var ys = curve.ys.ToList();
                var smoothedYs = new List<int>();

                int halfWindow = request.WindowSize / 2;

                for (int i = 0; i < ys.Count; i++)
                {
                    int start = System.Math.Max(0, i - halfWindow);
                    int end = System.Math.Min(ys.Count - 1, i + halfWindow);
                    
                    double sum = 0;
                    int count = 0;
                    for (int j = start; j <= end; j++)
                    {
                        sum += ys[j];
                        count++;
                    }
                    
                    smoothedYs.Add((int)System.Math.Round(sum / count));
                }

                return ApplyCurveCommand(project, part, abbr, xs.ToArray(), smoothedYs.ToArray(), "smooth");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        public class ScaleCurveRequest
        {
            public double ScaleY { get; set; } = 1.0;  // Y轴缩放倍数
            public int MinY { get; set; } = int.MinValue;  // 最小Y值限制
            public int MaxY { get; set; } = int.MaxValue;  // 最大Y值限制
        }

        [HttpPost("{partNo}/curves/{abbr}/scale")]
        public IActionResult ScaleCurve(int partNo, string abbr, [FromBody] ScaleCurveRequest request)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");
            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest("Invalid part index");

            var partBase = project.parts[partNo];
            if (!(partBase is UVoicePart part)) return BadRequest("Not a voice part");

            try
            {
                var curve = part.curves.FirstOrDefault(c => c.abbr == abbr);
                if (curve == null || curve.xs.Count == 0)
                    return BadRequest("Curve not found or is empty");

                var xs = curve.xs.ToList();
                var scaledYs = curve.ys
                    .Select(y => (int)System.Math.Round(y * request.ScaleY))
                    .Select(y => System.Math.Min(request.MaxY, System.Math.Max(request.MinY, y)))
                    .ToList();

                return ApplyCurveCommand(project, part, abbr, xs.ToArray(), scaledYs.ToArray(), "scale");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        public class OffsetCurveRequest
        {
            public int OffsetY { get; set; }  // Y轴偏移值
            public int MinY { get; set; } = int.MinValue;
            public int MaxY { get; set; } = int.MaxValue;
        }

        [HttpPost("{partNo}/curves/{abbr}/offset")]
        public IActionResult OffsetCurve(int partNo, string abbr, [FromBody] OffsetCurveRequest request)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");
            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest("Invalid part index");

            var partBase = project.parts[partNo];
            if (!(partBase is UVoicePart part)) return BadRequest("Not a voice part");

            try
            {
                var curve = part.curves.FirstOrDefault(c => c.abbr == abbr);
                if (curve == null || curve.xs.Count == 0)
                    return BadRequest("Curve not found or is empty");

                var xs = curve.xs.ToList();
                var offsetYs = curve.ys
                    .Select(y => y + request.OffsetY)
                    .Select(y => System.Math.Min(request.MaxY, System.Math.Max(request.MinY, y)))
                    .ToList();

                return ApplyCurveCommand(project, part, abbr, xs.ToArray(), offsetYs.ToArray(), "offset");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpDelete("{partNo}/curves/{abbr}")]
        public IActionResult ClearCurve(int partNo, string abbr)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");
            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest("Invalid part index");

            var partBase = project.parts[partNo];
            if (!(partBase is UVoicePart part)) return BadRequest("Not a voice part");

            try
            {
                var curve = part.curves.FirstOrDefault(c => c.abbr == abbr);
                if (curve == null)
                    return NotFound("Curve not found");

                DocManager.Inst.StartUndoGroup("command.part.edit", true);
                DocManager.Inst.ExecuteCmd(new MergedSetCurveCommand(project, part, abbr, 
                    curve.xs.ToArray(), curve.ys.ToArray(), 
                    new int[0], new int[0]));
                DocManager.Inst.EndUndoGroup();

                return Ok(new { message = $"Curve {abbr} cleared successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // 辅助方法：应用曲线命令
        private IActionResult ApplyCurveCommand(UProject project, UVoicePart part, string abbr, int[] xs, int[] ys, string operation)
        {
            if (xs == null || ys == null || xs.Length != ys.Length)
                return BadRequest("Invalid curve data");

            var curve = part.curves.FirstOrDefault(c => c.abbr == abbr);
            int[] oldXs = curve?.xs.ToArray() ?? new int[0];
            int[] oldYs = curve?.ys.ToArray() ?? new int[0];

            if (curve == null)
            {
                if (!project.expressions.ContainsKey(abbr))
                {
                    project.expressions.Add(abbr, new UExpressionDescriptor(abbr, abbr, -1000, 1000, 0) 
                    { 
                        type = UExpressionType.Curve 
                    });
                }
            }

            DocManager.Inst.StartUndoGroup($"command.part.curve.{operation}", true);
            DocManager.Inst.ExecuteCmd(new MergedSetCurveCommand(project, part, abbr, oldXs, oldYs, xs, ys));
            DocManager.Inst.EndUndoGroup();

            return Ok(new { 
                message = $"Curve {abbr} {operation} applied successfully", 
                pointCount = xs.Length 
            });
        }

        private sealed class FadeSampleProvider : NAudio.Wave.ISampleProvider
        {
            private readonly NAudio.Wave.ISampleProvider source;
            private readonly int fadeInSamples;
            private readonly int fadeOutSamples;
            private long totalSamplesRead;
            private readonly long totalSamples;

            public FadeSampleProvider(NAudio.Wave.ISampleProvider source, double fadeInSeconds, double fadeOutSeconds)
            {
                this.source = source;
                fadeInSamples = (int)System.Math.Max(0, System.Math.Round(fadeInSeconds * source.WaveFormat.SampleRate)) * source.WaveFormat.Channels;
                fadeOutSamples = (int)System.Math.Max(0, System.Math.Round(fadeOutSeconds * source.WaveFormat.SampleRate)) * source.WaveFormat.Channels;
                totalSamples = source.WaveFormat.SampleRate * source.WaveFormat.Channels * 60L;
            }

            public NAudio.Wave.WaveFormat WaveFormat => source.WaveFormat;

            public int Read(float[] buffer, int offset, int count)
            {
                var read = source.Read(buffer, offset, count);
                if (read <= 0)
                {
                    return read;
                }

                for (int i = 0; i < read; i++)
                {
                    var sampleIndex = totalSamplesRead + i;
                    var gain = 1.0f;

                    if (fadeInSamples > 0 && sampleIndex < fadeInSamples)
                    {
                        gain = (float)sampleIndex / fadeInSamples;
                    }

                    if (fadeOutSamples > 0)
                    {
                        var fadeOutStart = totalSamples - fadeOutSamples;
                        if (sampleIndex >= fadeOutStart)
                        {
                            gain = System.Math.Min(gain, (float)(totalSamples - sampleIndex) / fadeOutSamples);
                        }
                    }

                    buffer[offset + i] *= System.Math.Clamp(gain, 0f, 1f);
                }

                totalSamplesRead += read;
                return read;
            }
        }


    }
}

