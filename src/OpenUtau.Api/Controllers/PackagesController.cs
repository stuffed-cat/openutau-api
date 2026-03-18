using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PackagesController : ControllerBase
    {
        [HttpPost("install")]
        public async Task<IActionResult> InstallPackage(IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file uploaded" });

            var ext = Path.GetExtension(file.FileName).ToLower();

            try
            {
                var tempPath = Path.Combine(Path.GetTempPath(), file.FileName);
                using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                if (ext == PackageManager.OudepExt)
                {
                    await PackageManager.Inst.InstallFromFileAsync(tempPath);
                }
                else if (ext == ".vogeon")
                {
                    OpenUtau.Core.Vogen.VogenSingerInstaller.Install(tempPath);
                }
                else if (ext == ".dll")
                {
                    OpenUtau.Core.Api.PhonemizerInstaller.Install(tempPath);
                }
                else if (ext == ".zip" || ext == ".rar" || ext == ".uar")
                {
                    var basePath = PathManager.Inst.SingersInstallPath;
                    var installer = new OpenUtau.Classic.VoicebankInstaller(basePath, (progress, info) => { }, System.Text.Encoding.GetEncoding("shift_jis"), System.Text.Encoding.GetEncoding("shift_jis"));
                    installer.Install(tempPath, "utau");
                    SingerManager.Inst.SearchAllSingers();
                }
                else
                {
                    if (System.IO.File.Exists(tempPath)) {
                        System.IO.File.Delete(tempPath);
                    }
                    return BadRequest(new { error = $"Unsupported package extension: {ext}" });
                }

                if (System.IO.File.Exists(tempPath)) {
                    System.IO.File.Delete(tempPath);
                }

                return Ok(new { message = "Package installed successfully" });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
