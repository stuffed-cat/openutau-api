using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Format;
using OpenUtau.Core.Render;
using OpenUtau.Core.SignalChain;
using OpenUtau.Core.Ustx;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PlaybackController : ControllerBase
    {
        [HttpPost("open")]
        public IActionResult OpenProject(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("File missing");
            var tempFile = Path.Combine(Path.GetTempPath(), "test_" + Guid.NewGuid() + ".ustx");
            using (var stream = new FileStream(tempFile, FileMode.Create))
            {
                file.CopyTo(stream);
            }
            
            var project = OpenUtau.Core.Format.Ustx.Load(tempFile);
            if (project != null) {
                DocManager.Inst.ExecuteCmd(new LoadProjectNotification(project));
            }
            
            System.IO.File.Delete(tempFile);
            return Ok(new { status = "Loaded" });
        }

        // 1. Play / Pause
        [HttpPost("play")]
        public IActionResult Play([FromQuery] int tick = -1, [FromQuery] int endTick = -1, [FromQuery] int trackNo = -1)
        {
            if (DocManager.Inst.Project == null) return BadRequest("Project is null");
            // API Server usually does not have UI thread loop, AudioOutput may be null.
            // If initialized with MiniAudioOutput, it might play on server.
            try {
                if (PlaybackManager.Inst.AudioOutput == null) {
                    PlaybackManager.Inst.AudioOutput = new OpenUtau.Audio.MiniAudioOutput();
                }
                PlaybackManager.Inst.Play(DocManager.Inst.Project, tick == -1 ? DocManager.Inst.playPosTick : tick, endTick, trackNo);
                return Ok(new { status = "Playing", tick = tick });
            } catch (Exception e) {
                return StatusCode(500, e.Message);
            }
        }

        // 2. Pause
        [HttpPost("pause")]
        public IActionResult Pause()
        {
            try {
                PlaybackManager.Inst.PausePlayback();
                return Ok(new { status = "Paused" });
            } catch (Exception e) {
                return StatusCode(500, e.Message);
            }
        }

        // 3. Stop
        [HttpPost("stop")]
        public IActionResult Stop()
        {
            try {
                PlaybackManager.Inst.StopPlayback();
                return Ok(new { status = "Stopped" });
            } catch (Exception e) {
                return StatusCode(500, e.Message);
            }
        }

        // 4. Seek (Jump)
        [HttpPost("seek")]
        public IActionResult Seek([FromQuery] int tick)
        {
            try {
                DocManager.Inst.ExecuteCmd(new SeekPlayPosTickNotification(tick));
                return Ok(new { tick = tick });
            } catch (Exception e) {
                return StatusCode(500, e.Message);
            }
        }

        // 5. Preview / Render a specific Part -> download WAV
        [HttpGet("preview/part/{trackNo}/{partIdx}")]
        public IActionResult PreviewPart(int trackNo, int partIdx)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded.");
            if (trackNo < 0 || trackNo >= project.tracks.Count) return BadRequest("Track not found.");
            
            var parts = project.parts.Where(p => p.trackNo == trackNo).ToList();
            if (partIdx < 0 || partIdx >= parts.Count) return BadRequest("Part not found.");
            var part = parts[partIdx];

            string tempFile = Path.Combine(Path.GetTempPath(), $"preview_part_{trackNo}_{partIdx}.wav");

            try {
                CancellationTokenSource renderCancellation = new CancellationTokenSource();
                RenderEngine engine = new RenderEngine(project, startTick: part.position, endTick: part.End, trackNo: trackNo);
                var projectMix = engine.RenderMixdown(DocManager.Inst.MainScheduler, ref renderCancellation, wait: true).Item1;
                
                WaveFileWriter.CreateWaveFile16(tempFile, new ExportAdapter(projectMix).ToMono(1, 0));

                var bytes = System.IO.File.ReadAllBytes(tempFile);
                return File(bytes, "audio/wav", $"part_{trackNo}_{partIdx}.wav");
            } catch (Exception e) {
                return StatusCode(500, e.Message);
            } finally {
                if (System.IO.File.Exists(tempFile)) System.IO.File.Delete(tempFile);
            }
        }
    }
}
