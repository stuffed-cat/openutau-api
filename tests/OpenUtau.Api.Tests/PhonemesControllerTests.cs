using Microsoft.AspNetCore.Mvc;
using OpenUtau.Api.Controllers;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using Xunit;

namespace OpenUtau.Api.Tests
{
    [Collection("Sequential")]
    public class PhonemesControllerTests
    {
        private readonly PhonemesController _controller;

        public PhonemesControllerTests()
        {
            SetupHelper.InitDocManager();
            _controller = new PhonemesController();
            ResetProject();
        }

        [Fact]
        public void SetPhonemeAlias_ShouldBeUndoable()
        {
            var result = _controller.SetPhonemeAlias(0, 0, 0, "alias-a");

            Assert.IsType<OkObjectResult>(result);
            var note = ((UVoicePart)DocManager.Inst.Project.parts[0]).notes.First();
            Assert.Equal("alias-a", note.GetPhonemeOverride(0).phoneme);
            Assert.True(DocManager.Inst.GetUndoState(out _));

            DocManager.Inst.Undo();
            Assert.Null(note.GetPhonemeOverride(0).phoneme);
        }

        [Fact]
        public void SetPhonemeAlias_InvalidPart_ReturnsBadRequest()
        {
            var result = _controller.SetPhonemeAlias(99, 0, 0, "alias-a");

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Invalid partNo", badRequest.Value);
        }

        [Fact]
        public void SetPhonemeAlias_InvalidNote_ReturnsBadRequest()
        {
            var result = _controller.SetPhonemeAlias(0, 99, 0, "alias-a");

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Invalid noteIndex", badRequest.Value);
        }

        private static void ResetProject()
        {
            SetupHelper.CreateAndLoadRealProject(project =>
            {
                project.tracks.Clear();
                project.tracks.Add(new UTrack(project) { TrackNo = 0 });

                project.parts.Clear();
                var part = new UVoicePart { trackNo = 0, position = 0, Duration = 960 };
                var note = project.CreateNote(60, 0, 480);
                note.lyric = "la";
                part.notes.Add(note);
                project.parts.Add(part);
            });
        }
    }
}
