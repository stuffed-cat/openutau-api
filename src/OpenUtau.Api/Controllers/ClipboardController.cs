using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using System.Linq;
using System.Collections.Generic;

namespace OpenUtau.Api.Controllers {

    [ApiController]
    [Route("api/[controller]")]
    public class ClipboardController : ControllerBase {

        public class NoteActionRequest {
            public int PartTrackNo { get; set; }
            public int PartPosition { get; set; }
            public List<int>? NoteIndexes { get; set; }
        }

        public class PasteNotesRequest {
            public int PartTrackNo { get; set; }
            public int PartPosition { get; set; }
            public int PasteTick { get; set; }
        }

        public class PartActionRequest {
            public List<PartIdentifier>? Parts { get; set; }
        }

        public class PartIdentifier {
            public int TrackNo { get; set; }
            public int Position { get; set; }
        }

        [HttpGet("debug_parts")]
        public IActionResult DebugParts() {
            if (DocManager.Inst.Project == null) return NotFound("No project");
            return Ok(DocManager.Inst.Project.parts.Select(p => new {
                p.trackNo, p.position, p.name, type = p.GetType().Name
            }));
        }

        private UVoicePart? FindPart(int trackNo, int position) {
            return DocManager.Inst.Project?.parts.FirstOrDefault(p => p is UVoicePart && p.trackNo == trackNo && p.position == position) as UVoicePart;
        }

        private UPart? FindGenericPart(int trackNo, int position) {
            return DocManager.Inst.Project?.parts.FirstOrDefault(p => p.trackNo == trackNo && p.position == position);
        }

        [HttpPost("notes/copy")]
        public IActionResult CopyNotes([FromBody] NoteActionRequest request) {
            if (DocManager.Inst.Project == null) return BadRequest("Project not loaded");
            
            var part = FindPart(request.PartTrackNo, request.PartPosition);
            if (part == null) return NotFound("Voice part not found");

            if (request.NoteIndexes == null || !request.NoteIndexes.Any()) {
                return BadRequest("No notes specified");
            }

            var selectedNotes = new List<UNote>();
            foreach (var idx in request.NoteIndexes) {
                if (idx >= 0 && idx < part.notes.Count) {
                    selectedNotes.Add(part.notes.ElementAt(idx));
                }
            }

            DocManager.Inst.NotesClipboard = selectedNotes.Select(note => note.Clone()).ToList();
            return Ok(new { message = "Notes copied to clipboard", count = selectedNotes.Count });
        }

