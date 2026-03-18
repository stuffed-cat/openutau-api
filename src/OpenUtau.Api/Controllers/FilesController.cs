using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FilesController : ControllerBase
    {
        public class BrowseRequest {
            public string Path { get; set; }
        }

        [HttpPost("browse")]
        public IActionResult Browse([FromBody] BrowseRequest request)
        {
            var dirPath = request.Path;
            if (string.IsNullOrEmpty(dirPath)) {
                dirPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            if (!Directory.Exists(dirPath)) {
                return NotFound(new { Error = "Directory not found" });
            }

            try {
                var dirs = Directory.GetDirectories(dirPath).Select(g => new {
                    Name = Path.GetFileName(g),
                    Path = g,
                    IsDirectory = true
                });

                var files = Directory.GetFiles(dirPath).Select(g => new {
                    Name = Path.GetFileName(g),
                    Path = g,
                    IsDirectory = false
                });

                return Ok(dirs.Concat(files).ToList());
            } 
            catch (Exception ex) {
                return BadRequest(new { Error = ex.Message });
            }
        }
    }
}
