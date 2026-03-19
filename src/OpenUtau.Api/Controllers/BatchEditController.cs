using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Editing;
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
    public class BatchEditController : ControllerBase
    {
        [HttpGet("supported")] 
        public IActionResult GetSupportedBatchEdits()
        {
            return Ok(new[]
            {
                new { editName = "quantize", description = "Quantize selected notes", parameters = new[] { "quantize" } },
                new { editName = "auto-legato", description = "Auto adjust note lengths for legato", parameters = Array.Empty<string>() },
                new { editName = "fix-overlap", description = "Resolve overlapping notes", parameters = Array.Empty<string>() },
                new { editName = "load-rendered-pitch", description = "Bake rendered pitch into note pitch points", parameters = Array.Empty<string>() },
                new { editName = "bake-pitch", description = "Convert PITD curve to pitch points", parameters = Array.Empty<string>() },
                new { editName = "refresh-real-curves", description = "Refresh renderer-defined real curves", parameters = Array.Empty<string>() },
                new { editName = "add-tail-dash", description = "Add tail notes with dash lyric", parameters = new[] { "lyric" } },
                new { editName = "add-tail-rest", description = "Add tail notes with rest lyric", parameters = new[] { "lyric" } },
                new { editName = "remove-tail-dash", description = "Remove dash tail notes", parameters = new[] { "lyric" } },
                new { editName = "remove-tail-rest", description = "Remove rest tail notes", parameters = new[] { "lyric" } },
                new { editName = "transpose", description = "Transpose selected notes by semitones", parameters = new[] { "deltaNoteNum" } },
                new { editName = "octave-up", description = "Transpose selected notes up one octave", parameters = Array.Empty<string>() },
                new { editName = "octave-down", description = "Transpose selected notes down one octave", parameters = Array.Empty<string>() },
                new { editName = "commonnote-copy", description = "Copy commonnote selection to clipboard", parameters = Array.Empty<string>() },
                new { editName = "commonnote-paste", description = "Paste commonnote clipboard content", parameters = Array.Empty<string>() },
                new { editName = "add-breath-note", description = "Insert breath notes", parameters = new[] { "lyric" } },
                new { editName = "randomize-timing", description = "Randomize note timing", parameters = Array.Empty<string>() },
                new { editName = "randomize-phoneme-offset", description = "Randomize phoneme offsets", parameters = Array.Empty<string>() },
                new { editName = "randomize-tuning", description = "Randomize tuning values", parameters = new[] { "max" } },
                new { editName = "lengthen-crossfade", description = "Lengthen crossfade overlap", parameters = new[] { "ratio" } },
                new { editName = "remove-tone-suffix", description = "Remove pitch suffix from lyrics", parameters = Array.Empty<string>() },
                new { editName = "remove-letter-suffix", description = "Remove alphabetic suffix from lyrics", parameters = Array.Empty<string>() },
                new { editName = "move-suffix-to-voice-color", description = "Move lyric suffix to voice color", parameters = Array.Empty<string>() },
                new { editName = "remove-phonetic-hint", description = "Remove phonetic hints from lyrics", parameters = Array.Empty<string>() },
                new { editName = "dash-to-plus", description = "Convert dash lyric to plus", parameters = Array.Empty<string>() },
                new { editName = "dash-to-plus-tilda", description = "Convert dash lyric to plus~", parameters = Array.Empty<string>() },
                new { editName = "insert-slur", description = "Insert slur notes", parameters = Array.Empty<string>() },
                new { editName = "reset-pitch", description = "Reset pitch bends", parameters = Array.Empty<string>() },
                new { editName = "reset-vibrato", description = "Reset vibratos", parameters = Array.Empty<string>() },
                new { editName = "reset-all-expressions", description = "Reset note expressions and curves", parameters = Array.Empty<string>() },
                new { editName = "clear-vibratos", description = "Clear vibrato lengths", parameters = Array.Empty<string>() },
                new { editName = "reset-aliases", description = "Reset phoneme aliases", parameters = Array.Empty<string>() },
                new { editName = "clear-timings", description = "Clear phoneme timing overrides", parameters = Array.Empty<string>() },
                new { editName = "reset-all", description = "Reset all note tuning/expression data", parameters = Array.Empty<string>() },
                new { editName = "hanzi-to-pinyin", description = "Convert Chinese lyrics to pinyin", parameters = Array.Empty<string>() },
                new { editName = "japanese-vcv-to-cv", description = "Convert Japanese VCV to CV", parameters = Array.Empty<string>() },
                new { editName = "romaji-to-hiragana", description = "Convert romaji lyrics to hiragana", parameters = Array.Empty<string>() },
                new { editName = "hiragana-to-romaji", description = "Convert hiragana lyrics to romaji", parameters = Array.Empty<string>() },
                new { editName = "katakana-to-hiragana", description = "Convert katakana lyrics to hiragana", parameters = Array.Empty<string>() },
                new { editName = "hiragana-to-katakana", description = "Convert hiragana lyrics to katakana", parameters = Array.Empty<string>() },
                new { editName = "korean-romaji-to-hangeul", description = "Convert Korean romaji lyrics to hangul", parameters = Array.Empty<string>() },
            });
        }

        [HttpPost("run")]
        public IActionResult RunBatchEdit(
            [FromForm] IFormFile file, 
            [FromQuery] string editName, 
            [FromQuery] int partIndex = 0,
            [FromQuery] int? quantize = 15,
            [FromQuery] string? lyric = null,
            [FromQuery] string? name = null,
            [FromQuery] int? deltaNoteNum = null,
            [FromQuery] double? ratio = null,
            [FromQuery] int? max = null)
        {
            return ExecuteEdit(file, (project) => 
            {
                if (partIndex < 0 || partIndex >= project.parts.Count) return;
                var part = project.parts[partIndex] as UVoicePart;
                if (part == null) return;
                
                BatchEdit edit = null;
                switch (editName.ToLowerInvariant())
                {
                    case "quantize":
                        edit = new QuantizeNotes(quantize ?? 15);
                        break;
                    case "auto-legato":
                        edit = new AutoLegato();
                        break;
                    case "fix-overlap":
                        edit = new FixOverlap();
                        break;
                    case "load-rendered-pitch":
                        edit = new LoadRenderedPitch();
                        break;
                    case "bake-pitch":
                        edit = new BakePitch();
                        break;
                    case "refresh-real-curves":
                        edit = new RefreshRealCurves();
                        break;
                    case "add-tail-dash":
                        edit = new AddTailNote("-", name ?? "pianoroll.menu.notes.addtaildash");
                        break;
                    case "add-tail-rest":
                        edit = new AddTailNote(lyric ?? "R", name ?? "pianoroll.menu.notes.addtailrest");
                        break;
                    case "remove-tail-dash":
                        edit = new RemoveTailNote("-", name ?? "pianoroll.menu.notes.removetaildash");
                        break;
                    case "remove-tail-rest":
                        edit = new RemoveTailNote(lyric ?? "R", name ?? "pianoroll.menu.notes.removetailrest");
                        break;
                    case "transpose":
                        edit = new Transpose(deltaNoteNum ?? 0, name ?? "api.batchedit.transpose");
                        break;
                    case "octave-up":
                        edit = new Transpose(12, name ?? "pianoroll.menu.notes.octaveup");
                        break;
                    case "octave-down":
                        edit = new Transpose(-12, name ?? "pianoroll.menu.notes.octavedown");
                        break;
                    case "commonnote-copy":
                        edit = new CommonnoteCopy();
                        break;
                    case "commonnote-paste":
                        edit = new CommonnotePaste();
                        break;
                    case "add-breath-note":
                        edit = new AddBreathNote(lyric ?? "br");
                        break;
                    case "randomize-timing":
                        edit = new RandomizeTiming();
                        break;
                    case "randomize-phoneme-offset":
                        edit = new RandomizePhonemeOffset();
                        break;
                    case "randomize-tuning":
                        edit = new RandomizeTuning(max ?? 100);
                        break;
                    case "lengthen-crossfade":
                        edit = new LengthenCrossfade(ratio ?? 0.5);
                        break;
                    case "remove-tone-suffix":
                        edit = new RemoveToneSuffix();
                        break;
                    case "remove-letter-suffix":
                        edit = new RemoveLetterSuffix();
                        break;
                    case "move-suffix-to-voice-color":
                        edit = new MoveSuffixToVoiceColor();
                        break;
                    case "remove-phonetic-hint":
                        edit = new RemovePhoneticHint();
                        break;
                    case "dash-to-plus":
                        edit = new DashToPlus();
                        break;
                    case "dash-to-plus-tilda":
                        edit = new DashToPlusTilda();
                        break;
                    case "insert-slur":
                        edit = new InsertSlur();
                        break;
                    case "romaji-to-hiragana":
                        edit = new RomajiToHiragana();
                        break;
                    case "hiragana-to-romaji":
                        edit = new HiraganaToRomaji();
                        break;
                    case "japanese-vcv-to-cv":
                        edit = new JapaneseVCVtoCV();
                        break;
                    case "katakana-to-hiragana":
                        edit = new KatakanaToHiragana();
                        break;
                    case "hiragana-to-katakana":
                        edit = new HiraganaToKatakana();
                        break;
                    case "korean-romaji-to-hangeul":
                        edit = new KoreanRomajiToHangeul();
                        break;
                    case "hanzi-to-pinyin":
                        edit = new HanziToPinyin();
                        break;
                    case "reset-pitch":
                        edit = new ResetPitchBends();
                        break;
                    case "reset-vibrato":
                        edit = new ResetVibratos();
                        break;
                    case "reset-all-expressions":
                        edit = new ResetAllExpressions();
                        break;
                    case "clear-vibratos":
                        edit = new ClearVibratos();
                        break;
                    case "reset-aliases":
                        edit = new ResetAliases();
                        break;
                    case "clear-timings":
                        edit = new ClearTimings();
                        break;
                    case "reset-all":
                        edit = new ResetAll();
                        break;
                    default:
                        throw new ArgumentException("Unknown batch edit type: " + editName);
                }

                if (edit != null)
                {
                    edit.Run(project, part, part.notes.ToList(), DocManager.Inst);
                }
            });
        }

        private IActionResult ExecuteEdit([FromForm] IFormFile file, Action<UProject> modifier)
        {
            if (file == null || file.Length == 0) return BadRequest("No file uploaded");

            try
            {
                var tempFile = Path.GetTempFileName();
                using (var stream = new FileStream(tempFile, FileMode.Create)) { file.CopyTo(stream); }

                Formats.LoadProject(new string[] { tempFile });
                var project = DocManager.Inst.Project;
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
    }

    public class KatakanaToHiragana : SingleNoteLyricEdit {
        public override string Name => "katakana-to-hiragana";
        protected override string Transform(string lyric) {
            return WanaKanaNet.WanaKana.ToHiragana(lyric);
        }
    }

    public class HiraganaToKatakana : SingleNoteLyricEdit {
        public override string Name => "hiragana-to-katakana";
        protected override string Transform(string lyric) {
            return WanaKanaNet.WanaKana.ToKatakana(lyric);
        }
    }

    public class KoreanRomajiToHangeul : SingleNoteLyricEdit {
        public override string Name => "korean-romaji-to-hangeul";
        protected override string Transform(string lyric) {
            var hangeul = KoreanPhonemizerUtil.TryParseKoreanRomaji(lyric);
            return hangeul ?? lyric;
        }
    }

}