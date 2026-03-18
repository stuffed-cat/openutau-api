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
        [HttpGet("/api/project/track/{trackNo}")]
        public IActionResult GetTrackProperties(int trackNo) {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");

            if (trackNo < 0 || trackNo >= project.tracks.Count) return BadRequest("Invalid track index");
            var track = project.tracks[trackNo];

            return Ok(new {
                trackNo = track.TrackNo,
                trackName = track.TrackName,
                singer = track.Singer?.Id ?? track.singer,
                phonemizer = track.Phonemizer?.GetType().Name ?? track.phonemizer,
                rendererSettings = track.RendererSettings,
                mute = track.Mute,
                solo = track.Solo,
                volume = track.Volume,
                pan = track.Pan
            });
        }

        [HttpPost("{trackIndex}/rename")]
        public IActionResult RenameTrack(int trackIndex, [FromQuery] string name)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project is currently loaded.");
            if (trackIndex < 0 || trackIndex >= project.tracks.Count) return NotFound("Track not found");
            if (string.IsNullOrEmpty(name)) return BadRequest("Name cannot be empty");

            try
            {
                var track = project.tracks[trackIndex];
                DocManager.Inst.ExecuteCmd(new RenameTrackCommand(project, track, name));
                return Ok(new { message = $"Track {trackIndex} renamed to {name}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("{trackIndex}/setcolor")]
        public IActionResult SetTrackColor(int trackIndex, [FromQuery] string color)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project is currently loaded.");
            if (trackIndex < 0 || trackIndex >= project.tracks.Count) return NotFound("Track not found");

            try
            {
                var track = project.tracks[trackIndex];
                DocManager.Inst.ExecuteCmd(new ChangeTrackColorCommand(project, track, color));
                return Ok(new { message = $"Track {trackIndex} color set to {color}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("{trackIndex}/setphonmizer")]
        public IActionResult SetTrackPhonemizer(int trackIndex, [FromQuery] string phonemizerName)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project is currently loaded.");
            if (trackIndex < 0 || trackIndex >= project.tracks.Count) return NotFound("Track not found");

            try
            {
                var factory = DocManager.Inst.PhonemizerFactories.FirstOrDefault(f => f.name == phonemizerName);
                if (factory == null) return BadRequest($"Phonemizer '{phonemizerName}' not found");

                var phonemizer = factory.Create();
                var track = project.tracks[trackIndex];
                DocManager.Inst.ExecuteCmd(new TrackChangePhonemizerCommand(project, track, phonemizer));
                return Ok(new { message = $"Track {trackIndex} phonemizer set to {phonemizerName}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("{trackIndex}/setrenderer")]
        public IActionResult SetTrackRenderer(int trackIndex, [FromQuery] string rendererId)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project is currently loaded.");
            if (trackIndex < 0 || trackIndex >= project.tracks.Count) return NotFound("Track not found");

            try
            {
                var track = project.tracks[trackIndex];
                var newSettings = new URenderSettings
                {
                    renderer = rendererId,
                    // Preserve other existing settings if needed?
                };
                DocManager.Inst.ExecuteCmd(new TrackChangeRenderSettingCommand(project, track, newSettings));
                return Ok(new { message = $"Track {trackIndex} renderer set to {rendererId}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("{trackIndex}/voicecolormapping")]
        public IActionResult SetVoiceColorMapping(int trackIndex, [FromQuery] bool validate = true)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project is currently loaded.");
            if (trackIndex != -1 && (trackIndex < 0 || trackIndex >= project.tracks.Count)) return NotFound("Track not found");

            try
            {
                // This triggers the remapping on the specified track
                // If trackIndex is -1 it checks all tracks
                DocManager.Inst.ExecuteCmd(new VoiceColorRemappingNotification(trackIndex, validate));
                return Ok(new { message = $"Voice color remapping triggered for track {trackIndex}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

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
