using Microsoft.AspNetCore.Mvc;
using OpenUtau.Api.Controllers;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using System.Linq;
using Xunit;

namespace OpenUtau.Api.Tests
{
    [Collection("Sequential")]
    public class ProjectManagementControllerTests
    {
        private ProjectManagementController _controller;
        private UProject _project;

        public ProjectManagementControllerTests()
        {
            SetupHelper.InitDocManager();
            _controller = new ProjectManagementController();
            
            SetupHelper.SetProject(new UProject());
            _project = DocManager.Inst.Project;
            _project.tracks.Clear();
            _project.tracks.Add(new UTrack(_project) { TrackNo = 0 });
            _project.parts.Clear();
            _project.tempos.Clear();
            _project.tempos.Add(new UTempo { position = 0, bpm = 120 });
        }

        [Fact]
        public void NewProject_ReplacesCurrentSessionProject()
        {
            _project.parts.Add(new UVoicePart() { name = "ToBeDeleted" });
            
            var response = _controller.NewProject() as OkObjectResult;
            
            Assert.NotNull(response);
            Assert.Empty(DocManager.Inst.Project.parts);
            Assert.True(DocManager.Inst.Project.expressions.Count > 0);
        }

        [Fact]
        public void RemapTimeAxis_ValidBpm_UpdatesBpmAndAdjustsTicks()
        {
            var part = new UVoicePart() { name = "TestVoice", position = 480 };
            var note = _project.CreateNote(60, 480, 480);
            part.notes.Add(note);
            _project.parts.Add(part);

            var res = _controller.RemapTimeAxis(240);
            var response = res as OkObjectResult;
            if (res is ObjectResult objRes && response == null) {
                Assert.True(false, "Remap time axis failed: " + objRes.Value?.ToString());
            }

            Assert.NotNull(response);
            Assert.Equal(240, _project.tempos[0].bpm);

            var updatedPart = _project.parts.First() as UVoicePart;
            Assert.Equal(960, updatedPart.position);
            
            var updatedNote = updatedPart.notes.First();
            Assert.Equal(960, updatedNote.duration);
        }

        [Fact]
        public void RemapTimeAxis_InvalidBpm_ReturnsBadRequest()
        {
            var response = _controller.RemapTimeAxis(0);
            var badRequest = response as BadRequestObjectResult;
            Assert.NotNull(badRequest);
            Assert.Equal("BPM must be greater than 0", badRequest.Value);
            
            response = _controller.RemapTimeAxis(-120);
            badRequest = response as BadRequestObjectResult;
            Assert.NotNull(badRequest);
            Assert.Equal("BPM must be greater than 0", badRequest.Value);
        }

        [Fact]
        public void RemapTimeAxis_NoProject_ReturnsBadRequest()
        {
            SetupHelper.SetProject(null);
            var response = _controller.RemapTimeAxis(120);
            
            var badRequest = response as BadRequestObjectResult;
            Assert.NotNull(badRequest);
            Assert.Equal("No project in session", badRequest.Value);
        }
    }
}
