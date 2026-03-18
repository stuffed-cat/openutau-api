using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
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
        public IActionResult UpdatePreferences([FromBody] Dictionary<string, JsonElement> request)
        {
            var prefs = Preferences.Default;
            var type = prefs.GetType();
            foreach (var kvp in request)
            {
                var field = type.GetField(kvp.Key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (field != null)
                {
                    try
                    {
                        var value = JsonSerializer.Deserialize(kvp.Value.GetRawText(), field.FieldType);
                        field.SetValue(prefs, value);
                    }
                    catch (Exception)
                    {
                        // Ignore validation errors for individual fields
                    }
                }
            }
            Preferences.Save();
            return Ok(Preferences.Default);
        }
    }
}
