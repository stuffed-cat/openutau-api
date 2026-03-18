using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Editing;
using System.Linq;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/project/notes/{partNo}/{noteIndex}")]
    public class PhonemesController : ControllerBase
    {
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
