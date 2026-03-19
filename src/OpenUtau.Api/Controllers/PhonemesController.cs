using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Format;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Editing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/project/notes/{partNo}/{noteIndex}")]
    public class PhonemesController : ControllerBase
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

        [HttpGet("phonemes")]
        public IActionResult GetPhonemes(int partNo, int noteIndex)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");
            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest("Invalid partNo");

            if (project.parts[partNo] is not UVoicePart part) return BadRequest("Not a voice part");
            if (noteIndex < 0 || noteIndex >= part.notes.Count) return BadRequest("Invalid noteIndex");

            var note = part.notes.ElementAt(noteIndex);
            
            return Ok(new
            {
                phonemeOverrides = note.phonemeOverrides.Select(o => new {
                    index = o.index,
                    phoneme = o.phoneme,
                    offset = o.offset,
                    preutterDelta = o.preutterDelta,
                    overlapDelta = o.overlapDelta
                }),
                phonemeExpressions = note.phonemeExpressions.Select(e => new {
                    index = e.index,
                    abbr = e.abbr,
                    value = e.value
                }),
                phonemeIndexes = note.phonemeIndexes 
            });
        }

        [HttpGet("flags")]
        public IActionResult GetNoteFlags(int partNo, int noteIndex)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");
            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest("Invalid partNo");

            if (project.parts[partNo] is not UVoicePart part) return BadRequest("Not a voice part");
            if (noteIndex < 0 || noteIndex >= part.notes.Count) return BadRequest("Invalid noteIndex");

            Ustx.AddDefaultExpressions(project);
            var note = part.notes.ElementAt(noteIndex);
            var track = project.tracks[part.trackNo];
            return Ok(new
            {
                partNo,
                noteIndex,
                flags = GetClassicFlagDescriptors(project).Select(descriptor => new
                {
                    name = descriptor.name,
                    abbr = descriptor.abbr,
                    flag = descriptor.flag,
                    type = descriptor.type.ToString(),
                    values = note.GetExpression(project, track, descriptor.abbr)
                        .Select(value => (int?)Math.Round(value.Item1))
                        .ToArray()
                }).ToList()
            });
        }

        [HttpPut("flags")]
        public IActionResult SetNoteFlags(int partNo, int noteIndex, [FromBody] ClassicFlagsRequest request)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");
            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest("Invalid partNo");

            if (project.parts[partNo] is not UVoicePart part) return BadRequest("Not a voice part");
            if (noteIndex < 0 || noteIndex >= part.notes.Count) return BadRequest("Invalid noteIndex");
            if (request == null || request.Flags == null || request.Flags.Count == 0) return BadRequest("No flags provided");

            Ustx.AddDefaultExpressions(project);
            var note = part.notes.ElementAt(noteIndex);
            var resolvedFlags = new List<(UExpressionDescriptor descriptor, int? value)>();
            foreach (var flag in request.Flags)
            {
                if (!TryResolveClassicFlagDescriptor(project, flag.Flag, out var descriptor))
                {
                    return BadRequest(new { error = $"Flag '{flag.Flag}' not found or ambiguous." });
                }
                resolvedFlags.Add((descriptor, flag.Value));
            }

            try
            {
                DocManager.Inst.StartUndoGroup("api", true);
                foreach (var flag in resolvedFlags)
                {
                    DocManager.Inst.ExecuteCmd(new SetNoteExpressionCommand(project, project.tracks[part.trackNo], part, note, flag.descriptor.abbr, flag.value.HasValue ? new float?[] { flag.value.Value } : Array.Empty<float?>()));
                }
                DocManager.Inst.EndUndoGroup();
                return Ok(new { message = "Note flags updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("phonemes/{phoneIndex}/offset")]
        public IActionResult SetPhonemeOffset(int partNo, int noteIndex, int phoneIndex, [FromQuery] int offset)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");
            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest("Invalid partNo");

            if (project.parts[partNo] is not UVoicePart part) return BadRequest("Not a voice part");
            if (noteIndex < 0 || noteIndex >= part.notes.Count) return BadRequest("Invalid noteIndex");

            var note = part.notes.ElementAt(noteIndex);

            DocManager.Inst.StartUndoGroup($"Set Phoneme Offset");
            DocManager.Inst.ExecuteCmd(new PhonemeOffsetCommand(part, note, phoneIndex, offset));
            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = $"Phoneme {phoneIndex} offset set to {offset}" });
        }

        [HttpPost("phonemes/{phoneIndex}/preutter")]
        public IActionResult SetPhonemePreutter(int partNo, int noteIndex, int phoneIndex, [FromQuery] float delta)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");
            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest("Invalid partNo");

            if (project.parts[partNo] is not UVoicePart part) return BadRequest("Not a voice part");
            if (noteIndex < 0 || noteIndex >= part.notes.Count) return BadRequest("Invalid noteIndex");

            var note = part.notes.ElementAt(noteIndex);

            DocManager.Inst.StartUndoGroup($"Set Phoneme Preutter");
            DocManager.Inst.ExecuteCmd(new PhonemePreutterCommand(part, note, phoneIndex, delta));
            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = $"Phoneme {phoneIndex} preutter delta set to {delta}" });
        }

        [HttpPost("phonemes/{phoneIndex}/overlap")]
        public IActionResult SetPhonemeOverlap(int partNo, int noteIndex, int phoneIndex, [FromQuery] float delta)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");
            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest("Invalid partNo");

            if (project.parts[partNo] is not UVoicePart part) return BadRequest("Not a voice part");
            if (noteIndex < 0 || noteIndex >= part.notes.Count) return BadRequest("Invalid noteIndex");

            var note = part.notes.ElementAt(noteIndex);

            DocManager.Inst.StartUndoGroup($"Set Phoneme Overlap");
            DocManager.Inst.ExecuteCmd(new PhonemeOverlapCommand(part, note, phoneIndex, delta));
            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = $"Phoneme {phoneIndex} overlap delta set to {delta}" });
        }

        [HttpPost("phonemes/{phoneIndex}/alias")]
        public IActionResult SetPhonemeAlias(int partNo, int noteIndex, int phoneIndex, [FromQuery] string? alias)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");
            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest("Invalid partNo");

            if (project.parts[partNo] is not UVoicePart part) return BadRequest("Not a voice part");
            if (noteIndex < 0 || noteIndex >= part.notes.Count) return BadRequest("Invalid noteIndex");

            var note = part.notes.ElementAt(noteIndex);

            DocManager.Inst.StartUndoGroup("Set Phoneme Alias");
            DocManager.Inst.ExecuteCmd(new ChangePhonemeAliasCommand(part, note, phoneIndex, alias));
            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = $"Phoneme {phoneIndex} alias set to {alias ?? string.Empty}" });
        }

        [HttpPost("tuning")]
        public IActionResult SetNoteTuning(int partNo, int noteIndex, [FromQuery] int tuning)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");
            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest("Invalid partNo");

            if (project.parts[partNo] is not UVoicePart part) return BadRequest("Not a voice part");
            if (noteIndex < 0 || noteIndex >= part.notes.Count) return BadRequest("Invalid noteIndex");

            var note = part.notes.ElementAt(noteIndex);

            DocManager.Inst.StartUndoGroup($"Set Note Tuning");
            DocManager.Inst.ExecuteCmd(new ChangeNoteTuningCommand(part, note, tuning));
            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = $"Note tuning set to {tuning}" });
        }
    }
}
