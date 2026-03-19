using Microsoft.AspNetCore.Mvc;
using OpenUtau.Api.Controllers;
using OpenUtau.Core;
using OpenUtau.Core.Format;
using OpenUtau.Core.Ustx;
using System.Collections.Generic;
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
        public void SetTrackFlags_UpdatesClassicFlags()
        {
            var result = _controller.SetTrackFlags(0, new TracksController.ClassicFlagsRequest
            {
                Flags = new List<TracksController.ClassicFlagRequest>
                {
                    new TracksController.ClassicFlagRequest { Flag = "g", Value = -5 },
                    new TracksController.ClassicFlagRequest { Flag = "B", Value = 50 }
                }
            });

            Assert.IsType<OkObjectResult>(result);

            var track = DocManager.Inst.Project.tracks[0];
            Assert.Contains(track.TrackExpressions, expr => expr.abbr == Ustx.GEN && expr.CustomDefaultValue == -5);
            Assert.Contains(track.TrackExpressions, expr => expr.abbr == Ustx.BRE && expr.CustomDefaultValue == 50);

            var flags = Assert.IsType<OkObjectResult>(_controller.GetTrackFlags(0)).Value!;
            var flagsProp = flags.GetType().GetProperty("flags")!;
            var values = ((System.Collections.IEnumerable)flagsProp.GetValue(flags)!).Cast<object>().ToList();
            Assert.Contains(values, item => item.GetType().GetProperty("flag")!.GetValue(item)?.ToString() == "g" && (int?)item.GetType().GetProperty("value")!.GetValue(item) == -5);
        }

        [Fact]
        public void UpdateTrackPhonemizerConfig_ReplaceAndPatch_ReturnsUpdatedConfig()
        {
            var factories = DocManager.Inst.PhonemizerFactories ?? OpenUtau.Api.PhonemizerFactory.GetAll();
            typeof(DocManager).GetProperty("PhonemizerFactories", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.SetValue(DocManager.Inst, factories);
            var selected = factories
                .Select(factory => new {
                    Factory = factory,
                    Phonemizer = factory.Create(),
                })
                .Where(x => x.Phonemizer != null)
                .Select(x => {
                    var member = x.Phonemizer!.GetType()
                        .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .FirstOrDefault(m =>
                        {
                            if (m.Name is "Name" or "Tag" or "Author" or "Language" or "Testing" or "singer" or "timeAxis" or "bpm" or "DictionariesPath" or "PluginDir") {
                                return false;
                            }
                            if (m is FieldInfo field) {
                                return (field.FieldType == typeof(string) || field.FieldType.IsPrimitive || field.FieldType.IsEnum);
                            }
                            if (m is PropertyInfo prop) {
                                return prop.CanWrite && prop.GetIndexParameters().Length == 0 &&
                                       (prop.PropertyType == typeof(string) || prop.PropertyType.IsPrimitive || prop.PropertyType.IsEnum);
                            }
                            return false;
                        });

                    return new { x.Factory, x.Phonemizer, Member = member };
                })
                .FirstOrDefault(x => x.Member != null);

            Assert.NotNull(selected);
            Assert.NotNull(selected.Phonemizer);
            Assert.NotNull(selected.Member);

            object patchValue = selected.Member is FieldInfo field
                ? field.FieldType == typeof(bool) ? true
                : field.FieldType == typeof(int) ? 123
                : field.FieldType.IsEnum ? Enum.GetValues(field.FieldType).GetValue(0)!
                : "api_test_value"
                : selected.Member is PropertyInfo prop && prop.PropertyType == typeof(bool) ? true
                : selected.Member is PropertyInfo prop2 && prop2.PropertyType == typeof(int) ? 123
                : selected.Member is PropertyInfo prop3 && prop3.PropertyType.IsEnum ? Enum.GetValues(prop3.PropertyType).GetValue(0)!
                : "api_test_value";

            var result = _controller.UpdateTrackPhonemizerConfig(0, new TracksController.TrackPhonemizerConfigRequest {
                PhonemizerType = selected.Factory.type.FullName,
                ConfigPatch = JsonSerializer.SerializeToElement(new Dictionary<string, object?> {
                    { selected.Member.Name, patchValue }
                })
            });

            var response = Assert.IsAssignableFrom<ObjectResult>(result);
            Assert.True((response.StatusCode ?? 200) == 200, response.Value?.ToString() ?? "null");
            var project = DocManager.Inst.Project;
            Assert.NotNull(project.tracks[0].Phonemizer);
            Assert.Equal(selected.Factory.type.FullName, project.tracks[0].Phonemizer.GetType().FullName);

            var member = selected.Member;
            var value = member is FieldInfo field2 ? field2.GetValue(project.tracks[0].Phonemizer) : ((PropertyInfo)member).GetValue(project.tracks[0].Phonemizer);
            var expected = patchValue is Array arr ? arr.GetValue(0) : patchValue;
            Assert.Equal(expected, value);
        }
    }
}
