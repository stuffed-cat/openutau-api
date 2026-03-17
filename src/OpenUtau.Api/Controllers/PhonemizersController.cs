using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using System.Linq;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PhonemizersController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetPhonemizers()
        {
            var factories = DocManager.Inst.PhonemizerFactories;
            if (factories == null)
            {
                return Ok(new object[] { });
            }

            var phonemizers = factories.Select(f => new {
                Name = f.name,
                Tag = f.tag,
                Author = f.author,
                Language = f.language,
                Type = f.type.FullName
            });

            return Ok(phonemizers);
        }
    }
}
