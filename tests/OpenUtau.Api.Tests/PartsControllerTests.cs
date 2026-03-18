using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Api.Controllers;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using Xunit;

namespace OpenUtau.Api.Tests
{
    [Collection("Sequential")]
    public class PartsControllerTests
    {
        private readonly PartsController _controller;

        public PartsControllerTests()
        {
            SetupHelper.InitDocManager();
            _controller = new PartsController();
            
            var project = new UProject();
            project.tracks.Add(new UTrack(project) { TrackNo = 0 });
            
            var part1 = new UVoicePart()
            {
                name = "TestPart1",
                trackNo = 0,
                position = 0,
                duration = 1920
            };
            var note1 = project.CreateNote(60, 480, 480);
            part1.notes.Add(note1);

            var part2 = new UVoicePart()
            {
                name = "TestPart2",
                trackNo = 0,
                position = 1920,
                duration = 1920
            };
            var note2 = project.CreateNote(62, 0, 480); // relative to part2 position
            part2.notes.Add(note2);

            project.parts.Add(part1);
            project.parts.Add(part2);

            SetupHelper.SetProject(project);
        }

        [Fact]
        public void GetPartProperties_ValidPart_ReturnsOk()
        {
            var result = _controller.GetPartProperties(0);
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
            
            // Just verifying properties via reflection since it returns an anonymous object
            var val = okResult.Value;
            var nameProp = val.GetType().GetProperty("name");
            Assert.Equal("TestPart1", nameProp.GetValue(val));

            var json = System.Text.Json.JsonSerializer.Serialize(val);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            var curves = root.GetProperty("curves");
            Assert.True(curves.ValueKind == System.Text.Json.JsonValueKind.Array);
        }

        [Fact]
        public void GetPartProperties_InvalidPart_ReturnsBadRequestOrNotFound()
        {
            var result = _controller.GetPartProperties(999);
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Invalid part index", badRequestResult.Value);
        }

        [Fact]
        public void RenamePart_ValidPart_ChangesName()
        {
            var result = _controller.RenamePart(0, "NewNameForPart1");
            var okResult = Assert.IsType<OkObjectResult>(result);
            
            var project = DocManager.Inst.Project;
            Assert.Equal("NewNameForPart1", project.parts[0].name);
        }

        [Fact]
        public void MergeParts_ValidParts_MergesProperly()
        {
            var partsCountBefore = DocManager.Inst.Project.parts.Count;
            Assert.Equal(2, partsCountBefore);

            var result = _controller.MergeParts(0, new[] { 1 });
            var okResult = Assert.IsType<OkObjectResult>(result);

            var project = DocManager.Inst.Project;
            // Now there should be exactly 1 part
            Assert.Single(project.parts);

            var mergedPart = Assert.IsType<UVoicePart>(project.parts[0]);
            Assert.Equal("TestPart1", mergedPart.name);
            Assert.Equal(0, mergedPart.position);
            Assert.Equal(3840, mergedPart.Duration); // 1920 + 1920
            
            // Should contain 2 notes
            Assert.Equal(2, mergedPart.notes.Count);
        }
    }
}
