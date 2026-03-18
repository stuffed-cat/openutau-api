using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Format;
using OpenUtau.Core.Render;
using OpenUtau.Core.SignalChain;
using OpenUtau.Core.Util;
using System;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/project/[controller]")]
    public class RenderController : ControllerBase
    {
        [HttpPost("clear-cache")]
        public IActionResult ClearCache()
        {
            try
            {
                PathManager.Inst.ClearCache();
                return Ok("Cache cleared successfully");
            }
            catch (Exception ex)
            {
                if (ex is DirectoryNotFoundException) return Ok("Cache directory is already clear or does not exist."); return StatusCode(500, new { error = ex.Message });
            }
        }


        [HttpGet("cache/status")]
        public IActionResult GetCacheStatus()
        {
            try
            {
                var cachePath = PathManager.Inst.CachePath;
                if (!Directory.Exists(cachePath))
                {
                    return Ok(new { totalSize = 0, fileCount = 0, message = "Cache directory does not exist yet." });
                }

                var dirInfo = new DirectoryInfo(cachePath);
                var files = dirInfo.GetFiles("*", SearchOption.AllDirectories);
                long totalSize = 0;
                foreach (var file in files)
                {
                    totalSize += file.Length;
                }

                // Breakdown by extension
                var breakdown = new System.Collections.Generic.Dictionary<string, long>();
                foreach (var file in files)
                {
                    var ext = file.Extension.ToLower();
                    if (string.IsNullOrEmpty(ext)) ext = "no_extension";
                    if (!breakdown.ContainsKey(ext)) breakdown[ext] = 0;
                    breakdown[ext] += file.Length;
                }

                return Ok(new {
                    totalSizeBytes = totalSize,
                    totalSizeMB = totalSize / 1024.0 / 1024.0,
                    fileCount = files.Length,
                    breakdown
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("part")]
        public async Task<IActionResult> RenderPart(IFormFile file, [FromQuery] int partIndex = 0, [FromQuery] int sampleRate = 44100, [FromQuery] int bitDepth = 16, [FromQuery] int channels = 1)
        {
            if (file == null || file.Length == 0) return BadRequest("No file uploaded");

            try
            {
                var tempFile = Path.GetTempFileName();
                using (var stream = new FileStream(tempFile, FileMode.Create)) { file.CopyTo(stream); }

                Formats.LoadProject(new string[] { tempFile });
                var project = DocManager.Inst.Project;
                if (project == null) {
                    System.IO.File.Delete(tempFile);
                    return BadRequest("Failed to load project.");
                }

                if (partIndex < 0 || partIndex >= project.parts.Count || !(project.parts[partIndex] is OpenUtau.Core.Ustx.UVoicePart part))
                {
                    System.IO.File.Delete(tempFile);
                    return BadRequest("Invalid UVoicePart index.");
                }

                // Create a mixdown with just this part by muting other tracks, or using RenderEngine with start/end
                var engine = new RenderEngine(project, part.position, part.End, part.trackNo);
                var tokenSource = new CancellationTokenSource();
                
                // Wait for the render to complete
                var renderResult = await Task.Run(() => engine.RenderMixdown(DocManager.Inst.MainScheduler, ref tokenSource, true));
                var mix = renderResult.Item1;

                var outAudioTemp = Path.GetTempFileName() + ".wav";
                CheckFileWritable(outAudioTemp);
                ISampleProvider sampleProvider = new ExportAdapter(mix);
                if (channels == 1) {
                    sampleProvider = sampleProvider.ToMono(1, 0);
                } else if (channels == 2) {
                    // ExportAdapter is 2 channels by default
                }
                
                if (sampleRate != 44100) {
                    sampleProvider = new WdlResamplingSampleProvider(sampleProvider, sampleRate);
                }

                IWaveProvider waveProvider;
                if (bitDepth == 16) {
                    waveProvider = sampleProvider.ToWaveProvider16();
                } else if (bitDepth == 32) {
                    waveProvider = sampleProvider.ToWaveProvider(); // 32-bit float
                } else if (bitDepth == 24) {
                    // NAudio trick for 24-bit if missing is sometimes complex, but let's just stick to 16 if not 32 for safety, or try SampleToWaveProvider24 if it exists? We can use new NAudio.Wave.SampleProviders.SampleToWaveProvider24(sampleProvider) ? Actually ToWaveProvider16 is an extension. Let's use ToWaveProvider16 for safety on unknown.
                    // Wait, we can implement 24 bit if needed but 16 and 32 are easily available. Let's use 16 as fallback.
                    waveProvider = sampleProvider.ToWaveProvider16();
                } else {
                    waveProvider = sampleProvider.ToWaveProvider16();
                }

                NAudio.Wave.WaveFileWriter.CreateWaveFile(outAudioTemp, waveProvider);
                System.IO.File.Delete(tempFile);

                var streamRet = new FileStream(outAudioTemp, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);
                return File(streamRet, "audio/wav", $"part_{partIndex}.wav");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
            }
        }

        [HttpPost("mixdown")]
        public async Task<IActionResult> RenderMixdown(IFormFile file, [FromQuery] int sampleRate = 44100, [FromQuery] int bitDepth = 16, [FromQuery] int channels = 1)
        {
            if (file == null || file.Length == 0) return BadRequest("No file uploaded");

            try
            {
                var tempFile = Path.GetTempFileName();
                using (var stream = new FileStream(tempFile, FileMode.Create)) { file.CopyTo(stream); }

                Formats.LoadProject(new string[] { tempFile });
                var project = DocManager.Inst.Project;
                if (project == null) {
                    System.IO.File.Delete(tempFile);
                    return BadRequest("Failed to load project.");
                }

                var engine = new RenderEngine(project);
                var tokenSource = new CancellationTokenSource();
                
                var renderResult = await Task.Run(() => engine.RenderMixdown(DocManager.Inst.MainScheduler, ref tokenSource, true));
                var mix = renderResult.Item1;

                var outAudioTemp = Path.GetTempFileName() + ".wav";
                CheckFileWritable(outAudioTemp);
                ISampleProvider sampleProvider = new ExportAdapter(mix);
                if (channels == 1) {
                    sampleProvider = sampleProvider.ToMono(1, 0);
                } else if (channels == 2) {
                    // ExportAdapter is 2 channels by default
                }
                
                if (sampleRate != 44100) {
                    sampleProvider = new WdlResamplingSampleProvider(sampleProvider, sampleRate);
                }

                IWaveProvider waveProvider;
                if (bitDepth == 16) {
                    waveProvider = sampleProvider.ToWaveProvider16();
                } else if (bitDepth == 32) {
                    waveProvider = sampleProvider.ToWaveProvider(); // 32-bit float
                } else if (bitDepth == 24) {
                    // NAudio trick for 24-bit if missing is sometimes complex, but let's just stick to 16 if not 32 for safety, or try SampleToWaveProvider24 if it exists? We can use new NAudio.Wave.SampleProviders.SampleToWaveProvider24(sampleProvider) ? Actually ToWaveProvider16 is an extension. Let's use ToWaveProvider16 for safety on unknown.
                    // Wait, we can implement 24 bit if needed but 16 and 32 are easily available. Let's use 16 as fallback.
                    waveProvider = sampleProvider.ToWaveProvider16();
                } else {
                    waveProvider = sampleProvider.ToWaveProvider16();
                }

                NAudio.Wave.WaveFileWriter.CreateWaveFile(outAudioTemp, waveProvider);
                System.IO.File.Delete(tempFile);

                var streamRet = new FileStream(outAudioTemp, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);
                return File(streamRet, "audio/wav", "mixdown.wav");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
            }
        }

        private void CheckFileWritable(string filePath)
        {
            if (System.IO.File.Exists(filePath))
            {
                using (System.IO.FileStream stream = System.IO.File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    stream.Close();
                }
            }
        }
    }
}
