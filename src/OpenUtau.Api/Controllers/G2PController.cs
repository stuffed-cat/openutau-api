using Microsoft.AspNetCore.Mvc;
using OpenUtau.Api;
using OpenUtau.Core.Api;
using System.Linq;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class G2PController : ControllerBase
    {
        [HttpGet]
        public IActionResult SupportedG2P()
        {
            var types = typeof(IG2p).Assembly.GetTypes()
                .Where(t => typeof(IG2p).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .Select(t => t.Name)
                .ToList();
            
            return Ok(types);
        }

        [HttpPost("{lang}/query")]
        public IActionResult Query(string lang, [FromBody] G2PQueryRequest request)
        {
            var type = typeof(IG2p).Assembly.GetTypes()
                .FirstOrDefault(t => typeof(IG2p).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract && t.Name.ToLower().Contains(lang.ToLower()));

            if (type == null)
                return NotFound($"G2P for language {lang} not found.");

            var obj = System.Activator.CreateInstance(type) as IG2p;
            if (obj == null)
            {
                return StatusCode(500, "Failed to instantiate G2P module.");
            }

            var result = obj.Query(request.Text);
            return Ok(result);
        }
    }

    public class G2PQueryRequest 
    {
        public string Text { get; set; } = string.Empty;
    }
}
