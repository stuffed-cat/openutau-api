using Microsoft.AspNetCore.Mvc;
using OpenUtau.Classic;
using System.Linq;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ToolsController : ControllerBase
    {
        [HttpGet("resamplers")]
        public IActionResult GetResamplers()
        {
            ToolsManager.Inst.Initialize(); 
            var resamplers = ToolsManager.Inst.Resamplers.Select(r => new {
                Name = r.ToString(),
                FilePath = r.FilePath
            });

            return Ok(resamplers);
        }

        [HttpGet("wavtools")]
        public IActionResult GetWavtools()
        {
            ToolsManager.Inst.Initialize();
            var wavtools = ToolsManager.Inst.Wavtools.Select(w => new {
                Name = w.ToString()
            });

            return Ok(wavtools);
        }
    }
}
