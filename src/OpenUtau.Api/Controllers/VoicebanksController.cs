using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using System.Linq;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VoicebanksController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetVoicebanks()
        {
            var singers = SingerManager.Inst.Singers.Values.Select(s => new {
                Id = s.Id,
                Name = s.Name,
                Author = s.Author,
                Version = s.Version,
                SingerType = s.SingerType.ToString()
            });
            return Ok(singers);
        }
    }
}
