using Microsoft.AspNetCore.Mvc;
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
        public IActionResult RenderPart(int partNo)
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

            try
            {
                // Create a renderer matching the enunu rendering logic
                var renderer = new EnunuRenderer();
                
                // For a proper render via API, we would need to set up a RenderPhrase, 
                // but for Enunu, it may be complex. Let's return the basic renderer instantiation info
                // since Enunu rendering usually goes via PlaybackManager async
                return Ok(new { 
                    message = "Enunu render endpoint is partially implemented. Real rendering requires async pipeline.",
                    renderer = renderer.GetType().Name,
                    singer = singer.Name
                });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
