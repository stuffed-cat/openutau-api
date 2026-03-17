using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core.Format;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using OpenUtau.Api.Models;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/project/[controller]")]
    public class ProjectGenerateController : ControllerBase
    {
        [HttpPost]
        public IActionResult Generate([FromBody] ProjectCreateRequest request)
        {
            try
            {
                var project = new UProject();
                if (project.tempos == null || project.tempos.Count == 0) project.tempos = new List<UTempo> { new UTempo(0, request.BPM) };
                else project.tempos[0].bpm = request.BPM;
                
                if (project.timeSignatures == null || project.timeSignatures.Count == 0) project.timeSignatures = new List<UTimeSignature> { new UTimeSignature() { beatPerBar = request.TimeSignatureNumerator, beatUnit = request.TimeSignatureDenominator } };
                else {
                    project.timeSignatures[0].beatPerBar = request.TimeSignatureNumerator;
                    project.timeSignatures[0].beatUnit = request.TimeSignatureDenominator;
                }

                foreach (var trackDef in request.Tracks)
                {
                    var track = new UTrack(project);
                    if (trackDef.SingerId != null)
                        track.Singer = SingerManager.Inst.Singers.Values.FirstOrDefault(s => s.Id == trackDef.SingerId);
                    
                    var factory = DocManager.Inst.PhonemizerFactories.FirstOrDefault(f => f.name == trackDef.Phonemizer);
                    if (factory != null)
                    {
                        track.Phonemizer = factory.Create();
                    }

                    if (trackDef.Renderer != null)
                        track.RendererSettings.renderer = trackDef.Renderer;
                    
                    project.tracks.Add(track);

                    var part = new UVoicePart()
                    {
                        name = "Part",
                        position = 0,
                        trackNo = project.tracks.Count - 1
                    };

                    foreach (var noteDef in trackDef.Notes)
                    {
                        var note = project.CreateNote(noteDef.Tone, noteDef.Position, noteDef.Duration);
                        if (noteDef.Lyric != null)
                            note.lyric = noteDef.Lyric;

                        if (noteDef.Vibrato != null)
                        {
                            note.vibrato.length = noteDef.Vibrato.Length;
                            note.vibrato.period = noteDef.Vibrato.Period;
                            note.vibrato.depth = noteDef.Vibrato.Depth;
                            note.vibrato.@in = noteDef.Vibrato.In;
                            note.vibrato.@out = noteDef.Vibrato.Out;
                            note.vibrato.shift = noteDef.Vibrato.Shift;
                            note.vibrato.drift = noteDef.Vibrato.Drift;
                        }

                        if (noteDef.Pitch != null)
                        {
                            note.pitch.data.Clear();
                            foreach(var ppt in noteDef.Pitch.Data)
                            {
                                note.pitch.data.Add(new PitchPoint(ppt.X, ppt.Y));
                            }
                        }

                        if (noteDef.Phonemes != null)
                        {
                            foreach(var pho in noteDef.Phonemes)
                            {
                                note.phonemeOverrides.Add(new UPhonemeOverride() { phoneme = pho.Phoneme });
                            }
                        }

                        part.notes.Add(note);
                    }
                    project.parts.Add(part);
                }

                var tempFile = Path.GetTempFileName() + ".ustx";
                Ustx.Save(tempFile, project);
                var stream = new FileStream(tempFile, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);
                return File(stream, "application/json", "generated.ustx");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
