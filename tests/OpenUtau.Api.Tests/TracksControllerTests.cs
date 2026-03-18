using Microsoft.AspNetCore.Mvc;
using OpenUtau.Api.Controllers;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using Xunit;

namespace OpenUtau.Api.Tests
{
    [Collection("Sequential")]
    public class TracksControllerTests
    {
        private readonly TracksController _controller;

        public TracksControllerTests()
        {
            SetupHelper.InitDocManager();
            _controller = new TracksController();
            
            var project = new UProject();
            project.tracks.Clear();
            var track = new UTrack(project) 
            { 
                TrackNo = 0,
                TrackName = "OriginalTrack",
                TrackColor = "Blue"
            };
            project.tracks.Add(track);

            SetupHelper.SetProject(project);
        }

        [Fact]
        public void GetTrackProperties_ValidTrack_ReturnsOk()
        {
            var result = _controller.GetTrackProperties(0);
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
            
            // Just verifying properties via reflection since it returns an anonymous object
            var val = okResult.Value;
            var nameProp = val.GetType().GetProperty("trackName");
            Assert.Equal("OriginalTrack", nameProp.GetValue(val));
        }

        [Fact]
        public void GetTrackProperties_InvalidTrack_ReturnsBadRequest()
        {
            var result = _controller.GetTrackProperties(999);
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Invalid track index", badRequestResult.Value);
        }

        [Fact]
        public void RenameTrack_ValidTrack_ChangesName()
        {
            var result = _controller.RenameTrack(0, "NewTrackName");
            var okResult = Assert.IsType<OkObjectResult>(result);
            
            var project = DocManager.Inst.Project;
            Assert.Equal("NewTrackName", project.tracks[0].TrackName);
        }

        [Fact]
        public void SetTrackColor_ValidTrack_ChangesColor()
        {
            var result = _controller.SetTrackColor(0, "Red");
            var okResult = Assert.IsType<OkObjectResult>(result);
            
            var project = DocManager.Inst.Project;
            Assert.Equal("Red", project.tracks[0].TrackColor);
        }
    }
}
