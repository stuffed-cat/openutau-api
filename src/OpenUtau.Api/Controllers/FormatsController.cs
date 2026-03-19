using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Api.Utils;
using OpenUtau.Classic;
using OpenUtau.Core.Format;
using OpenUtau.Core.Ustx;
using System.IO;
using System.Threading.Tasks;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FormatsController : ControllerBase
    {
        private static UProject? LoadProjectFromRequest(IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName).ToLower();
            var tempFilePath = Path.GetTempFileName() + ext;

            using (var stream = new FileStream(tempFilePath, FileMode.Create))
            {
                file.CopyTo(stream);
            }

            try
            {
                return Formats.ReadProject(new string[] { tempFilePath });
            }
            finally
            {
                if (System.IO.File.Exists(tempFilePath))
                {
                    System.IO.File.Delete(tempFilePath);
                }
            }
        }

        private static IActionResult ValidateProjectPart(UProject? project, int partNo, out UVoicePart? part)
        {
            part = null;
            if (project == null)
            {
                return new BadRequestObjectResult("Invalid project");
            }
            if (partNo < 0 || partNo >= project.parts.Count)
            {
                return new BadRequestObjectResult("Invalid partNo");
            }
            part = project.parts[partNo] as UVoicePart;
            if (part == null)
            {
                return new BadRequestObjectResult("Part is not voice part");
            }
            return null;
        }

        [HttpPost("convert")]
        public async Task<IActionResult> ConvertToUstx(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file provided");

            var ext = Path.GetExtension(file.FileName).ToLower();
            var tempFilePath = Path.GetTempFileName() + ext;
            
            using (var stream = new FileStream(tempFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            try
            {
                var project = Formats.ReadProject(new string[] { tempFilePath });
                if (project == null)
                    return BadRequest("Failed to read project from the provided file.");

                var outputPath = Path.GetTempFileName() + ".ustx";
                Ustx.Save(outputPath, project);

                var memoryStream = new MemoryStream(await System.IO.File.ReadAllBytesAsync(outputPath));

                System.IO.File.Delete(tempFilePath);
                System.IO.File.Delete(outputPath);

                return File(memoryStream, "application/json", Path.GetFileNameWithoutExtension(file.FileName) + ".ustx");
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("export/ust/{partNo}")]
        public IActionResult ExportUst(IFormFile file, int partNo)
        {
            try
            {
                var project = LoadProjectFromRequest(file);
                var validationError = ValidateProjectPart(project, partNo, out var part);
                if (validationError != null) return validationError;

                var outputPath = Path.GetTempFileName() + ".ust";
                Ust.SavePart(project!, part!, outputPath);

                var bytes = System.IO.File.ReadAllBytes(outputPath);
                System.IO.File.Delete(outputPath);

                return File(bytes, "text/plain", $"part_{partNo}.ust");
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("export/vsqx/{partNo}")]
        public IActionResult ExportVsqx(IFormFile file, int partNo)
        {
            try
            {
                var project = LoadProjectFromRequest(file);
                var validationError = ValidateProjectPart(project, partNo, out var part);
                if (validationError != null) return validationError;

                var outputPath = Path.GetTempFileName() + ".vsqx";
                SimpleExporters.ExportVsqx(project!, part!, outputPath);

                var bytes = System.IO.File.ReadAllBytes(outputPath);
                System.IO.File.Delete(outputPath);

                return File(bytes, "application/xml", $"part_{partNo}.vsqx");
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("export/vpr/{partNo}")]
        public IActionResult ExportVpr(IFormFile file, int partNo)
        {
            try
            {
                var project = LoadProjectFromRequest(file);
                var validationError = ValidateProjectPart(project, partNo, out var part);
                if (validationError != null) return validationError;

                var outputPath = Path.GetTempFileName() + ".vpr";
                SimpleExporters.ExportVpr(project!, part!, outputPath);

                var bytes = System.IO.File.ReadAllBytes(outputPath);
                System.IO.File.Delete(outputPath);

                return File(bytes, "application/zip", $"part_{partNo}.vpr");
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}
