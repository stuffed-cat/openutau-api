using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Format;
using OpenUtau.Core.Ustx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/project/[controller]")]
    public class NotesController : ControllerBase
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

        [HttpPost("add")]
        public IActionResult AddNote(
            IFormFile file,
            [FromQuery] int partIndex,
            [FromQuery] int position,
            [FromQuery] int duration,
            [FromQuery] int tone,
            [FromQuery] string lyric)
        {
            return ExecuteEdit(file, (project) => 
            {
                if (partIndex < 0 || partIndex >= project.parts.Count) return;
                var part = project.parts[partIndex] as UVoicePart;
                if (part == null) return;

                var note = project.CreateNote(tone, position, duration);
                if (!string.IsNullOrEmpty(lyric)) note.lyric = lyric;
                
                part.notes.Add(note);
            });
        }

        [HttpPost("remove")]
        public IActionResult RemoveNote(
            IFormFile file,
            [FromQuery] int partIndex,
            [FromQuery] int matchPosition)
        {
            return ExecuteEdit(file, (project) => 
            {
                if (partIndex < 0 || partIndex >= project.parts.Count) return;
                var part = project.parts[partIndex] as UVoicePart;
                if (part == null) return;

                var noteToRemove = part.notes.FirstOrDefault(n => n.position == matchPosition);
                if (noteToRemove != null)
                {
                    part.notes.Remove(noteToRemove);
                }
            });
        }

        [HttpPost("batch_remove")]
        public IActionResult BatchRemoveNotes(
            IFormFile file,
            [FromQuery] int partIndex,
            [FromQuery] string matchPositionsJson) // json array of ints e.g. "[0, 480, 960]"
        {
            var positions = string.IsNullOrEmpty(matchPositionsJson) 
                ? new List<int>() 
                : (JsonSerializer.Deserialize<List<int>>(matchPositionsJson) ?? new List<int>());

            return ExecuteEdit(file, (project) => 
            {
                if (partIndex < 0 || partIndex >= project.parts.Count) return;
                var part = project.parts[partIndex] as UVoicePart;
                if (part == null) return;

                var toRemove = part.notes.Where(n => positions.Contains(n.position)).ToList();
                foreach(var n in toRemove) {
                    part.notes.Remove(n);
                }
            });
        }

        [HttpPost("update")]
        public IActionResult UpdateNote(
            IFormFile file,
            [FromQuery] int partIndex,
            [FromQuery] int matchPosition,
            [FromQuery] int? newPosition = null,
            [FromQuery] int? newDuration = null,
            [FromQuery] int? newTone = null,
            [FromQuery] string? newLyric = null,
            [FromQuery] string? vibratoJson = null,
            [FromQuery] string? expressionsJson = null) // e.g. {"dyn": 100, "clr": -20}
        {
            return ExecuteEdit(file, (project) => 
            {
                if (partIndex < 0 || partIndex >= project.parts.Count) return;
                var part = project.parts[partIndex] as UVoicePart;
                if (part == null) return;

                var note = part.notes.FirstOrDefault(n => n.position == matchPosition);
                if (note == null) return;

                // Move/Change duration or pitch/lyric
                // Due to part.notes being a SortedSet<UNote>, changing position requires re-adding
                bool needReAdd = newPosition.HasValue && newPosition.Value != note.position;
                if (needReAdd && newPosition.HasValue) {
                    part.notes.Remove(note);
                    note.position = newPosition.Value;
                }

                if (newDuration.HasValue) note.duration = newDuration.Value;
                if (newTone.HasValue) note.tone = newTone.Value;
                if (!string.IsNullOrEmpty(newLyric)) note.lyric = newLyric;

                // Set/Modify Vibrato
                if (!string.IsNullOrEmpty(vibratoJson))
                {
                    try {
                        var v = JsonSerializer.Deserialize<VibratoUpdateModel>(vibratoJson);
                        if (v != null) {
                            if (v.length.HasValue) note.vibrato.length = v.length.Value;
                            if (v.period.HasValue) note.vibrato.period = v.period.Value;
                            if (v.depth.HasValue) note.vibrato.depth = v.depth.Value;
                            if (v.@in.HasValue) note.vibrato.@in = v.@in.Value;
                            if (v.@out.HasValue) note.vibrato.@out = v.@out.Value;
                            if (v.shift.HasValue) note.vibrato.shift = v.shift.Value;
                            if (v.drift.HasValue) note.vibrato.drift = v.drift.Value;
                        }
                    } catch {}
                }

                // Expressions (dynamics, clarity etc)
                if (!string.IsNullOrEmpty(expressionsJson))
                {
                    try {
                        var expDict = JsonSerializer.Deserialize<Dictionary<string, float>>(expressionsJson);
                        if (expDict != null) {
                            foreach(var kvp in expDict) {
                                var existing = note.phonemeExpressions.FirstOrDefault(e => e.abbr == kvp.Key);
                                if (existing != null) { 
                                    existing.value = kvp.Value; 
                                    existing.index = 0; 
                                }
                                else { 
                                    var newExp = project.expressions.TryGetValue(kvp.Key, out var d) && d != null
                                        ? new UExpression(d) { value = kvp.Value, index = 0 }
                                        : new UExpression(kvp.Key) { value = kvp.Value, index = 0 };
                                    note.phonemeExpressions.Add(newExp); 
                                }
                            }
                        }
                    } catch {}
                }

                if (needReAdd) {
                    part.notes.Add(note);
                }
            });
        }
    }

    public class VibratoUpdateModel {
        [JsonPropertyName("length")] public float? length { get; set; }
        [JsonPropertyName("period")] public float? period { get; set; }
        [JsonPropertyName("depth")] public float? depth { get; set; }
        [JsonPropertyName("in")] public float? @in { get; set; }
        [JsonPropertyName("out")] public float? @out { get; set; }
        [JsonPropertyName("shift")] public float? shift { get; set; }
        [JsonPropertyName("drift")] public float? drift { get; set; }
    }
}
