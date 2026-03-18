using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using System.Collections.Generic;
using System.Linq;
using System;

namespace OpenUtau.Api.Controllers
{
    public class VibratoChangeRequest
    {
        public int PartIndex { get; set; }
        public List<int> NoteIndexes { get; set; } = new List<int>();
        public float? Length { get; set; }
        public float? Period { get; set; }
        public float? Depth { get; set; }
        public float? In { get; set; }
        public float? Out { get; set; }
        public float? Shift { get; set; }
        public float? Drift { get; set; }
        public float? VolLink { get; set; }
    }

    public class PitchPointDto
    {
        public float X { get; set; }
        public float Y { get; set; }
        public string Shape { get; set; } = "io";
    }

    public class PitchChangeRequest
    {
        public int PartIndex { get; set; }
        public List<int> NoteIndexes { get; set; } = new List<int>();
        public List<PitchPointDto>? Points { get; set; }
        public bool SnapFirst { get; set; } = true;
    }

    public class ExpressionChangeRequest
    {
        public int PartIndex { get; set; }
        public List<int> NoteIndexes { get; set; } = new List<int>();
        public string Abbr { get; set; } = string.Empty;
        public List<float?>? Values { get; set; }
    }

    [ApiController]
    [Route("api/notes/[controller]")]
    public class PropertiesController : ControllerBase
    {
        [HttpPost("vibrato")]
        public IActionResult SetVibrato([FromBody] VibratoChangeRequest request)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");

            if (request.PartIndex < 0 || request.PartIndex >= project.parts.Count)
                return BadRequest("Invalid part index");

            var part = project.parts[request.PartIndex] as UVoicePart;
            if (part == null) return BadRequest("Part is not voice part");

            if (request.NoteIndexes == null || request.NoteIndexes.Count == 0)
                return BadRequest("No notes specified");

            DocManager.Inst.StartUndoGroup();
            int count = 0;
            foreach (var idx in request.NoteIndexes)
            {
                if (idx < 0 || idx >= part.notes.Count) continue;
                var note = part.notes.ElementAt(idx);
                
                if (request.Length.HasValue) DocManager.Inst.ExecuteCmd(new VibratoLengthCommand(part, note, request.Length.Value));
                if (request.Period.HasValue) DocManager.Inst.ExecuteCmd(new VibratoPeriodCommand(part, note, request.Period.Value));
                if (request.Depth.HasValue) DocManager.Inst.ExecuteCmd(new VibratoDepthCommand(part, note, request.Depth.Value));
                if (request.In.HasValue) DocManager.Inst.ExecuteCmd(new VibratoFadeInCommand(part, note, request.In.Value));
                if (request.Out.HasValue) DocManager.Inst.ExecuteCmd(new VibratoFadeOutCommand(part, note, request.Out.Value));
                if (request.Shift.HasValue) DocManager.Inst.ExecuteCmd(new VibratoShiftCommand(part, note, request.Shift.Value));
                if (request.Drift.HasValue) DocManager.Inst.ExecuteCmd(new VibratoDriftCommand(part, note, request.Drift.Value));
                if (request.VolLink.HasValue) DocManager.Inst.ExecuteCmd(new VibratoVolumeLinkCommand(part, note, request.VolLink.Value));
                count++;
            }
            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = "Vibrato updated", count = count });
        }

        [HttpPost("pitch")]
        public IActionResult SetPitchCurve([FromBody] PitchChangeRequest request)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");

            if (request.PartIndex < 0 || request.PartIndex >= project.parts.Count)
                return BadRequest("Invalid part index");

            var part = project.parts[request.PartIndex] as UVoicePart;
            if (part == null) return BadRequest("Part is not voice part");

            if (request.NoteIndexes == null || request.NoteIndexes.Count == 0)
                return BadRequest("No notes specified");

            var pitch = new UPitch { snapFirst = request.SnapFirst };
            if (request.Points != null)
            {
                foreach (var pt in request.Points)
                {
                    if (Enum.TryParse<PitchPointShape>(pt.Shape, true, out var shape))
                    {
                        pitch.AddPoint(new PitchPoint(pt.X, pt.Y, shape));
                    }
                    else
                    {
                        pitch.AddPoint(new PitchPoint(pt.X, pt.Y));
                    }
                }
            }

            var notesToChange = new List<UNote>();
            foreach (var idx in request.NoteIndexes)
            {
                if (idx >= 0 && idx < part.notes.Count)
                {
                    notesToChange.Add(part.notes.ElementAt(idx));
                }
            }

            DocManager.Inst.StartUndoGroup();
            DocManager.Inst.ExecuteCmd(new SetPitchPointsCommand(part, notesToChange, pitch));
            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = "Pitch curve updated", count = notesToChange.Count });
        }

        [HttpPost("expressions")]
        public IActionResult SetExpression([FromBody] ExpressionChangeRequest request)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");

            if (request.PartIndex < 0 || request.PartIndex >= project.parts.Count)
                return BadRequest("Invalid part index");

            var part = project.parts[request.PartIndex] as UVoicePart;
            if (part == null) return BadRequest("Part is not voice part");

            if (request.NoteIndexes == null || request.NoteIndexes.Count == 0)
                return BadRequest("No notes specified");

            if (string.IsNullOrEmpty(request.Abbr))
                return BadRequest("Expression abbreviation is required");

            var track = project.tracks[part.trackNo];
            
            float?[] vals = request.Values?.ToArray() ?? new float?[] { null };

            DocManager.Inst.StartUndoGroup();
            int count = 0;
            foreach (var idx in request.NoteIndexes)
            {
                if (idx < 0 || idx >= part.notes.Count) continue;
                var note = part.notes.ElementAt(idx);

                DocManager.Inst.ExecuteCmd(new SetNoteExpressionCommand(project, track, part, note, request.Abbr, vals));
                count++;
            }
            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = "Expression updated", count = count });
        }
        
        [HttpGet("expressions/info")]
        public IActionResult GetExpressionsInfo([FromQuery] int trackIndex)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");
            if (trackIndex < 0 || trackIndex >= project.tracks.Count)
                return BadRequest("Invalid track index");
                
            var track = project.tracks[trackIndex];
            var exps = project.expressions.Values.Select(e => new {
                name = e.name,
                abbr = e.abbr,
                type = e.type.ToString(),
                min = e.min,
                max = e.max,
                defaultValue = e.defaultValue,
                isVoiceColor = e.abbr == OpenUtau.Core.Format.Ustx.CLR
            }).ToList();
            
            var colors = track.VoiceColorNames;
            
            return Ok(new {
                expressions = exps,
                voiceColors = colors
            });
        }
    }
}
