using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Format;
using OpenUtau.Core.Analysis.Crepe;
using NAudio.Wave;
using System.IO;
using System;
using System.Linq;
using System.Collections.Generic;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/analysis")]
    public class AudioAnalysisController : ControllerBase
    {
        private string SaveTempFile(IFormFile file, string extension)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "OpenUtauApi");
            Directory.CreateDirectory(tempDir);
            var tempFile = Path.Combine(tempDir, Guid.NewGuid().ToString() + extension);
            using (var stream = new FileStream(tempFile, FileMode.Create))
            {
                file.CopyTo(stream);
            }
            return tempFile;
        }

        [HttpPost("f0")]
        public IActionResult AnalyzeF0(IFormFile file, [FromQuery] double stepMs = 10.0, [FromQuery] double threshold = 0.21)
        {
            if (file == null || file.Length == 0) return BadRequest("No audio file uploaded.");
            var tempFile = SaveTempFile(file, ".wav"); 
            
            try
            {
                double[] f0 = null;
                using (var waveStream = Wave.OpenFile(tempFile))
                {
                    var sampleProvider = waveStream.ToSampleProvider();
                    if (sampleProvider.WaveFormat.Channels > 1) {
                        sampleProvider = new NAudio.Wave.SampleProviders.StereoToMonoSampleProvider(sampleProvider);
                    }
                    var signal = Wave.GetSignal(sampleProvider);
                    
                    try {
                        using (var crepe = new Crepe())
                        {
                            f0 = crepe.ComputeF0(signal, stepMs, threshold);
                        }
                    } catch (IndexOutOfRangeException) {
                        // Crepe throws IndexOutOfRangeException on perfect synthetic signals due to internal NaN/ArgMax issues.
                        // For a real user API, returning an empty array or warning would be safer than throwing 500 when they test with sine waves.
                        return StatusCode(400, "ComputeF0 failed internally due to unsupported audio structure (maybe perfect sine?). Please provide natural human vocals.");
                    }
                }
                System.IO.File.Delete(tempFile);
                return Ok(new { stepMs, f0 });
            }
            catch (Exception ex)
            {
                if (System.IO.File.Exists(tempFile)) System.IO.File.Delete(tempFile);
                return StatusCode(500, ex.Message + "\n" + ex.StackTrace);
            }
        }

        [HttpPost("midi")]
        public IActionResult ImportMidi(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("No file uploaded.");
            var tempFile = SaveTempFile(file, ".mid");

            try
            {
                UProject project = Formats.ReadProject(new string[] { tempFile });
                if (project == null) return BadRequest("Failed to read MIDI project.");
                
                System.IO.File.Delete(tempFile);

                var outTemp = Path.Combine(Path.GetTempPath(), "OpenUtauApi", Guid.NewGuid().ToString() + ".ustx");
                Ustx.Save(outTemp, project);
                var bytes = System.IO.File.ReadAllBytes(outTemp);
                System.IO.File.Delete(outTemp);

                return File(bytes, "application/octet-stream", "project.ustx");
            }
            catch (Exception ex)
            {
                if (System.IO.File.Exists(tempFile)) System.IO.File.Delete(tempFile);
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("energy")]
        public IActionResult AnalyzeEnergy(IFormFile file, [FromQuery] double stepMs = 10.0)
        {
            if (file == null || file.Length == 0) return BadRequest("No audio file uploaded.");
            var tempFile = SaveTempFile(file, ".wav");

            try
            {
                List<double> energies = new List<double>();
                using (var waveStream = Wave.OpenFile(tempFile))
                {
                    var sampleProvider = waveStream.ToSampleProvider();
                    if (sampleProvider.WaveFormat.Channels > 1) {
                        sampleProvider = new NAudio.Wave.SampleProviders.StereoToMonoSampleProvider(sampleProvider);
                    }
                    var signal = Wave.GetSignal(sampleProvider);
                    
                    int sampleRate = signal.SamplingRate;
                    int stepSamples = (int)(sampleRate * stepMs / 1000.0);
                    
                    for (int i = 0; i < signal.Length; i += stepSamples)
                    {
                        double sumSq = 0;
                        int count = Math.Min(stepSamples, signal.Length - i);
                        for (int j = 0; j < count; j++)
                        {
                            // Math absolute bounds checks could be added, but this is safe within signal.Samples memory bounds
                            double v = signal.Samples[i + j];
                            sumSq += v * v;
                        }
                        double rms = Math.Sqrt(sumSq / count);
                        energies.Add(rms);
                    }
                }
                System.IO.File.Delete(tempFile);
                return Ok(new { stepMs, energies });
            }
            catch (Exception ex)
            {
                if (System.IO.File.Exists(tempFile)) System.IO.File.Delete(tempFile);
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("split")]
        public IActionResult SplitAudio(IFormFile file, 
            [FromQuery] double minDurationMs = 100.0, 
            [FromQuery] double minSilenceMs = 200.0,
            [FromQuery] double thresholdRms = 0.01)
        {
            if (file == null || file.Length == 0) return BadRequest("No audio file uploaded.");
            var tempFile = SaveTempFile(file, ".wav");

            try
            {
                var segments = new List<object>();
                using (var waveStream = Wave.OpenFile(tempFile))
                {
                    var sampleProvider = waveStream.ToSampleProvider();
                    if (sampleProvider.WaveFormat.Channels > 1) {
                        sampleProvider = new NAudio.Wave.SampleProviders.StereoToMonoSampleProvider(sampleProvider);
                    }
                    var signal = Wave.GetSignal(sampleProvider);
                    
                    int sampleRate = signal.SamplingRate;
                    double stepMs = 10.0;
                    int stepSamples = (int)(sampleRate * stepMs / 1000.0);
                    
                    bool inVoice = false;
                    int voiceStartMs = 0;
                    int silenceCountMs = 0;

                    for (int i = 0; i < signal.Length; i += stepSamples)
                    {
                        double sumSq = 0;
                        int count = Math.Min(stepSamples, signal.Length - i);
                        for (int j = 0; j < count; j++)
                        {
                            double v = signal.Samples[i + j];
                            sumSq += v * v;
                        }
                        double rms = Math.Sqrt(sumSq / count);
                        
                        int currentMs = (int)(i / (double)sampleRate * 1000);

                        if (rms > thresholdRms)
                        {
                            if (!inVoice)
                            {
                                inVoice = true;
                                voiceStartMs = currentMs;
                            }
                            silenceCountMs = 0;
                        }
                        else
                        {
                            if (inVoice)
                            {
                                silenceCountMs += (int)stepMs;
                                if (silenceCountMs >= minSilenceMs)
                                {
                                    int voiceEndMs = currentMs - silenceCountMs;
                                    if (voiceEndMs - voiceStartMs >= minDurationMs)
                                    {
                                        segments.Add(new { startMs = voiceStartMs, endMs = voiceEndMs });
                                    }
                                    inVoice = false;
                                }
                            }
                        }
                    }

                    if (inVoice)
                    {
                        int currentMs = (int)(signal.Length / (double)sampleRate * 1000);
                        int voiceEndMs = currentMs - silenceCountMs;
                        if (voiceEndMs - voiceStartMs >= minDurationMs)
                        {
                            segments.Add(new { startMs = voiceStartMs, endMs = voiceEndMs });
                        }
                    }
                }
                
                System.IO.File.Delete(tempFile);
                return Ok(new { segments });
            }
            catch (Exception ex)
            {
                if (System.IO.File.Exists(tempFile)) System.IO.File.Delete(tempFile);
                return StatusCode(500, ex.Message);
            }
        }
    }
}
