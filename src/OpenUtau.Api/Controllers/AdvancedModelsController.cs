using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Format;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Render;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdvancedModelsController : ControllerBase
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

        [HttpGet("singers")]
        public IActionResult GetAdvancedModels()
        {
            var singers = SingerManager.Inst.Singers.Values;
            var response = singers.Select(s => new {
                Id = s.Id,
                Name = s.Name,
                SingerType = s.SingerType.ToString(),
                IsDiffSinger = s.SingerType == USingerType.DiffSinger,
                IsEnunu = s.SingerType == USingerType.Enunu,
                IsVoicevox = s.SingerType == USingerType.Voicevox,
                Speakers = s.Subbanks.Select(b => new {
                    Name = b.Color,
                    Prefix = b.Prefix,
                    Suffix = b.Suffix
                })
            });
            return Ok(response);
        }

        [HttpPost("track/{trackIndex}/configEngine")]
        public IActionResult ConfigEngine(IFormFile file, int trackIndex, [FromQuery] string renderer, [FromQuery] string fallbackSpeaker)
        {
            return ExecuteEdit(file, project =>
            {
                if (trackIndex < 0 || trackIndex >= project.tracks.Count) throw new Exception("Invalid track index");
                var track = project.tracks[trackIndex];
                
                track.Validate(new ValidateOptions(), project);

                if (!string.IsNullOrEmpty(renderer))
                {
                    track.RendererSettings.renderer = renderer;
                }

                if (fallbackSpeaker != null)
                {
                    // Update Voice Color default
                    if (track.VoiceColorExp != null && track.VoiceColorExp.options.Contains(fallbackSpeaker))
                    {
                        track.VoiceColorNames = new string[] { fallbackSpeaker };
                    }
                }
            });
        }

        public class CurveData
        {
            public int[] Xs { get; set; }
            public int[] Ys { get; set; }
        }

        [HttpPost("part/{partNo}/vocalShaper")]
        public IActionResult ApplyVocalShaper(IFormFile file, int partNo, [FromQuery] string abbr, [FromBody] CurveData curveData)
        {
            // Specifically handling "tenc", "genc", "brec", "voic" or engine-specific "velc", "ene", "pexp", "cl1", "cl2" (DiffSinger)
            return ExecuteEdit(file, project =>
            {
                if (partNo < 0 || partNo >= project.parts.Count) throw new Exception("Invalid part index");
                var partBase = project.parts[partNo];
                if (partBase is UVoicePart part)
                {
                    if (curveData != null && curveData.Xs != null && curveData.Ys != null && curveData.Xs.Length == curveData.Ys.Length)
                    {
                        var curve = part.curves.FirstOrDefault(c => c.abbr == abbr);
                        if (curve == null)
                        {
                            project.expressions.TryGetValue(abbr, out var descriptor);
                            if (descriptor == null) descriptor = new UExpressionDescriptor(abbr, abbr, -1000, 1000, 0) { type = UExpressionType.Curve };
                            curve = new UCurve(descriptor);
                            part.curves.Add(curve);
                        }
                        curve.xs = curveData.Xs.ToList();
                        curve.ys = curveData.Ys.ToList();
                    }
                }
            });
        }

        [HttpPost("part/{partNo}/note/{noteIndex}/setVoiceColor")]
        public IActionResult SetNoteVoiceColor(IFormFile file, int partNo, int noteIndex, [FromQuery] string voiceColor)
        {
            return ExecuteEdit(file, project =>
            {
                if (partNo < 0 || partNo >= project.parts.Count) throw new Exception("Invalid part index");
                var partBase = project.parts[partNo];
                if (partBase is UVoicePart part)
                {
                    if (noteIndex < 0 || noteIndex >= part.notes.Count) throw new Exception("Invalid note index");
                    var note = part.notes.ElementAt(noteIndex);
                    
                    project.expressions.TryGetValue("CLR", out var clrDescriptor);
                    if (clrDescriptor != null)
                    {
                        var colorIndex = Array.IndexOf(clrDescriptor.options, voiceColor);
                        if (colorIndex >= 0)
                        {
                            note.phonemeExpressions.RemoveAll(e => e.descriptor?.abbr == "CLR");
                            note.phonemeExpressions.Add(new UExpression(clrDescriptor)
                            {
                                index = 0,
                                value = colorIndex
                            });
                        }
                    }
                }
            });
        }

    }
}
