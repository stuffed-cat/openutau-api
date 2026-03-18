using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using System.Collections.Generic;
using System.Linq;

namespace OpenUtau.Api.Controllers
{
    public class SelectionStateInfo
    {
        public int PartTrackNo { get; set; } = -1;
        public int PartPosition { get; set; } = -1;
        public List<int> SelectedNoteIndexes { get; set; } = new List<int>();
    }

    public static class SelectionManager
    {
        public static SelectionStateInfo Current { get; set; } = new SelectionStateInfo();
        
        public static UVoicePart? GetActivePart(UProject project) 
        {
            if (project == null || Current.PartTrackNo < 0) return null;
            return project.parts.FirstOrDefault(p => p is UVoicePart && p.trackNo == Current.PartTrackNo && p.position == Current.PartPosition) as UVoicePart;
        }

        public static List<UNote> GetSelectedNotes(UVoicePart part)
        {
            if (part == null) return new List<UNote>();
            var results = new List<UNote>();
            var notesList = part.notes.ToList();
            foreach(var idx in Current.SelectedNoteIndexes)
            {
                if (idx >= 0 && idx < notesList.Count)
                    results.Add(notesList[idx]);
            }
            return results;
        }
    }

    public class NotesSelectionRequest
    {
        public int PartTrackNo { get; set; }
        public int PartPosition { get; set; }
        public List<int>? NoteIndexes { get; set; }
        public int? StartTick { get; set; }
        public int? EndTick { get; set; }
    }

    public class NotesMoveRequest
    {
        public int DeltaTick { get; set; }
        public int DeltaTone { get; set; }
    }

    public class NotesResizeRequest
    {
        public int DeltaDuration { get; set; }
    }

    public class NotesLyricsRequest
    {
        public string? Lyric { get; set; }
        public List<string>? Lyrics { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class SelectionController : ControllerBase
    {
        [HttpPost("notes/set")]
        public IActionResult SetNotesSelection([FromBody] NotesSelectionRequest request)
        {
            if (DocManager.Inst.Project == null) return BadRequest("No project loaded");

            SelectionManager.Current.PartTrackNo = request.PartTrackNo;
            SelectionManager.Current.PartPosition = request.PartPosition;

            var part = SelectionManager.GetActivePart(DocManager.Inst.Project);
            if (part == null) return NotFound("Active part not found");

            if (request.StartTick.HasValue && request.EndTick.HasValue)
            {
                var notesList = part.notes.ToList();
                var indexes = new List<int>();
                for (int i = 0; i < notesList.Count; i++)
                {
                    var n = notesList[i];
                    if (n.position >= request.StartTick.Value && n.position < request.EndTick.Value)
                    {
                        indexes.Add(i);
                    }
                }
                SelectionManager.Current.SelectedNoteIndexes = indexes;
            }
            else
            {
                SelectionManager.Current.SelectedNoteIndexes = request.NoteIndexes ?? new List<int>();
            }
            
            return Ok(new { message = "Selection set", selected_count = SelectionManager.Current.SelectedNoteIndexes.Count });
        }

        [HttpGet("notes")]
        public IActionResult GetNotesSelection()
        {
            if (DocManager.Inst.Project == null) return BadRequest("No project loaded");
            var part = SelectionManager.GetActivePart(DocManager.Inst.Project);
            if (part == null) return NotFound("Active part not found");
            
            var notes = SelectionManager.GetSelectedNotes(part);
            return Ok(notes.Select(n => new {
                position = n.position,
                end = n.End,
                duration = n.duration,
                lyric = n.lyric,
                tone = n.tone
            }));
        }

        [HttpPost("notes/move")]
        public IActionResult MoveNotes([FromBody] NotesMoveRequest request)
        {
            if (DocManager.Inst.Project == null) return BadRequest("No project loaded");
            var part = SelectionManager.GetActivePart(DocManager.Inst.Project);
            if (part == null) return NotFound("Active part not found");
            
            var notes = SelectionManager.GetSelectedNotes(part);
            if (notes.Count == 0) return BadRequest("No notes selected");

            DocManager.Inst.StartUndoGroup("Batch move notes");
            DocManager.Inst.ExecuteCmd(new MoveNoteCommand(part, notes, request.DeltaTick, request.DeltaTone));
            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = "Notes moved", count = notes.Count });
        }

        [HttpPost("notes/resize")]
        public IActionResult ResizeNotes([FromBody] NotesResizeRequest request)
        {
            if (DocManager.Inst.Project == null) return BadRequest("No project loaded");
            var part = SelectionManager.GetActivePart(DocManager.Inst.Project);
            if (part == null) return NotFound("Active part not found");

            var notes = SelectionManager.GetSelectedNotes(part);
            if (notes.Count == 0) return BadRequest("No notes selected");

            DocManager.Inst.StartUndoGroup("Batch resize notes");
            DocManager.Inst.ExecuteCmd(new ResizeNoteCommand(part, notes, request.DeltaDuration));
            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = "Notes resized", count = notes.Count });
        }

        [HttpPost("notes/lyrics")]
        public IActionResult ChangeLyrics([FromBody] NotesLyricsRequest request)
        {
            if (DocManager.Inst.Project == null) return BadRequest("No project loaded");
            var part = SelectionManager.GetActivePart(DocManager.Inst.Project);
            if (part == null) return NotFound("Active part not found");

            var notes = SelectionManager.GetSelectedNotes(part);
            if (notes.Count == 0) return BadRequest("No notes selected");

            DocManager.Inst.StartUndoGroup("Batch change lyrics");
            for (int i = 0; i < notes.Count; i++)
            {
                var note = notes[i];
                var newLyric = (request.Lyrics != null && i < request.Lyrics.Count) ? request.Lyrics[i] : (request.Lyric ?? note.lyric);
                if (newLyric != note.lyric) {
                    DocManager.Inst.ExecuteCmd(new ChangeNoteLyricCommand(part, note, newLyric));
                }
            }
            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = "Lyrics changed", count = notes.Count });
        }
    }
}
