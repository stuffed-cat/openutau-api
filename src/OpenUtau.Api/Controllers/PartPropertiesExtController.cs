using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/project")]
    public class PartPropertiesExtController : ControllerBase
    {
        /// <summary>
        /// 分割 Part (Split Part at a specific tick position)
        /// </summary>
        [HttpPost("part/{partNo}/split")]
        public IActionResult SplitPart(int partNo, [FromQuery] int splitTick)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest(new { error = "Project not loaded" });
            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest(new { error = "Invalid part index" });

            var part = project.parts[partNo];
            if (!(part is UVoicePart voicePart))
                return BadRequest(new { error = "Can only split voice parts" });

            if (splitTick <= part.position || splitTick >= part.End)
                return BadRequest(new { error = "Split position must be within the part's duration" });

            DocManager.Inst.StartUndoGroup("Split part");

            // Move notes
            var notesToMove = voicePart.notes.Where(n => (part.position + n.position) >= splitTick).ToList();
            if (notesToMove.Count > 0) {
                DocManager.Inst.ExecuteCmd(new RemoveNoteCommand(voicePart, notesToMove));
            }

            // Create new part
            var newPart = new UVoicePart()
            {
                position = splitTick,
                trackNo = part.trackNo,
                Duration = part.End - splitTick
            };
            
            // Add notes to new part
            foreach (var note in notesToMove)
            {
                var newNote = note.Clone();
                int absPos = part.position + note.position;
                newNote.position = absPos - newPart.position;
                newPart.notes.Add(newNote); // No need to use Command here since part isn't added to project yet
            }

            // Adjust original part's duration (resize from end -> false)
            int durChange = splitTick - part.End; 
            DocManager.Inst.ExecuteCmd(new ResizePartCommand(project, part, durChange, false));

            DocManager.Inst.ExecuteCmd(new AddPartCommand(project, newPart));
            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = "Part split successfully", originalPartDuration = part.Duration, newPartDuration = newPart.Duration });
        }

        /// <summary>
        /// 合并 Parts (Merge Multiple Parts)
        /// </summary>
        [HttpPost("parts/merge")]
        public IActionResult MergeParts([FromBody] int[] partIndexes)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest(new { error = "Project not loaded" });
            
            if (partIndexes == null || partIndexes.Length < 2)
                return BadRequest(new { error = "Need at least 2 parts to merge" });

            var partsToMerge = new List<UVoicePart>();
            foreach (var idx in partIndexes)
            {
                if (idx < 0 || idx >= project.parts.Count) return BadRequest(new { error = $"Invalid part index: {idx}" });
                if (!(project.parts[idx] is UVoicePart vp)) return BadRequest(new { error = $"Part {idx} is not a voice part" });
                partsToMerge.Add(vp);
            }

            // check if they are all on the same track
            var trackNo = partsToMerge.First().trackNo;
            if (partsToMerge.Any(p => p.trackNo != trackNo))
                return BadRequest(new { error = "All parts must be on the same track to be merged" });

            var voiceParts = partsToMerge.OrderBy(p => p.position).ToList();
            var firstPart = voiceParts.First();
            
            DocManager.Inst.StartUndoGroup("Merge parts");

            int newEnd = voiceParts.Max(p => p.End);
            int newDur = newEnd - firstPart.position;
            
            var notesToAdd = new List<UNote>();
            for (int i = 1; i < voiceParts.Count; i++)
            {
                var curPart = voiceParts[i];
                foreach (var note in curPart.notes)
                {
                    var cloned = note.Clone();
                    int absPos = curPart.position + note.position;
                    cloned.position = absPos - firstPart.position;
                    notesToAdd.Add(cloned);
                }
                DocManager.Inst.ExecuteCmd(new RemovePartCommand(project, curPart));
            }

            if (notesToAdd.Count > 0) {
                DocManager.Inst.ExecuteCmd(new AddNoteCommand(firstPart, notesToAdd));
            }
            
            // Adjust duration of the first part (resize from end -> false)
            int durDiff = newDur - firstPart.Duration;
            if (durDiff != 0) {
                DocManager.Inst.ExecuteCmd(new ResizePartCommand(project, firstPart, durDiff, false));
            }

            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = "Parts merged successfully", newDuration = firstPart.Duration, newPosition = firstPart.position });
        }

        /// <summary>
        /// 独奏 (Solo Part - essentially Solos the entire Track holding the Part)
        /// </summary>
        [HttpPost("part/{partNo}/solo")]
        public IActionResult SoloPart(int partNo, [FromQuery] bool solo = true)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest(new { error = "Project not loaded" });
            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest(new { error = "Invalid part index" });

            var part = project.parts[partNo];
            var track = project.tracks[part.trackNo];
            
            track.Solo = solo;
            
            // Recalculate mute states across all tracks
            bool hasSolo = project.tracks.Any(t => t.Solo);
            foreach (var t in project.tracks)
            {
                t.Muted = hasSolo ? !t.Solo : t.Mute;
                DocManager.Inst.ExecuteCmd(new VolumeChangeNotification(t.TrackNo, t.Muted ? -24 : t.Volume));
                DocManager.Inst.ExecuteCmd(new PanChangeNotification(t.TrackNo, t.Pan));
            }

            return Ok(new { 
                message = $"Track {part.trackNo} (holding part {partNo}) {(solo ? "soloed" : "un-soloed")} successfully",
                trackNo = part.trackNo,
                solo = track.Solo,
                muted = track.Muted
            });
        }
    }
}
