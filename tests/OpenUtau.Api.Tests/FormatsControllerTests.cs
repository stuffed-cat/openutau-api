using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Api.Controllers;
using OpenUtau.Core.Ustx;
using Xunit;

namespace OpenUtau.Api.Tests
{
    [Collection("Sequential")]
    public class FormatsControllerTests
    {
        private readonly FormatsController _controller;

        public FormatsControllerTests()
        {
            SetupHelper.InitDocManager();
            _controller = new FormatsController();
        }

        [Fact]
        public void ExportUst_ValidFile_ReturnsUstFile()
        {
            var file = CreateRealUstxFile();

            var result = _controller.ExportUst(file, 0);

            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal("text/plain", fileResult.ContentType);
            Assert.Equal("part_0.ust", fileResult.FileDownloadName);
            Assert.True(fileResult.FileContents.Length > 0);
        }

        [Fact]
        public void ExportVsqx_ValidFile_ReturnsVsqxFile()
        {
            var file = CreateRealUstxFile();

            var result = _controller.ExportVsqx(file, 0);

            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal("application/xml", fileResult.ContentType);
            Assert.Equal("part_0.vsqx", fileResult.FileDownloadName);
            Assert.True(fileResult.FileContents.Length > 0);
        }

        [Fact]
        public void ExportVpr_ValidFile_ReturnsZipFile()
        {
            var file = CreateRealUstxFile();

            var result = _controller.ExportVpr(file, 0);

            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal("application/zip", fileResult.ContentType);
            Assert.Equal("part_0.vpr", fileResult.FileDownloadName);
            Assert.True(fileResult.FileContents.Length > 2);
            Assert.Equal((byte)'P', fileResult.FileContents[0]);
            Assert.Equal((byte)'K', fileResult.FileContents[1]);
        }

        [Fact]
        public void ExportUst_InvalidPart_ReturnsBadRequest()
        {
            var file = CreateRealUstxFile();

            var result = _controller.ExportUst(file, 99);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Invalid partNo", badRequest.Value);
        }

        private static IFormFile CreateRealUstxFile()
        {
            var project = OpenUtau.Core.Format.Ustx.Create();
            project.tracks.Add(new UTrack(project) { TrackNo = 0 });

            var part = new UVoicePart { trackNo = 0, position = 0, Duration = 960 };
            var note = project.CreateNote(60, 0, 480);
            note.lyric = "la";
            part.notes.Add(note);
            project.parts.Add(part);

            var tempFile = Path.GetTempFileName() + ".ustx";
            OpenUtau.Core.Format.Ustx.Save(tempFile, project);

            var bytes = File.ReadAllBytes(tempFile);
            File.Delete(tempFile);

            return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "test.ustx")
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/x-yaml"
            };
        }
    }
}