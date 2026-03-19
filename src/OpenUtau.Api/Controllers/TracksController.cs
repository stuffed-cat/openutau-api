using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Format;
using OpenUtau.Core.Ustx;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/project/[controller]")]
    public class TracksController : ControllerBase
    {
        public class ClassicFlagRequest
        {
            public string Flag { get; set; } = string.Empty;
            public int? Value { get; set; }
        }

        public class ClassicFlagsRequest
        {
            public List<ClassicFlagRequest> Flags { get; set; } = new List<ClassicFlagRequest>();
        }

        private static IEnumerable<UExpressionDescriptor> GetClassicFlagDescriptors(UProject project)
        {
            return project.expressions.Values
                .Where(expr => !string.IsNullOrWhiteSpace(expr.flag) || expr.isFlag)
                .OrderBy(expr => expr.flag, StringComparer.OrdinalIgnoreCase)
                .ThenBy(expr => expr.abbr, StringComparer.OrdinalIgnoreCase);
        }

        private static bool TryResolveClassicFlagDescriptor(UProject project, string flag, out UExpressionDescriptor descriptor)
        {
            descriptor = null;
            if (string.IsNullOrWhiteSpace(flag))
            {
                return false;
            }

            var matches = GetClassicFlagDescriptors(project)
                .Where(expr => string.Equals(expr.flag, flag, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matches.Count != 1)
            {
                return false;
            }

            descriptor = matches[0];
            return true;
        }

        [HttpGet("/api/project/track/{trackNo}/properties")]
        public IActionResult GetTrackProperties(int trackNo) {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");

            if (trackNo < 0 || trackNo >= project.tracks.Count) return BadRequest("Invalid track index");
            var track = project.tracks[trackNo];

            return Ok(new {
                trackNo = track.TrackNo,
                trackName = track.TrackName,
                singer = track.Singer?.Id ?? track.singer,
                phonemizer = track.Phonemizer?.GetType().Name ?? track.phonemizer,
                renderer = track.RendererSettings?.renderer,
                rendererSettings = track.RendererSettings,
                mute = track.Mute,
                solo = track.Solo,
                volume = track.Volume,
                pan = track.Pan,
                voiceColorNames = track.VoiceColorNames
            });
        }

        [HttpGet("/api/project/track/{trackNo}/expressions")]
        public IActionResult GetTrackExpressions(int trackNo) {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");

            if (trackNo < 0 || trackNo >= project.tracks.Count) return BadRequest("Invalid track index");
            var track = project.tracks[trackNo];

            return Ok(project.expressions.Values.Select(expr => new {
                name = expr.name,
                abbr = expr.abbr,
                type = expr.type.ToString(),
                min = expr.min,
                max = expr.max,
                defaultValue = expr.defaultValue,
                isFlag = expr.isFlag,
                flag = expr.flag
            }).ToList());
        }

        [HttpGet("/api/project/track/{trackNo}/flags")]
        public IActionResult GetTrackFlags(int trackNo)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");

            if (trackNo < 0 || trackNo >= project.tracks.Count) return BadRequest("Invalid track index");
            var track = project.tracks[trackNo];

            Ustx.AddDefaultExpressions(project);
            return Ok(new
            {
                trackNo = track.TrackNo,
                flags = GetClassicFlagDescriptors(project).Select(descriptor =>
                {
                    var current = track.TrackExpressions.FirstOrDefault(exp => exp.abbr == descriptor.abbr) ?? descriptor;
                    var value = (int?)Math.Round(current.CustomDefaultValue);
                    return new
                    {
                        name = current.name,
                        abbr = current.abbr,
                        flag = current.flag,
                        type = current.type.ToString(),
                        value,
                        option = current.type == UExpressionType.Options && current.options != null && value.HasValue && value.Value >= 0 && value.Value < current.options.Length
                            ? current.options[value.Value]
                            : null
                    };
                }).ToList()
            });
        }

        [HttpPut("/api/project/track/{trackNo}/flags")]
        public IActionResult SetTrackFlags(int trackNo, [FromBody] ClassicFlagsRequest request)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");

            if (trackNo < 0 || trackNo >= project.tracks.Count) return BadRequest("Invalid track index");
            if (request == null || request.Flags == null || request.Flags.Count == 0) return BadRequest("No flags provided");

            var track = project.tracks[trackNo];
            Ustx.AddDefaultExpressions(project);

            try
            {
                var updatedTrackDescriptors = track.TrackExpressions.Select(descriptor => descriptor.Clone()).ToList();
                foreach (var flag in request.Flags)
                {
                    if (!TryResolveClassicFlagDescriptor(project, flag.Flag, out var baseDescriptor))
                    {
                        return BadRequest(new { error = $"Flag '{flag.Flag}' not found or ambiguous." });
                    }

                    var existing = updatedTrackDescriptors.FirstOrDefault(descriptor => descriptor.abbr == baseDescriptor.abbr);
                    if (!flag.Value.HasValue)
                    {
                        if (existing != null)
                        {
                            updatedTrackDescriptors.Remove(existing);
                        }
                        continue;
                    }

                    if (existing == null)
                    {
                        existing = baseDescriptor.Clone();
                        updatedTrackDescriptors.Add(existing);
                    }

                    if (existing.type == UExpressionType.Options)
                    {
                        var value = flag.Value.Value;
                        if (existing.options == null || value < 0 || value >= existing.options.Length)
                        {
                            return BadRequest(new { error = $"Flag '{flag.Flag}' value is out of range." });
                        }
                        existing.CustomDefaultValue = value;
                    }
                    else
                    {
                        existing.CustomDefaultValue = flag.Value.Value;
                    }
                }

                DocManager.Inst.StartUndoGroup("api", true);
                DocManager.Inst.ExecuteCmd(new ConfigureExpressionsCommand(project, project.expressions.Values.ToArray(), track, updatedTrackDescriptors.ToArray()));
                DocManager.Inst.EndUndoGroup();
                return Ok(new { message = "Track flags updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("{trackIndex}/rename")]
        public IActionResult RenameTrack(int trackIndex, [FromQuery] string name)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project is currently loaded.");
            if (trackIndex < 0 || trackIndex >= project.tracks.Count) return NotFound("Track not found");
            if (string.IsNullOrEmpty(name)) return BadRequest("Name cannot be empty");

            try
            {
                var track = project.tracks[trackIndex];
                DocManager.Inst.StartUndoGroup("api", true);
                DocManager.Inst.ExecuteCmd(new RenameTrackCommand(project, track, name));
                DocManager.Inst.EndUndoGroup();
                return Ok(new { message = $"Track {trackIndex} renamed to {name}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("{trackIndex}/setcolor")]
        public IActionResult SetTrackColor(int trackIndex, [FromQuery] string color)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project is currently loaded.");
            if (trackIndex < 0 || trackIndex >= project.tracks.Count) return NotFound("Track not found");

            try
            {
                var track = project.tracks[trackIndex];
                DocManager.Inst.StartUndoGroup("api", true);
                DocManager.Inst.ExecuteCmd(new ChangeTrackColorCommand(project, track, color));
                DocManager.Inst.EndUndoGroup();
                return Ok(new { message = $"Track {trackIndex} color set to {color}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("{trackIndex}/setsinger")]
        public IActionResult SetTrackSinger(int trackIndex, [FromQuery] string singerName)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project is currently loaded.");
            if (trackIndex < 0 || trackIndex >= project.tracks.Count) return NotFound("Track not found");

            try
            {
                var singer = SingerManager.Inst.GetSinger(singerName);
                if (singer == null) return BadRequest($"Singer '{singerName}' not found");

                var track = project.tracks[trackIndex];
                DocManager.Inst.StartUndoGroup("api", true);
                DocManager.Inst.ExecuteCmd(new TrackChangeSingerCommand(project, track, singer));
                DocManager.Inst.EndUndoGroup();
                return Ok(new { message = $"Track {trackIndex} singer set to {singer.Name}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("{trackIndex}/setphonmizer")]
        public IActionResult SetTrackPhonemizer(int trackIndex, [FromQuery] string phonemizerName)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project is currently loaded.");
            if (trackIndex < 0 || trackIndex >= project.tracks.Count) return NotFound("Track not found");

            try
            {
                var factory = DocManager.Inst.PhonemizerFactories.FirstOrDefault(f => f.name == phonemizerName);
                if (factory == null) return BadRequest($"Phonemizer '{phonemizerName}' not found");

                var phonemizer = factory.Create();
                var track = project.tracks[trackIndex];
                DocManager.Inst.StartUndoGroup("api", true);
                DocManager.Inst.ExecuteCmd(new TrackChangePhonemizerCommand(project, track, phonemizer));
                DocManager.Inst.EndUndoGroup();
                return Ok(new { message = $"Track {trackIndex} phonemizer set to {phonemizerName}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("{trackIndex}/phonemizer/config")]
        public IActionResult GetTrackPhonemizerConfig(int trackIndex)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project is currently loaded.");
            if (trackIndex < 0 || trackIndex >= project.tracks.Count) return NotFound("Track not found");

            var track = project.tracks[trackIndex];
            if (track.Phonemizer == null) return NotFound("Track phonemizer not found");

            return Ok(new {
                trackIndex,
                phonemizerType = track.Phonemizer.GetType().FullName,
                config = SnapshotConfig(track.Phonemizer)
            });
        }

        [HttpPost("{trackIndex}/phonemizer/config")]
        public IActionResult UpdateTrackPhonemizerConfig(int trackIndex, [FromBody] TrackPhonemizerConfigRequest request)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project is currently loaded.");
            if (trackIndex < 0 || trackIndex >= project.tracks.Count) return NotFound("Track not found");
            if (request == null) return BadRequest("Request body is required.");

            try
            {
                var track = project.tracks[trackIndex];
                var targetPhonemizer = track.Phonemizer;
                var needsReplace = !string.IsNullOrWhiteSpace(request.PhonemizerType);

                if (needsReplace)
                {
                    targetPhonemizer = CreatePhonemizer(request.PhonemizerType, request.SingerId ?? track.Singer?.Id, out var error);
                    if (targetPhonemizer == null)
                    {
                        return BadRequest(new { error });
                    }
                }

                if (targetPhonemizer == null)
                {
                    return BadRequest("Track phonemizer not found.");
                }

                if (request.ConfigPatch.ValueKind != JsonValueKind.Null && request.ConfigPatch.ValueKind != JsonValueKind.Undefined)
                {
                    if (request.ConfigPatch.ValueKind != JsonValueKind.Object)
                    {
                        return BadRequest("Config patch must be a JSON object.");
                    }

                    ApplyConfigPatch(targetPhonemizer, request.ConfigPatch);
                }

                if (needsReplace)
                {
                    DocManager.Inst.StartUndoGroup("api", true);
                    DocManager.Inst.ExecuteCmd(new TrackChangePhonemizerCommand(project, track, targetPhonemizer));
                    DocManager.Inst.EndUndoGroup();
                }

                return Ok(new {
                    message = "Track phonemizer config updated successfully.",
                    trackIndex,
                    phonemizerType = targetPhonemizer.GetType().FullName,
                    config = SnapshotConfig(targetPhonemizer)
                });
            }
            catch (Exception ex)
            {
                if (DocManager.Inst.HasOpenUndoGroup) DocManager.Inst.EndUndoGroup();
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("{trackIndex}/setrenderer")]
        public IActionResult SetTrackRenderer(int trackIndex, [FromQuery] string rendererId)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project is currently loaded.");
            if (trackIndex < 0 || trackIndex >= project.tracks.Count) return NotFound("Track not found");

            try
            {
                var track = project.tracks[trackIndex];
                var newSettings = track.RendererSettings.Clone();
                newSettings.renderer = rendererId;
                DocManager.Inst.StartUndoGroup("api", true);
                DocManager.Inst.ExecuteCmd(new TrackChangeRenderSettingCommand(project, track, newSettings));
                DocManager.Inst.EndUndoGroup();
                return Ok(new { message = $"Track {trackIndex} renderer set to {rendererId}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

[HttpPost("{trackIndex}/setresampler")]
        public IActionResult SetTrackResampler(int trackIndex, [FromQuery] string resamplerId)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project is currently loaded.");
            if (trackIndex < 0 || trackIndex >= project.tracks.Count) return NotFound("Track not found");
            try
            {
                var track = project.tracks[trackIndex];
                var newSettings = track.RendererSettings.Clone();
                newSettings.resampler = resamplerId;
                DocManager.Inst.StartUndoGroup("api", true);
                DocManager.Inst.ExecuteCmd(new TrackChangeRenderSettingCommand(project, track, newSettings));
                DocManager.Inst.EndUndoGroup();
                return Ok(new { message = $"Track {trackIndex} resampler set to {resamplerId}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("{trackIndex}/setwavtool")]
        public IActionResult SetTrackWavtool(int trackIndex, [FromQuery] string wavtoolId)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project is currently loaded.");
            if (trackIndex < 0 || trackIndex >= project.tracks.Count) return NotFound("Track not found");
            try
            {
                var track = project.tracks[trackIndex];
                var newSettings = track.RendererSettings.Clone();
                newSettings.wavtool = wavtoolId;
                DocManager.Inst.StartUndoGroup("api", true);
                DocManager.Inst.ExecuteCmd(new TrackChangeRenderSettingCommand(project, track, newSettings));
                DocManager.Inst.EndUndoGroup();
                return Ok(new { message = $"Track {trackIndex} wavtool set to {wavtoolId}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private Phonemizer? CreatePhonemizer(string phonemizerType, string? singerId, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(phonemizerType))
            {
                error = "Phonemizer type is required.";
                return null;
            }

            var factory = DocManager.Inst.PhonemizerFactories?.FirstOrDefault(f =>
                string.Equals(f.name, phonemizerType, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(f.type.FullName, phonemizerType, StringComparison.OrdinalIgnoreCase));
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

        private static object? SnapshotConfig(object target)
        {
            return SnapshotConfig(target, new HashSet<object>(ReferenceEqualityComparer.Instance), 0);
        }

        private static object? SnapshotConfig(object target, HashSet<object> visited, int depth)
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

        private static void ApplyConfigPatch(object target, JsonElement patch)
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

        private static void ApplyConfigMemberPatch(object target, string memberName, JsonElement value)
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
            if (value.ValueKind != JsonValueKind.Null && value.ValueKind != JsonValueKind.Undefined)
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

            return type.Namespace != null && (type.Namespace.StartsWith("OpenUtau", StringComparison.Ordinal) || type.Namespace.StartsWith("System", StringComparison.Ordinal));
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        public class TrackPhonemizerConfigRequest
        {
            public string? PhonemizerType { get; set; }
            public string? SingerId { get; set; }
            public JsonElement ConfigPatch { get; set; }
        }

        [HttpPost("{trackIndex}/voicecolormapping")]
        public IActionResult SetVoiceColorMapping(int trackIndex, [FromQuery] bool validate = true)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project is currently loaded.");
            if (trackIndex != -1 && (trackIndex < 0 || trackIndex >= project.tracks.Count)) return NotFound("Track not found");

            try
            {
                // This triggers the remapping on the specified track
                // If trackIndex is -1 it checks all tracks
                DocManager.Inst.ExecuteCmd(new VoiceColorRemappingNotification(trackIndex, validate));
                DocManager.Inst.EndUndoGroup();
                return Ok(new { message = $"Voice color remapping triggered for track {trackIndex}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

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
        public IActionResult AddTrack(IFormFile file, [FromQuery] int? trackIndex = null)
        {
            return ExecuteEdit(file, project =>
            {
                var newTrack = new UTrack(project) { TrackName = "New Track" };
                if (trackIndex.HasValue && trackIndex.Value >= 0 && trackIndex.Value < project.tracks.Count)
                {
                    project.tracks.Insert(trackIndex.Value, newTrack);
                    foreach (var p in project.parts.Where(p => p.trackNo >= trackIndex.Value)) { p.trackNo++; }
                }
                else
                {
                    project.tracks.Add(newTrack);
                }
            });
        }

        [HttpPost("remove")]
        public IActionResult RemoveTrack(IFormFile file, [FromQuery] int trackIndex) // [FromQuery] keeps it easy
        {
            return ExecuteEdit(file, project =>
            {
                if (trackIndex < 0 || trackIndex >= project.tracks.Count) throw new Exception("Invalid track index");
                project.tracks.RemoveAt(trackIndex);
                project.parts.RemoveAll(p => p.trackNo == trackIndex);
                foreach (var p in project.parts.Where(p => p.trackNo > trackIndex)) { p.trackNo--; }
            });
        }

        [HttpPost("move")]
        public IActionResult MoveTrack(IFormFile file, [FromQuery] int fromIndex, [FromQuery] int toIndex)
        {
            return ExecuteEdit(file, project =>
            {
                if (fromIndex < 0 || fromIndex >= project.tracks.Count || toIndex < 0 || toIndex >= project.tracks.Count) throw new Exception("Invalid index");
                if (fromIndex == toIndex) return;

                var track = project.tracks[fromIndex];
                project.tracks.RemoveAt(fromIndex);
                project.tracks.Insert(toIndex, track);
                
                foreach(var p in project.parts)
                {
                    if (p.trackNo == fromIndex) { p.trackNo = toIndex; }
                    else if (fromIndex < toIndex && p.trackNo > fromIndex && p.trackNo <= toIndex) { p.trackNo--; }
                    else if (fromIndex > toIndex && p.trackNo >= toIndex && p.trackNo < fromIndex) { p.trackNo++; }
                }
            });
        }

        [HttpPost("config")]
        public IActionResult ConfigTrack(
            IFormFile file, 
            [FromQuery] int trackIndex, 
            [FromQuery] string? name = null,
            [FromQuery] string? color = null,
            [FromQuery] bool? mute = null,
            [FromQuery] bool? solo = null,
            [FromQuery] string? singerId = null,
            [FromQuery] string? phonemizer = null,
            [FromQuery] string? renderer = null,
            [FromQuery] string? resampler = null,
            [FromQuery] string? wavtool = null)
        {
            return ExecuteEdit(file, project =>
            {
                if (trackIndex < 0 || trackIndex >= project.tracks.Count) throw new Exception("Invalid track index");
                var track = project.tracks[trackIndex];
                
                if (name != null) track.TrackName = name;
                if (color != null) track.TrackColor = color;
                if (mute.HasValue) track.Mute = mute.Value;
                if (solo.HasValue) track.Solo = solo.Value;
                
                if (singerId != null && SingerManager.Inst.Singers != null)
                {
                    track.Singer = SingerManager.Inst.Singers.Values.FirstOrDefault(s => s.Id == singerId) ?? track.Singer;
                }
                
                if (phonemizer != null && DocManager.Inst.PhonemizerFactories != null)
                {
                    var factory = DocManager.Inst.PhonemizerFactories.FirstOrDefault(f => f.name == phonemizer);
                    if (factory != null) track.Phonemizer = factory.Create();
                }
                
                if (renderer != null) track.RendererSettings.renderer = renderer;
                if (resampler != null) track.RendererSettings.resampler = resampler;
                if (wavtool != null) track.RendererSettings.wavtool = wavtool;
            });
        }
    }
}
