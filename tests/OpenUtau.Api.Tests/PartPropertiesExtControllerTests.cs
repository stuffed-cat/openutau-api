using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Util;
using OpenUtau.Api.Controllers;

namespace OpenUtau.Api.Tests
{
    [Collection("Sequential")]
    public class PartPropertiesExtControllerTests : IDisposable
    {
        private PartPropertiesExtController _controller;
        
        public PartPropertiesExtControllerTests()
        {
            SetupHelper.InitDocManager();

            _controller = new PartPropertiesExtController();
            
            // Initialize a real UProject
            SetupHelper.CreateAndLoadRealProject(project => {
            
            // Set up a track
            var track = new UTrack(project) { TrackNo = 0 };
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

            });
        }

        public void Dispose()
        {
            SetupHelper.CreateAndLoadRealProject();
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
            Assert.Equal(1440, part2.Duration);
            Assert.Equal(2, part2.notes.Count);
        }


        [Fact]
        public void SplitNote_ValidTick_ShouldSplitNoteAndMaintainVibrato()
        {
            var project = DocManager.Inst.Project;
            var part = project.parts[0] as UVoicePart;
            var noteToSplit = part.notes.First(); // Assume it spans 0..480

            // Add vibrato to note to check copy logic
            noteToSplit.vibrato.length = 50;
            noteToSplit.vibrato.period = 175;
            noteToSplit.vibrato.depth = 25;
            noteToSplit.vibrato.@in = 10;
            noteToSplit.vibrato.@out = 10;
            noteToSplit.vibrato.shift = 0;
            noteToSplit.duration = 480;

            int originalCount = part.notes.Count;
            int splitTick = 240; // absolute is false by default, so it's part relative

            var result = _controller.SplitNote(0, 0, splitTick);

            var okResult = Assert.IsType<OkObjectResult>(result);
            
            // Should have 1 more note
            Assert.Equal(originalCount + 1, part.notes.Count);
            
            // Wait, Notes are in a SortedSet, so index 0 and 1 are the split results
            var n1 = part.notes.ElementAt(0);
            var n2 = part.notes.ElementAt(1);

            Assert.Equal(0, n1.position);
            Assert.Equal(240, n1.duration);
            
            Assert.Equal(240, n2.position);
            Assert.Equal(240, n2.duration);
            Assert.Equal(NotePresets.Default.SplittedLyric ?? "-", n2.lyric);
        }

        [Fact]
        public void SplitNote_AbsoluteTick_ShouldSplitInsideNote()
        {
            var project = DocManager.Inst.Project;
            var part = project.parts[0] as UVoicePart;
            Assert.NotNull(part);

            part.position = 480;

            // Absolute tick 720 falls inside the first note (relative tick 240).
            var result = _controller.SplitNote(0, 0, 720, absolute: true);

            var okResult = Assert.IsType<OkObjectResult>(result);

            Assert.Equal(5, part.notes.Count);
            var first = part.notes.ElementAt(0);
            var second = part.notes.ElementAt(1);

            Assert.Equal(0, first.position);
            Assert.Equal(240, first.duration);
            Assert.Equal(240, second.position);
            Assert.Equal(240, second.duration);
            Assert.Equal(NotePresets.Default.SplittedLyric ?? "-", second.lyric);
        }

        [Fact]
        public void SplitNote_ShouldBeUndoable()
        {
            var project = DocManager.Inst.Project;
            var part = project.parts[0] as UVoicePart;
            var originalCount = part.notes.Count;

            var result = _controller.SplitNote(0, 0, 240);
            var okResult = Assert.IsType<OkObjectResult>(result);

            Assert.Equal(originalCount + 1, part.notes.Count);

            DocManager.Inst.Undo();

            Assert.Equal(originalCount, part.notes.Count);
            var note = part.notes.First();
            Assert.Equal(0, note.position);
            Assert.Equal(480, note.duration);
        }

        [Fact]
        public void SplitNote_InvalidTick_ShouldReturnBadRequest()
        {
            var result = _controller.SplitNote(0, 0, 0);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("Split position must be within the note's duration", badRequest.Value.ToString());
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
            var track2 = new UTrack(project) { TrackNo = 1 };
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
