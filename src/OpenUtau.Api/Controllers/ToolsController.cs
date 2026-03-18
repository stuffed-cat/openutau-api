using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Collections.Generic;
using OpenUtau.Core.G2p;
using System.Threading.Tasks;
using OpenUtau.Classic;
using Classic;
using OpenUtau.Core;
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
    
        private List<Type> GetAvailableG2ps()
        {
            var g2ps = new List<Type>();
            // Load from current assembly & Core
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                try {
                    foreach (var type in assembly.GetExportedTypes())
                    {
                        if (!type.IsAbstract && type.IsSubclassOf(typeof(Api.G2pPack)))
                        {
                            g2ps.Add(type);
                        }
                    }
                } catch { }
            }
            return g2ps.Distinct().ToList();
        }

        [HttpPost("wavtool/install")]
        public async Task<IActionResult> InstallWavtool(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            try {
                var tempPath = Path.Combine(Path.GetTempPath(), file.FileName);
                using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                ExeInstaller.Install(tempPath, ExeType.wavtool);
                ToolsManager.Inst.Initialize();

                return Ok(new { Message = "Wavtool installed successfully." });
            } catch (System.Exception ex) {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("resampler/install")]
        public async Task<IActionResult> InstallResampler(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            try {
                var tempPath = Path.Combine(Path.GetTempPath(), file.FileName);
                using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                ExeInstaller.Install(tempPath, ExeType.resampler);
                ToolsManager.Inst.Initialize();

                return Ok(new { Message = "Resampler installed successfully." });
            } catch (System.Exception ex) {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("phonetic-assistant")]
        public IActionResult PhoneticAssistant([FromQuery] string g2p, [FromQuery] string grapheme)
        {
            if (string.IsNullOrEmpty(g2p) && string.IsNullOrEmpty(grapheme)) {
                return Ok(GetAvailableG2ps().Select(t => t.Name).ToList());
            }

            var g2pType = GetAvailableG2ps().FirstOrDefault(t => t.Name == g2p);
            if (g2pType == null) {
                return BadRequest(new { Error = "G2P not found" });
            }

            if (string.IsNullOrEmpty(grapheme)) {
                return BadRequest(new { Error = "No grapheme provided" });
            }

            try {
                var g2pInst = System.Activator.CreateInstance(g2pType) as Api.G2pPack;
                if (g2pInst == null) return StatusCode(500, "Failed to instantiate G2P");

                string[] phonemes = g2pInst.Query(grapheme);
                if (phonemes == null) return Ok(new { Grapheme = grapheme, Phonemes = new string[0] });

                return Ok(new { Grapheme = grapheme, Phonemes = phonemes });
            } catch (System.Exception ex) {
                return StatusCode(500, ex.Message);
            }
        }
}
}
