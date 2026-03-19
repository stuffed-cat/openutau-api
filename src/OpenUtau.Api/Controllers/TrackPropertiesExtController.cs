using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using System.Linq;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/project/track")]
    public class TrackPropertiesExtController : ControllerBase
    {
        [HttpPost("{trackNo}/mute")]
        public IActionResult SetMute(int trackNo, [FromQuery] bool mute)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");
            if (trackNo < 0 || trackNo >= project.tracks.Count) return BadRequest("Invalid track index");

            DocManager.Inst.StartUndoGroup("api", true);
            try
            {
                var track = project.tracks[trackNo];
                track.Mute = mute;

                // Recalculate global Muted states (if Solo/Mute logic requires it)
                bool hasSolo = project.tracks.Any(t => t.Solo);
                foreach (var t in project.tracks)
                {
                    t.Muted = hasSolo ? !t.Solo : t.Mute;
                    DocManager.Inst.ExecuteCmd(new VolumeChangeNotification(t.TrackNo, t.Muted ? -24 : t.Volume));
                }

                return Ok(new { success = true, trackNo, mute, muted = track.Muted });
            }
            finally
            {
                DocManager.Inst.EndUndoGroup();
            }
        }

        [HttpPost("{trackNo}/solo")]
        public IActionResult SetSolo(int trackNo, [FromQuery] bool solo)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");
            if (trackNo < 0 || trackNo >= project.tracks.Count) return BadRequest("Invalid track index");

            DocManager.Inst.StartUndoGroup("api", true);
            try
            {
                var track = project.tracks[trackNo];
                track.Solo = solo;

                // Recalculate global Muted states
                bool hasSolo = project.tracks.Any(t => t.Solo);
                foreach (var t in project.tracks)
                {
                    t.Muted = hasSolo ? !t.Solo : t.Mute;
                    DocManager.Inst.ExecuteCmd(new VolumeChangeNotification(t.TrackNo, t.Muted ? -24 : t.Volume));
                }

                // Optional: send UI notification just in case.
                DocManager.Inst.ExecuteCmd(new SoloTrackNotification(trackNo, solo));

                return Ok(new { success = true, trackNo, solo, muted = track.Muted });
            }
            finally
            {
                DocManager.Inst.EndUndoGroup();
            }
        }

        [HttpPost("{trackNo}/pan")]
        public IActionResult SetPan(int trackNo, [FromQuery] double pan)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");
            if (trackNo < 0 || trackNo >= project.tracks.Count) return BadRequest("Invalid track index");

            DocManager.Inst.StartUndoGroup("api", true);
            try
            {
                var track = project.tracks[trackNo];
                track.Pan = System.Math.Clamp(pan, -100, 100);
                DocManager.Inst.ExecuteCmd(new PanChangeNotification(trackNo, track.Pan));

                return Ok(new { success = true, trackNo, pan = track.Pan });
            }
            finally
            {
                DocManager.Inst.EndUndoGroup();
            }
        }

        [HttpPost("{trackNo}/volume")]
        public IActionResult SetVolume(int trackNo, [FromQuery] double volume)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");
            if (trackNo < 0 || trackNo >= project.tracks.Count) return BadRequest("Invalid track index");

            DocManager.Inst.StartUndoGroup("api", true);
            try
            {
                var track = project.tracks[trackNo];
                track.Volume = System.Math.Clamp(volume, -24, 24); // generally volume bounds are around this in Utau
                DocManager.Inst.ExecuteCmd(new VolumeChangeNotification(trackNo, track.Muted ? -24 : track.Volume));

                return Ok(new { success = true, trackNo, volume = track.Volume });
            }
            finally
            {
                DocManager.Inst.EndUndoGroup();
            }
        }
    }
}
