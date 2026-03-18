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
        [HttpPost("run")]
        public IActionResult RunBatchEdit(
            [FromForm] IFormFile file, 
            [FromQuery] string editName, 
            [FromQuery] int partIndex = 0,
            [FromQuery] int? quantize = 15)
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