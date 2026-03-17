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
