using System;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using OpenUtau.Api.Controllers;
using Xunit;

namespace OpenUtau.Api.Tests
{
    [Collection("Sequential")]
    public class TrackPropertiesExtControllerTests : IDisposable
    {
        private readonly TrackPropertiesExtController _controller;
        
        public TrackPropertiesExtControllerTests()
        {
            SetupHelper.InitDocManager();
            _controller = new TrackPropertiesExtController();
            ResetProject();
        }

        public void Dispose()
        {
            ResetProject();
        }

        [Fact]
        public void SetMute_ShouldBeUndoable()
        {
            var result = _controller.SetMute(0, true);

            Assert.IsType<OkObjectResult>(result);
            var project = DocManager.Inst.Project;

            Assert.True(project.tracks[0].Mute);
            Assert.True(project.tracks[0].Muted);
            Assert.False(project.tracks[1].Mute);

            Assert.True(DocManager.Inst.GetUndoState(out _));
            DocManager.Inst.Undo();

            Assert.False(project.tracks[0].Mute);
            Assert.False(project.tracks[0].Muted);
            Assert.False(project.tracks[1].Muted);
        }

        [Fact]
        public void SetSolo_ShouldBeUndoable()
        {
            var result = _controller.SetSolo(0, true);

            Assert.IsType<OkObjectResult>(result);
            var project = DocManager.Inst.Project;

            Assert.True(project.tracks[0].Solo);
            Assert.False(project.tracks[0].Muted);

            Assert.False(project.tracks[1].Solo);
            Assert.True(project.tracks[1].Muted); // Other tracks become musically muted

            Assert.True(DocManager.Inst.GetUndoState(out _));
            DocManager.Inst.Undo();

            Assert.False(project.tracks[0].Solo);
            Assert.False(project.tracks[0].Muted);
            Assert.False(project.tracks[1].Muted);
        }

        [Fact]
        public void SetVolume_ShouldBeUndoable()
        {
            var result = _controller.SetVolume(1, -5.5);

            Assert.IsType<OkObjectResult>(result);
            var project = DocManager.Inst.Project;

            Assert.Equal(-5.5, project.tracks[1].Volume);

            Assert.True(DocManager.Inst.GetUndoState(out _));
            DocManager.Inst.Undo();

            Assert.Equal(0, project.tracks[1].Volume);
        }

        [Fact]
        public void SetPan_ShouldBeUndoable()
        {
            var result = _controller.SetPan(0, 10.0);

            Assert.IsType<OkObjectResult>(result);
            var project = DocManager.Inst.Project;

            Assert.Equal(10.0, project.tracks[0].Pan);

            Assert.True(DocManager.Inst.GetUndoState(out _));
            DocManager.Inst.Undo();

            Assert.Equal(0, project.tracks[0].Pan);
        }

        [Fact]
        public void InvalidTrackIndex_ShouldReturnBadRequest_AndNotChangeUndoState()
        {
            Assert.False(DocManager.Inst.GetUndoState(out _));

            var result = _controller.SetVolume(99, 3.0);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Invalid track index", badRequest.Value);
            Assert.False(DocManager.Inst.GetUndoState(out _));
            Assert.Equal(0, DocManager.Inst.Project.tracks[0].Volume);
            Assert.Equal(0, DocManager.Inst.Project.tracks[1].Volume);
        }

        private static void ResetProject()
        {
            SetupHelper.CreateAndLoadRealProject(project =>
            {
                project.tracks.Clear();

                project.tracks.Add(new UTrack(project)
                {
                    TrackNo = 0,
                    Mute = false,
                    Solo = false,
                    Volume = 0,
                    Pan = 0
                });

                project.tracks.Add(new UTrack(project)
                {
                    TrackNo = 1,
                    Mute = false,
                    Solo = false,
                    Volume = 0,
                    Pan = 0
                });
            });
        }
    }
}
