using Microsoft.AspNetCore.Mvc;
using OpenUtau.Api.Controllers;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace OpenUtau.Api.Tests
{
    [Collection("Sequential")]
    public class TracksControllerTests
    {
        private readonly TracksController _controller;

        public TracksControllerTests()
        {
            SetupHelper.InitDocManager();
            _controller = new TracksController();
            
            SetupHelper.CreateAndLoadRealProject(project => {
            project.tracks.Clear();
            var track = new UTrack(project) 
            { 
                TrackNo = 0,
                TrackName = "OriginalTrack",
                TrackColor = "Blue"
            };
            project.tracks.Add(track);

            });
        }

        [Fact]
        public void GetTrackProperties_ValidTrack_ReturnsOk()
        {
            var result = _controller.GetTrackProperties(0);
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
            
            // Just verifying properties via reflection since it returns an anonymous object
            var val = okResult.Value;
            var nameProp = val.GetType().GetProperty("trackName");
            Assert.Equal("OriginalTrack", nameProp.GetValue(val));
        }

        [Fact]
        public void GetTrackProperties_InvalidTrack_ReturnsBadRequest()
        {
            var result = _controller.GetTrackProperties(999);
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Invalid track index", badRequestResult.Value);
        }

        [Fact]
        public void RenameTrack_ValidTrack_ChangesName()
        {
            var result = _controller.RenameTrack(0, "NewTrackName");
            var okResult = Assert.IsType<OkObjectResult>(result);
            
            var project = DocManager.Inst.Project;
            Assert.Equal("NewTrackName", project.tracks[0].TrackName);
        }

        [Fact]
        public void SetTrackColor_ValidTrack_ChangesColor()
        {
            var result = _controller.SetTrackColor(0, "Red");
            var okResult = Assert.IsType<OkObjectResult>(result);
            
            var project = DocManager.Inst.Project;
            Assert.Equal("Red", project.tracks[0].TrackColor);
        }

        [Fact]
        public void SetTrackSinger_ValidSinger_ChangesSinger()
        {
            var vb = new OpenUtau.Classic.Voicebank() { Id = "TestSinger", Name = "TestSinger", File = "dummy/character.txt", BasePath = "dummy" };
            var singer = new OpenUtau.Classic.ClassicSinger(vb);
            OpenUtau.Core.SingerManager.Inst.Singers["TestSinger"] = singer;

            var result = _controller.SetTrackSinger(0, "TestSinger");
            var okResult = Assert.IsType<OkObjectResult>(result);
            
            var project = DocManager.Inst.Project;
            Assert.NotNull(project.tracks[0].Singer);
            Assert.Same(singer, project.tracks[0].Singer);
        }

        [Fact]
        public void SetTrackSinger_InvalidSinger_ReturnsBadRequest()
        {
            var result = _controller.SetTrackSinger(0, "NonExistentSinger");
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("not found", badRequestResult.Value.ToString());
        }

        [Fact]
        public void UpdateTrackPhonemizerConfig_ReplaceAndPatch_ReturnsUpdatedConfig()
        {
            var factory = DocManager.Inst.PhonemizerFactories.FirstOrDefault(f =>
                f.type.FullName == "OpenUtau.Core.DiffSinger.Phonemizers.DiffSingerRhythmizerPhonemizer" ||
                f.name == "DiffSinger Rhythmizer Phonemizer");

            Assert.NotNull(factory);

            var result = _controller.UpdateTrackPhonemizerConfig(0, new TracksController.TrackPhonemizerConfigRequest {
                PhonemizerType = factory.type.FullName,
                ConfigPatch = JsonSerializer.SerializeToElement(new {
                    rhythmizer = "api_test_rhythmizer"
                })
            });

            var okResult = Assert.IsType<OkObjectResult>(result);
            var project = DocManager.Inst.Project;
            Assert.NotNull(project.tracks[0].Phonemizer);
            Assert.Equal(factory.type.FullName, project.tracks[0].Phonemizer.GetType().FullName);

            var member = project.tracks[0].Phonemizer.GetType().GetField("rhythmizer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? (MemberInfo)project.tracks[0].Phonemizer.GetType().GetProperty("rhythmizer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.NotNull(member);
            var value = member is FieldInfo field ? field.GetValue(project.tracks[0].Phonemizer) : ((PropertyInfo)member).GetValue(project.tracks[0].Phonemizer);
            Assert.Equal("api_test_rhythmizer", value);
        }
    }
}
