using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Api.Controllers;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Format;
using Xunit;

namespace OpenUtau.Api.Tests
{
    [Collection("Sequential")]
    public class NotesControllerTests
    {
        public NotesControllerTests()
        {
            SetupHelper.InitDocManager();
        }

        private UProject CreateTestProject(out UVoicePart part)
        {
            var project = new UProject();
            var track = new UTrack(project);
            project.tracks.Add(track);
            
            part = new UVoicePart() { trackNo = 0, position = 0, Duration = 1000 };
            project.parts.Add(part);
            
            var note = project.CreateNote(60, 480, 480);
            note.lyric = "test";
            part.notes.Add(note);

            SetupHelper.SetProject(project);
            return project;
        }

        private IFormFile CreateProjectFile(UProject project)
        {
            var tempFile = Path.GetTempFileName() + ".ustx";
            Ustx.Save(tempFile, project);
            var bytes = File.ReadAllBytes(tempFile);
            File.Delete(tempFile);
            
            return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "test.ustx");
        }

        [Fact]
        public void GetNoteProperties_ValidNote_ReturnsOk()
        {
            CreateTestProject(out _);
            var controller = new NotesController();

            var result = controller.GetNoteProperties(0, 0);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var json = JsonSerializer.Serialize(okResult.Value);
            Assert.Contains("test", json);
            Assert.Contains("60", json);
        }

        [Fact]
        public void GetNoteProperties_InvalidPart_ReturnsBadRequest()
        {
            CreateTestProject(out _);
            var controller = new NotesController();

            var result = controller.GetNoteProperties(99, 0);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Invalid part index", badRequest.Value);
        }

        [Fact]
        public void AddNote_ValidNote_AddsNoteToPart()
        {
            var project = CreateTestProject(out UVoicePart _) ;
            var controller = new NotesController();
            var file = CreateProjectFile(project);

            var result = controller.AddNote(file, partIndex: 0, position: 1000, duration: 480, tone: 62, lyric: "new");

            var fileResult = Assert.IsType<FileStreamResult>(result);
            Assert.Equal("application/json", fileResult.ContentType);
            
            // Read back to verify
            var newProject = Ustx.Load(((FileStream)fileResult.FileStream).Name);
            var part = newProject.parts[0] as UVoicePart;
            Assert.Equal(2, part.notes.Count);
            var lastNote = part.notes.ElementAt(1);
            Assert.Equal(1000, lastNote.position);
            Assert.Equal("new", lastNote.lyric);
        }

        [Fact]
        public void RemoveNote_ValidIndex_RemovesNote()
        {
            var project = CreateTestProject(out UVoicePart _) ;
            var controller = new NotesController();
            var file = CreateProjectFile(project);

            var result = controller.RemoveNote(file, partIndex: 0, matchPosition: 480);

            var fileResult = Assert.IsType<FileStreamResult>(result);
            
            var newProject = Ustx.Load(((FileStream)fileResult.FileStream).Name);
            var part = newProject.parts[0] as UVoicePart;
            Assert.Empty(part.notes);
        }

        [Fact]
        public void UpdateNote_ValidData_UpdatesNoteAndReturnsOk()
        {
            var project = CreateTestProject(out UVoicePart _) ;
            var controller = new NotesController();
            var file = CreateProjectFile(project);

            var result = controller.UpdateNote(
                file, 
                partIndex: 0, 
                matchPosition: 480,
                newPosition: 500,
                newDuration: 960,
                newTone: 65,
                newLyric: "updated"
            );

            var fileResult = Assert.IsType<FileStreamResult>(result);
            
            var newProject = Ustx.Load(((FileStream)fileResult.FileStream).Name);
            var part = newProject.parts[0] as UVoicePart;
            Assert.Single(part.notes);
            var note = part.notes.ElementAt(0);
            Assert.Equal(500, note.position); 
            Assert.Equal(960, note.duration);
            Assert.Equal("updated", note.lyric);
        }
    }
}
