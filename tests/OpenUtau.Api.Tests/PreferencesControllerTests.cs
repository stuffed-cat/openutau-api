using System.Collections.Generic;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Api.Controllers;
using OpenUtau.Core.Util;
using Xunit;

namespace OpenUtau.Api.Tests
{
    [Collection("Sequential")]
    public class PreferencesControllerTests
    {
        private PreferencesController _controller;

        public PreferencesControllerTests()
        {
            SetupHelper.InitDocManager();
            _controller = new PreferencesController();
            
            // Native initialization of preferences
            Preferences.Reset();
        }

        [Fact]
        public void GetPreferences_ReturnsNativeDefault()
        {
            var res = _controller.GetPreferences();
            var okResult = res as OkObjectResult;
            Assert.NotNull(okResult);
            
            var prefs = okResult.Value as Preferences.SerializablePreferences;
            Assert.NotNull(prefs);
            Assert.Equal(Preferences.Default.ThemeName, prefs.ThemeName);
        }

        [Fact]
        public void UpdatePreferences_RealUpdate_ChangesMultipleFields()
        {
            // Create a payload of various realistic fields mapped to the Preference schema
            var jsonString = @"{
                ""OnnxRunner"": ""TestRunner"",
                ""OnnxGpu"": 1,
                ""DiffSingerSteps"": 50,
                ""PreRender"": false,
                ""Language"": ""zh-CN""
            }";
            var request = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonString);
            
            var res = _controller.UpdatePreferences(request);
            var okResult = res as OkObjectResult;
            Assert.NotNull(okResult);
            
            // Assert that the native Preferences.Default object has genuinely mutated
            Assert.Equal("TestRunner", Preferences.Default.OnnxRunner);
            Assert.Equal(1, Preferences.Default.OnnxGpu);
            Assert.Equal(50, Preferences.Default.DiffSingerSteps);
            Assert.False(Preferences.Default.PreRender);
            Assert.Equal("zh-CN", Preferences.Default.Language);
        }

        [Fact]
        public void UpdatePreferences_InvalidField_IgnoredGracefully()
        {
            var originalTheme = Preferences.Default.ThemeName;
            var jsonString = @"{
                ""SomeFakeField"": ""TestRunner"",
                ""ThemeName"": ""Dark""
            }";
            var request = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonString);
            
            var res = _controller.UpdatePreferences(request);
            var okResult = res as OkObjectResult;
            Assert.NotNull(okResult);

            Assert.Equal("Dark", Preferences.Default.ThemeName);
        }
    }
}
