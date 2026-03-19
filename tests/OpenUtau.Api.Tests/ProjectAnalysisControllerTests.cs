using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using OpenUtau.Api.Controllers;
using OpenUtau.Classic;
using OpenUtau.Core;
using OpenUtau.Core.Format;
using OpenUtau.Core.Ustx;
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

        private (string singerKey, string baseDir) CreateSinger(string otoContent, IEnumerable<(string fileName, byte[] bytes)>? files = null)
        {
            var singerKey = $"TestSinger-{Guid.NewGuid():N}";
            var baseDir = Path.Combine(Path.GetTempPath(), $"OpenUtauApi-{Guid.NewGuid():N}");
            var singerDir = Path.Combine(baseDir, singerKey);
            Directory.CreateDirectory(singerDir);

            File.WriteAllText(Path.Combine(singerDir, "character.txt"), "name=Test Singer\n", System.Text.Encoding.UTF8);
            File.WriteAllText(Path.Combine(singerDir, "oto.ini"), otoContent, System.Text.Encoding.UTF8);

            foreach (var file in files ?? Array.Empty<(string fileName, byte[] bytes)>())
            {
                File.WriteAllBytes(Path.Combine(singerDir, file.fileName), file.bytes);
            }

            var voicebank = new Voicebank
            {
                Name = "Test Singer",
                File = Path.Combine(singerDir, "character.txt"),
                BasePath = baseDir,
            };

            var singer = new ClassicSinger(voicebank);
            singer.Reload();
            SingerManager.Inst.Singers[singerKey] = singer;
            return (singerKey, baseDir);
        }

        private static void RemoveSinger(string singerKey, string baseDir)
        {
            SingerManager.Inst.Singers.Remove(singerKey);
            if (Directory.Exists(baseDir))
            {
                Directory.Delete(baseDir, true);
            }
        }

        private static IFormFile CreateProjectFile(Action<UProject> builder)
        {
            var project = Ustx.Create();
            builder(project);

            var tempFile = Path.GetTempFileName() + ".ustx";
            Ustx.Save(tempFile, project);

            var bytes = File.ReadAllBytes(tempFile);
            File.Delete(tempFile);

            return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "test.ustx")
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/x-yaml"
            };
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

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ValidateVoicebank_MissingSingerId_ReturnsBadRequest(string singerId)
        {
            var result = _controller.ValidateVoicebank(singerId);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Missing singerId", badRequest.Value);
        }

        [Fact]
        public void ValidateVoicebank_ReturnsZeroIssuesForHealthyVoicebank()
        {
            var wavBytes = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45 };
            var (singerKey, baseDir) = CreateSinger("tone.wav=la,0,0,0,0,0\n", new[] { ("tone.wav", wavBytes) });

            try
            {
                var result = _controller.ValidateVoicebank(singerKey);

                var ok = Assert.IsType<OkObjectResult>(result);
                var json = JsonSerializer.Serialize(ok.Value);
                using var doc = JsonDocument.Parse(json);

                var summary = doc.RootElement.GetProperty("summary");
                Assert.Equal(0, summary.GetProperty("issueCount").GetInt32());
                Assert.Equal(0, summary.GetProperty("missingAudioFiles").GetInt32());
                Assert.Equal(0, summary.GetProperty("invalidOtoEntries").GetInt32());
                Assert.Empty(doc.RootElement.GetProperty("issues").EnumerateArray());
            }
            finally
            {
                RemoveSinger(singerKey, baseDir);
            }
        }

        [Fact]
        public void ValidateVoicebank_CountsOnlyMissingAudioWhenOtoIsValid()
        {
            var (singerKey, baseDir) = CreateSinger("tone.wav=la,0,0,0,0,0\n");

            try
            {
                var result = _controller.ValidateVoicebank(singerKey);

                var ok = Assert.IsType<OkObjectResult>(result);
                var json = JsonSerializer.Serialize(ok.Value);
                using var doc = JsonDocument.Parse(json);

                var summary = doc.RootElement.GetProperty("summary");
                Assert.Equal(1, summary.GetProperty("issueCount").GetInt32());
                Assert.Equal(1, summary.GetProperty("missingAudioFiles").GetInt32());
                Assert.Equal(0, summary.GetProperty("invalidOtoEntries").GetInt32());

                var issue = doc.RootElement.GetProperty("issues").EnumerateArray().Single();
                Assert.Equal("MissingAudio", issue.GetProperty("type").GetString());
            }
            finally
            {
                RemoveSinger(singerKey, baseDir);
            }
        }

        [Fact]
        public void ValidateVoicebank_CountsOnlyInvalidOtoWhenAudioExists()
        {
            var wavBytes = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45 };
            var (singerKey, baseDir) = CreateSinger("bad-line\n", new[] { ("tone.wav", wavBytes) });

            try
            {
                var result = _controller.ValidateVoicebank(singerKey);

                var ok = Assert.IsType<OkObjectResult>(result);
                var json = JsonSerializer.Serialize(ok.Value);
                using var doc = JsonDocument.Parse(json);

                var summary = doc.RootElement.GetProperty("summary");
                Assert.Equal(1, summary.GetProperty("issueCount").GetInt32());
                Assert.Equal(0, summary.GetProperty("missingAudioFiles").GetInt32());
                Assert.Equal(1, summary.GetProperty("invalidOtoEntries").GetInt32());

                var issue = doc.RootElement.GetProperty("issues").EnumerateArray().Single();
                Assert.Equal("InvalidOto", issue.GetProperty("type").GetString());
            }
            finally
            {
                RemoveSinger(singerKey, baseDir);
            }
        }

        [Fact]
        public void ValidateVoicebank_CountsMultipleIndependentFailures()
        {
            var wavBytes = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45 };
            var otoContent = string.Join("\n", new[]
            {
                "tone-a.wav=la,0,0,0,0,0",
                "tone-b.wav=li,0,0,0,0,0",
                "bad-line"
            }) + "\n";
            var (singerKey, baseDir) = CreateSinger(otoContent, new[] { ("tone-a.wav", wavBytes) });

            try
            {
                var result = _controller.ValidateVoicebank(singerKey);

                var ok = Assert.IsType<OkObjectResult>(result);
                var json = JsonSerializer.Serialize(ok.Value);
                using var doc = JsonDocument.Parse(json);

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
            finally
            {
                RemoveSinger(singerKey, baseDir);
            }
        }

        [Fact]
        public void ValidateVoicebank_CountsMultipleMissingFilesSeparately()
        {
            var otoContent = string.Join("\n", new[]
            {
                "tone-a.wav=la,0,0,0,0,0",
                "tone-b.wav=li,0,0,0,0,0"
            }) + "\n";
            var (singerKey, baseDir) = CreateSinger(otoContent);

            try
            {
                var result = _controller.ValidateVoicebank(singerKey);

                var ok = Assert.IsType<OkObjectResult>(result);
                var json = JsonSerializer.Serialize(ok.Value);
                using var doc = JsonDocument.Parse(json);

                var summary = doc.RootElement.GetProperty("summary");
                Assert.Equal(2, summary.GetProperty("issueCount").GetInt32());
                Assert.Equal(2, summary.GetProperty("missingAudioFiles").GetInt32());
                Assert.Equal(0, summary.GetProperty("invalidOtoEntries").GetInt32());

                var issues = doc.RootElement.GetProperty("issues").EnumerateArray().ToArray();
                Assert.Equal(2, issues.Length);
                Assert.All(issues, issue => Assert.Equal("MissingAudio", issue.GetProperty("type").GetString()));
            }
            finally
            {
                RemoveSinger(singerKey, baseDir);
            }
        }

        [Fact]
        public void ValidateVoicebank_DoesNotInventIssuesForValidOtoWithExistingFile()
        {
            var wavBytes = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45 };
            var (singerKey, baseDir) = CreateSinger("tone.wav=la,0,0,0,0,0\n", new[] { ("tone.wav", wavBytes) });

            try
            {
                var result = _controller.ValidateVoicebank(singerKey);

                var ok = Assert.IsType<OkObjectResult>(result);
                var json = JsonSerializer.Serialize(ok.Value);
                using var doc = JsonDocument.Parse(json);

                var summary = doc.RootElement.GetProperty("summary");
                Assert.Equal(0, summary.GetProperty("issueCount").GetInt32());
                Assert.Empty(doc.RootElement.GetProperty("issues").EnumerateArray());
            }
            finally
            {
                RemoveSinger(singerKey, baseDir);
            }
        }

        [Fact]
        public void DetectConflicts_ReturnsExpectedConflictTypes()
        {
            var file = CreateProjectFile(project =>
            {
                project.tracks.Add(new UTrack(project) { TrackNo = 0 });

                var part = new UVoicePart { trackNo = 0, position = 0, Duration = 960 };
                var first = project.CreateNote(60, 0, 480);
                first.lyric = "la";
                part.notes.Add(first);

                var second = project.CreateNote(62, 240, 10);
                second.lyric = "li";
                part.notes.Add(second);

                var third = project.CreateNote(120, 500, 480);
                third.lyric = "lu";
                part.notes.Add(third);

                project.parts.Add(part);
            });

            var result = _controller.DetectConflicts(file);

            var ok = Assert.IsType<OkObjectResult>(result);
            var json = JsonSerializer.Serialize(ok.Value);
            using var doc = JsonDocument.Parse(json);

            Assert.Equal(3, doc.RootElement.GetProperty("TotalConflicts").GetInt32());
            var types = doc.RootElement.GetProperty("Details")
                .EnumerateArray()
                .Select(item => item.GetProperty("type").GetString())
                .ToArray();

            Assert.Contains("Overlap", types);
            Assert.Contains("TooShort", types);
            Assert.Contains("PitchOutOfRange", types);
        }

        [Fact]
        public void DetectConflicts_HealthyProject_ReturnsZeroConflicts()
        {
            var file = CreateProjectFile(project =>
            {
                project.tracks.Add(new UTrack(project) { TrackNo = 0 });

                var part = new UVoicePart { trackNo = 0, position = 0, Duration = 960 };
                var note = project.CreateNote(60, 0, 480);
                note.lyric = "la";
                part.notes.Add(note);
                project.parts.Add(part);
            });

            var result = _controller.DetectConflicts(file);

            var ok = Assert.IsType<OkObjectResult>(result);
            var json = JsonSerializer.Serialize(ok.Value);
            using var doc = JsonDocument.Parse(json);

            Assert.Equal(0, doc.RootElement.GetProperty("TotalConflicts").GetInt32());
            Assert.Empty(doc.RootElement.GetProperty("Details").EnumerateArray());
        }

        [Fact]
        public void GetStatistics_ReturnsExpectedCounts()
        {
            var file = CreateProjectFile(project =>
            {
                project.tracks.Add(new UTrack(project) { TrackNo = 0 });

                var part = new UVoicePart { trackNo = 0, position = 0, Duration = 960 };
                var first = project.CreateNote(60, 0, 480);
                first.lyric = "la";
                part.notes.Add(first);

                var second = project.CreateNote(62, 360, 480);
                second.lyric = "li";
                part.notes.Add(second);

                project.parts.Add(part);
            });

            var result = _controller.GetStatistics(file);

            var ok = Assert.IsType<OkObjectResult>(result);
            var json = JsonSerializer.Serialize(ok.Value);
            using var doc = JsonDocument.Parse(json);

            Assert.Equal(2, doc.RootElement.GetProperty("Tracks").GetInt32());
            var parts = doc.RootElement.GetProperty("Parts");
            Assert.Equal(1, parts.GetProperty("Total").GetInt32());
            Assert.Equal(1, parts.GetProperty("Voice").GetInt32());
            Assert.Equal(0, parts.GetProperty("Wave").GetInt32());

            var notes = doc.RootElement.GetProperty("Notes");
            Assert.Equal(2, notes.GetProperty("Total").GetInt32());
            Assert.Equal(2, notes.GetProperty("WithValidLyrics").GetInt32());

            Assert.Equal(100.0, doc.RootElement.GetProperty("CompletenessPercentage").GetDouble());
            Assert.Equal(75.0, doc.RootElement.GetProperty("QualityScore").GetDouble());
            Assert.Equal(1, doc.RootElement.GetProperty("Issues").GetProperty("Overlaps").GetInt32());
        }

        [Fact]
        public void ValidateImport_InvalidFile_ReturnsBadRequest()
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes("not a midi file");
            var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "test.txt")
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/plain"
            };

            var result = _controller.ValidateImport(file);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var json = JsonSerializer.Serialize(badRequest.Value);
            using var doc = JsonDocument.Parse(json);

            Assert.False(doc.RootElement.GetProperty("valid").GetBoolean());
            Assert.Contains("Parse error", doc.RootElement.GetProperty("error").GetString());
        }
    }
}