using System;
using System.Collections.Generic;
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

        private static (string singerId, string baseDir) CreateEditableSinger(string[] otoIniFiles, IEnumerable<(string relativePath, byte[] bytes)>? extraFiles = null)
        {
            var singerId = $"EditSinger-{Guid.NewGuid():N}";
            var baseDir = Path.Combine(Path.GetTempPath(), $"OpenUtauApi-{Guid.NewGuid():N}");
            var singerDir = Path.Combine(baseDir, singerId);
            Directory.CreateDirectory(singerDir);

            var characterPath = Path.Combine(singerDir, "character.txt");
            File.WriteAllText(characterPath, "name=Edit Singer\n", System.Text.Encoding.UTF8);

            foreach (var otoIniFile in otoIniFiles)
            {
                var fullPath = Path.Combine(singerDir, otoIniFile);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                File.WriteAllText(fullPath, "tone.wav=la,10,20,-30,40,50\n", System.Text.Encoding.UTF8);
            }

            foreach (var extra in extraFiles ?? Array.Empty<(string relativePath, byte[] bytes)>())
            {
                var fullPath = Path.Combine(singerDir, extra.relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                File.WriteAllBytes(fullPath, extra.bytes);
            }

            var voicebank = new Voicebank
            {
                Name = "Edit Singer",
                File = characterPath,
                BasePath = baseDir,
            };

            var singer = new ClassicSinger(voicebank);
            singer.Reload();
            SingerManager.Inst.Singers[singerId] = singer;
            return (singerId, baseDir);
        }

        private static void RemoveEditableSinger(string singerId, string baseDir)
        {
            SingerManager.Inst.Singers.Remove(singerId);
            if (Directory.Exists(baseDir))
            {
                Directory.Delete(baseDir, true);
            }
        }

        [Fact]
        public void UpdateSingerOtos_PersistsOtoParameters()
        {
            var wavBytes = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45 };
            var (singerId, baseDir) = CreateEditableSinger(new[] { "oto.ini" }, new[] { ("tone.wav", wavBytes) });

            try
            {
                var result = _controller.UpdateSingerOtos(singerId, new SingersController.OtoEditRequest
                {
                    OtoEdits = new List<SingersController.OtoEdit>
                    {
                        new SingersController.OtoEdit
                        {
                            Alias = "la",
                            File = "tone.wav",
                            Offset = 11,
                            Consonant = 22,
                            Cutoff = -33,
                            Preutter = 44,
                            Overlap = 55,
                        }
                    }
                });

                Assert.IsType<OkObjectResult>(result);

                var saved = File.ReadAllText(Path.Combine(baseDir, singerId, "oto.ini"), System.Text.Encoding.UTF8);
                Assert.Contains("tone.wav=la,11,22,-33,44,55", saved);

                var otoResult = Assert.IsType<OkObjectResult>(_controller.GetSingerOtos(singerId));
                var json = JsonSerializer.Serialize(otoResult.Value);
                using var doc = JsonDocument.Parse(json);
                var oto = doc.RootElement.EnumerateArray().Single();
                Assert.Equal(11, oto.GetProperty("Offset").GetDouble());
                Assert.Equal(22, oto.GetProperty("Consonant").GetDouble());
                Assert.Equal(-33, oto.GetProperty("Cutoff").GetDouble());
                Assert.Equal(44, oto.GetProperty("Preutter").GetDouble());
                Assert.Equal(55, oto.GetProperty("Overlap").GetDouble());
            }
            finally
            {
                RemoveEditableSinger(singerId, baseDir);
            }
        }

        [Fact]
        public void UpdateSingerOtos_InvalidSinger_ReturnsNotFound()
        {
            var result = _controller.UpdateSingerOtos("missing", new SingersController.OtoEditRequest
            {
                OtoEdits = new List<SingersController.OtoEdit>
                {
                    new SingersController.OtoEdit { Alias = "la", Offset = 1 }
                }
            });

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public void UpdateSingerOtos_UnknownOto_ReturnsNotFound()
        {
            var wavBytes = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45 };
            var (singerId, baseDir) = CreateEditableSinger(new[] { "oto.ini" }, new[] { ("tone.wav", wavBytes) });

            try
            {
                var result = _controller.UpdateSingerOtos(singerId, new SingersController.OtoEditRequest
                {
                    OtoEdits = new List<SingersController.OtoEdit>
                    {
                        new SingersController.OtoEdit { Alias = "missing", Offset = 1 }
                    }
                });

                Assert.IsType<NotFoundObjectResult>(result);
            }
            finally
            {
                RemoveEditableSinger(singerId, baseDir);
            }
        }

        [Fact]
        public void UpdateSingerOtos_NoParameters_ReturnsBadRequest()
        {
            var wavBytes = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45 };
            var (singerId, baseDir) = CreateEditableSinger(new[] { "oto.ini" }, new[] { ("tone.wav", wavBytes) });

            try
            {
                var result = _controller.UpdateSingerOtos(singerId, new SingersController.OtoEditRequest
                {
                    OtoEdits = new List<SingersController.OtoEdit>
                    {
                        new SingersController.OtoEdit { Alias = "la" }
                    }
                });

                var badRequest = Assert.IsType<BadRequestObjectResult>(result);
                Assert.Contains("No oto parameters provided", badRequest.Value.ToString());
            }
            finally
            {
                RemoveEditableSinger(singerId, baseDir);
            }
        }

        [Fact]
        public void UpdateSingerOtos_UsesSetToTargetSpecificOto()
        {
            var wavBytes = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45 };
            var (singerId, baseDir) = CreateEditableSinger(new[] { Path.Combine("bankA", "oto.ini"), Path.Combine("bankB", "oto.ini") }, new[]
            {
                (Path.Combine("bankA", "tone.wav"), wavBytes),
                (Path.Combine("bankB", "tone.wav"), wavBytes)
            });

            try
            {
                var result = _controller.UpdateSingerOtos(singerId, new SingersController.OtoEditRequest
                {
                    OtoEdits = new List<SingersController.OtoEdit>
                    {
                        new SingersController.OtoEdit
                        {
                            Alias = "la",
                            Set = "bankB",
                            File = "tone.wav",
                            Offset = 99
                        }
                    }
                });

                Assert.IsType<OkObjectResult>(result);

                var bankA = File.ReadAllText(Path.Combine(baseDir, singerId, "bankA", "oto.ini"), System.Text.Encoding.UTF8);
                var bankB = File.ReadAllText(Path.Combine(baseDir, singerId, "bankB", "oto.ini"), System.Text.Encoding.UTF8);

                Assert.Contains("tone.wav=la,10,20,-30,40,50", bankA);
                Assert.Contains("tone.wav=la,99,20,-30,40,50", bankB);
            }
            finally
            {
                RemoveEditableSinger(singerId, baseDir);
            }
        }
    }
}