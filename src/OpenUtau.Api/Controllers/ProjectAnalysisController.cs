using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Format;
using OpenUtau.Core.Ustx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/project/[controller]")]
    public class ProjectAnalysisController : ControllerBase
    {
        private (UProject, string) LoadTempProject(IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName);
            var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ext);
            using (var stream = new FileStream(tempFile, FileMode.Create))
            {
                file.CopyTo(stream);
            }
            try
            {
                var project = Formats.ReadProject(new string[] { tempFile });
                if (project == null) 
                {
                    throw new Exception("Failed to load project or unsupported format.");
                }
                return (project, tempFile);
            }
            catch
            {
                if (System.IO.File.Exists(tempFile)) System.IO.File.Delete(tempFile);
                throw;
            }
        }

        [HttpPost("detect-conflicts")]
        public IActionResult DetectConflicts(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("No file uploaded");

            try
            {
                var (project, tempFile) = LoadTempProject(file);
                var conflicts = new List<object>();

                foreach (var part in project.parts.OfType<UVoicePart>())
                {
                    var notes = part.notes.OrderBy(n => n.position).ToList();
                    for (int i = 0; i < notes.Count; i++)
                    {
                        var note = notes[i];
                        if (i > 0)
                        {
                            var prev = notes[i - 1];
                            if (note.position < prev.End)
                            {
                                conflicts.Add(new { 
                                    type = "Overlap", 
                                    part = part.name, 
                                    note = note.lyric, 
                                    position = note.position, 
                                    message = $"Overlap with previous note '{prev.lyric}'" 
                                });
                            }
                        }

                        // Check unreasonable settings
                        if (note.duration < 15)
                        {
                            conflicts.Add(new { 
                                type = "TooShort", 
                                part = part.name, 
                                note = note.lyric, 
                                position = note.position, 
                                message = $"Note duration ({note.duration} ticks) is unreasonably short" 
                            });
                        }
                        if (note.tone < 24 || note.tone > 107) // Standard MIDI piano range C1-B7
                        {
                            conflicts.Add(new { 
                                type = "PitchOutOfRange", 
                                part = part.name, 
                                note = note.lyric, 
                                position = note.position, 
                                message = $"Note pitch ({note.tone}) is outside standard human range" 
                            });
                        }
                    }
                }

                System.IO.File.Delete(tempFile);
                return Ok(new { TotalConflicts = conflicts.Count, Details = conflicts });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("statistics")]
        public IActionResult GetStatistics(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("No file uploaded");

            try
            {
                var (project, tempFile) = LoadTempProject(file);
                
                int totalTracks = project.tracks.Count;
                int totalParts = project.parts.Count;
                int voiceParts = project.parts.OfType<UVoicePart>().Count();
                int waveParts = project.parts.OfType<UWavePart>().Count();
                
                int totalNotes = 0;
                int validLyricsCount = 0;
                int overlapErrors = 0;

                foreach (var part in project.parts.OfType<UVoicePart>())
                {
                    totalNotes += part.notes.Count;
                    var notes = part.notes.OrderBy(n => n.position).ToList();
                    for (int i = 0; i < notes.Count; i++)
                    {
                        var note = notes[i];
                        if (!string.IsNullOrWhiteSpace(note.lyric) && note.lyric != "a" && note.lyric != "R")
                        {
                            validLyricsCount++;
                        }
                        if (i > 0 && note.position < notes[i - 1].End)
                        {
                            overlapErrors++;
                        }
                    }
                }

                double completeness = totalNotes == 0 ? 0 : (double)validLyricsCount / totalNotes * 100.0;
                
                // Base 100, minus for every overlap or bad config
                double qualityScore = 100.0;
                if (totalNotes > 0)
                {
                    qualityScore -= ((double)overlapErrors / totalNotes) * 50.0; // severe penalty for overlaps
                }
                
                System.IO.File.Delete(tempFile);

                return Ok(new {
                    Tracks = totalTracks,
                    Parts = new { Total = totalParts, Voice = voiceParts, Wave = waveParts },
                    Notes = new { Total = totalNotes, WithValidLyrics = validLyricsCount },
                    CompletenessPercentage = Math.Round(completeness, 2),
                    QualityScore = Math.Max(0, Math.Round(qualityScore, 2)),
                    Issues = new { Overlaps = overlapErrors }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("import-validate")]
        public IActionResult ValidateImport(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("No file uploaded");
            
            var warnings = new List<string>();
            var ext = Path.GetExtension(file.FileName);
            var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ext);
            
            // Check extension
            var extLower = ext.ToLower();
            if (extLower != ".mid" && extLower != ".midi")
            {
                warnings.Add("File extension is not .mid/.midi. Content sniffing will be used.");
            }

            try
            {
                using (var stream = new FileStream(tempFile, FileMode.Create))
                {
                    file.CopyTo(stream);
                }

                var project = Formats.ReadProject(new string[] { tempFile });
                
                if (project == null)
                {
                    return BadRequest(new { valid = false, error = "Failed to parse MIDI or unsupported format" });
                }

                // Check tempo
                if (project.tempos == null || project.tempos.Count == 0)
                {
                    warnings.Add("No tempo markers found. Default tempo will be used.");
                }

                // Check tracks
                int maxTracksLimit = 16;
                if (project.tracks.Count > maxTracksLimit)
                {
                    warnings.Add($"Project contains {project.tracks.Count} tracks, which exceeds recommended limit ({maxTracksLimit}).");
                }

                int totalNotes = 0;
                foreach(var part in project.parts.OfType<UVoicePart>()) {
                    totalNotes += part.notes.Count;
                }

                if (totalNotes == 0)
                {
                    warnings.Add("No notes found in imported file.");
                }

                System.IO.File.Delete(tempFile);

                return Ok(new {
                    valid = true,
                    tracksFound = project.tracks.Count,
                    notesFound = totalNotes,
                    warnings = warnings
                });
            }
            catch (Exception ex)
            {
                if (System.IO.File.Exists(tempFile)) System.IO.File.Delete(tempFile);
                return BadRequest(new { valid = false, error = $"Parse error: {ex.Message}" });
            }
        }
    }
}
