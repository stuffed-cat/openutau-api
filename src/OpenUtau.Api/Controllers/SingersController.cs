using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using OpenUtau.Core;
using OpenUtau.Classic;
using System.Linq;
using System.Text;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SingersController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetSingers()
        {
            var singers = SingerManager.Inst.Singers.Values.Select(singer => new {
                Id = singer.Id,
                Name = singer.Name,
                Author = singer.Author,
                Version = singer.Version,
                SingerType = singer.SingerType.ToString(),
                BasePath = singer.BasePath,
                Subbanks = singer.Subbanks.Select(b => new {
                    Name = b.Color,
                    Prefix = b.Prefix,
                    Suffix = b.Suffix,
                    ToneSet = b.toneSet
                })
            });
            return Ok(singers);
        }

        [HttpGet("{id}/info")]
        public IActionResult GetSingerInfo(string id)
        {
            var singer = SingerManager.Inst.Singers.Values.FirstOrDefault(s => s.Id == id);
            if (singer == null)
            {
                return NotFound("Singer not found");
            }
            
            return Ok(new {
                Id = singer.Id,
                Name = singer.Name,
                Author = singer.Author,
                Version = singer.Version,
                SingerType = singer.SingerType.ToString(),
                BasePath = singer.BasePath,
                Subbanks = singer.Subbanks.Select(b => new {
                    Name = b.Color,
                    Prefix = b.Prefix,
                    Suffix = b.Suffix,
                    ToneSet = b.toneSet
                })
            });
        }


        [HttpPost("refresh")]
        public IActionResult RefreshSingers()
        {
            SingerManager.Inst.SearchAllSingers();
            return Ok(new { message = "Singers refreshed successfully" });
        }

        [HttpPost("install")]
        [HttpPost("/api/voicebanks/import")]
        public IActionResult InstallSinger([FromForm] string archiveFilePath, [FromForm] string archiveEncoding = "shift_jis", [FromForm] string textEncoding = "shift_jis", [FromForm] string singerType = "utau")
        {
            try
            {
                var basePath = PathManager.Inst.SingersInstallPath;
                var installer = new OpenUtau.Classic.VoicebankInstaller(basePath, (progress, info) => {
                    // Ignore progress for API
                }, System.Text.Encoding.GetEncoding(archiveEncoding), System.Text.Encoding.GetEncoding(textEncoding));
                
                installer.Install(archiveFilePath, singerType);
                SingerManager.Inst.SearchAllSingers();
                
                return Ok(new { message = "Singer installed successfully" });
            }
            catch (System.Exception e)
            {
                return BadRequest(new { error = e.Message });
            }
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteSinger(string id)
        {
            // 通过唯一ID查找歌手
            var singer = SingerManager.Inst.Singers.Values.FirstOrDefault(s => s.Id == id);
            if (singer == null)
            {
                return NotFound(new { error = "Singer not found" });
            }

            try
            {
                // 获取当前声库配置文件所在的文件夹根目录并将其整个删除
                if (!string.IsNullOrEmpty(singer.Location) && System.IO.Directory.Exists(singer.Location))
                {
                    System.IO.Directory.Delete(singer.Location, true);
                }
                
                // 删除完毕后触发全局搜索扫描以刷新已加载的声库列表
                SingerManager.Inst.SearchAllSingers();
                return Ok(new { message = "Singer deleted successfully" });
            }
            catch (System.Exception e)
            {
                return BadRequest(new { error = $"Failed to delete singer: {e.Message}" });
            }
        }
        
        [HttpGet("{id}/image")]
        public IActionResult GetSingerImage(string id)
        {
            var singer = SingerManager.Inst.Singers.Values.FirstOrDefault(s => s.Id == id);
            if (singer == null)
            {
                return NotFound(new { error = "Singer not found" });
            }
            
            singer.EnsureLoaded();
            if (singer.AvatarData == null || singer.AvatarData.Length == 0)
            {
                return NotFound(new { error = "Image not found" });
            }
            
            string extension = System.IO.Path.GetExtension(singer.Avatar)?.ToLowerInvariant() ?? "";
            string mimeType = extension switch
            {
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".bmp" => "image/bmp",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "application/octet-stream",
            };

            return File(singer.AvatarData, mimeType);
        }

        [HttpGet("{id}/portrait")]
        public IActionResult GetSingerPortrait(string id)
        {
            var singer = SingerManager.Inst.Singers.Values.FirstOrDefault(s => s.Id == id);
            if (singer == null)
            {
                return NotFound(new { error = "Singer not found" });
            }

            try
            {
                var portraitData = singer.LoadPortrait();
                if (portraitData == null || portraitData.Length == 0)
                {
                    return NotFound(new { error = "Portrait not found" });
                }

                string extension = System.IO.Path.GetExtension(singer.Portrait)?.ToLowerInvariant() ?? "";
                string mimeType = extension switch
                {
                    ".png" => "image/png",
                    ".jpg" => "image/jpeg",
                    ".jpeg" => "image/jpeg",
                    ".bmp" => "image/bmp",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    _ => "application/octet-stream",
                };

                return File(portraitData, mimeType);
            }
            catch
            {
                return NotFound(new { error = "Error loading portrait" });
            }
        }

        [HttpGet("{id}/otos")]
        public IActionResult GetSingerOtos(string id)
        {
            var singer = SingerManager.Inst.Singers.Values.FirstOrDefault(s => s.Id == id);
            if (singer == null)
            {
                return NotFound(new { error = "Singer not found" });
            }

            // Return a structured list of OTO entries
            var otos = singer.Otos.Select(o => new {
                Alias = o.Alias,
                Phonetic = o.Phonetic,
                Set = o.Set,
                File = o.DisplayFile,
                Color = o.Color,
                Offset = o.Offset,
                Consonant = o.Consonant,
                Cutoff = o.Cutoff,
                Preutter = o.Preutter,
                Overlap = o.Overlap
            });
            
            return Ok(otos);
        }

        
        [HttpGet("{id}/otos/sample")]
        public IActionResult GetSingerOtoSample(string id, [FromQuery] string set = "", [FromQuery] string alias = "")
        {
            var singer = SingerManager.Inst.Singers.Values.FirstOrDefault(s => s.Id == id);
            if (singer == null)
            {
                return NotFound(new { error = "Singer not found" });
            }

            var oto = singer.Otos.FirstOrDefault(o => (string.IsNullOrEmpty(set) || o.Set == set) && o.Alias == alias);
            if (oto == null)
            {
                return NotFound(new { error = "Oto not found" });
            }

            if (string.IsNullOrEmpty(oto.File) || !System.IO.File.Exists(oto.File))
            {
                return NotFound(new { error = "Audio file not found" });
            }

            string extension = System.IO.Path.GetExtension(oto.File)?.ToLowerInvariant() ?? "";
            string mimeType = extension switch
            {
                ".wav" => "audio/wav",
                ".mp3" => "audio/mpeg",
                ".flac" => "audio/flac",
                ".ogg" => "audio/ogg",
                _ => "application/octet-stream",
            };

            return PhysicalFile(oto.File, mimeType);
        }

        [HttpGet("{id}/dictionary")]
        public IActionResult GetSingerDictionary(string id)
        {
            var singer = SingerManager.Inst.Singers.Values.FirstOrDefault(s => s.Id == id);
            if (singer == null)
            {
                return NotFound(new { error = "Singer not found" });
            }

            var dictPath = Path.Combine(singer.Location, "dictionary.yaml");
            if (!System.IO.File.Exists(dictPath))
            {
                return NotFound(new { error = "Dictionary not found" });
            }

            return Content(System.IO.File.ReadAllText(dictPath), "application/x-yaml");
        }

        public class DictionaryRequest
        {
            public string Content { get; set; } = string.Empty;
        }

        [HttpPost("{id}/dictionary")]
        public IActionResult SaveSingerDictionary(string id, [FromBody] DictionaryRequest request)
        {
            var singer = SingerManager.Inst.Singers.Values.FirstOrDefault(s => s.Id == id);
            if (singer == null)
            {
                return NotFound(new { error = "Singer not found" });
            }

            var dictPath = Path.Combine(singer.Location, "dictionary.yaml");
            System.IO.File.WriteAllText(dictPath, request.Content);

            return Ok(new { message = "Dictionary saved successfully" });
        }

        [HttpDelete("{id}/dictionary")]
        public IActionResult DeleteSingerDictionary(string id)
        {
            var singer = SingerManager.Inst.Singers.Values.FirstOrDefault(s => s.Id == id);
            if (singer == null)
            {
                return NotFound(new { error = "Singer not found" });
            }

            var dictPath = Path.Combine(singer.Location, "dictionary.yaml");
            if (System.IO.File.Exists(dictPath))
            {
                System.IO.File.Delete(dictPath);
            }

            return Ok(new { message = "Dictionary deleted successfully" });
        }

        [HttpGet("{id}/prefix-map")]
        public IActionResult GetSingerPrefixMap(string id)
        {
            var singer = SingerManager.Inst.Singers.Values.FirstOrDefault(s => s.Id == id) as ClassicSinger;
            if (singer == null)
            {
                return NotFound(new { error = "Classic Singer not found" });
            }

            var mapPath = Path.Combine(singer.Location, "prefix.map");
            if (!System.IO.File.Exists(mapPath))
            {
                return NotFound(new { error = "Prefix map not found" });
            }

            return Content(System.IO.File.ReadAllText(mapPath, singer.TextFileEncoding), "text/plain");
        }

        public class PrefixMapRequest
        {
            public string Content { get; set; } = string.Empty;
        }

        [HttpPost("{id}/prefix-map")]
        public IActionResult SaveSingerPrefixMap(string id, [FromBody] PrefixMapRequest request)
        {
            var singer = SingerManager.Inst.Singers.Values.FirstOrDefault(s => s.Id == id) as ClassicSinger;
            if (singer == null)
            {
                return NotFound(new { error = "Classic Singer not found" });
            }

            var mapPath = Path.Combine(singer.Location, "prefix.map");
            System.IO.File.WriteAllText(mapPath, request.Content, singer.TextFileEncoding);
            singer.Reload();

            return Ok(new { message = "Prefix map saved successfully" });
        }

        [HttpDelete("{id}/prefix-map")]
        public IActionResult DeleteSingerPrefixMap(string id)
        {
            var singer = SingerManager.Inst.Singers.Values.FirstOrDefault(s => s.Id == id) as ClassicSinger;
            if (singer == null)
            {
                return NotFound(new { error = "Classic Singer not found" });
            }

            var mapPath = Path.Combine(singer.Location, "prefix.map");
            if (System.IO.File.Exists(mapPath))
            {
                System.IO.File.Delete(mapPath);
            }
            singer.Reload();

            return Ok(new { message = "Prefix map deleted successfully" });
        }

        public class SingerEditRequest
        {
            public string? Name { get; set; }
            public string? Author { get; set; }
            public string? Voice { get; set; }
            public string? Web { get; set; }
            public string? Version { get; set; }
            public string? Sample { get; set; }
            public string? TextFileEncoding { get; set; }
            public string? Image { get; set; }
            public string? Portrait { get; set; }
            public string? SingerType { get; set; }
            public string? DefaultPhonemizer { get; set; }
            public bool? UseFilenameAsAlias { get; set; }
            public List<OtoEdit>? OtoEdits { get; set; }
        }

        public class OtoEdit
        {
            public string Alias { get; set; } = string.Empty;
            public string? Set { get; set; }
            public string? File { get; set; }
            public string? NewAlias { get; set; }
            public double? Offset { get; set; }
            public double? Consonant { get; set; }
            public double? Cutoff { get; set; }
            public double? Preutter { get; set; }
            public double? Overlap { get; set; }
        }

        public class OtoEditRequest
        {
            public List<OtoEdit>? OtoEdits { get; set; }
        }

        private static void ApplyCharacterTxtMetadata(string txtPath, VoicebankConfig config)
        {
            var lines = new List<string>();
            if (System.IO.File.Exists(txtPath))
            {
                lines.AddRange(System.IO.File.ReadAllLines(txtPath, Encoding.UTF8));
            }

            var knownKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "name", "author", "created by", "voice", "sample", "web", "version", "image"
            };

            string? ParseValue(string line, out string key)
            {
                key = string.Empty;
                var separators = new[] { '=', ':', '：' };
                foreach (var sep in separators)
                {
                    var index = line.IndexOf(sep);
                    if (index > 0)
                    {
                        key = line.Substring(0, index).Trim();
                        return line.Substring(index + 1).Trim();
                    }
                }
                return null;
            }

            var output = new List<string>();
            foreach (var line in lines)
            {
                var parsed = ParseValue(line, out var key);
                if (parsed != null && knownKeys.Contains(key))
                {
                    continue;
                }
                output.Add(line);
            }

            void AddOrUpdate(string key, string? value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return;
                }
                output.Add($"{key}={value}");
            }

            AddOrUpdate("name", config.Name);
            AddOrUpdate("author", config.Author);
            AddOrUpdate("voice", config.Voice);
            AddOrUpdate("sample", config.Sample);
            AddOrUpdate("web", config.Web);
            AddOrUpdate("version", config.Version);
            AddOrUpdate("image", config.Image);

            System.IO.File.WriteAllLines(txtPath, output, Encoding.UTF8);
        }

        [HttpPut("{id}/otos")]
        public IActionResult UpdateSingerOtos(string id, [FromBody] OtoEditRequest request)
        {
            var singer = SingerManager.Inst.Singers.Values.FirstOrDefault(s => s.Id == id) as OpenUtau.Classic.ClassicSinger;
            if (singer == null)
            {
                return NotFound(new { error = "Classic Singer not found" });
            }

            if (request == null)
            {
                return BadRequest(new { error = "Request body is required" });
            }

            if (request?.OtoEdits == null || request.OtoEdits.Count == 0)
            {
                return BadRequest(new { error = "No oto edits provided" });
            }

            var resolvedEdits = new List<(OpenUtau.Core.Ustx.UOto Oto, OtoEdit Edit)>();

            foreach (var edit in request.OtoEdits)
            {
                if (string.IsNullOrWhiteSpace(edit.Alias))
                {
                    return BadRequest(new { error = "OTO alias is required" });
                }

                var matches = singer.Otos.Where(o =>
                    string.Equals(o.Alias, edit.Alias, StringComparison.OrdinalIgnoreCase) &&
                    (string.IsNullOrWhiteSpace(edit.Set) || string.Equals(o.Set, edit.Set, StringComparison.OrdinalIgnoreCase)) &&
                    (string.IsNullOrWhiteSpace(edit.File) || string.Equals(o.DisplayFile, edit.File, StringComparison.OrdinalIgnoreCase) || string.Equals(o.File, edit.File, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (matches.Count == 0)
                {
                    return NotFound(new { error = $"Oto not found: {edit.Alias}" });
                }

                if (matches.Count > 1)
                {
                    return BadRequest(new { error = $"Ambiguous oto match for alias '{edit.Alias}'" });
                }

                var hasAnyUpdate = edit.Offset.HasValue || edit.Consonant.HasValue || edit.Cutoff.HasValue || edit.Preutter.HasValue || edit.Overlap.HasValue;
                if (!hasAnyUpdate)
                {
                    return BadRequest(new { error = $"No oto parameters provided for '{edit.Alias}'" });
                }

                static bool IsInvalidDouble(double value)
                {
                    return double.IsNaN(value) || double.IsInfinity(value);
                }

                if (edit.Offset.HasValue && IsInvalidDouble(edit.Offset.Value) ||
                    edit.Consonant.HasValue && IsInvalidDouble(edit.Consonant.Value) ||
                    edit.Cutoff.HasValue && IsInvalidDouble(edit.Cutoff.Value) ||
                    edit.Preutter.HasValue && IsInvalidDouble(edit.Preutter.Value) ||
                    edit.Overlap.HasValue && IsInvalidDouble(edit.Overlap.Value))
                {
                    return BadRequest(new { error = "OTO values must be finite numbers" });
                }

                resolvedEdits.Add((matches[0], edit));
            }

            foreach (var (oto, edit) in resolvedEdits)
            {
                if (edit.Offset.HasValue) oto.Offset = edit.Offset.Value;
                if (edit.Consonant.HasValue) oto.Consonant = edit.Consonant.Value;
                if (edit.Cutoff.HasValue) oto.Cutoff = edit.Cutoff.Value;
                if (edit.Preutter.HasValue) oto.Preutter = edit.Preutter.Value;
                if (edit.Overlap.HasValue) oto.Overlap = edit.Overlap.Value;
            }

            singer.Save();
            singer.Reload();

            return Ok(new { message = "OTO entries updated successfully", updated = resolvedEdits.Count });
        }

        [HttpPost("{id}/edit")]
        [HttpPut("{id}/metadata")]
        public IActionResult EditSinger(string id, [FromBody] SingerEditRequest request)
        {
            var singer = SingerManager.Inst.Singers.Values.FirstOrDefault(s => s.Id == id) as OpenUtau.Classic.ClassicSinger;
            if (singer == null)
            {
                return NotFound(new { error = "Classic Singer not found" });
            }

            if (request == null)
            {
                return BadRequest(new { error = "Request body is required" });
            }

            var yamlFile = System.IO.Path.Combine(singer.Location, "character.yaml");
            var txtFile = System.IO.Path.Combine(singer.Location, "character.txt");
            OpenUtau.Classic.VoicebankConfig? config = null;
            if (System.IO.File.Exists(yamlFile))
            {
                using (var stream = System.IO.File.OpenRead(yamlFile))
                {
                    config = OpenUtau.Classic.VoicebankConfig.Load(stream);
                }
            }
            if (config == null) config = new OpenUtau.Classic.VoicebankConfig();

            if (request.Name != null) config.Name = request.Name;
            if (request.Author != null) config.Author = request.Author;
            if (request.Voice != null) config.Voice = request.Voice;
            if (request.Web != null) config.Web = request.Web;
            if (request.Version != null) config.Version = request.Version;
            if (request.Sample != null) config.Sample = request.Sample;
            if (request.TextFileEncoding != null) config.TextFileEncoding = request.TextFileEncoding;
            if (request.Image != null) config.Image = request.Image;
            if (request.Portrait != null) config.Portrait = request.Portrait;
            if (request.SingerType != null) config.SingerType = request.SingerType;
            if (request.DefaultPhonemizer != null) config.DefaultPhonemizer = request.DefaultPhonemizer;
            if (request.UseFilenameAsAlias != null) config.UseFilenameAsAlias = request.UseFilenameAsAlias.Value;

            using (var stream = System.IO.File.Open(yamlFile, System.IO.FileMode.Create))
            {
                config.Save(stream);
            }

            ApplyCharacterTxtMetadata(txtFile, config);

            if (request.OtoEdits != null && request.OtoEdits.Count > 0)
            {
                bool otoChanged = false;
                foreach (var edit in request.OtoEdits)
                {
                    var oto = singer.Otos.FirstOrDefault(o => o.Alias == edit.Alias);
                    if (oto != null)
                    {
                        // Alias cannot be changed directly via UOto
                        if (edit.Offset.HasValue) oto.Offset = edit.Offset.Value;
                        if (edit.Consonant.HasValue) oto.Consonant = edit.Consonant.Value;
                        if (edit.Cutoff.HasValue) oto.Cutoff = edit.Cutoff.Value;
                        if (edit.Preutter.HasValue) oto.Preutter = edit.Preutter.Value;
                        if (edit.Overlap.HasValue) oto.Overlap = edit.Overlap.Value;
                        otoChanged = true;
                    }
                }
                if (otoChanged)
                {
                    singer.Save();
                }
            }
            
            singer.Reload();
            return Ok(new { message = "Singer updated successfully" });
        }

        public class MergeRequest
        {
            public string OtherSingerId { get; set; } = string.Empty;
            public Dictionary<string, string>? FolderRenames { get; set; }
            public Dictionary<string, string>? SubbankRenames { get; set; }
            public Dictionary<string, string>? VoiceColorRenames { get; set; }
        }

        [HttpPost("{id}/merge")]
        public async Task<IActionResult> MergeVoicebank(string id, [FromBody] MergeRequest request)
        {
            // Similar logic to MergeVoicebankViewModel
            var thisSinger = SingerManager.Inst.Singers.Values.FirstOrDefault(s => s.Id == id) as OpenUtau.Classic.ClassicSinger;
            var otherSinger = SingerManager.Inst.Singers.Values.FirstOrDefault(s => s.Id == request.OtherSingerId) as OpenUtau.Classic.ClassicSinger;

            if (thisSinger == null || otherSinger == null)
            {
                return NotFound(new { error = "One or both Classic Singers not found" });
            }

            return await Task.Run<IActionResult>(() => {
                try
                {
                    thisSinger.EnsureLoaded();
                    otherSinger.EnsureLoaded();

                    // Convert subbanks
                    var oldSubbanks = otherSinger.Subbanks
                        .OrderByDescending(subbank => subbank.Prefix.Length + subbank.Suffix.Length)
                        .Select(b => b.subbank)
                        .ToList();

                    var newSubbanks = oldSubbanks.Select(oldSubbank => {
                        var subbankKey = $"{oldSubbank.Prefix},{oldSubbank.Suffix}";
                        string newName = subbankKey;
                        if (request.SubbankRenames != null && request.SubbankRenames.TryGetValue(subbankKey, out var rename)) {
                            newName = rename;
                        }

                        string newColor = oldSubbank.Color;
                        if (request.VoiceColorRenames != null && request.VoiceColorRenames.TryGetValue(oldSubbank.Color, out var colorRename)) {
                            newColor = colorRename;
                        }

                        if(newName.Contains(",")){
                            var n = newName.Split(",");
                            return new OpenUtau.Classic.Subbank() {
                                Prefix = n[0],
                                Suffix = n[^1],
                                Color = newColor,
                                ToneRanges = oldSubbank.ToneRanges
                            };
                        } else {
                            return new OpenUtau.Classic.Subbank() {
                                Prefix = "",
                                Suffix = newName,
                                Color = newColor,
                                ToneRanges = oldSubbank.ToneRanges 
                            };
                        }
                    }).ToList();

                    var otosToConvert = new List<Tuple<string, string>>();
                    var filesToCopy = new List<Tuple<string, string>>();
                    string[] supportedAudioTypes = new string[]{".wav", ".flac", ".ogg", ".mp3", ".aiff", ".aif", ".aifc"};

                    var foldersToProcess = request.FolderRenames ?? new Dictionary<string, string>();
                    if (foldersToProcess.Count == 0) {
                        // Just merge root if no renames provided
                        foldersToProcess["."] = ".";
                    }

                    foreach (var kvp in foldersToProcess)
                    {
                        var folderName = kvp.Key;
                        var newFolderName = kvp.Value;
                        
                        System.IO.Directory.CreateDirectory(System.IO.Path.Join(thisSinger.Location, newFolderName));
                        
                        void AddFolder(string fromDir, string toDir)
                        {
                            if (System.IO.File.Exists(System.IO.Path.Join(fromDir, "oto.ini")))
                            {
                                System.IO.Directory.CreateDirectory(toDir);
                                otosToConvert.Add(new Tuple<string, string>(
                                    System.IO.Path.Join(fromDir, "oto.ini"),
                                    System.IO.Path.Join(toDir, "oto.ini")
                                ));
                                filesToCopy.AddRange(
                                    System.IO.Directory.GetFiles(fromDir)
                                        .Where(f => supportedAudioTypes.Contains(System.IO.Path.GetExtension(f)))
                                        .Select(f => new Tuple<string, string>(f, System.IO.Path.Join(toDir, System.IO.Path.GetFileName(f))))
                                );
                            }
                        }

                        if (folderName == ".")
                        {
                            AddFolder(otherSinger.Location, System.IO.Path.Join(thisSinger.Location, newFolderName));
                        }
                        else
                        {
                            string currentFolder = System.IO.Path.Join(otherSinger.Location, folderName);
                            foreach(var d in System.IO.Directory.EnumerateFiles(currentFolder, "oto.ini", System.IO.SearchOption.AllDirectories)
                                .Select(d => System.IO.Path.GetDirectoryName(d)!)) {
                                AddFolder(d, System.IO.Path.Join(thisSinger.Location, newFolderName, System.IO.Path.GetRelativePath(currentFolder, d)));
                            }
                        }
                    }

                    foreach (var oto in otosToConvert)
                    {
                        ConvertOto(oto.Item1, oto.Item2, oldSubbanks, newSubbanks, otherSinger, thisSinger);
                    }

                    foreach (var file in filesToCopy)
                    {
                        System.IO.File.Copy(file.Item1, file.Item2, true);
                    }

                    var yamlFile = System.IO.Path.Combine(thisSinger.Location, "character.yaml");
                    OpenUtau.Classic.VoicebankConfig? bankConfig = null;
                    if (System.IO.File.Exists(yamlFile)) {
                        using (var stream = System.IO.File.OpenRead(yamlFile)) {
                            bankConfig = OpenUtau.Classic.VoicebankConfig.Load(stream);
                        }
                    }
                    if (bankConfig == null) bankConfig = new OpenUtau.Classic.VoicebankConfig();

                    bankConfig.Subbanks = (thisSinger.Subbanks ?? new OpenUtau.Core.Ustx.USubbank[0])
                        .Select(s => s.subbank)
                        .Concat(newSubbanks)
                        .ToArray();

                    foreach(var subbank in bankConfig.Subbanks) {
                        if(subbank.ToneRanges == null || subbank.ToneRanges.Length == 0) {
                            subbank.ToneRanges = new string[] { "C1-B7" };
                        }
                    }

                    using (var stream = System.IO.File.Open(yamlFile, System.IO.FileMode.Create)) {
                        bankConfig.Save(stream);
                    }
                    
                    SingerManager.Inst.SearchAllSingers();
                    
                    return Ok(new { message = "Voicebanks merged successfully" });
                }
                catch (System.Exception e)
                {
                    return BadRequest(new { error = e.Message });
                }
            });
        }

        private void ConvertOto(string fromPath, string toPath, List<OpenUtau.Classic.Subbank> oldSubbanks, List<OpenUtau.Classic.Subbank> newSubbanks, OpenUtau.Classic.ClassicSinger otherSinger, OpenUtau.Classic.ClassicSinger thisSinger)
        {
            if(!System.IO.File.Exists(fromPath)) return;
            
            var patterns = oldSubbanks.Select(subbank => new System.Text.RegularExpressions.Regex($"^{System.Text.RegularExpressions.Regex.Escape(subbank.Prefix)}(.*){System.Text.RegularExpressions.Regex.Escape(subbank.Suffix)}$")).ToList();
            var otoSet = OpenUtau.Classic.VoicebankLoader.ParseOtoSet(fromPath, otherSinger.TextFileEncoding, otherSinger.UseFilenameAsAlias);
            
            foreach (var oto in otoSet.Otos){
                if (!oto.IsValid) continue;
                for (var i = 0; i < patterns.Count; i++) {
                    var m = patterns[i].Match(oto.Alias);
                    if (m.Success) {
                        oto.Alias = newSubbanks[i].Prefix + m.Groups[1].Value + newSubbanks[i].Suffix;
                        break;
                    }
                }
            }
            using (var stream = System.IO.File.Open(toPath, System.IO.FileMode.Create, System.IO.FileAccess.Write)){
                OpenUtau.Classic.VoicebankLoader.WriteOtoSet(otoSet, stream, thisSinger.TextFileEncoding);
            }
        }
    }
}
