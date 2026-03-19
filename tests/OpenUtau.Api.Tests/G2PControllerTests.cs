using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Api;
using OpenUtau.Api.Controllers;
using OpenUtau.Core;
using Xunit;

namespace OpenUtau.Api.Tests
{
    [Collection("Sequential")]
    public class G2PControllerTests : IDisposable
    {
        private readonly G2PController _controller;
        private readonly string _originalDataPath;
        private readonly string _tempRoot;

        public G2PControllerTests()
        {
            SetupHelper.InitDocManager();
            _originalDataPath = GetDataPath();
            _tempRoot = Path.Combine(Path.GetTempPath(), $"OpenUtauG2P-{Guid.NewGuid():N}");
            SetDataPath(_tempRoot);
            _controller = new G2PController();
        }

        public void Dispose()
        {
            SetDataPath(_originalDataPath);
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, true);
            }
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

        private static G2pDictionaryData CreateDictionaryData()
        {
            return new G2pDictionaryData
            {
                symbols = new[]
                {
                    new G2pDictionaryData.SymbolData { symbol = "a", type = "vowel" },
                    new G2pDictionaryData.SymbolData { symbol = "b", type = "consonant" },
                    new G2pDictionaryData.SymbolData { symbol = "c", type = "consonant" },
                    new G2pDictionaryData.SymbolData { symbol = "d", type = "consonant" }
                },
                entries = new[]
                {
                    new G2pDictionaryData.Entry { grapheme = "ab", phonemes = new[] { "a", "b" } }
                }
            };
        }

        [Fact]
        public void UpsertDictionary_CreatesQueryableEntry()
        {
            var result = _controller.UpsertDictionary("custom", new G2PController.DictionaryDocumentRequest
            {
                Symbols = CreateDictionaryData().symbols.Select(s => new G2PController.DictionarySymbolRequest
                {
                    Symbol = s.symbol,
                    Type = s.type
                }).ToList(),
                Entries = CreateDictionaryData().entries.Select(e => new G2PController.DictionaryEntryRequest
                {
                    Grapheme = e.grapheme,
                    Phonemes = e.phonemes.ToList()
                }).ToList()
            });

            Assert.IsType<OkObjectResult>(result);

            var content = Assert.IsType<ContentResult>(_controller.GetDictionary("custom")).Content;
            var g2p = G2pDictionary.NewBuilder().Load(content).Build();

            Assert.Equal(new[] { "a", "b" }, g2p.Query("ab"));
            Assert.Null(g2p.Query("cd"));
        }

        [Fact]
        public void AddDictionaryEntry_AppendsNewQueryableEntry()
        {
            _controller.UpsertDictionary("custom", new G2PController.DictionaryDocumentRequest
            {
                Symbols = CreateDictionaryData().symbols.Select(s => new G2PController.DictionarySymbolRequest
                {
                    Symbol = s.symbol,
                    Type = s.type
                }).ToList(),
                Entries = new List<G2PController.DictionaryEntryRequest>
                {
                    new G2PController.DictionaryEntryRequest { Grapheme = "ab", Phonemes = new List<string> { "a", "b" } }
                }
            });

            var result = _controller.AddDictionaryEntry("custom", new G2PController.DictionaryEntryRequest
            {
                Grapheme = "cd",
                Phonemes = new List<string> { "c", "d" }
            });

            Assert.IsType<OkObjectResult>(result);

            var content = Assert.IsType<ContentResult>(_controller.GetDictionary("custom")).Content;
            var g2p = G2pDictionary.NewBuilder().Load(content).Build();

            Assert.Equal(new[] { "c", "d" }, g2p.Query("cd"));
            Assert.Equal(new[] { "a", "b" }, g2p.Query("ab"));
        }

        [Fact]
        public void UpdateDictionaryEntry_ReplacesExistingPhonemes()
        {
            _controller.UpsertDictionary("custom", new G2PController.DictionaryDocumentRequest
            {
                Symbols = CreateDictionaryData().symbols.Select(s => new G2PController.DictionarySymbolRequest
                {
                    Symbol = s.symbol,
                    Type = s.type
                }).ToList(),
                Entries = new List<G2PController.DictionaryEntryRequest>
                {
                    new G2PController.DictionaryEntryRequest { Grapheme = "ab", Phonemes = new List<string> { "a", "b" } }
                }
            });

            var result = _controller.UpdateDictionaryEntry("custom", "ab", new G2PController.DictionaryEntryRequest
            {
                Phonemes = new List<string> { "d", "c" }
            });

            Assert.IsType<OkObjectResult>(result);

            var content = Assert.IsType<ContentResult>(_controller.GetDictionary("custom")).Content;
            var g2p = G2pDictionary.NewBuilder().Load(content).Build();

            Assert.Equal(new[] { "d", "c" }, g2p.Query("ab"));
        }

        [Fact]
        public void DeleteDictionaryEntry_RemovesEntryFromDictionary()
        {
            _controller.UpsertDictionary("custom", new G2PController.DictionaryDocumentRequest
            {
                Symbols = CreateDictionaryData().symbols.Select(s => new G2PController.DictionarySymbolRequest
                {
                    Symbol = s.symbol,
                    Type = s.type
                }).ToList(),
                Entries = new List<G2PController.DictionaryEntryRequest>
                {
                    new G2PController.DictionaryEntryRequest { Grapheme = "ab", Phonemes = new List<string> { "a", "b" } }
                }
            });

            var result = _controller.DeleteDictionaryEntry("custom", "ab");

            Assert.IsType<OkObjectResult>(result);

            var content = Assert.IsType<ContentResult>(_controller.GetDictionary("custom")).Content;
            var g2p = G2pDictionary.NewBuilder().Load(content).Build();

            Assert.Null(g2p.Query("ab"));
        }

        [Fact]
        public void InvalidDictionaryName_ReturnsBadRequest()
        {
            var result = _controller.UpsertDictionary("../bad", new G2PController.DictionaryDocumentRequest());

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("must not contain path separators", badRequest.Value!.ToString());
        }
    }
}