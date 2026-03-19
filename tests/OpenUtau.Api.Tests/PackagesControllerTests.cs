using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Api.Controllers;
using OpenUtau.Core;
using OpenUtau.Core.Util;

namespace OpenUtau.Api.Tests
{
    [Collection("Sequential")]
    public class PackagesControllerTests : IDisposable
    {
        private readonly PackagesController _controller;
        private readonly string _originalDataPath;
        private readonly string _sourceRoot;
        private readonly string _tempRoot;

        public PackagesControllerTests()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            SetupHelper.InitDocManager();
            Preferences.Reset();
            Preferences.Default.InstallToAdditionalSingersPath = false;
            Preferences.Default.AdditionalSingerPath = string.Empty;

            _originalDataPath = GetDataPath();
            _sourceRoot = Path.Combine(Path.GetTempPath(), $"MalformedVoicebankSource-{Guid.NewGuid():N}");
            _tempRoot = Path.Combine(Path.GetTempPath(), $"OpenUtauPackages-{Guid.NewGuid():N}");
            SetDataPath(_tempRoot);
            _controller = new PackagesController();
        }

        public void Dispose()
        {
            SetDataPath(_originalDataPath);
            if (Directory.Exists(_sourceRoot))
            {
                Directory.Delete(_sourceRoot, true);
            }
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, true);
            }
        }

        [Fact]
        public async Task InstallPackage_IgnoresInvalidCharacterYaml()
        {
            var archivePath = CreateMalformedVoicebankArchive();
            await using var stream = File.OpenRead(archivePath);
            var formFile = new FormFile(stream, 0, stream.Length, "file", Path.GetFileName(archivePath));

            var result = await _controller.InstallPackage(formFile, string.Empty);

            Assert.IsAssignableFrom<ObjectResult>(result);

            var installedYaml = Path.Combine(_tempRoot, "Singers", "InvalidBank", "character.yaml");
            Assert.True(File.Exists(installedYaml));

            SingerManager.Inst.SearchAllSingers();
            Assert.Contains(SingerManager.Inst.Singers.Values, singer => singer.Location.Contains("InvalidBank", StringComparison.OrdinalIgnoreCase));
        }

        private string CreateMalformedVoicebankArchive()
        {
            Directory.CreateDirectory(_sourceRoot);
            var archivePath = Path.Combine(_sourceRoot, "MalformedVoicebank.zip");
            using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);

            var txtEntry = archive.CreateEntry("InvalidBank/character.txt");
            using (var writer = new StreamWriter(txtEntry.Open()))
            {
                writer.WriteLine("name=Invalid Bank");
                writer.WriteLine("author=Test");
            }

            var yamlEntry = archive.CreateEntry("InvalidBank/character.yaml");
            using (var writer = new StreamWriter(yamlEntry.Open()))
            {
                writer.WriteLine("name: Invalid Bank");
                writer.WriteLine("description: This yaml intentionally breaks");
                writer.WriteLine("  nested: mapping");
            }

            return archivePath;
        }

        private static string GetDataPath()
        {
            var property = typeof(PathManager).GetProperty("DataPath", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
            return (string)property.GetValue(PathManager.Inst)!;
        }

        private static void SetDataPath(string path)
        {
            Directory.CreateDirectory(path);
            var property = typeof(PathManager).GetProperty("DataPath", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
            property.SetValue(PathManager.Inst, path);
        }
    }
}