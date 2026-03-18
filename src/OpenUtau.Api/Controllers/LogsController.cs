using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Linq;
using OpenUtau.Core;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LogsController : ControllerBase
    {
        [HttpGet("location")]
        public IActionResult GetLogsLocation()
        {
            return Ok(new { Path = PathManager.Inst.LogsPath });
        }

        [HttpGet("list")]
        public IActionResult ListLogs()
        {
            var logsPath = PathManager.Inst.LogsPath;
            if (!Directory.Exists(logsPath)) {
                return Ok(new string[0]);
            }
            
            var files = Directory.GetFiles(logsPath, "*.txt").Select(Path.GetFileName).ToList();
            return Ok(files);
        }

        [HttpGet("{logFile}")]
        public IActionResult GetLog(string logFile)
        {
            var logsPath = PathManager.Inst.LogsPath;
            var filePath = Path.Combine(logsPath, logFile);
            if (!System.IO.File.Exists(filePath)) {
                return NotFound();
            }

            try {
                var content = System.IO.File.ReadAllText(filePath);
                return Content(content, "text/plain");
            } catch (System.Exception ex) {
                return StatusCode(500, ex.Message);
            }
        }
    }
}
