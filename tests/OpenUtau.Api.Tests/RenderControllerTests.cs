using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Api.Controllers;
using OpenUtau.Core;
using Xunit;
using OpenUtau.Core.Ustx;

namespace OpenUtau.Api.Tests
{
    [Collection("Sequential")]
    public class RenderControllerTests
    {
        private readonly RenderController _controller;

        public RenderControllerTests()
        {
            SetupHelper.InitDocManager();
            _controller = new RenderController();
        }

        private IFormFile CreateRealUstxFile()
        {
            var project = OpenUtau.Core.Format.Ustx.Create();
            var track = new UTrack();
            project.tracks.Add(track);
            var part = new UVoicePart() { position = 0, trackNo = 0 };
            var note = UNote.Create();
            note.duration = 480;
            note.tone = 60;
            note.lyric = "a";
            note.pitch.AddPoint(new PitchPoint(0, 0));
            note.pitch.AddPoint(new PitchPoint(0, 0));
            part.notes.Add(note);
            project.parts.Add(part);

            string tempFile = Path.GetTempFileName() + ".ustx";
            OpenUtau.Core.Format.Ustx.Save(tempFile, project);

            var stream = new FileStream(tempFile, FileMode.Open, FileAccess.Read);
            return new FormFile(stream, 0, stream.Length, "file", Path.GetFileName(tempFile))
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/x-yaml"
            };
        }

        [Fact]
        public void ClearCache_ReturnsOk()
        {
            // Act
            var result = _controller.ClearCache() as ObjectResult;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.StatusCode);
        }

        [Fact]
        public void GetCacheStatus_ReturnsStatus()
        {
            // Act
            var result = _controller.GetCacheStatus() as OkObjectResult;

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Value);
        }

        [Fact]
        public async Task RenderPart_NoFile_ReturnsBadRequest()
        {
            // Act
            var result = await _controller.RenderPart(null) as BadRequestObjectResult;

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task RenderPart_ValidFile_ReturnsFileStream()
        {
            // Arrange
            var file = CreateRealUstxFile();

            // Act
            var res = await _controller.RenderPart(file);
            var result = res as FileStreamResult;
            if (result == null) {
                var errObj = res as ObjectResult;
                Assert.Fail($"Result is null. Status: {errObj?.StatusCode}, Error: {errObj?.Value}");
            }

            // Assert
            Assert.NotNull(result);
            Assert.Equal("audio/wav", result.ContentType);
            Assert.True(result.FileStream.Length > 0);
        }
        
        [Fact]
        public async Task RenderMixdown_ValidFile_ReturnsFileStream()
        {
            // Arrange
            var file = CreateRealUstxFile();

            // Act
            var result = await _controller.RenderMixdown(file) as FileStreamResult;

            // Assert
            Assert.NotNull(result);
            Assert.Equal("audio/wav", result.ContentType);
            Assert.True(result.FileStream.Length > 0);
        }
    }
}
