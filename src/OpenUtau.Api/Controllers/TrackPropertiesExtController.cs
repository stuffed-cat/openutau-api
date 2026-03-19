using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using System;
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
                DocManager.Inst.ExecuteCmd(new SetTrackMuteCommand(project, track, mute));
                PublishTrackMixState(project);
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
                DocManager.Inst.ExecuteCmd(new SetTrackSoloCommand(project, track, solo));
                PublishTrackMixState(project);
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
                DocManager.Inst.ExecuteCmd(new SetTrackPanCommand(track, System.Math.Clamp(pan, -100, 100)));
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
                DocManager.Inst.ExecuteCmd(new SetTrackVolumeCommand(track, System.Math.Clamp(volume, -24, 24))); // generally volume bounds are around this in Utau
                DocManager.Inst.ExecuteCmd(new VolumeChangeNotification(trackNo, track.Muted ? -24 : track.Volume));

                return Ok(new { success = true, trackNo, volume = track.Volume });
            }
            finally
            {
                DocManager.Inst.EndUndoGroup();
            }
        }

        private static void PublishTrackMixState(UProject project)
        {
            bool hasSolo = project.tracks.Any(t => t.Solo);
            foreach (var t in project.tracks)
            {
                t.Muted = hasSolo ? !t.Solo : t.Mute;
                DocManager.Inst.ExecuteCmd(new VolumeChangeNotification(t.TrackNo, t.Muted ? -24 : t.Volume));
            }
        }

        private sealed class SetTrackMuteCommand : UCommand
        {
            private readonly UProject project;
            private readonly UTrack track;
            private readonly bool newMute;
            private readonly bool oldMute;

            public SetTrackMuteCommand(UProject project, UTrack track, bool mute)
            {
                this.project = project;
                this.track = track;
                newMute = mute;
                oldMute = track.Mute;
            }

            public override void Execute()
            {
                track.Mute = newMute;
                RecalculateMutedState(project);
            }

            public override void Unexecute()
            {
                track.Mute = oldMute;
                RecalculateMutedState(project);
            }

            public override string ToString() => "Set track mute";
        }

        private sealed class SetTrackSoloCommand : UCommand
        {
            private readonly UProject project;
            private readonly UTrack track;
            private readonly bool newSolo;
            private readonly bool oldSolo;

            public SetTrackSoloCommand(UProject project, UTrack track, bool solo)
            {
                this.project = project;
                this.track = track;
                newSolo = solo;
                oldSolo = track.Solo;
            }

            public override void Execute()
            {
                track.Solo = newSolo;
                RecalculateMutedState(project);
            }

            public override void Unexecute()
            {
                track.Solo = oldSolo;
                RecalculateMutedState(project);
            }

            public override string ToString() => "Set track solo";
        }

        private sealed class SetTrackPanCommand : UCommand
        {
            private readonly UTrack track;
            private readonly double newPan;
            private readonly double oldPan;

            public SetTrackPanCommand(UTrack track, double pan)
            {
                this.track = track;
                newPan = pan;
                oldPan = track.Pan;
            }

            public override void Execute()
            {
                track.Pan = newPan;
            }

            public override void Unexecute()
            {
                track.Pan = oldPan;
            }

            public override string ToString() => "Set track pan";
        }

        private sealed class SetTrackVolumeCommand : UCommand
        {
            private readonly UTrack track;
            private readonly double newVolume;
            private readonly double oldVolume;

            public SetTrackVolumeCommand(UTrack track, double volume)
            {
                this.track = track;
                newVolume = volume;
                oldVolume = track.Volume;
            }

            public override void Execute()
            {
                track.Volume = newVolume;
            }

            public override void Unexecute()
            {
                track.Volume = oldVolume;
            }

            public override string ToString() => "Set track volume";
        }

        private static void RecalculateMutedState(UProject project)
        {
            bool hasSolo = project.tracks.Any(t => t.Solo);
            foreach (var t in project.tracks)
            {
                t.Muted = hasSolo ? !t.Solo : t.Mute;
            }
        }
    }
}
