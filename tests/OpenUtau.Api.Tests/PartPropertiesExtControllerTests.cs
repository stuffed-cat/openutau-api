using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using OpenUtau.Api.Controllers;

namespace OpenUtau.Api.Tests
{
    public class PartPropertiesExtControllerTests : IDisposable
    {
        private PartPropertiesExtController _controller;
        
        public PartPropertiesExtControllerTests()
        {
            _controller = new PartPropertiesExtController();
            
            // Initialize a real UProject
            var project = new UProject();
            
            // Set up a track
            var track = new UTrack { TrackNo = 0 };
            project.tracks.Add(track);
            
            // Set up a part
            var part = new UVoicePart { trackNo = 0, position = 0, Duration = 1920 };
            project.parts.Add(part);
            
            // Manually add notes
            var note1 = project.CreateNote(60, 0, 480);
            var note2 = project.CreateNote(62, 480, 480);
            var note3 = project.CreateNote(64, 960, 480);
            var note4 = project.CreateNote(65, 1440, 480);
            part.notes.Add(note1);
            part.notes.Add(note2);
            part.notes.Add(note3);
            part.notes.Add(note4);

            SetupHelper.InitDocManager();
            SetupHelper.SetProject(project);
        }

        public void Dispose()
        {
            SetupHelper.SetProject(null);
        }

        [Fact]
        public void SplitPart_ValidTick_ShouldSplitIntoTwoParts()
        {
            // The part currently spans 0 to 1920 with 4 notes.
            // Split at tick 960 should result in 2 parts.
            var result = _controller.SplitPart(0, 960);
            
            var okResult = Assert.IsType<OkObjectResult>(result);
            var project = DocManager.Inst.Project;
            
            // Original part was removed, 2 new parts added. Total parts for track 0 = 2.
            Assert.Equal(2, project.parts.Count);
            
            var part1 = project.parts[0] as UVoicePart;
            var part2 = project.parts[1] as UVoicePart;
            
            Assert.NotNull(part1);
            Assert.NotNull(part2);
            
            Assert.Equal(0, part1.position);
            Assert.Equal(960, part1.Duration);
            Assert.Equal(2, part1.notes.Count);
            
            Assert.Equal(960, part2.position);
            Assert.Equal(960, part2.Duration);
            Assert.Equal(2, part2.notes.Count);
        }

        [Fact]
        public void MergeParts_ValidIndexes_ShouldMergeIntoOnePart()
        {
            // Add a second part adjacent to the first
            var project = DocManager.Inst.Project;
            var part2 = new UVoicePart { trackNo = 0, position = 1920, Duration = 1920 };
            var note5 = project.CreateNote(67, 0, 480);
            part2.notes.Add(note5);
            project.parts.Add(part2);
            
            Assert.Equal(2, project.parts.Count);
            
            // Action
            var result = _controller.MergeParts(new int[] { 0, 1 });
            
            var okResult = Assert.IsType<OkObjectResult>(result);
            
            Assert.Single(project.parts);
            
            var mergedPart = project.parts[0] as UVoicePart;
            Assert.NotNull(mergedPart);
            Assert.Equal(0, mergedPart.position);
            Assert.Equal(3840, mergedPart.Duration);
            Assert.Equal(5, mergedPart.notes.Count);
        }

        [Fact]
        public void SoloPart_ShouldSoloTrackAndMuteOthers()
        {
            // Add another track and part
            var project = DocManager.Inst.Project;
            var track2 = new UTrack { TrackNo = 1 };
            project.tracks.Add(track2);
            
            var part2 = new UVoicePart { trackNo = 1, position = 0, Duration = 1920 };
            project.parts.Add(part2);
            
            // Action
            var result = _controller.SoloPart(0);
            
            var okResult = Assert.IsType<OkObjectResult>(result);
            
            Assert.True(project.tracks[0].Solo);
            Assert.False(project.tracks[0].Muted);
            
            Assert.False(project.tracks[1].Solo);
            Assert.True(project.tracks[1].Muted);
        }
    }
}
