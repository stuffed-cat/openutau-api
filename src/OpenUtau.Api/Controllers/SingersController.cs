using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;
using OpenUtau.Core;
using System.Linq;

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

        public class SingerEditRequest
        {
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
            public string? NewAlias { get; set; }
            public double? Offset { get; set; }
            public double? Consonant { get; set; }
            public double? Cutoff { get; set; }
            public double? Preutter { get; set; }
            public double? Overlap { get; set; }
        }

        [HttpPost("{id}/edit")]
        public IActionResult EditSinger(string id, [FromBody] SingerEditRequest request)
        {
            var singer = SingerManager.Inst.Singers.Values.FirstOrDefault(s => s.Id == id) as OpenUtau.Classic.ClassicSinger;
            if (singer == null)
            {
                return NotFound(new { error = "Classic Singer not found" });
            }

            var yamlFile = System.IO.Path.Combine(singer.Location, "character.yaml");
            OpenUtau.Classic.VoicebankConfig? config = null;
            if (System.IO.File.Exists(yamlFile))
            {
                using (var stream = System.IO.File.OpenRead(yamlFile))
                {
                    config = OpenUtau.Classic.VoicebankConfig.Load(stream);
                }
            }
            if (config == null) config = new OpenUtau.Classic.VoicebankConfig();

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
