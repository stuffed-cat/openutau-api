using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Api.Controllers;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using Xunit;

namespace OpenUtau.Api.Tests
{
    [Collection("Sequential")]
    public class ExportControllerTests
    {
        private readonly ExportController _controller;

        public ExportControllerTests()
        {
            SetupHelper.InitDocManager();
            _controller = new ExportController();
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
        public async Task ExportTrack_NoFile_ReturnsBadRequest()
        {
            // Act
            var result = await _controller.ExportTrack(null, 0) as BadRequestObjectResult;

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task ExportTrack_InvalidTrackIndex_ReturnsBadRequest()
        {
            // Arrange
            var file = CreateRealUstxFile();

            // Act
            var result = await _controller.ExportTrack(file, 99) as BadRequestObjectResult;

            // Assert
            Assert.NotNull(result);
            Assert.Contains("track index out of bounds", result.Value.ToString());
        }

        [Fact]
        public async Task ExportTrack_ValidFile_ReturnsFileStream()
        {
            // Arrange
            var file = CreateRealUstxFile();

            // Act
            var res = await _controller.ExportTrack(file, 0);
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
    }
}
