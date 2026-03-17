using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core.Util;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PreferencesController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetPreferences()
        {
            return Ok(Preferences.Default);
        }

        [HttpPost]
        public IActionResult UpdatePreferences([FromBody] UpdatePreferencesRequest request)
        {
            if (request.AdditionalSingerPath != null) Preferences.Default.AdditionalSingerPath = request.AdditionalSingerPath;
            Preferences.Save();
            return Ok(Preferences.Default);
        }
    }

    public class UpdatePreferencesRequest
    {
        public string? AdditionalSingerPath { get; set; }
    }
}
