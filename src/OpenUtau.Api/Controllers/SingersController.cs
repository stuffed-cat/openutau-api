using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using System.Linq;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SingersController : ControllerBase
    {
        [HttpGet("{id}/info")]
        public IActionResult GetSingerInfo(string id)
        {
            var singer = SingerManager.Inst.Singers.Values.FirstOrDefault(s => s.Id == id);
            if (singer == null)
            {
                return NotFound("Singer not found");
            }
            
            return Ok(new {
                Id = singer.Id,
                Name = singer.Name,
                Author = singer.Author,
                Version = singer.Version,
                SingerType = singer.SingerType.ToString(),
                BasePath = singer.BasePath,
                Subbanks = singer.Subbanks.Select(b => new {
                    Name = b.Color,
                    Prefix = b.Prefix,
                    Suffix = b.Suffix,
                    ToneSet = b.toneSet
                })
            });
        }
    }
}
