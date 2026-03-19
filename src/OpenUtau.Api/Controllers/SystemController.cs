using Microsoft.AspNetCore.Mvc;
using OpenUtau.Api;
using OpenUtau.Core;
using OpenUtau.Core.Render;
using OpenUtau.Core.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SystemController : ControllerBase
    {
        [HttpGet("info")]
        public IActionResult GetSystemInfo()
        {
            var phonemizers = DocManager.Inst.PhonemizerFactories?.ToList() ?? new List<PhonemizerFactory>();
            var renderersBySingerType = Enum.GetValues(typeof(OpenUtau.Core.Ustx.USingerType))
                .Cast<OpenUtau.Core.Ustx.USingerType>()
                .ToDictionary(
                    type => type.ToString(),
                    type => Renderers.GetSupportedRenderers(type).ToList());

            var uniqueRenderers = renderersBySingerType.Values
                .SelectMany(renderers => renderers)
                .Select(renderer => renderer?.ToString())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Ok(new
            {
                DataPath = PathManager.Inst.DataPath,
                CachePath = PathManager.Inst.CachePath,
                Version = new
                {
                    Api = GetAssemblyVersion(typeof(SystemController).Assembly),
                    Core = GetAssemblyVersion(typeof(DocManager).Assembly),
                    OpenUtau = GetAssemblyVersion(typeof(Renderers).Assembly)
                },
                Runtime = new
                {
                    OSDescription = RuntimeInformation.OSDescription,
                    OSArchitecture = RuntimeInformation.OSArchitecture.ToString(),
                    ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                    FrameworkDescription = RuntimeInformation.FrameworkDescription,
                    DotNetVersion = Environment.Version.ToString(),
                    MachineName = Environment.MachineName
                },
                Loaded = new
                {
                    Phonemizers = phonemizers.Count,
                    Renderers = uniqueRenderers.Count,
                    RenderersBySingerType = renderersBySingerType.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Value.Count())
                },
                Message = "OpenUtau Core is running headlessly!"
            });
        }

        private static string GetAssemblyVersion(Assembly assembly)
        {
            var info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                return info;
            }

            return assembly.GetName().Version?.ToString() ?? "unknown";
        }
    }
}
