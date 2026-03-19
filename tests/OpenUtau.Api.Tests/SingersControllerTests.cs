using System;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Api.Controllers;
using OpenUtau.Classic;
using OpenUtau.Core;
using Xunit;

namespace OpenUtau.Api.Tests
{
    [Collection("Sequential")]
    public class SingersControllerTests : IDisposable
    {
        private readonly SingersController _controller;
        private readonly string _singerId;
        private readonly string _baseDir;
        private readonly string _singerDir;

        public SingersControllerTests()
        {
            SetupHelper.InitDocManager();
            _controller = new SingersController();

            _singerId = $"TestSinger-{Guid.NewGuid():N}";
            _baseDir = Path.Combine(Path.GetTempPath(), $"OpenUtauApi-{Guid.NewGuid():N}");
            _singerDir = Path.Combine(_baseDir, _singerId);
            Directory.CreateDirectory(_singerDir);

            var characterPath = Path.Combine(_singerDir, "character.txt");
            File.WriteAllText(characterPath, "name=Test Singer\n", System.Text.Encoding.UTF8);

            var prefixMapPath = Path.Combine(_singerDir, "prefix.map");
            File.WriteAllText(prefixMapPath, "C4\tpre-\t-suf\n", System.Text.Encoding.UTF8);

            var voicebank = new Voicebank
            {
                Name = "Test Singer",
                File = characterPath,
                BasePath = _baseDir,
            };
            var singer = new ClassicSinger(voicebank);
            singer.Reload();
            SingerManager.Inst.Singers[_singerId] = singer;
        }

        public void Dispose()
        {
            SingerManager.Inst.Singers.Remove(_singerId);
            if (Directory.Exists(_baseDir))
            {
                Directory.Delete(_baseDir, true);
            }
        }

        [Fact]
        public void GetSingerPrefixMap_ReturnsContent()
        {
            var result = _controller.GetSingerPrefixMap(_singerId);

            var content = Assert.IsType<ContentResult>(result);
            Assert.Equal("text/plain", content.ContentType);
            Assert.Contains("C4\tpre-\t-suf", content.Content);
        }

        [Fact]
        public void SaveSingerPrefixMap_UpdatesFile()
        {
            var result = _controller.SaveSingerPrefixMap(_singerId, new SingersController.PrefixMapRequest
            {
                Content = "D4\ta\tb\n"
            });

            Assert.IsType<OkObjectResult>(result);

            var saved = File.ReadAllText(Path.Combine(_singerDir, "prefix.map"));
            Assert.Equal("D4\ta\tb\n", saved);
        }

        [Fact]
        public void DeleteSingerPrefixMap_RemovesFile()
        {
            var result = _controller.DeleteSingerPrefixMap(_singerId);

            Assert.IsType<OkObjectResult>(result);
            Assert.False(File.Exists(Path.Combine(_singerDir, "prefix.map")));
        }

        [Fact]
        public void SaveSingerPrefixMap_InvalidSinger_ReturnsNotFound()
        {
            var result = _controller.SaveSingerPrefixMap("missing", new SingersController.PrefixMapRequest { Content = "x" });

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Contains("Classic Singer not found", notFound.Value.ToString());
        }
    }
}