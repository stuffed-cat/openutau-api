using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Api.Controllers;
using OpenUtau.Classic;
using OpenUtau.Core;
using OpenUtau.Core.Format;
using OpenUtau.Core.Render;
using OpenUtau.Core.Ustx;
using Xunit;

namespace OpenUtau.Api.Tests
{
    [Collection("Sequential")]
    public class PitchCurveControllerTests : IDisposable
    {
        private readonly PitchCurveController _controller;
        private readonly string _baseDir;
        private readonly string _singerId;

        public PitchCurveControllerTests()
        {
            SetupHelper.InitDocManager();
            _controller = new PitchCurveController();

            _singerId = $"TestSinger-{Guid.NewGuid():N}";
            _baseDir = Path.Combine(Path.GetTempPath(), $"OpenUtauApi-{Guid.NewGuid():N}");
            CreateSingerFixture(_baseDir, _singerId);

            SetupHelper.CreateAndLoadRealProject(project =>
            {
                project.tracks.Clear();
                project.tracks.Add(new UTrack(project)
                {
                    TrackNo = 0,
                    TrackName = "PitchTrack"
                });

                project.parts.Clear();
                var part = new UVoicePart
                {
                    trackNo = 0,
                    position = 0,
                    Duration = 960
                };

                var note1 = project.CreateNote(60, 0, 480);
                note1.lyric = "a";
                part.notes.Add(note1);

                var note2 = project.CreateNote(62, 480, 480);
                note2.lyric = "a";
                part.notes.Add(note2);

                project.parts.Add(part);
            });

            DocManager.Inst.Project.tracks[0].Singer = SingerManager.Inst.Singers[_singerId];
            DocManager.Inst.Project.tracks[0].RendererSettings.renderer = Renderers.ENUNU;
            DocManager.Inst.Project.tracks[0].RendererSettings.Renderer = Renderers.CreateRenderer(Renderers.ENUNU);
            PrepareRenderPhrase((UVoicePart)DocManager.Inst.Project.parts[0]);
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
        public void BakePitch_WithExplicitSelection_OnlyChangesSelectedNotes()
        {
            var part = (UVoicePart)DocManager.Inst.Project.parts[0];
            var selectedNote = part.notes.ElementAt(0);
            var untouchedNote = part.notes.ElementAt(1);

            selectedNote.pitch.AddPoint(new PitchPoint(13, 37));
            var selectedBefore = selectedNote.pitch.data.Count;
            var untouchedBefore = untouchedNote.pitch.data.Count;

            var result = _controller.BakePitch(0, new PitchCurveController.BakePitchRequest
            {
                NoteIndexes = new List<int> { 0 }
            });

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);

            Assert.NotEqual(selectedBefore, selectedNote.pitch.data.Count);
            Assert.Equal(untouchedBefore, untouchedNote.pitch.data.Count);
            Assert.DoesNotContain(selectedNote.pitch.data, p => p.X == 13 && p.Y == 37);
        }

        private static void CreateSingerFixture(string baseDir, string singerId)
        {
            var singerDir = Path.Combine(baseDir, singerId);
            Directory.CreateDirectory(singerDir);

            var characterPath = Path.Combine(singerDir, "character.txt");
            File.WriteAllText(characterPath, "name=Test Singer\n", System.Text.Encoding.UTF8);

            var otoPath = Path.Combine(singerDir, "oto.ini");
            File.WriteAllText(otoPath, "a.wav=a,0,0,0,0,0\n", System.Text.Encoding.UTF8);

            var voicebank = new Voicebank
            {
                Id = singerId,
                Name = "Test Singer",
                File = characterPath,
                BasePath = baseDir,
            };

            var singer = new ClassicSinger(voicebank);
            singer.Reload();
            SingerManager.Inst.Singers[singerId] = singer;
        }

        private static void PrepareRenderPhrase(UVoicePart part)
        {
            var project = DocManager.Inst.Project;
            var track = project.tracks[part.trackNo];

            var note = part.notes.First();
            if (project.expressions.TryGetValue(OpenUtau.Core.Format.Ustx.MODP, out var modpDescriptor))
            {
                note.phonemeExpressions.Add(new UExpression(modpDescriptor)
                {
                    index = 0,
                    value = 0,
                });
            }

            var phoneme = new UPhoneme
            {
                Parent = note,
                position = note.position,
                phoneme = "a"
            };

            typeof(UPhoneme).GetProperty("Duration", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.SetValue(phoneme, note.duration);
            typeof(UPhoneme).GetProperty("PositionMs", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.SetValue(phoneme, project.timeAxis.TickPosToMsPos(part.position + note.position));
            typeof(UPhoneme).GetProperty("EndMs", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.SetValue(phoneme, project.timeAxis.TickPosToMsPos(part.position + note.End));
            typeof(UPhoneme).GetProperty("preutter", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.SetValue(phoneme, 0d);
            typeof(UPhoneme).GetProperty("overlap", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.SetValue(phoneme, 0d);
            typeof(UPhoneme).GetProperty("autoPreutter", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.SetValue(phoneme, 0d);
            typeof(UPhoneme).GetProperty("autoOverlap", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.SetValue(phoneme, 0d);

            var ctor = typeof(RenderPhrase).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .First(c => c.GetParameters().Length == 4);
            var phrase = (RenderPhrase)ctor.Invoke(new object[]
            {
                project,
                track,
                part,
                new[] { phoneme }
            });

            part.renderPhrases = new List<RenderPhrase> { phrase };
        }
    }
}