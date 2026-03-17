using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core.Render;
using OpenUtau.Core.Ustx;
using System;
using System.Linq;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RenderersController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetSupportedRenderers([FromQuery] string singerType = null)
        {
            if (string.IsNullOrEmpty(singerType))
            {
                // Return all mapped out loosely if not specified
                var allTypes = Enum.GetValues(typeof(USingerType)).Cast<USingerType>();
                var result = allTypes.ToDictionary(
                    type => type.ToString(),
                    type => Renderers.GetSupportedRenderers(type)
                );
                return Ok(result);
            }

            if (Enum.TryParse(typeof(USingerType), singerType, true, out var parsedType))
            {
                return Ok(Renderers.GetSupportedRenderers((USingerType)parsedType));
            }

            return BadRequest("Invalid singer type.");
        }
    }
}
