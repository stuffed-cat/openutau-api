using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using System.Linq;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TimelineController : ControllerBase
    {
        // ================= BPM ================= //

        [HttpPost("bpm")]
        public IActionResult SetBpm([FromQuery] double bpm)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("Project not loaded.");

            DocManager.Inst.StartUndoGroup("Set BPM");
            DocManager.Inst.ExecuteCmd(new BpmCommand(project, bpm));
            DocManager.Inst.EndUndoGroup();
            return Ok(new { success = true, bpm });
        }

        // ================= Tempo ================= //

        [HttpPost("tempo/add")]
        public IActionResult AddTempo([FromBody] TempoChangeRequest request)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("Project not loaded.");

            DocManager.Inst.StartUndoGroup("Add tempo");
            DocManager.Inst.ExecuteCmd(new AddTempoChangeCommand(project, request.Position, request.Bpm));
            DocManager.Inst.EndUndoGroup();
            return Ok(new { success = true });
        }

        [HttpPut("tempo/{position}")]
        public IActionResult EditTempo(int position, [FromBody] TempoChangeRequest request)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("Project not loaded.");

            var existing = project.tempos.FirstOrDefault(t => t.position == position);
            if (existing == null) return NotFound("Tempo change not found at this position.");

            DocManager.Inst.StartUndoGroup("Edit tempo");
            DocManager.Inst.ExecuteCmd(new DelTempoChangeCommand(project, position));
            DocManager.Inst.ExecuteCmd(new AddTempoChangeCommand(project, position, request.Bpm));
            DocManager.Inst.EndUndoGroup();
            return Ok(new { success = true });
        }

        [HttpDelete("tempo/{position}")]
        public IActionResult DeleteTempo(int position)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("Project not loaded.");

            // Cannot delete the initial tempo (position 0)
            if (position == 0) return BadRequest("Cannot delete initial tempo change.");

            DocManager.Inst.StartUndoGroup("Delete tempo");
            DocManager.Inst.ExecuteCmd(new DelTempoChangeCommand(project, position));
            DocManager.Inst.EndUndoGroup();
            return Ok(new { success = true });
        }

        // ================= Time Signature ================= //

        [HttpPost("timesig/add")]
        public IActionResult AddTimeSignature([FromBody] TimeSignatureRequest request)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("Project not loaded.");

            DocManager.Inst.StartUndoGroup("Add time signature");
            DocManager.Inst.ExecuteCmd(new AddTimeSigCommand(project, request.BarPosition, request.BeatPerBar, request.BeatUnit));
            DocManager.Inst.EndUndoGroup();
            return Ok(new { success = true });
        }

        [HttpPut("timesig/{position}")]
        public IActionResult EditTimeSignature(int position, [FromBody] TimeSignatureRequest request)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("Project not loaded.");

            var existing = project.timeSignatures.FirstOrDefault(ts => ts.barPosition == position);
            if (existing == null) return NotFound("Time signature not found at this position.");

            DocManager.Inst.StartUndoGroup("Edit time signature");
            DocManager.Inst.ExecuteCmd(new DelTimeSigCommand(project, position));
            DocManager.Inst.ExecuteCmd(new AddTimeSigCommand(project, position, request.BeatPerBar, request.BeatUnit));
            DocManager.Inst.EndUndoGroup();
            return Ok(new { success = true });
        }

        [HttpDelete("timesig/{position}")]
        public IActionResult DeleteTimeSignature(int position)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("Project not loaded.");

            if (position == 0) return BadRequest("Cannot delete initial time signature.");

            DocManager.Inst.StartUndoGroup("Delete time signature");
            DocManager.Inst.ExecuteCmd(new DelTimeSigCommand(project, position));
            DocManager.Inst.EndUndoGroup();
            return Ok(new { success = true });
        }

        // ================= Time Conversions ================= //

        [HttpGet("tick-to-ms/{tick}")]
        public IActionResult TickToMs(int tick)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("Project not loaded.");

            double ms = project.timeAxis.TickPosToMsPos(tick);
            return Ok(new { tick, ms });
        }

        [HttpGet("ms-to-tick/{ms}")]
        public IActionResult MsToTick(double ms)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("Project not loaded.");

            int tick = project.timeAxis.MsPosToTickPos(ms);
            return Ok(new { ms, tick });
        }
    }

    public class TempoChangeRequest
    {
        public int Position { get; set; }
        public double Bpm { get; set; }
    }

    public class TimeSignatureRequest
    {
        public int BarPosition { get; set; }
        public int BeatPerBar { get; set; }
        public int BeatUnit { get; set; }
    }
}
