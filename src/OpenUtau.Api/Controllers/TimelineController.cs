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
        [HttpGet("tempos")]
        public IActionResult GetTempos()
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("Project not loaded.");

            return Ok(project.tempos.Select(t => new { t.position, t.bpm }));
        }

        [HttpPost("tempos")]
        public IActionResult AddTempo([FromBody] TempoChangeRequest request)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("Project not loaded.");

            DocManager.Inst.ExecuteCmd(new AddTempoChangeCommand(project, request.Position, request.Bpm));
            return Ok(new { success = true });
        }

        [HttpDelete("tempos/{position}")]
        public IActionResult DeleteTempo(int position)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("Project not loaded.");

            DocManager.Inst.ExecuteCmd(new DelTempoChangeCommand(project, position));
            return Ok(new { success = true });
        }

        [HttpGet("time-signatures")]
        public IActionResult GetTimeSignatures()
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("Project not loaded.");

            return Ok(project.timeSignatures.Select(ts => new { ts.barPosition, ts.beatPerBar, ts.beatUnit }));
        }

        [HttpPost("time-signatures")]
        public IActionResult AddTimeSignature([FromBody] TimeSignatureRequest request)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("Project not loaded.");

            DocManager.Inst.ExecuteCmd(new AddTimeSigCommand(project, request.BarPosition, request.BeatPerBar, request.BeatUnit));
            return Ok(new { success = true });
        }

        [HttpDelete("time-signatures/{barPosition}")]
        public IActionResult DeleteTimeSignature(int barPosition)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("Project not loaded.");

            DocManager.Inst.ExecuteCmd(new DelTimeSigCommand(project, barPosition));
            return Ok(new { success = true });
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
