using Microsoft.AspNetCore.Mvc;
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
        public IActionResult RenderPart(int partNo)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");
            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest("Invalid part index");

            var part = project.parts[partNo] as UVoicePart;
            if (part == null) return BadRequest("Not a voice part");

            // Normally this would invoke the VogenRenderer logic.
            // Placeholder for now as direct renderer invocation is complex via API
            return Ok(new { message = $"Reder requested for part {partNo} via Vogen." });
        }
    }
}
