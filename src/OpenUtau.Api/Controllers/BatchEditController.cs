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
        private static readonly Lazy<IReadOnlyDictionary<string, BatchEditSpec>> BatchEditSpecs =
            new Lazy<IReadOnlyDictionary<string, BatchEditSpec>>(BuildBatchEditSpecs);

        [HttpGet("supported")]
        public IActionResult GetSupportedBatchEdits()
        {
            return Ok(BatchEditSpecs.Value.Values
                .OrderBy(spec => spec.EditName, StringComparer.OrdinalIgnoreCase)
                .Select(spec => new
                {
                    editName = spec.EditName,
                    description = spec.Description,
                    parameters = spec.Parameters
                }));
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
            return ExecuteEdit(file, project =>
            {
                if (partIndex < 0 || partIndex >= project.parts.Count) return;
                var part = project.parts[partIndex] as UVoicePart;
                if (part == null) return;
                var edit = CreateBatchEdit(editName, quantize, lyric, name, deltaNoteNum, ratio, max);
                if (edit == null)
                {
                    throw new ArgumentException("Unknown batch edit type: " + editName);
                }

                if (edit != null)
                {
                    edit.Run(project, part, part.notes.ToList(), DocManager.Inst);
                }
            });
        }

        private static BatchEdit? CreateBatchEdit(
            string editName,
            int? quantize,
            string? lyric,
            string? name,
            int? deltaNoteNum,
            double? ratio,
            int? max)
        {
            if (string.IsNullOrWhiteSpace(editName) || !BatchEditSpecs.Value.TryGetValue(editName, out var spec))
            {
                return null;
            }

            return spec.Factory(new BatchEditArgs
            {
                Quantize = quantize,
                Lyric = lyric,
                Name = name,
                DeltaNoteNum = deltaNoteNum,
                Ratio = ratio,
                Max = max
            });
        }

        private static IReadOnlyDictionary<string, BatchEditSpec> BuildBatchEditSpecs()
        {
            var specs = DiscoverBatchEditSpecs()
                .Concat(GetManualBatchEditSpecs())
                .GroupBy(spec => spec.EditName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

            return new System.Collections.ObjectModel.ReadOnlyDictionary<string, BatchEditSpec>(specs);
        }

        private static IEnumerable<BatchEditSpec> DiscoverBatchEditSpecs()
        {
            foreach (var type in AppDomain.CurrentDomain.GetAssemblies().SelectMany(GetLoadableTypes))
            {
                if (type == null || type.IsAbstract || !typeof(BatchEdit).IsAssignableFrom(type))
                {
                    continue;
                }

                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    continue;
                }

                var editName = ToKebabCase(type.Name);
                if (string.IsNullOrWhiteSpace(editName))
                {
                    continue;
                }

                BatchEdit? instance;
                try
                {
                    instance = Activator.CreateInstance(type) as BatchEdit;
                }
                catch
                {
                    continue;
                }

                if (instance == null)
                {
                    continue;
                }

                yield return new BatchEditSpec
                {
                    EditName = editName,
                    Description = string.IsNullOrWhiteSpace(instance.GetType().Name) ? editName : instance.GetType().Name,
                    Parameters = Array.Empty<string>(),
                    Factory = _ => (BatchEdit)Activator.CreateInstance(type)!
                };
            }
        }

        private static IEnumerable<BatchEditSpec> GetManualBatchEditSpecs()
        {
            yield return new BatchEditSpec
            {
                EditName = "reset-pitch",
                Description = "Reset pitch bends",
                Parameters = Array.Empty<string>(),
                Factory = _ => new ResetPitchBends()
            };
            yield return new BatchEditSpec
            {
                EditName = "reset-vibrato",
                Description = "Reset vibratos",
                Parameters = Array.Empty<string>(),
                Factory = _ => new ResetVibratos()
            };
            yield return new BatchEditSpec
            {
                EditName = "quantize",
                Description = "Quantize selected notes",
                Parameters = new[] { "quantize" },
                Factory = args => new QuantizeNotes(args.Quantize ?? 15)
            };
            yield return new BatchEditSpec
            {
                EditName = "add-tail-dash",
                Description = "Add tail notes with dash lyric",
                Parameters = new[] { "lyric" },
                Factory = args => new AddTailNote("-", args.Name ?? "pianoroll.menu.notes.addtaildash")
            };
            yield return new BatchEditSpec
            {
                EditName = "add-tail-rest",
                Description = "Add tail notes with rest lyric",
                Parameters = new[] { "lyric" },
                Factory = args => new AddTailNote(args.Lyric ?? "R", args.Name ?? "pianoroll.menu.notes.addtailrest")
            };
            yield return new BatchEditSpec
            {
                EditName = "remove-tail-dash",
                Description = "Remove dash tail notes",
                Parameters = new[] { "lyric" },
                Factory = args => new RemoveTailNote("-", args.Name ?? "pianoroll.menu.notes.removetaildash")
            };
            yield return new BatchEditSpec
            {
                EditName = "remove-tail-rest",
                Description = "Remove rest tail notes",
                Parameters = new[] { "lyric" },
                Factory = args => new RemoveTailNote(args.Lyric ?? "R", args.Name ?? "pianoroll.menu.notes.removetailrest")
            };
            yield return new BatchEditSpec
            {
                EditName = "transpose",
                Description = "Transpose selected notes by semitones",
                Parameters = new[] { "deltaNoteNum" },
                Factory = args => new Transpose(args.DeltaNoteNum ?? 0, args.Name ?? "api.batchedit.transpose")
            };
            yield return new BatchEditSpec
            {
                EditName = "octave-up",
                Description = "Transpose selected notes up one octave",
                Parameters = Array.Empty<string>(),
                Factory = args => new Transpose(12, args.Name ?? "pianoroll.menu.notes.octaveup")
            };
            yield return new BatchEditSpec
            {
                EditName = "octave-down",
                Description = "Transpose selected notes down one octave",
                Parameters = Array.Empty<string>(),
                Factory = args => new Transpose(-12, args.Name ?? "pianoroll.menu.notes.octavedown")
            };
            yield return new BatchEditSpec
            {
                EditName = "add-breath-note",
                Description = "Insert breath notes",
                Parameters = new[] { "lyric" },
                Factory = args => new AddBreathNote(args.Lyric ?? "br")
            };
            yield return new BatchEditSpec
            {
                EditName = "randomize-tuning",
                Description = "Randomize tuning values",
                Parameters = new[] { "max" },
                Factory = args => new RandomizeTuning(args.Max ?? 100)
            };
            yield return new BatchEditSpec
            {
                EditName = "lengthen-crossfade",
                Description = "Lengthen crossfade overlap",
                Parameters = new[] { "ratio" },
                Factory = args => new LengthenCrossfade(args.Ratio ?? 0.5)
            };
        }

        private static IEnumerable<Type> GetLoadableTypes(System.Reflection.Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(type => type != null)!;
            }
        }

        private static string ToKebabCase(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            var result = new List<char>(name.Length * 2);
            for (int i = 0; i < name.Length; i++)
            {
                var ch = name[i];
                if (char.IsUpper(ch))
                {
                    if (i > 0)
                    {
                        var prev = name[i - 1];
                        var next = i + 1 < name.Length ? name[i + 1] : '\0';
                        if (char.IsLower(prev) || (char.IsUpper(prev) && char.IsLower(next)))
                        {
                            result.Add('-');
                        }
                    }

                    result.Add(char.ToLowerInvariant(ch));
                }
                else if (ch == '_')
                {
                    result.Add('-');
                }
                else
                {
                    result.Add(ch);
                }
            }

            var kebab = new string(result.ToArray());
            while (kebab.Contains("--"))
            {
                kebab = kebab.Replace("--", "-");
            }

            return kebab.Trim('-');
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

        private sealed class BatchEditSpec
        {
            public string EditName { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string[] Parameters { get; set; } = Array.Empty<string>();
            public Func<BatchEditArgs, BatchEdit> Factory { get; set; } = _ => throw new InvalidOperationException();
        }

        private sealed class BatchEditArgs
        {
            public int? Quantize { get; set; }
            public string? Lyric { get; set; }
            public string? Name { get; set; }
            public int? DeltaNoteNum { get; set; }
            public double? Ratio { get; set; }
            public int? Max { get; set; }
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