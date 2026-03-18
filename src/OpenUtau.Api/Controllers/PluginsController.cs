using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Api;
using OpenUtau.Classic;
using OpenUtau.Core;
using OpenUtau.Core.Render;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Util;
using System.IO.Compression;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PluginsController : ControllerBase
    {
        // 1. Get all Phonemizers
        [HttpGet("phonemizers")]
        public IActionResult GetPhonemizers()
        {
            var factories = PhonemizerFactory.GetAll().Select(f => new {
                Name = f.name,
                FullName = f.type.FullName,
                Tag = f.tag,
                Author = f.author,
                Language = f.language
            });
            return Ok(factories);
        }

        // 2. Get all Legacy Plugins
        [HttpGet("legacy")]
        public IActionResult GetLegacyPlugins()
        {
            var plugins = DocManager.Inst.Plugins?.Select(p => new {
                Name = p.Name,
                Shortcut = p.Shortcut,
                Executable = p.Executable,
                UseShell = p.UseShell
            });
            return Ok(plugins != null ? (object)plugins : Array.Empty<object>());
        }

        // 3. Get all Renderers
        [HttpGet("renderers")]
        public IActionResult GetRenderers()
        {
            var types = Enum.GetValues(typeof(USingerType)).Cast<USingerType>();
            var renderersBySinger = types.ToDictionary(
                t => t.ToString(),
                t => Renderers.GetSupportedRenderers(t)
            );
            return Ok(renderersBySinger);
        }

        // 4. Install Plugin (ZIP or DLL)
        [HttpPost("install")]
        public async Task<IActionResult> InstallPlugin(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("File missing");
            var pluginDir = PathManager.Inst.PluginsPath;
            Directory.CreateDirectory(pluginDir);

            if (file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                var tempZip = Path.Combine(Path.GetTempPath(), file.FileName);
                using (var stream = new FileStream(tempZip, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
                
                try {
                    ZipFile.ExtractToDirectory(tempZip, pluginDir, overwriteFiles: true);
                } finally {
                    System.IO.File.Delete(tempZip);
                }
            }
            else if (file.FileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                var destPath = Path.Combine(pluginDir, file.FileName);
                using (var stream = new FileStream(destPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
            }
            else
            {
                return BadRequest("Only .zip and .dll files are supported for plugins.");
            }

            // Reload
            DocManager.Inst.SearchAllPlugins();
            DocManager.Inst.SearchAllLegacyPlugins();

            return Ok(new { status = "Installed and reloaded" });
        }

        // 5. Run Legacy Plugin
        [HttpPost("legacy/run/{pluginName}")]
        public async Task<IActionResult> RunLegacyPlugin(string pluginName, [FromQuery] int trackNo, [FromQuery] int partNo)
        {
            var plugin = DocManager.Inst.Plugins?.FirstOrDefault(p => p.Name == pluginName);
            if (plugin == null) return NotFound("Plugin not found");

            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project loaded");
            if (trackNo < 0 || trackNo >= project.tracks.Count) return BadRequest("Track not found");
            var part = project.parts.Where(p => p.trackNo == trackNo).ElementAtOrDefault(partNo) as UVoicePart;
            if (part == null) return BadRequest("Voice part not found");

            var runner = PluginRunner.from(PathManager.Inst, DocManager.Inst);
            await runner.Execute(project, part, part.notes.FirstOrDefault(), part.notes.LastOrDefault(), plugin);

            return Ok(new { status = "Executed" });
        }

        // 6. Config Management - list plugin config files
        [HttpGet("configs")]
        public IActionResult GetPluginConfigs()
        {
            var pluginDir = PathManager.Inst.PluginsPath;
            if (!Directory.Exists(pluginDir)) return Ok(Array.Empty<string>());
            
            var configs = Directory.EnumerateFiles(pluginDir, "*.yaml", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(pluginDir, "*.json", SearchOption.AllDirectories))
                .Select(f => Path.GetRelativePath(pluginDir, f))
                .ToList();
            
            return Ok(configs);
        }

        // 7. Get Config content
        [HttpGet("configs/{*configPath}")]
        public IActionResult GetConfigContent(string configPath)
        {
            var pluginDir = PathManager.Inst.PluginsPath;
            configPath = Uri.UnescapeDataString(configPath);
            var path = Path.GetFullPath(Path.Combine(pluginDir, configPath));
            if (!path.StartsWith(Path.GetFullPath(pluginDir))) return Forbid(); // Path traversal check
            if (!System.IO.File.Exists(path)) return NotFound();
            
            var content = System.IO.File.ReadAllText(path);
            return Ok(new { path = configPath, content = content });
        }

        // 8. Set Config content
        [HttpPut("configs/{*configPath}")]
        public async Task<IActionResult> SetConfigContent(string configPath)
        {
            var pluginDir = PathManager.Inst.PluginsPath;
            configPath = Uri.UnescapeDataString(configPath);
            var path = Path.GetFullPath(Path.Combine(pluginDir, configPath));
            if (!path.StartsWith(Path.GetFullPath(pluginDir))) return Forbid();
            
            using var reader = new StreamReader(Request.Body);
            var content = await reader.ReadToEndAsync();
            
            System.IO.File.WriteAllText(path, content);
            return Ok(new { status = "Saved" });
        }
    }
}
