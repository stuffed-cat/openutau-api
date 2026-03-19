using Microsoft.AspNetCore.Mvc;
using OpenUtau.Api.Controllers;
using OpenUtau.Core;
using OpenUtau.Core.Format;
using OpenUtau.Core.Ustx;
using System.Collections.Generic;
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

        [Fact]
        public void SetNoteFlags_UpdatesClassicFlags()
        {
            var result = _controller.SetNoteFlags(0, 0, new PhonemesController.ClassicFlagsRequest
            {
                Flags = new List<PhonemesController.ClassicFlagRequest>
                {
                    new PhonemesController.ClassicFlagRequest { Flag = "g", Value = -5 },
                    new PhonemesController.ClassicFlagRequest { Flag = "P", Value = 92 }
                }
            });

            Assert.IsType<OkObjectResult>(result);

            var project = DocManager.Inst.Project;
            var track = project.tracks[0];
            var part = (UVoicePart)project.parts[0];
            var note = part.notes.First();

            Assert.Contains(note.phonemeExpressions, expr => expr.abbr == Ustx.GEN && expr.value == -5);
            Assert.Contains(note.phonemeExpressions, expr => expr.abbr == Ustx.NORM && expr.value == 92);

            var flagsResult = Assert.IsType<OkObjectResult>(_controller.GetNoteFlags(0, 0));
            var flags = flagsResult.Value!;
            var flagsProp = flags.GetType().GetProperty("flags")!;
            var values = ((System.Collections.IEnumerable)flagsProp.GetValue(flags)!).Cast<object>().ToList();
            Assert.Contains(values, item => item.GetType().GetProperty("flag")!.GetValue(item)?.ToString() == "g");
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