        [HttpPost("notes/cut")]
        public IActionResult CutNotes([FromBody] NoteActionRequest request) {
            if (DocManager.Inst.Project == null) return BadRequest("Project not loaded");
            
            var part = FindPart(request.PartTrackNo, request.PartPosition);
            if (part == null) return NotFound("Voice part not found");

            if (request.NoteIndexes == null || !request.NoteIndexes.Any()) {
                return BadRequest("No notes specified");
            }

            var selectedNotes = new List<UNote>();
            foreach (var idx in request.NoteIndexes) {
                if (idx >= 0 && idx < part.notes.Count) {
                    selectedNotes.Add(part.notes.ElementAt(idx));
                }
            }

            DocManager.Inst.NotesClipboard = selectedNotes.Select(note => note.Clone()).ToList();
            
            DocManager.Inst.StartUndoGroup("command.note.delete");
            DocManager.Inst.ExecuteCmd(new OpenUtau.Core.RemoveNoteCommand(part, selectedNotes));
            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = "Notes cut to clipboard", count = selectedNotes.Count });
        }

        [HttpPost("notes/paste")]
        public IActionResult PasteNotes([FromBody] PasteNotesRequest request) {
            if (DocManager.Inst.Project == null) return BadRequest("Project not loaded");
            if (DocManager.Inst.NotesClipboard == null || DocManager.Inst.NotesClipboard.Count == 0) {
                return BadRequest("Notes clipboard is empty");
            }

            var part = FindPart(request.PartTrackNo, request.PartPosition);
            if (part == null) return NotFound("Voice part not found");

            int minPosition = DocManager.Inst.NotesClipboard.Select(n => n.position).Min();
            
            if (request.PasteTick < part.position) {
                return BadRequest("Paste tick is before part start");
            }

            int offset = request.PasteTick - minPosition - part.position;
            var notes = DocManager.Inst.NotesClipboard.Select(note => note.Clone()).ToList();

            DocManager.Inst.StartUndoGroup("command.note.paste");
            foreach (var note in notes) {
                note.position += offset;
                DocManager.Inst.ExecuteCmd(new OpenUtau.Core.AddNoteCommand(part, note));
            }
            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = "Notes pasted", count = notes.Count });
        }

        [HttpPost("parts/copy")]
        public IActionResult CopyParts([FromBody] PartActionRequest request) {
            if (DocManager.Inst.Project == null) return BadRequest("Project not loaded");
            
            if (request.Parts == null || !request.Parts.Any()) {
                return BadRequest("No parts specified");
            }

            var selectedParts = new List<UPart>();
            foreach (var p in request.Parts) {
                var found = FindGenericPart(p.TrackNo, p.Position);
                if (found != null) selectedParts.Add(found);
            }

            DocManager.Inst.PartsClipboard = selectedParts.Select(part => part.Clone()).ToList();
            return Ok(new { message = "Parts copied to clipboard", count = selectedParts.Count });
        }

        [HttpPost("parts/cut")]
        public IActionResult CutParts([FromBody] PartActionRequest request) {
            if (DocManager.Inst.Project == null) return BadRequest("Project not loaded");
            
            if (request.Parts == null || !request.Parts.Any()) {
                return BadRequest("No parts specified");
            }

            var selectedParts = new List<UPart>();
            foreach (var p in request.Parts) {
                var found = FindGenericPart(p.TrackNo, p.Position);
                if (found != null) selectedParts.Add(found);
            }

            DocManager.Inst.PartsClipboard = selectedParts.Select(part => part.Clone()).ToList();

            DocManager.Inst.StartUndoGroup("command.part.delete");
            foreach (var part in selectedParts) {
                DocManager.Inst.ExecuteCmd(new OpenUtau.Core.RemovePartCommand(DocManager.Inst.Project, part));
            }
            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = "Parts cut to clipboard", count = selectedParts.Count });
        }

        [HttpPost("parts/paste")]
        public IActionResult PasteParts() {
            if (DocManager.Inst.Project == null) return BadRequest("Project not loaded");
            var proj = DocManager.Inst.Project;

            if (DocManager.Inst.PartsClipboard == null || DocManager.Inst.PartsClipboard.Count == 0) {
                return BadRequest("Parts clipboard is empty");
            }

            var parts = DocManager.Inst.PartsClipboard
                .Select(part => part.Clone())
                .OrderBy(part => part.trackNo).ToList();

            int newTrackNo = proj.parts.Count > 0 ? proj.parts.Max(part => part.trackNo) : -1;
            int oldTrackNo = -1;

            foreach (var part in parts) {
                if (part.trackNo > oldTrackNo) {
                    oldTrackNo = part.trackNo;
                    newTrackNo++;
                }
                part.trackNo = newTrackNo;
            }

            DocManager.Inst.StartUndoGroup("command.part.paste");

            while (proj.tracks.Count <= newTrackNo) {
                DocManager.Inst.ExecuteCmd(new OpenUtau.Core.AddTrackCommand(proj, new UTrack(proj) {
                    TrackNo = proj.tracks.Count
                }));
            }

            foreach (var part in parts) {
                DocManager.Inst.ExecuteCmd(new OpenUtau.Core.AddPartCommand(proj, part));
            }

            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = "Parts pasted", count = parts.Count });
        }
    }
}
