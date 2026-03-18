using System;
using System.Diagnostics;
using System.IO;

namespace OpenUtau.Api
{
    public static class AudioExporter
    {
        public static string ConvertFormat(string inWavFile, string format)
        {
            var ext = format.ToLowerInvariant().TrimStart('.');
            if (string.IsNullOrEmpty(ext) || ext == "wav") return inWavFile;
            var outFile = Path.ChangeExtension(inWavFile, "." + ext);

            try
            {
                var process = new Process();
                process.StartInfo.FileName = "ffmpeg";
                // Convert WAV to requested format, overwrite if exists, hide banner
                process.StartInfo.Arguments = $"-y -hide_banner -loglevel error -i \"{inWavFile}\" \"{outFile}\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.Start();
                process.WaitForExit();

                if (process.ExitCode == 0 && File.Exists(outFile))
                {
                    File.Delete(inWavFile);
                    return outFile;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AudioExporter] ffmpeg failed: {ex.Message}");
            }
            // fallback to wav if ffmpeg not installed or failed
            return inWavFile;
        }

        public static string GetContentType(string format)
        {
            var ext = format.ToLowerInvariant().TrimStart('.');
            switch (ext)
            {
                case "flac": return "audio/flac";
                case "ogg":  return "audio/ogg";
                case "mp3":  return "audio/mpeg";
                default:     return "audio/wav";
            }
        }
    }
}
