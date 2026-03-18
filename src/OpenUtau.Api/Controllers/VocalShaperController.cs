using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OpenUtau.Core.Render;
using OpenUtau.Core.Format;
using System;
using System.IO;
using System.Linq;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VocalShaperController : ControllerBase
    {
        [HttpPost("process")]
        public IActionResult ProcessAudio(
            IFormFile file, 
            [FromForm] double f0Shift = 0.0,
            [FromForm] double gender = 0.5,
            [FromForm] double tension = 0.5,
            [FromForm] double breathiness = 0.5,
            [FromForm] double voicing = 1.0)
        {
            if (file == null || file.Length == 0) return BadRequest("No audio file provided.");

            try
            {
                var inputTemp = Path.GetTempFileName();
                using (var stream = new FileStream(inputTemp, FileMode.Create))
                {
                    file.CopyTo(stream);
                }

                Console.WriteLine($"[VocalShaper] Saved to {inputTemp}");

                // NAudio to read floats
                float[] samples;
                int fs;
                using (var waveStream = Wave.OpenFile(inputTemp))
                {
                    fs = waveStream.WaveFormat.SampleRate;
                    samples = Wave.GetSamples(waveStream.ToSampleProvider().ToMono(1.0f, 0.0f)).ToArray();
                }

                Console.WriteLine($"[VocalShaper] Read {samples.Length} samples at {fs}Hz. method=dio");

                // World Analysis config
                int hopSize = (int)(fs * 0.005); // 5ms
                int fftSize = 2048;
                if (fs > 48000) fftSize = 4096;
                
                var config = Worldline.InitAnalysisConfig(fs, hopSize, fftSize);
                
                Console.WriteLine($"[VocalShaper] F0 Extract...");
                double[] f0Array = Worldline.F0(samples, fs, config.frame_ms, 0);

                Console.WriteLine($"[VocalShaper] f0Array.Length: {f0Array.Length}");
                
                Console.WriteLine($"[VocalShaper] WorldAnalysisF0In...");
                Worldline.WorldAnalysisF0In(ref config, samples, f0Array, out var spEnvND, out var apND);

                Console.WriteLine($"[VocalShaper] NDArray ToArray...");
                int numFrames = f0Array.Length;
                double[] spEnvArray = spEnvND.ToArray<double>();
                double[] apArray = apND.ToArray<double>();

                Console.WriteLine($"[VocalShaper] Modifying params...");
                // Shape modification arrays
                double[] genderArray = Enumerable.Repeat(gender, numFrames).ToArray();
                double[] tensionArray = Enumerable.Repeat(tension, numFrames).ToArray();
                double[] breathinessArray = Enumerable.Repeat(breathiness, numFrames).ToArray();
                double[] voicingArray = Enumerable.Repeat(voicing, numFrames).ToArray();

                // F0 shift (Pitch envelope modification)
                if (f0Shift != 0)
                {
                    double f0Mult = Math.Pow(2.0, f0Shift / 12.0);
                    for (int i = 0; i < f0Array.Length; i++)
                    {
                        if (f0Array[i] > config.f0_floor)
                        {
                            f0Array[i] *= f0Mult;
                        }
                    }
                }

                int spSize = config.fft_size / 2 + 1;

                Console.WriteLine($"[VocalShaper] WorldSynthesis...");
                // World Synthesis
                double[] samplesOut = Worldline.WorldSynthesis(
                    f0Array,
                    spEnvArray, false, spSize,
                    apArray, false, config.fft_size,
                    config.frame_ms, fs,
                    genderArray, tensionArray, breathinessArray, voicingArray);

                Console.WriteLine($"[VocalShaper] Output array size: {samplesOut.Length}");

                var outputTemp = Path.ChangeExtension(inputTemp, ".wav");
                
                var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(fs, 1);
                using (var writer = new WaveFileWriter(outputTemp, waveFormat))
                {
                    writer.WriteSamples(samplesOut.Select(s => (float)s).ToArray(), 0, samplesOut.Length);
                }

                System.IO.File.Delete(inputTemp);

                Console.WriteLine($"[VocalShaper] DONE.");
                return PhysicalFile(outputTemp, "audio/wav", "processed.wav");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VocalShaper] ERROR: {ex}");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
