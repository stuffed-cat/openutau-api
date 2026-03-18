using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Editing;
using System.Linq;
using System.Collections.Generic;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/project")]
    public class PitchCurveController : ControllerBase
    {
        [HttpGet("note/{partNo}/{noteIndex}/pitch/points")]
        public IActionResult GetPitchCurve(int partNo, int noteIndex)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");
            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest("Invalid partNo");

            if (project.parts[partNo] is not UVoicePart part) return BadRequest("Not a voice part");
            if (noteIndex < 0 || noteIndex >= part.notes.Count) return BadRequest("Invalid noteIndex");

            var note = part.notes.ElementAt(noteIndex);
            
            return Ok(new
            {
                snapFirst = note.pitch.snapFirst,
                points = note.pitch.data.Select((p, i) => new
                {
                    index = i,
                    x = p.X,
                    y = p.Y,
                    shape = p.shape.ToString(),
                    autoCompleted = p.autoCompleted
                })
            });
        }

        public class PitchPointRequest
        {
            public float X { get; set; }
            public float Y { get; set; }
            public string Shape { get; set; } = "io";
        }

        [HttpPost("note/{partNo}/{noteIndex}/pitch/point/add")]
        public IActionResult AddPitchPoint(int partNo, int noteIndex, [FromBody] PitchPointRequest request, [FromQuery] int pointIndex = -1)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");
            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest("Invalid partNo");

            if (project.parts[partNo] is not UVoicePart part) return BadRequest("Not a voice part");
            if (noteIndex < 0 || noteIndex >= part.notes.Count) return BadRequest("Invalid noteIndex");

            var note = part.notes.ElementAt(noteIndex);
            
            PitchPointShape shapeEnum = PitchPointShape.io;
            if (System.Enum.TryParse<PitchPointShape>(request.Shape, true, out var parsed)) {
                shapeEnum = parsed;
            }

            var point = new PitchPoint(request.X, request.Y, shapeEnum);
            int idx = pointIndex;
            if (idx < 0) {
                idx = note.pitch.data.Count;
            }

            DocManager.Inst.StartUndoGroup("Add pitch point", true);
            DocManager.Inst.ExecuteCmd(new AddPitchPointCommand(part, note, point, idx));
            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = "Pitch point added", index = idx });
        }

        [HttpDelete("note/{partNo}/{noteIndex}/pitch/point/{pointIndex}")]
        public IActionResult DeletePitchPoint(int partNo, int noteIndex, int pointIndex)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");
            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest("Invalid partNo");

            if (project.parts[partNo] is not UVoicePart part) return BadRequest("Not a voice part");
            if (noteIndex < 0 || noteIndex >= part.notes.Count) return BadRequest("Invalid noteIndex");

            var note = part.notes.ElementAt(noteIndex);
            if (pointIndex < 0 || pointIndex >= note.pitch.data.Count) return BadRequest("Invalid pointIndex");

            DocManager.Inst.StartUndoGroup("Delete pitch point", true);
            DocManager.Inst.ExecuteCmd(new DeletePitchPointCommand(part, note, pointIndex));
            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = "Pitch point deleted" });
        }

        public class MovePointRequest
        {
            public float X { get; set; }
            public float Y { get; set; }
        }

        [HttpPost("note/{partNo}/{noteIndex}/pitch/point/{pointIndex}/move")]
        public IActionResult MovePitchPoint(int partNo, int noteIndex, int pointIndex, [FromBody] MovePointRequest request)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");
            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest("Invalid partNo");

            if (project.parts[partNo] is not UVoicePart part) return BadRequest("Not a voice part");
            if (noteIndex < 0 || noteIndex >= part.notes.Count) return BadRequest("Invalid noteIndex");

            var note = part.notes.ElementAt(noteIndex);
            if (pointIndex < 0 || pointIndex >= note.pitch.data.Count) return BadRequest("Invalid pointIndex");

            DocManager.Inst.StartUndoGroup("Move pitch point", true);
            var pt = note.pitch.data[pointIndex];
            float deltaX = request.X - pt.X;
            float deltaY = request.Y - pt.Y;
            DocManager.Inst.ExecuteCmd(new MovePitchPointCommand(part, pt, deltaX, deltaY));
            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = "Pitch point moved" });
        }

        [HttpPost("note/{partNo}/{noteIndex}/pitch/snap")]
        public IActionResult SnapPitchPoint(int partNo, int noteIndex)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");
            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest("Invalid partNo");

            if (project.parts[partNo] is not UVoicePart part) return BadRequest("Not a voice part");
            if (noteIndex < 0 || noteIndex >= part.notes.Count) return BadRequest("Invalid noteIndex");

            var note = part.notes.ElementAt(noteIndex);

            DocManager.Inst.StartUndoGroup("Snap pitch point", true);
            DocManager.Inst.ExecuteCmd(new SnapPitchPointCommand(part, note));
            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = "Toggle pitch snap completed", snapFirst = note.pitch.snapFirst });
        }

        public class ChangeShapeRequest
        {
            public string Shape { get; set; } = "io";
        }

        [HttpPost("note/{partNo}/{noteIndex}/pitch/shape/{pointIndex}")]
        public IActionResult SetPitchShape(int partNo, int noteIndex, int pointIndex, [FromBody] ChangeShapeRequest request)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");
            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest("Invalid partNo");

            if (project.parts[partNo] is not UVoicePart part) return BadRequest("Not a voice part");
            if (noteIndex < 0 || noteIndex >= part.notes.Count) return BadRequest("Invalid noteIndex");

            var note = part.notes.ElementAt(noteIndex);
            if (pointIndex < 0 || pointIndex >= note.pitch.data.Count) return BadRequest("Invalid pointIndex");

            PitchPointShape shapeEnum = PitchPointShape.io;
            if (System.Enum.TryParse<PitchPointShape>(request.Shape, true, out var parsed)) {
                shapeEnum = parsed;
            }

            DocManager.Inst.StartUndoGroup("Change pitch point shape", true);
            DocManager.Inst.ExecuteCmd(new ChangePitchPointShapeCommand(part, note.pitch.data[pointIndex], shapeEnum));
            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = "Shape changed", pointIndex = pointIndex, shape = shapeEnum.ToString() });
        }

        [HttpPost("part/{partNo}/pitch/sync")]
        public IActionResult SyncPitch(int partNo)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");
            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest("Invalid partNo");

            if (project.parts[partNo] is not UVoicePart part) return BadRequest("Not a voice part");

            // Execute LoadRenderedPitch batch edit on all notes in the part
            var loadEdit = new LoadRenderedPitch();
            DocManager.Inst.StartUndoGroup("Load rendered pitch", true);
            loadEdit.Run(project, part, part.notes.ToList(), DocManager.Inst);
            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = "Pitch synchronization across part completed" });
        }
    }
}