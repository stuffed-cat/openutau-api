using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using OpenUtau.Core.Render;
using OpenUtau.Core.SignalChain;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using System.Linq;
using System.Collections.Generic;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VogenController : ControllerBase
    {
        [HttpGet("singers")]
        public IActionResult GetSingers()
        {
            var singers = SingerManager.Inst.Singers.Values
                .Where(s => s.SingerType == USingerType.Vogen)
                .Select(s => new {
                    id = s.Id,
                    name = s.Name,
                    format = s.SingerType.ToString()
                });
            return Ok(singers);
        }

        [HttpGet("singers/{singerId}")]
        public IActionResult GetSingerDetails(string singerId)
        {
            var singer = SingerManager.Inst.Singers.Values
                .FirstOrDefault(s => s.SingerType == USingerType.Vogen && s.Id == singerId);
            
            if (singer == null) return NotFound($"Vogen singer {singerId} not found");

            return Ok(new {
                id = singer.Id,
                name = singer.Name,
                author = singer.Author,
                version = singer.Version,
                subbanks = singer.Subbanks.Select(sb => new {
                    prefix = sb.Prefix,
                    suffix = sb.Suffix,
                    toneSet = sb.toneSet,
                    color = sb.Color
                })
            });
        }

        [HttpPost("render/{partNo}")]
        public async Task<IActionResult> RenderPart(int partNo, [FromQuery] int sampleRate = 44100, [FromQuery] int bitDepth = 16, [FromQuery] int channels = 1)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");
            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest("Invalid part index");

            var part = project.parts[partNo] as UVoicePart;
            if (part == null) return BadRequest("Not a voice part");

            try
            {
                var engine = new RenderEngine(project, part.position, part.End, part.trackNo);
                var tokenSource = new CancellationTokenSource();
                
                var renderResult = await Task.Run(() => engine.RenderMixdown(DocManager.Inst.MainScheduler, ref tokenSource, true));
                var mix = renderResult.Item1;

                var outAudioTemp = Path.GetTempFileName() + ".wav";
                NAudio.Wave.ISampleProvider sampleProvider = new OpenUtau.Core.SignalChain.ExportAdapter(mix);
                if (channels == 1) {
                    sampleProvider = sampleProvider.ToMono(1, 0);
                }
                
                if (sampleRate != 44100) {
                    sampleProvider = new NAudio.Wave.SampleProviders.WdlResamplingSampleProvider(sampleProvider, sampleRate);
                }

                IWaveProvider waveProvider;
                if (bitDepth == 16) {
                    waveProvider = sampleProvider.ToWaveProvider16();
                } else if (bitDepth == 32) {
                    waveProvider = sampleProvider.ToWaveProvider();
                } else {
                    waveProvider = sampleProvider.ToWaveProvider16();
                }

                NAudio.Wave.WaveFileWriter.CreateWaveFile(outAudioTemp, waveProvider);

                var streamRet = new FileStream(outAudioTemp, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);
                return File(streamRet, "audio/wav", $"part_{partNo}_vogen.wav");
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
            }
        }
    }
}
