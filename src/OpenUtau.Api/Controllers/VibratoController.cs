using System.Linq;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/project/note/{partNo}/{noteIndex}/[controller]")]
    public class VibratoController : ControllerBase
    {
        private UVoicePart? GetPart(int partNo)
        {
            var project = DocManager.Inst.Project;
            if (project == null || partNo < 0 || partNo >= project.parts.Count) return null;
            return project.parts[partNo] as UVoicePart;
        }

        private UNote? GetNote(UVoicePart part, int noteIndex)
        {
            if (noteIndex < 0 || noteIndex >= part.notes.Count) return null;
            return part.notes.ElementAtOrDefault(noteIndex);
        }

        [HttpGet]
        public IActionResult GetVibrato(int partNo, int noteIndex)
        {
            var part = GetPart(partNo);
            if (part == null) return NotFound("Part not found");
            var note = GetNote(part, noteIndex);
            if (note == null) return NotFound("Note not found");

            return Ok(new
            {
                length = note.vibrato.length,
                period = note.vibrato.period,
                depth = note.vibrato.depth,
                fadeIn = note.vibrato.@in,
                fadeOut = note.vibrato.@out,
                shift = note.vibrato.shift,
                drift = note.vibrato.drift,
                volLink = note.vibrato.volLink
            });
        }

        [HttpPost("length")]
        public IActionResult SetLength(int partNo, int noteIndex, [FromQuery] float value)
        {
            var part = GetPart(partNo);
            if (part == null) return NotFound("Part not found");
            var note = GetNote(part, noteIndex);
            if (note == null) return NotFound("Note not found");

            DocManager.Inst.StartUndoGroup("vibrato.length", true);
            DocManager.Inst.ExecuteCmd(new VibratoLengthCommand(part, note, value));
            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = "Vibrato length updated", value = value });
        }

        [HttpPost("fade-in")]
        public IActionResult SetFadeIn(int partNo, int noteIndex, [FromQuery] float value)
        {
            var part = GetPart(partNo);
            if (part == null) return NotFound("Part not found");
            var note = GetNote(part, noteIndex);
            if (note == null) return NotFound("Note not found");

            DocManager.Inst.StartUndoGroup("vibrato.fade-in", true);
            DocManager.Inst.ExecuteCmd(new VibratoFadeInCommand(part, note, value));
            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = "Vibrato fade-in updated", value = value });
        }

        [HttpPost("fade-out")]
        public IActionResult SetFadeOut(int partNo, int noteIndex, [FromQuery] float value)
        {
            var part = GetPart(partNo);
            if (part == null) return NotFound("Part not found");
            var note = GetNote(part, noteIndex);
            if (note == null) return NotFound("Note not found");

            DocManager.Inst.StartUndoGroup("vibrato.fade-out", true);
            DocManager.Inst.ExecuteCmd(new VibratoFadeOutCommand(part, note, value));
            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = "Vibrato fade-out updated", value = value });
        }

        [HttpPost("depth")]
        public IActionResult SetDepth(int partNo, int noteIndex, [FromQuery] float value)
        {
            var part = GetPart(partNo);
            if (part == null) return NotFound("Part not found");
            var note = GetNote(part, noteIndex);
            if (note == null) return NotFound("Note not found");

            DocManager.Inst.StartUndoGroup("vibrato.depth", true);
            DocManager.Inst.ExecuteCmd(new VibratoDepthCommand(part, note, value));
            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = "Vibrato depth updated", value = value });
        }

        [HttpPost("period")]
        public IActionResult SetPeriod(int partNo, int noteIndex, [FromQuery] float value)
        {
            var part = GetPart(partNo);
            if (part == null) return NotFound("Part not found");
            var note = GetNote(part, noteIndex);
            if (note == null) return NotFound("Note not found");

            DocManager.Inst.StartUndoGroup("vibrato.period", true);
            DocManager.Inst.ExecuteCmd(new VibratoPeriodCommand(part, note, value));
            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = "Vibrato period updated", value = value });
        }

        [HttpPost("shift")]
        public IActionResult SetShift(int partNo, int noteIndex, [FromQuery] float value)
        {
            var part = GetPart(partNo);
            if (part == null) return NotFound("Part not found");
            var note = GetNote(part, noteIndex);
            if (note == null) return NotFound("Note not found");

            DocManager.Inst.StartUndoGroup("vibrato.shift", true);
            DocManager.Inst.ExecuteCmd(new VibratoShiftCommand(part, note, value));
            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = "Vibrato shift updated", value = value });
        }

        [HttpPost("drift")]
        public IActionResult SetDrift(int partNo, int noteIndex, [FromQuery] float value)
        {
            var part = GetPart(partNo);
            if (part == null) return NotFound("Part not found");
            var note = GetNote(part, noteIndex);
            if (note == null) return NotFound("Note not found");

            DocManager.Inst.StartUndoGroup("vibrato.drift", true);
            DocManager.Inst.ExecuteCmd(new VibratoDriftCommand(part, note, value));
            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = "Vibrato drift updated", value = value });
        }
        [HttpPost("volume-link")]
        public IActionResult SetVolumeLink(int partNo, int noteIndex, [FromQuery] float value)
        {
            var part = GetPart(partNo);
            if (part == null) return NotFound("Part not found");
            var note = GetNote(part, noteIndex);
            if (note == null) return NotFound("Note not found");

            DocManager.Inst.StartUndoGroup("vibrato.volLink", true);
            DocManager.Inst.ExecuteCmd(new VibratoVolumeLinkCommand(part, note, value));
            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = "Vibrato volume link updated", value = value });
        }
    }
}
