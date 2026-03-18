using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using OpenUtau.Core.Render;
using OpenUtau.Core.SignalChain;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OpenUtau.Core;
using OpenUtau.Core.Enunu;
using OpenUtau.Core.Ustx;
using System.Linq;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EnunuController : ControllerBase
    {
        [HttpGet("models")]
        public IActionResult GetModels()
        {
            if (DocManager.Inst.Project == null)
            {
                return BadRequest(new { error = "No project loaded" });
            }

            var enunuSingers = SingerManager.Inst.Singers.Values
                .Where(s => s.SingerType == USingerType.Enunu)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.Location,
                    s.SingerType
                });

            return Ok(enunuSingers);
        }

        [HttpGet("models/{singerId}/config")]
        public IActionResult GetConfig(string singerId)
        {
            if (DocManager.Inst.Project == null)
            {
                return BadRequest(new { error = "No project loaded" });
            }

            var singer = SingerManager.Inst.Singers.Values.FirstOrDefault(s => s.Id == singerId);
            if (singer == null)
            {
                return NotFound(new { error = "Singer not found" });
            }

            if (singer.SingerType != USingerType.Enunu)
            {
                return BadRequest(new { error = "Singer is not an Enunu model" });
            }

            try
            {
                var config = EnunuConfig.Load(singer);
                return Ok(config);
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("render/{partNo}")]
        public async Task<IActionResult> RenderPart(int partNo)
        {
            if (DocManager.Inst.Project == null)
            {
                return BadRequest(new { error = "No project loaded" });
            }

            var project = DocManager.Inst.Project;
            if (partNo < 0 || partNo >= project.parts.Count)
            {
                return NotFound(new { error = "Part not found" });
            }

            var part = project.parts[partNo] as UVoicePart;
            if (part == null)
            {
                return BadRequest(new { error = "Part is not a voice part" });
            }

            var track = project.tracks[part.trackNo];
            var singer = track.Singer;

            if (singer == null || singer.SingerType != USingerType.Enunu)
            {
                return BadRequest(new { error = "Track singer is not an Enunu model" });
            }

            // Enunu models might be slow, so we definitely need async and task run
            try
            {
                var engine = new RenderEngine(project, part.position, part.End, part.trackNo);
                var tokenSource = new CancellationTokenSource();
                
                var renderResult = await Task.Run(() => engine.RenderMixdown(DocManager.Inst.MainScheduler, ref tokenSource, true));
                var mix = renderResult.Item1;

                var outAudioTemp = Path.GetTempFileName() + ".wav";
                NAudio.Wave.WaveFileWriter.CreateWaveFile16(outAudioTemp, new ExportAdapter(mix).ToMono(1, 0));

                var streamRet = new FileStream(outAudioTemp, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);
                return File(streamRet, "audio/wav", $"part_{partNo}_enunu.wav");
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
            }
        }
    }
}
