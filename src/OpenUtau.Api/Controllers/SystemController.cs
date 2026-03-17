using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Util;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SystemController : ControllerBase
    {
        [HttpGet("info")]
        public IActionResult GetSystemInfo()
        {
            return Ok(new
            {
                DataPath = PathManager.Inst.DataPath,
                CachePath = PathManager.Inst.CachePath,
                Message = "OpenUtau Core is running headlessly!"
            });
        }
    }
}
