using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Format;
using OpenUtau.Core.Ustx;
using System;
using System.IO;
using System.Linq;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/project/[controller]")]
    public class TracksController : ControllerBase
    {
        private IActionResult ExecuteEdit(IFormFile file, Action<UProject> modifier)
        {
            if (file == null || file.Length == 0) return BadRequest("No file uploaded");

            try
            {
                var tempFile = Path.GetTempFileName();
                using (var stream = new FileStream(tempFile, FileMode.Create)) { file.CopyTo(stream); }

                var project = Ustx.Load(tempFile);
                if (project == null) {
                    System.IO.File.Delete(tempFile);
                    return BadRequest("Failed to load project.");
                }

                modifier(project);

                var outTemp = Path.GetTempFileName() + ".ustx";
                Ustx.Save(outTemp, project);
                System.IO.File.Delete(tempFile);

                var streamRet = new FileStream(outTemp, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);
                return File(streamRet, "application/json", "edited.ustx");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("add")]
        public IActionResult AddTrack(IFormFile file, [FromQuery] int? trackIndex = null)
        {
            return ExecuteEdit(file, project =>
            {
                var newTrack = new UTrack(project) { TrackName = "New Track" };
                if (trackIndex.HasValue && trackIndex.Value >= 0 && trackIndex.Value < project.tracks.Count)
                {
                    project.tracks.Insert(trackIndex.Value, newTrack);
                    foreach (var p in project.parts.Where(p => p.trackNo >= trackIndex.Value)) { p.trackNo++; }
                }
                else
                {
                    project.tracks.Add(newTrack);
                }
            });
        }

        [HttpPost("remove")]
        public IActionResult RemoveTrack(IFormFile file, [FromQuery] int trackIndex) // [FromQuery] keeps it easy
        {
            return ExecuteEdit(file, project =>
            {
                if (trackIndex < 0 || trackIndex >= project.tracks.Count) throw new Exception("Invalid track index");
                project.tracks.RemoveAt(trackIndex);
                project.parts.RemoveAll(p => p.trackNo == trackIndex);
                foreach (var p in project.parts.Where(p => p.trackNo > trackIndex)) { p.trackNo--; }
            });
        }

        [HttpPost("move")]
        public IActionResult MoveTrack(IFormFile file, [FromQuery] int fromIndex, [FromQuery] int toIndex)
        {
            return ExecuteEdit(file, project =>
            {
                if (fromIndex < 0 || fromIndex >= project.tracks.Count || toIndex < 0 || toIndex >= project.tracks.Count) throw new Exception("Invalid index");
                if (fromIndex == toIndex) return;

                var track = project.tracks[fromIndex];
                project.tracks.RemoveAt(fromIndex);
                project.tracks.Insert(toIndex, track);
                
                foreach(var p in project.parts)
                {
                    if (p.trackNo == fromIndex) { p.trackNo = toIndex; }
                    else if (fromIndex < toIndex && p.trackNo > fromIndex && p.trackNo <= toIndex) { p.trackNo--; }
                    else if (fromIndex > toIndex && p.trackNo >= toIndex && p.trackNo < fromIndex) { p.trackNo++; }
                }
            });
        }

        [HttpPost("config")]
        public IActionResult ConfigTrack(
            IFormFile file, 
            [FromQuery] int trackIndex, 
            [FromQuery] string? name = null,
            [FromQuery] string? color = null,
            [FromQuery] bool? mute = null,
            [FromQuery] bool? solo = null,
            [FromQuery] string? singerId = null,
            [FromQuery] string? phonemizer = null,
            [FromQuery] string? renderer = null)
        {
            return ExecuteEdit(file, project =>
            {
                if (trackIndex < 0 || trackIndex >= project.tracks.Count) throw new Exception("Invalid track index");
                var track = project.tracks[trackIndex];
                
                if (name != null) track.TrackName = name;
                if (color != null) track.TrackColor = color;
                if (mute.HasValue) track.Mute = mute.Value;
                if (solo.HasValue) track.Solo = solo.Value;
                
                if (singerId != null && SingerManager.Inst.Singers != null)
                {
                    track.Singer = SingerManager.Inst.Singers.Values.FirstOrDefault(s => s.Id == singerId) ?? track.Singer;
                }
                
                if (phonemizer != null && DocManager.Inst.PhonemizerFactories != null)
                {
                    var factory = DocManager.Inst.PhonemizerFactories.FirstOrDefault(f => f.name == phonemizer);
                    if (factory != null) track.Phonemizer = factory.Create();
                }
                
                if (renderer != null)
                {
                    track.RendererSettings.renderer = renderer;
                }
            });
        }
    }
}
