using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Editing;
using OpenUtau.Core.Render;
using System.Linq;
using System.Collections.Generic;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/project")]
    public class PitchCurveController : ControllerBase
    {
        public class BakePitchRequest
        {
            public List<int>? NoteIndexes { get; set; }
        }

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

        [HttpPost("part/{partNo}/pitch/bake")]
        public IActionResult BakePitch(int partNo, [FromBody] BakePitchRequest? request = null)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");
            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest("Invalid partNo");

            if (project.parts[partNo] is not UVoicePart part) return BadRequest("Not a voice part");

            var track = project.tracks.ElementAtOrDefault(part.trackNo);
            if (track == null) return BadRequest("Track not found");

            if (part.renderPhrases == null || part.renderPhrases.Count == 0)
            {
                project.ValidateFull();
            }

            if ((part.renderPhrases == null || part.renderPhrases.Count == 0) && part.phonemes.Count > 0)
            {
                part.renderPhrases = RenderPhrase.FromPart(project, track, part);
            }

            if (part.renderPhrases == null || part.renderPhrases.Count == 0)
            {
                return BadRequest(new { error = "Pitch baking requires rendered phrases. Load or validate the part first." });
            }

            var notes = ResolveNotesForBaking(part, request, out var selectionError);
            if (selectionError != null)
            {
                return BadRequest(new { error = selectionError });
            }

            if (notes.Count == 0)
            {
                return BadRequest(new { error = "No notes available for pitch baking." });
            }

            var bakePitch = new BakePitch();
            bakePitch.Run(project, part, notes, DocManager.Inst);

            return Ok(new
            {
                message = "Pitch baking completed",
                bakedNoteCount = notes.Count,
                renderPhraseCount = part.renderPhrases.Count
            });
        }

        private static List<UNote> ResolveNotesForBaking(UVoicePart part, BakePitchRequest? request, out string? selectionError)
        {
            selectionError = null;
            var notes = part.notes.ToList();
            if (request?.NoteIndexes != null)
            {
                if (request.NoteIndexes.Count == 0)
                {
                    selectionError = "noteIndexes cannot be empty.";
                    return new List<UNote>();
                }

                var selectedNotes = new List<UNote>();
                foreach (var noteIndex in request.NoteIndexes)
                {
                    if (noteIndex < 0 || noteIndex >= notes.Count)
                    {
                        selectionError = $"Invalid noteIndex: {noteIndex}";
                        return new List<UNote>();
                    }
                    selectedNotes.Add(notes[noteIndex]);
                }
                return selectedNotes;
            }

            if (SelectionManager.Current.PartTrackNo == part.trackNo && SelectionManager.Current.PartPosition == part.position)
            {
                var selected = SelectionManager.GetSelectedNotes(part);
                if (selected.Count > 0)
                {
                    return selected;
                }
            }

            return notes;
        }

        [HttpGet("part/{partNo}/pitch/rendered")]
        public IActionResult GetRenderedPitchCurve(int partNo)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");
            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest("Invalid partNo");
            if (project.parts[partNo] is not UVoicePart part) return BadRequest("Not a voice part");

            var track = project.tracks.ElementAtOrDefault(part.trackNo);
            if (track == null) return BadRequest("Track not found");

            var renderer = track.RendererSettings.Renderer;
            if (renderer == null || !renderer.SupportsRenderPitch)
            {
                return BadRequest(new { error = "Current renderer does not support rendered pitch data." });
            }

            if (part.renderPhrases == null || part.renderPhrases.Count == 0)
            {
                return Ok(new
                {
                    partNo,
                    partName = part.name,
                    trackNo = part.trackNo,
                    renderer = renderer.ToString(),
                    phrases = new object[0]
                });
            }

            var phrases = new List<object>();
            foreach (var phrase in part.renderPhrases)
            {
                var pitch = renderer.LoadRenderedPitch(phrase);
                if (pitch == null || pitch.ticks == null || pitch.tones == null)
                {
                    continue;
                }

                var points = pitch.ticks.Zip(pitch.tones, (tick, tone) => new
                {
                    relativeTick = tick,
                    partTick = phrase.position - part.position + tick,
                    absoluteTick = phrase.position + tick,
                    tone = tone,
                    frequencyHz = 440.0 * System.Math.Pow(2.0, (tone - 69.0) / 12.0)
                }).ToArray();

                phrases.Add(new
                {
                    phrasePosition = phrase.position,
                    phraseDuration = phrase.duration,
                    phraseLeading = phrase.leading,
                    noteCount = phrase.notes.Length,
                    notes = phrase.notes.Select(n => new
                    {
                        lyric = n.lyric,
                        tone = n.tone,
                        position = n.position,
                        duration = n.duration,
                        end = n.end
                    }),
                    points,
                    pointCount = points.Length
                });
            }

            return Ok(new
            {
                partNo,
                partName = part.name,
                trackNo = part.trackNo,
                renderer = renderer.ToString(),
                phraseCount = phrases.Count,
                phrases
            });
        }
    }
}