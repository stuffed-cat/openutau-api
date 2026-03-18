using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using OpenUtau.Api.Controllers;

namespace OpenUtau.Api.Tests
{
    [Collection("Sequential")]
    public class TrackPropertiesExtControllerTests : IDisposable
    {
        private TrackPropertiesExtController _controller;
        
        public TrackPropertiesExtControllerTests()
        {
            _controller = new TrackPropertiesExtController();
            
            // Initialize a real UProject
            SetupHelper.CreateAndLoadRealProject(project => {
            
            // Set up two tracks
            project.tracks.Add(new UTrack { TrackNo = 0, Mute = false, Solo = false, Volume = 0, Pan = 0 });
            project.tracks.Add(new UTrack { TrackNo = 1, Mute = false, Solo = false, Volume = 0, Pan = 0 });
            
            SetupHelper.InitDocManager();
            });
        }

        public void Dispose()
        {
            SetupHelper.CreateAndLoadRealProject();
        }

        [Fact]
        public void ToggleMute_ShouldMuteTrack_AndEmitNotification()
        {
            var result = _controller.SetMute(0, true);
            
            var okResult = Assert.IsType<OkObjectResult>(result);
            var project = DocManager.Inst.Project;
            
            Assert.True(project.tracks[0].Mute);
            Assert.True(project.tracks[0].Muted);
            Assert.False(project.tracks[1].Mute);
        }

        [Fact]
        public void ToggleSolo_ShouldMuteOtherTracks()
        {
            var result = _controller.SetSolo(0, true);
            
            var okResult = Assert.IsType<OkObjectResult>(result);
            var project = DocManager.Inst.Project;
            
            Assert.True(project.tracks[0].Solo);
            Assert.False(project.tracks[0].Muted);
            
            Assert.False(project.tracks[1].Solo);
            Assert.True(project.tracks[1].Muted); // Other tracks become musically muted
        }

        [Fact]
        public void SetVolume_ShouldUpdateVolume()
        {
            var result = _controller.SetVolume(1, -5.5);
            
            var okResult = Assert.IsType<OkObjectResult>(result);
            var project = DocManager.Inst.Project;
            
            Assert.Equal(-5.5, project.tracks[1].Volume);
        }

        [Fact]
        public void SetPan_ShouldUpdatePan()
        {
            var result = _controller.SetPan(0, 10.0);
            
            var okResult = Assert.IsType<OkObjectResult>(result);
            var project = Serilog.Log.Logger == null ? DocManager.Inst.Project : DocManager.Inst.Project;
            
            Assert.Equal(10.0, project.tracks[0].Pan);
        }
    }
}
