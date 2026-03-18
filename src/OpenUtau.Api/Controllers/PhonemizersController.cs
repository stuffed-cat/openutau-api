using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;
using OpenUtau.Core;
using OpenUtau.Api;
using OpenUtau.Core.Ustx;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PhonemizersController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetPhonemizers()
        {
            var factories = DocManager.Inst.PhonemizerFactories;
            if (factories == null)
            {
                return Ok(new object[] { });
            }

            var phonemizers = factories.Select(f => new {
                Name = f.name,
                Tag = f.tag,
                Author = f.author,
                Language = f.language,
                Type = f.type.FullName
            });

            return Ok(phonemizers);
        }

        [HttpPost("install")]
        public async Task<IActionResult> InstallPhonemizer(IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            try
            {
                var tempPath = Path.Combine(Path.GetTempPath(), file.FileName);
                using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                OpenUtau.Core.Api.PhonemizerInstaller.Install(tempPath);
                
                if (System.IO.File.Exists(tempPath)) {
                    System.IO.File.Delete(tempPath);
                }

                return Ok(new { Message = "Phonemizer installed successfully." });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("preview")]
        public IActionResult Preview([FromBody] PhonemizerPreviewRequest request)
        {
            var factories = DocManager.Inst.PhonemizerFactories;
            var factory = factories?.FirstOrDefault(f => f.type.FullName == request.PhonemizerType);
            if (factory == null)
            {
                return NotFound(new { error = "Phonemizer not found" });
            }

            var phonemizer = factory.Create();
            USinger singer = null;
            if (!string.IsNullOrEmpty(request.SingerId))
            {
                singer = SingerManager.Inst.Singers.Values.FirstOrDefault(s => s.Id == request.SingerId);
            }
            if (singer != null)
            {
                phonemizer.SetSinger(singer);
            }

            var timeAxis = DocManager.Inst.Project?.timeAxis ?? new TimeAxis();
            if (DocManager.Inst.Project == null) {
                timeAxis.BuildSegments(new UProject());
            }
            phonemizer.SetTiming(timeAxis);

            var apiNotes = new List<Phonemizer.Note>();
            foreach (var reqNote in request.Notes)
            {
                var note = new Phonemizer.Note
                {
                    lyric = reqNote.Lyric,
                    phoneticHint = reqNote.PhoneticHint,
                    position = reqNote.Position,
                    duration = reqNote.Duration,
                    tone = reqNote.Tone
                };
                if (reqNote.PhonemeAttributes != null && reqNote.PhonemeAttributes.Count > 0)
                {
                    note.phonemeAttributes = reqNote.PhonemeAttributes.Select(pa => new Phonemizer.PhonemeAttributes {
                        index = pa.Index,
                        consonantStretchRatio = pa.ConsonantStretchRatio,
                        toneShift = pa.ToneShift
                    }).ToArray();
                }
                apiNotes.Add(note);
            }

            try 
            {
                var result = phonemizer.Process(
                    apiNotes.ToArray(), 
                    null, null, null, null, 
                    apiNotes.ToArray()
                );

                var phonemes = new List<object>();
                if (result.phonemes != null) {
                    foreach (var p in result.phonemes) {
                        var exprs = new List<object>();
                        if (p.expressions != null) {
                            foreach (var e in p.expressions) {
                                exprs.Add(new { Abbr = e.abbr, Value = e.value });
                            }
                        }
                        phonemes.Add(new { Phoneme = p.phoneme, Position = p.position, Expressions = exprs });
                    }
                }

                return Ok(new { phonemes });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message, stack = ex.StackTrace });
            }
            finally 
            {
                phonemizer.CleanUp();
            }
        }

                [HttpPost("part/sync")]
        public IActionResult SyncPartPhonemes([FromBody] SyncPartPhonemesRequest request)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("Project not loaded.");

            var part = project.parts.FirstOrDefault(p => p.name == request.PartName || project.parts.IndexOf(p) == request.PartIndex) as UVoicePart;
            if (part == null) return NotFound("Part not found.");

            var result = new List<object>();
            foreach (var note in part.notes)
            {
                var notePhonemes = part.phonemes.Where(p => p.Parent == note).Select(p => new {
                    NoteLyric = note.lyric,
                    Phoneme = p.phoneme,
                    Position = p.position,
                    Error = p.Error ? true : false
                });
                result.AddRange(notePhonemes);
            }

            return Ok(new {
                part = part.name,
                phonemes = result
            });
        }

        [HttpPost("part/recalculate")]
        public IActionResult RecalculatePartPhonemes([FromBody] SyncPartPhonemesRequest request)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("Project not loaded.");

            var part = project.parts.FirstOrDefault(p => p.name == request.PartName || project.parts.IndexOf(p) == request.PartIndex) as UVoicePart;
            if (part == null) return NotFound("Part not found.");

            // Request the part to recalculate phonemes
            part.Validate(new ValidateOptions { SkipPhonemizer = false }, project, project.tracks[part.trackNo]);

            // Synchronously wait for the PhonemizerRunner to clear the queue
            var runnerProp = typeof(DocManager).GetProperty("PhonemizerRunner", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (runnerProp != null)
            {
                var runner = runnerProp.GetValue(DocManager.Inst);
                if (runner != null)
                {
                    var waitFinishMethod = runner.GetType().GetMethod("WaitFinish");
                    waitFinishMethod?.Invoke(runner, null);
                }
            }

            // Give the default scheduler a brief moment to process the response task
            System.Threading.Thread.Sleep(200);

            var result = new List<object>();
            foreach (var note in part.notes)
            {
                var notePhonemes = part.phonemes.Where(p => p.Parent == note).Select(p => new {
                    NoteLyric = note.lyric,
                    Phoneme = p.phoneme,
                    Position = p.position,
                    Error = p.Error ? true : false
                });
                result.AddRange(notePhonemes);
            }

            return Ok(new {
                part = part.name,
                phonemes = result
            });
        }
    }

    public class PhonemizerPreviewRequest
    {
        public string PhonemizerType { get; set; } = string.Empty;
        public string SingerId { get; set; } = string.Empty;
        public List<PreviewNote> Notes { get; set; } = new List<PreviewNote>();
    }

    public class PreviewNote
    {
        public string Lyric { get; set; } = string.Empty;
        public string PhoneticHint { get; set; } = string.Empty;
        public int Tone { get; set; }
        public int Position { get; set; }
        public int Duration { get; set; }
        public List<PreviewPhonemeAttributes> PhonemeAttributes { get; set; } = new List<PreviewPhonemeAttributes>();
    }

    public class PreviewPhonemeAttributes
    {
        public int Index { get; set; }
        public double? ConsonantStretchRatio { get; set; }
        public int ToneShift { get; set; }
    }

    public class SyncPartPhonemesRequest
    {
        public string PartName { get; set; } = string.Empty;
        public int PartIndex { get; set; }
    }
}
