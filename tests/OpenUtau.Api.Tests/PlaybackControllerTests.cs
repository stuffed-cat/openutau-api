using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using OpenUtau.Api.Controllers;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using Xunit;

namespace OpenUtau.Api.Tests
{
    public class PlaybackControllerTests
    {
        private readonly PlaybackController _controller;

        public PlaybackControllerTests()
        {
            SetupHelper.InitDocManager();
            var project = new UProject();
            var track = new UTrack(project);
            project.tracks.Add(track);
            var part = new UVoicePart();
            project.parts.Add(part);
            SetupHelper.SetProject(project);
            _controller = new PlaybackController();
        }

        [Fact]
        public void Seek_ValidTick_UpdatesPlayPosTick()
        {
            // Act
            var result = _controller.Seek(480) as OkObjectResult;

            // Assert
            if (result == null) {
                var errResult = _controller.PreviewPart(0, 0) as ObjectResult;
                Assert.Fail($"Result was null. Status: {errResult?.StatusCode}, Msg: {errResult?.Value}");
            }
            Assert.NotNull(result);
            Assert.Equal(480, DocManager.Inst.playPosTick);
        }

        [Fact]
        public void Pause_Stop_ExecuteWithoutExceptions()
        {
            // Act
            var pauseResult = _controller.Pause() as OkObjectResult;
            var stopResult = _controller.Stop() as OkObjectResult;

            // Assert
            Assert.NotNull(pauseResult);
            Assert.NotNull(stopResult);
        }

        [Fact]
        public void PreviewPart_ValidPart_RendersWavAndReturnsFile()
        {
            // Act
            var result = _controller.PreviewPart(0, 0) as FileContentResult;

            // Assert
            if (result == null) {
                var errResult = _controller.PreviewPart(0, 0) as ObjectResult;
                Assert.Fail($"Result was null. Status: {errResult?.StatusCode}, Msg: {errResult?.Value}");
            }
            Assert.NotNull(result);
            Assert.Equal("audio/wav", result.ContentType);
            Assert.True(result.FileContents.Length > 0); // Should have a valid WAV header at least
        }
        
        [Fact]
        public void PreviewPart_InvalidPart_ReturnsBadRequest()
        {
            // Act
            var resultTrackInvalid = _controller.PreviewPart(999, 0) as BadRequestObjectResult;
            var resultPartInvalid = _controller.PreviewPart(0, 999) as BadRequestObjectResult;

            // Assert
            Assert.NotNull(resultTrackInvalid);
            Assert.NotNull(resultPartInvalid);
        }
    }
}
