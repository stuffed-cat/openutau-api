with open("src/OpenUtau.Api/Controllers/AudioAnalysisController.cs", "r") as f:
    text = f.read()

waveform_api = """
        [HttpPost("waveform")]
        public IActionResult AnalyzeWaveform(IFormFile file, [FromQuery] int points = 1000)
        {
            if (file == null || file.Length == 0) return BadRequest("No audio file uploaded.");
            var tempFile = SaveTempFile(file, ".wav");

            try
            {
                List<double> maxWaveform = new List<double>();
                List<double> minWaveform = new List<double>();
                
                using (var waveStream = Wave.OpenFile(tempFile))
                {
                    var sampleProvider = waveStream.ToSampleProvider();
                    if (sampleProvider.WaveFormat.Channels > 1) {
                        sampleProvider = new NAudio.Wave.SampleProviders.StereoToMonoSampleProvider(sampleProvider);
                    }
                    var signal = Wave.GetSignal(sampleProvider);
                    
                    if (points <= 0) points = 1000;
                    if (points > signal.Length) points = signal.Length;
                    
                    int windowSize = signal.Length / points;
                    if (windowSize == 0) windowSize = 1;

                    for (int i = 0; i < points && i * windowSize < signal.Length; i++)
                    {
                        double max = double.MinValue;
                        double min = double.MaxValue;
                        
                        int start = i * windowSize;
                        int end = Math.Min(start + windowSize, signal.Length);
                        
                        for (int j = start; j < end; j++)
                        {
                            double v = signal.Samples[j];
                            if (v > max) max = v;
                            if (v < min) min = v;
                        }
                        
                        maxWaveform.Add(max == double.MinValue ? 0 : max);
                        minWaveform.Add(min == double.MaxValue ? 0 : min);
                    }
                }
                System.IO.File.Delete(tempFile);
                return Ok(new { points = maxWaveform.Count, maxWaveform, minWaveform });
            }
            catch (Exception ex)
            {
                if (System.IO.File.Exists(tempFile)) System.IO.File.Delete(tempFile);
                return StatusCode(500, ex.Message);
            }
        }
"""

text = text.replace("        [HttpPost(\"energy\")]", waveform_api + "\n        [HttpPost(\"energy\")]")

with open("src/OpenUtau.Api/Controllers/AudioAnalysisController.cs", "w") as f:
    f.write(text)

