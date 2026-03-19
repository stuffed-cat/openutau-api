using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using OpenUtau.Classic;
using OpenUtau.Core;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace OpenUtau.Api.Services
{
    public static class VoicebankPackageInstaller
    {
        private const string CharacterTxt = "character.txt";
        private const string CharacterYaml = "character.yaml";
        private const string InstallTxt = "install.txt";

        public static void Install(string archivePath, string singerType, Encoding archiveEncoding, Encoding textEncoding)
        {
            var extension = Path.GetExtension(archivePath).ToLowerInvariant();
            if (extension == ".zip")
            {
                InstallZip(archivePath, singerType, textEncoding);
                return;
            }

            InstallArchive(archivePath, singerType, archiveEncoding, textEncoding);
        }

        private static void InstallZip(string archivePath, string singerType, Encoding textEncoding)
        {
            var basePath = PathManager.Inst.SingersInstallPath;
            Directory.CreateDirectory(basePath);

            using var archive = ZipFile.OpenRead(archivePath);
            var entries = archive.Entries.ToList();
            var installRoot = ResolveInstallRoot(basePath, archivePath, entries.Select(e => e.FullName).ToList());
            var touches = ResolveTouchFiles(installRoot, entries.Select(e => e.FullName).ToList(), archivePath);
            var hasCharacterYaml = entries.Any(e => Path.GetFileName(e.FullName) == CharacterYaml);

            foreach (var entry in entries)
            {
                var key = entry.FullName.Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(key) || key.Contains(".."))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(entry.Name) || Path.GetFileName(key) == InstallTxt)
                {
                    continue;
                }

                var filePath = Path.Combine(installRoot, key);
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                entry.ExtractToFile(filePath, true);

                if (!hasCharacterYaml && Path.GetFileName(filePath) == CharacterTxt)
                {
                    WriteDefaultConfig(filePath.Replace(".txt", ".yaml"), singerType, textEncoding);
                }
                else if (Path.GetFileName(filePath) == CharacterYaml)
                {
                    NormalizeCharacterConfig(filePath, singerType, textEncoding);
                }
            }

            foreach (var touch in touches)
            {
                if (!File.Exists(touch))
                {
                    File.WriteAllText(touch, "\n");
                }

                WriteDefaultConfig(touch.Replace(".txt", ".yaml"), singerType, textEncoding);
            }
        }

        private static void InstallArchive(string archivePath, string singerType, Encoding archiveEncoding, Encoding textEncoding)
        {
            var basePath = PathManager.Inst.SingersInstallPath;
            Directory.CreateDirectory(basePath);

            var readerOptions = new ReaderOptions
            {
                ArchiveEncoding = new ArchiveEncoding
                {
                    Forced = archiveEncoding,
                }
            };

            var extractionOptions = new ExtractionOptions
            {
                Overwrite = true,
            };

            using var archive = ArchiveFactory.Open(archivePath, readerOptions);
            var entries = archive.Entries.ToList();
            var entryKeys = entries.Select(e => e.Key).ToList();
            var installRoot = ResolveInstallRoot(basePath, archivePath, entryKeys);
            var touches = ResolveTouchFiles(installRoot, entryKeys, archivePath);
            var hasCharacterYaml = entryKeys.Any(e => Path.GetFileName(e) == CharacterYaml);

            foreach (var entry in entries)
            {
                if (entry.Key.Contains(".."))
                {
                    continue;
                }

                if (entry.IsDirectory || entry.Key == InstallTxt)
                {
                    continue;
                }

                var filePath = Path.Combine(installRoot, entry.Key);
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                entry.WriteToFile(filePath, extractionOptions);

                if (!hasCharacterYaml && Path.GetFileName(filePath) == CharacterTxt)
                {
                    WriteDefaultConfig(filePath.Replace(".txt", ".yaml"), singerType, textEncoding);
                }
                else if (Path.GetFileName(filePath) == CharacterYaml)
                {
                    NormalizeCharacterConfig(filePath, singerType, textEncoding);
                }
            }

            foreach (var touch in touches)
            {
                if (!File.Exists(touch))
                {
                    File.WriteAllText(touch, "\n");
                }

                WriteDefaultConfig(touch.Replace(".txt", ".yaml"), singerType, textEncoding);
            }
        }

        private static string ResolveInstallRoot(string basePath, string archivePath, List<string> entryKeys)
        {
            var rootFiles = entryKeys
                .Where(key => !key.Contains('\\') && !key.Contains('/') && key != InstallTxt)
                .ToArray();

            if (rootFiles.Length > 0)
            {
                return Path.Combine(basePath, Path.GetFileNameWithoutExtension(archivePath).Trim());
            }

            return basePath;
        }

        private static List<string> ResolveTouchFiles(string installRoot, List<string> entryKeys, string archivePath)
        {
            var touches = new List<string>();
            var rootDirs = entryKeys
                .Where(key => key.EndsWith("/") || key.EndsWith("\\"))
                .Where(key => (key.IndexOf('\\') < 0 || key.IndexOf('\\') == key.Length - 1)
                         && (key.IndexOf('/') < 0 || key.IndexOf('/') == key.Length - 1))
                .ToArray();
            var rootFiles = entryKeys
                .Where(key => !key.Contains('\\') && !key.Contains('/') && key != InstallTxt)
                .ToArray();

            if (rootFiles.Length > 0)
            {
                if (rootFiles.All(e => e != CharacterTxt))
                {
                    touches.Add(Path.Combine(installRoot, CharacterTxt));
                }
                return touches;
            }

            foreach (var rootDir in rootDirs)
            {
                if (!entryKeys.Contains($"{rootDir}{CharacterTxt}") &&
                    !entryKeys.Contains($"{rootDir}\\{CharacterTxt}") &&
                    !entryKeys.Contains($"{rootDir}/{CharacterTxt}"))
                {
                    touches.Add(Path.Combine(installRoot, rootDir, CharacterTxt));
                }
            }

            return touches;
        }

        private static void NormalizeCharacterConfig(string yamlPath, string singerType, Encoding textEncoding)
        {
            try
            {
                using var stream = File.OpenRead(yamlPath);
                var config = VoicebankConfig.Load(stream);
                if (string.IsNullOrEmpty(config.SingerType))
                {
                    config.SingerType = singerType;
                    using var saveStream = File.Open(yamlPath, FileMode.Create);
                    config.Save(saveStream);
                }
            }
            catch
            {
                WriteDefaultConfig(yamlPath, singerType, textEncoding);
            }
        }

        private static void WriteDefaultConfig(string yamlPath, string singerType, Encoding textEncoding)
        {
            var config = new VoicebankConfig
            {
                SingerType = singerType,
                TextFileEncoding = textEncoding.WebName,
            };

            using var stream = File.Open(yamlPath, FileMode.Create);
            config.Save(stream);
        }
    }
}