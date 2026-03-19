using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Reflection;
using System.Text.Json;
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

        [HttpGet("config")]
        public IActionResult GetPhonemizerConfig([FromQuery] string phonemizerType, [FromQuery] string? singerId = null)
        {
            var phonemizer = CreatePhonemizer(phonemizerType, singerId, out var error);
            if (phonemizer == null)
            {
                return NotFound(new { error });
            }

            return Ok(new
            {
                phonemizerType,
                singerId,
                config = SnapshotConfig(phonemizer)
            });
        }

        [HttpPost("config")]
        public IActionResult UpdatePhonemizerConfig([FromQuery] string phonemizerType, [FromQuery] string? singerId, [FromBody] JsonElement configPatch)
        {
            var phonemizer = CreatePhonemizer(phonemizerType, singerId, out var error);
            if (phonemizer == null)
            {
                return NotFound(new { error });
            }

            try
            {
                if (configPatch.ValueKind != JsonValueKind.Object)
                {
                    return BadRequest("Config patch must be a JSON object.");
                }

                ApplyConfigPatch(phonemizer, configPatch);

                return Ok(new
                {
                    message = "Phonemizer config updated successfully.",
                    phonemizerType,
                    singerId,
                    config = SnapshotConfig(phonemizer)
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
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

            // Request the part to recalculate phonemes and Wait Finish
            part.Validate(new ValidateOptions { SkipPhonemizer = false }, project, project.tracks[part.trackNo]);

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

        private Phonemizer? CreatePhonemizer(string phonemizerType, string? singerId, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(phonemizerType))
            {
                error = "Phonemizer type is required.";
                return null;
            }

            var factory = DocManager.Inst.PhonemizerFactories?.FirstOrDefault(f => f.type.FullName == phonemizerType);
            if (factory == null)
            {
                error = "Phonemizer not found.";
                return null;
            }

            var phonemizer = factory.Create();
            if (phonemizer == null)
            {
                error = "Failed to create phonemizer instance.";
                return null;
            }

            if (!string.IsNullOrWhiteSpace(singerId))
            {
                var singer = SingerManager.Inst.Singers.Values.FirstOrDefault(s => s.Id == singerId);
                if (singer == null)
                {
                    error = "Singer not found.";
                    return null;
                }

                phonemizer.SetSinger(singer);
            }

            return phonemizer;
        }

        private static readonly HashSet<string> ConfigSkipMemberNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Name", "Tag", "Author", "Language", "Testing", "singer", "timeAxis", "bpm",
            "DictionariesPath", "PluginDir"
        };

        private static bool IsConfigLikeType(Type type)
        {
            if (type == null) return false;

            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null)
            {
                type = underlying;
            }

            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan))
            {
                return true;
            }

            if (typeof(System.Collections.IDictionary).IsAssignableFrom(type))
            {
                return false;
            }

            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && type != typeof(string))
            {
                return true;
            }

            if (type.IsDefined(typeof(SerializableAttribute), inherit: true))
            {
                return true;
            }

            var name = type.Name;
            return name.EndsWith("Config", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("Setting", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("Settings", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("Options", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("Args", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("Yaml", StringComparison.OrdinalIgnoreCase);
        }

        private object? SnapshotConfig(object target)
        {
            return SnapshotConfig(target, new HashSet<object>(ReferenceEqualityComparer.Instance), 0);
        }

        private object? SnapshotConfig(object target, HashSet<object> visited, int depth)
        {
            if (target == null)
            {
                return null;
            }

            var type = target.GetType();
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan))
            {
                return target;
            }

            if (depth > 5)
            {
                return target.ToString();
            }

            if (!visited.Add(target))
            {
                return "[Circular]";
            }

            if (target is System.Collections.IEnumerable enumerable && target is not string)
            {
                var items = new List<object?>();
                foreach (var item in enumerable)
                {
                    items.Add(item == null ? null : SnapshotConfig(item, visited, depth + 1));
                }
                return items;
            }

            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var member in GetInspectableMembers(type))
            {
                if (ConfigSkipMemberNames.Contains(member.Name))
                {
                    continue;
                }

                var memberType = GetMemberType(member);
                if (!IsConfigLikeType(memberType))
                {
                    continue;
                }

                var value = GetMemberValue(target, member);
                if (value == null)
                {
                    result[member.Name] = null;
                    continue;
                }

                result[member.Name] = SnapshotConfig(value, visited, depth + 1);
            }

            return result;
        }

        private void ApplyConfigPatch(object target, JsonElement patch)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (patch.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Config patch must be a JSON object.");
            }

            foreach (var prop in patch.EnumerateObject())
            {
                ApplyConfigMemberPatch(target, prop.Name, prop.Value);
            }
        }

        private void ApplyConfigMemberPatch(object target, string memberName, JsonElement value)
        {
            var member = GetInspectableMembers(target.GetType())
                .FirstOrDefault(m => string.Equals(m.Name, memberName, StringComparison.OrdinalIgnoreCase));

            if (member == null)
            {
                throw new ArgumentException($"Config member '{memberName}' was not found on {target.GetType().Name}.");
            }

            if (ConfigSkipMemberNames.Contains(member.Name))
            {
                throw new ArgumentException($"Config member '{memberName}' is read-only.");
            }

            var memberType = GetMemberType(member);
            var currentValue = GetMemberValue(target, member);

            if (value.ValueKind == JsonValueKind.Object && currentValue != null && IsConfigLikeType(memberType) && !typeof(System.Collections.IEnumerable).IsAssignableFrom(memberType))
            {
                ApplyConfigPatch(currentValue, value);
                return;
            }

            object? convertedValue = null;
            if (value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Undefined)
            {
                convertedValue = null;
            }
            else
            {
                convertedValue = JsonSerializer.Deserialize(value.GetRawText(), memberType);
            }

            SetMemberValue(target, member, convertedValue);
        }

        private static IEnumerable<MemberInfo> GetInspectableMembers(Type type)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            while (type != null && type != typeof(object))
            {
                if (type != typeof(Phonemizer))
                {
                    foreach (var field in type.GetFields(flags))
                    {
                        if (!field.IsStatic)
                        {
                            yield return field;
                        }
                    }

                    foreach (var prop in type.GetProperties(flags))
                    {
                        if (prop.GetIndexParameters().Length == 0 && prop.GetMethod != null)
                        {
                            yield return prop;
                        }
                    }
                }

                type = type.BaseType;
            }
        }

        private static Type GetMemberType(MemberInfo member)
        {
            return member switch
            {
                FieldInfo field => field.FieldType,
                PropertyInfo prop => prop.PropertyType,
                _ => throw new NotSupportedException($"Unsupported member type: {member.MemberType}")
            };
        }

        private static object? GetMemberValue(object target, MemberInfo member)
        {
            return member switch
            {
                FieldInfo field => field.GetValue(target),
                PropertyInfo prop => prop.GetValue(target),
                _ => throw new NotSupportedException($"Unsupported member type: {member.MemberType}")
            };
        }

        private static void SetMemberValue(object target, MemberInfo member, object? value)
        {
            switch (member)
            {
                case FieldInfo field:
                    field.SetValue(target, value);
                    break;
                case PropertyInfo prop:
                    if (!prop.CanWrite)
                    {
                        throw new InvalidOperationException($"Property '{prop.Name}' is read-only.");
                    }
                    prop.SetValue(target, value);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported member type: {member.MemberType}");
            }
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

    internal sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
