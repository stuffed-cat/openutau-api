using Microsoft.AspNetCore.Mvc;
using OpenUtau.Api;
using OpenUtau.Core;
using OpenUtau.Core.Api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class G2PController : ControllerBase
    {
        private static string GetDictionaryPath(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Dictionary name is required.", nameof(name));
            }

            if (name != Path.GetFileName(name))
            {
                throw new ArgumentException("Dictionary name must not contain path separators.", nameof(name));
            }

            var fileName = Path.GetExtension(name).Length == 0 ? $"{name}.yaml" : name;
            return Path.Combine(PathManager.Inst.DictionariesPath, fileName);
        }

        private static G2pDictionaryData LoadDictionaryData(string path)
        {
            if (!System.IO.File.Exists(path))
            {
                return new G2pDictionaryData
                {
                    symbols = Array.Empty<G2pDictionaryData.SymbolData>(),
                    entries = Array.Empty<G2pDictionaryData.Entry>()
                };
            }

            var data = Yaml.DefaultDeserializer.Deserialize<G2pDictionaryData>(System.IO.File.ReadAllText(path));
            return data ?? new G2pDictionaryData
            {
                symbols = Array.Empty<G2pDictionaryData.SymbolData>(),
                entries = Array.Empty<G2pDictionaryData.Entry>()
            };
        }

        public class DictionarySymbolRequest
        {
            public string Symbol { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
        }

        public class DictionaryEntryRequest
        {
            public string Grapheme { get; set; } = string.Empty;
            public List<string> Phonemes { get; set; } = new List<string>();
        }

        public class DictionaryDocumentRequest
        {
            public List<DictionarySymbolRequest> Symbols { get; set; } = new List<DictionarySymbolRequest>();
            public List<DictionaryEntryRequest> Entries { get; set; } = new List<DictionaryEntryRequest>();
        }

        private static void SaveDictionaryData(string path, G2pDictionaryData data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            System.IO.File.WriteAllText(path, Yaml.DefaultSerializer.Serialize(data));
        }

        [HttpGet]
        public IActionResult SupportedG2P()
        {
            var types = typeof(IG2p).Assembly.GetTypes()
                .Where(t => typeof(IG2p).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .Select(t => t.Name)
                .ToList();
            
            return Ok(types);
        }

        [HttpGet("dictionaries")]
        public IActionResult ListDictionaries()
        {
            Directory.CreateDirectory(PathManager.Inst.DictionariesPath);
            var dictionaries = Directory.GetFiles(PathManager.Inst.DictionariesPath, "*.yaml")
                .Select(Path.GetFileName)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return Ok(dictionaries);
        }

        [HttpGet("dictionaries/{name}")]
        public IActionResult GetDictionary(string name)
        {
            try
            {
                var path = GetDictionaryPath(name);
                if (!System.IO.File.Exists(path))
                {
                    return NotFound(new { error = "Dictionary not found" });
                }

                return Content(System.IO.File.ReadAllText(path), "text/plain");
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPut("dictionaries/{name}")]
        public IActionResult UpsertDictionary(string name, [FromBody] DictionaryDocumentRequest request)
        {
            try
            {
                var path = GetDictionaryPath(name);
                request ??= new DictionaryDocumentRequest();
                var data = new G2pDictionaryData
                {
                    symbols = request.Symbols.Select(symbol => new G2pDictionaryData.SymbolData
                    {
                        symbol = symbol.Symbol,
                        type = symbol.Type
                    }).ToArray(),
                    entries = request.Entries.Select(entry => new G2pDictionaryData.Entry
                    {
                        grapheme = entry.Grapheme,
                        phonemes = entry.Phonemes.ToArray()
                    }).ToArray()
                };
                SaveDictionaryData(path, data);
                return Ok(new { message = "Dictionary saved successfully", name = Path.GetFileName(path) });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("dictionaries/{name}/entries")]
        public IActionResult AddDictionaryEntry(string name, [FromBody] DictionaryEntryRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new { error = "Request body is required" });
                }

                if (string.IsNullOrWhiteSpace(request.Grapheme))
                {
                    return BadRequest(new { error = "Grapheme is required" });
                }

                if (request.Phonemes == null || request.Phonemes.Count == 0)
                {
                    return BadRequest(new { error = "Phonemes are required" });
                }

                var path = GetDictionaryPath(name);
                var data = LoadDictionaryData(path);
                var symbols = data.symbols?.ToList() ?? new List<G2pDictionaryData.SymbolData>();
                var entries = data.entries?.ToList() ?? new List<G2pDictionaryData.Entry>();
                var existingEntry = entries.FindIndex(e => string.Equals(e.grapheme, request.Grapheme, StringComparison.OrdinalIgnoreCase));

                var entry = new G2pDictionaryData.Entry
                {
                    grapheme = request.Grapheme,
                    phonemes = request.Phonemes.ToArray()
                };

                if (existingEntry >= 0)
                {
                    entries[existingEntry] = entry;
                }
                else
                {
                    entries.Add(entry);
                }

                data.symbols = symbols.ToArray();
                data.entries = entries.ToArray();
                SaveDictionaryData(path, data);

                return Ok(new { message = "Dictionary entry saved successfully", name = Path.GetFileName(path), grapheme = request.Grapheme });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPut("dictionaries/{name}/entries/{grapheme}")]
        public IActionResult UpdateDictionaryEntry(string name, string grapheme, [FromBody] DictionaryEntryRequest request)
        {
            request ??= new DictionaryEntryRequest();
            request.Grapheme = string.IsNullOrWhiteSpace(request.Grapheme) ? grapheme : request.Grapheme;
            return AddDictionaryEntry(name, request);
        }

        [HttpDelete("dictionaries/{name}/entries/{grapheme}")]
        public IActionResult DeleteDictionaryEntry(string name, string grapheme)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(grapheme))
                {
                    return BadRequest(new { error = "Grapheme is required" });
                }

                var path = GetDictionaryPath(name);
                if (!System.IO.File.Exists(path))
                {
                    return NotFound(new { error = "Dictionary not found" });
                }

                var data = LoadDictionaryData(path);
                var entries = data.entries?.ToList() ?? new List<G2pDictionaryData.Entry>();
                var removed = entries.RemoveAll(e => string.Equals(e.grapheme, grapheme, StringComparison.OrdinalIgnoreCase));
                if (removed == 0)
                {
                    return NotFound(new { error = "Dictionary entry not found" });
                }

                data.entries = entries.ToArray();
                SaveDictionaryData(path, data);
                return Ok(new { message = "Dictionary entry deleted successfully", name = Path.GetFileName(path), grapheme });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("{lang}/query")]
        public IActionResult Query(string lang, [FromBody] G2PQueryRequest request)
        {
            var type = typeof(IG2p).Assembly.GetTypes()
                .FirstOrDefault(t => typeof(IG2p).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract && t.Name.ToLower().Contains(lang.ToLower()));

            if (type == null)
                return NotFound($"G2P for language {lang} not found.");

            var obj = System.Activator.CreateInstance(type) as IG2p;
            if (obj == null)
            {
                return StatusCode(500, "Failed to instantiate G2P module.");
            }

            var result = obj.Query(request.Text);
            return Ok(result);
        }
    }

    public class G2PQueryRequest 
    {
        public string Text { get; set; } = string.Empty;
    }
}
