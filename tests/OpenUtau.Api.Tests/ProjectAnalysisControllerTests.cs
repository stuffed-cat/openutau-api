using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Api.Controllers;
using OpenUtau.Classic;
using OpenUtau.Core;
using Xunit;

namespace OpenUtau.Api.Tests
{
    [Collection("Sequential")]
    public class ProjectAnalysisControllerTests : IDisposable
    {
        private readonly ProjectAnalysisController _controller;
        private readonly string _singerKey;
        private readonly string _baseDir;
        private readonly string _singerDir;

        public ProjectAnalysisControllerTests()
        {
            SetupHelper.InitDocManager();
            _controller = new ProjectAnalysisController();

            _singerKey = $"TestSinger-{Guid.NewGuid():N}";
            _baseDir = Path.Combine(Path.GetTempPath(), $"OpenUtauApi-{Guid.NewGuid():N}");
            _singerDir = Path.Combine(_baseDir, _singerKey);
            Directory.CreateDirectory(_singerDir);

            File.WriteAllText(Path.Combine(_singerDir, "character.txt"), "name=Test Singer\n", System.Text.Encoding.UTF8);
            File.WriteAllText(Path.Combine(_singerDir, "oto.ini"), "missing.wav=la,0,0,0,0,0\nbad-line\n", System.Text.Encoding.UTF8);

            var voicebank = new Voicebank
            {
                Name = "Test Singer",
                File = Path.Combine(_singerDir, "character.txt"),
                BasePath = _baseDir,
            };

            var singer = new ClassicSinger(voicebank);
            singer.Reload();
            SingerManager.Inst.Singers[_singerKey] = singer;
        }

        public void Dispose()
        {
            SingerManager.Inst.Singers.Remove(_singerKey);
            if (Directory.Exists(_baseDir))
            {
                Directory.Delete(_baseDir, true);
            }
        }

        [Fact]
        public void ValidateVoicebank_ReturnsMissingAudioAndInvalidOtoIssues()
        {
            var result = _controller.ValidateVoicebank(_singerKey);

            var ok = Assert.IsType<OkObjectResult>(result);
            var json = JsonSerializer.Serialize(ok.Value);
            using var doc = JsonDocument.Parse(json);

            Assert.Equal(_singerKey, doc.RootElement.GetProperty("singerId").GetString());
            var summary = doc.RootElement.GetProperty("summary");
            Assert.Equal(2, summary.GetProperty("issueCount").GetInt32());
            Assert.Equal(1, summary.GetProperty("missingAudioFiles").GetInt32());
            Assert.Equal(1, summary.GetProperty("invalidOtoEntries").GetInt32());

            var types = doc.RootElement.GetProperty("issues")
                .EnumerateArray()
                .Select(item => item.GetProperty("type").GetString())
                .ToArray();

            Assert.Contains("MissingAudio", types);
            Assert.Contains("InvalidOto", types);
        }

        [Fact]
        public void ValidateVoicebank_InvalidSinger_ReturnsNotFound()
        {
            var result = _controller.ValidateVoicebank("missing-singer");

            Assert.IsType<NotFoundObjectResult>(result);
        }
    }
}