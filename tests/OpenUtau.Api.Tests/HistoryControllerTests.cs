using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using OpenUtau.Api.Controllers;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;

namespace OpenUtau.Api.Tests
{
    [Collection("Sequential")]
    public class HistoryControllerTests
    {
        private HistoryController _controller;
        private UProject _project;

        public HistoryControllerTests()
        {
            SetupHelper.InitDocManager();
            _controller = new HistoryController();
            _project = DocManager.Inst.Project;
            // Clear any lingering state
            _project.parts.Clear();
            _project.tracks.Clear(); _project.tracks.Add(new UTrack(_project) { TrackNo = 0 });
            
            // Note: DocManager.Inst.Clear undo queues is not public natively, we can just new up history or use Undo() until empty.
            // Oh actually there is "ClearHistory"? No, DocManager creates new project history when UProject is set.
// removed
            // Let's just create a new project and set it.
            SetupHelper.SetProject(new UProject());
            _project = DocManager.Inst.Project;
            _project.tracks.Clear(); _project.tracks.Add(new UTrack(_project) { TrackNo = 0 });
            _project.parts.Clear();
        }

        [Fact]
        public void UndoRedo_RealIntegration_ChangesProjectState()
        {
            // 1. Initial State: No parts.
            Assert.Equal(0, _project.parts.Count);

            // 2. Perform an action that creates an undo group natively
            var part = new UVoicePart() { name = "TestPart" };
            DocManager.Inst.StartUndoGroup();
            DocManager.Inst.ExecuteCmd(new AddPartCommand(_project, part));
            DocManager.Inst.EndUndoGroup();

            Assert.Equal(1, _project.parts.Count);
            
            // Check State API Before Undo
            var stateRes1 = _controller.GetHistoryState() as OkObjectResult;
            Assert.NotNull(stateRes1);
            var state1 = stateRes1.Value;
            var canUndo1 = (bool)state1.GetType().GetProperty("CanUndo").GetValue(state1, null);
            var canRedo1 = (bool)state1.GetType().GetProperty("CanRedo").GetValue(state1, null);
            Assert.True(canUndo1);
            Assert.False(canRedo1);

            // 3. Undo the action
            var undoRes = _controller.Undo() as OkObjectResult;
            Assert.NotNull(undoRes);
            
            // Assert part is gone
            Assert.Equal(0, _project.parts.Count);

            // Check State API After Undo
            var stateRes2 = _controller.GetHistoryState() as OkObjectResult;
            var state2 = stateRes2.Value;
            var canUndo2 = (bool)state2.GetType().GetProperty("CanUndo").GetValue(state2, null);
            var canRedo2 = (bool)state2.GetType().GetProperty("CanRedo").GetValue(state2, null);
            Assert.False(canUndo2);
            Assert.True(canRedo2);

            // 4. Redo the action
            var redoRes = _controller.Redo() as OkObjectResult;
            Assert.NotNull(redoRes);

            // Assert part is back
            Assert.Equal(1, _project.parts.Count);
            Assert.Equal("TestPart", _project.parts.First().name);
        }
    }
}
