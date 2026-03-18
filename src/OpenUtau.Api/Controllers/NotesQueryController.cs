using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using System.Linq;
using System.Collections.Generic;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/project")]
    public class NotesQueryController : ControllerBase
    {
        [HttpGet("notes/list")]
        public IActionResult GetAllNotes()
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");

            var result = new List<object>();

            for (int pIndex = 0; pIndex < project.parts.Count; pIndex++)
            {
                var part = project.parts[pIndex] as UVoicePart;
                if (part == null) continue;

                var notesList = part.notes.ToList();
                for (int nIndex = 0; nIndex < notesList.Count; nIndex++)
                {
                    var note = notesList[nIndex];
                    result.Add(new {
                        partNo = pIndex,
                        noteIndex = nIndex,
                        position = note.position,
                        duration = note.duration,
                        tone = note.tone,
                        lyric = note.lyric,
                        phonemes = note.phonemeIndexes.Select(idx => {
                            var p = part.phonemes[idx];
                            return new {
                                p.phoneme,
                                p.position,
                                                                p.preutter,
                                p.overlap,
                                p.tailIntrude,
                                p.tailOverlap
                            };
                        }).ToList()
                    });
                }
            }

            return Ok(result);
        }

        [HttpGet("part/{partNo}/notes")]
        public IActionResult GetPartNotes(int partNo)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");

            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest("Invalid part index");
            var part = project.parts[partNo] as UVoicePart;
            if (part == null) return BadRequest("Not a voice part");

            var result = new List<object>();
            var notesList = part.notes.ToList();
            for (int nIndex = 0; nIndex < notesList.Count; nIndex++)
            {
                var note = notesList[nIndex];
                result.Add(new {
                    noteIndex = nIndex,
                    position = note.position,
                    duration = note.duration,
                    tone = note.tone,
                    lyric = note.lyric,
                    phonemeCount = note.phonemeIndexes.Length
                });
            }

            return Ok(result);
        }

        [HttpGet("part/{partNo}/notes/{noteIndex}")]
        public IActionResult GetNoteDetails(int partNo, int noteIndex)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");

            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest("Invalid part index");
            var part = project.parts[partNo] as UVoicePart;
            if (part == null) return BadRequest("Not a voice part");

            if (noteIndex < 0 || noteIndex >= part.notes.Count) return BadRequest("Invalid note index");
            var note = part.notes.ElementAt(noteIndex);

            return Ok(new {
                position = note.position,
                duration = note.duration,
                tone = note.tone,
                lyric = note.lyric,
                pitchPoints = note.pitch.data.Select(pt => new { x = pt.X, y = pt.Y, shape = pt.shape.ToString() }).ToList(),
                vibrato = new {
                    length = note.vibrato.length,
                    fadeIn = note.vibrato.@in,
                    fadeOut = note.vibrato.@out,
                    depth = note.vibrato.depth,
                    period = note.vibrato.period,
                    shift = note.vibrato.shift,
                    drift = note.vibrato.drift,
                    volumeLink = note.vibrato.volLink
                },
                phonemes = note.phonemeIndexes.Select(idx => {
                    var p = part.phonemes[idx];
                    return new {
                        p.phoneme,
                        p.position,
                                                p.preutter,
                        p.overlap,
                        p.tailIntrude,
                        p.tailOverlap
                    };
                }).ToList(),
                phonemeExpressions = note.phonemeExpressions.Select(e => new { e.abbr, e.value }).ToList(),
                phonemeOverrides = note.phonemeOverrides.Select(o => new { o.phoneme, o.offset, o.preutterDelta, o.overlapDelta }).ToList()
            });
        }
    }
}
