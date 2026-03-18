using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Format;
using OpenUtau.Core.Ustx;
using OpenUtau.Classic;
using System.IO.Compression;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FormatController : ControllerBase
    {
        [HttpPost("import")]
        public IActionResult ImportProject(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("Missing file");

            var tempFile = Path.Combine(Path.GetTempPath(), file.FileName);
            try
            {
                using (var stream = new FileStream(tempFile, FileMode.Create))
                {
                    file.CopyTo(stream);
                }

                var project = Formats.ReadProject(new string[] { tempFile });
                if (project == null) return BadRequest("Failed to import format.");

                // Convert to YAML/JSON string (using USTx save logic we can just serialize it or save to temp and read)
                var outTemp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".ustx");
                Ustx.Save(outTemp, project);
                var content = System.IO.File.ReadAllText(outTemp);
                System.IO.File.Delete(outTemp);

                return Content(content, "text/plain");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
            finally
            {
                if (System.IO.File.Exists(tempFile)) System.IO.File.Delete(tempFile);
            }
        }

        private UProject? LoadProjectFromRequest(IFormFile file)
        {
            var tempFile = Path.Combine(Path.GetTempPath(), file.FileName);
            using (var stream = new FileStream(tempFile, FileMode.Create))
            {
                file.CopyTo(stream);
            }
            try {
                return Formats.ReadProject(new string[] { tempFile });
            } finally {
                if (System.IO.File.Exists(tempFile)) System.IO.File.Delete(tempFile);
            }
        }

        [HttpPost("export/midi")]
        public IActionResult ExportMidi(IFormFile file)
        {
            try
            {
                var project = LoadProjectFromRequest(file);
                if (project == null) return BadRequest("Invalid project");

                var outTemp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".mid");
                MidiWriter.Save(outTemp, project);

                var bytes = System.IO.File.ReadAllBytes(outTemp);
                System.IO.File.Delete(outTemp);

                return File(bytes, "audio/midi", "export.mid");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("export/ust/{partNo}")]
        public IActionResult ExportUst(IFormFile file, int partNo)
        {
            try
            {
                var project = LoadProjectFromRequest(file);
                if (project == null) return BadRequest("Invalid project");
                if (partNo < 0 || partNo >= project.parts.Count) return BadRequest("Invalid partNo");
                var part = project.parts[partNo] as UVoicePart;
                if (part == null) return BadRequest("Part is not voice part");

                var outTemp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".ust");
                Ust.SavePart(project, part, outTemp);

                var bytes = System.IO.File.ReadAllBytes(outTemp);
                System.IO.File.Delete(outTemp);

                return File(bytes, "text/plain", $"part_{partNo}.ust");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("export/ds/{partNo}")]
        public IActionResult ExportDiffSinger(IFormFile file, int partNo)
        {
            try
            {
                var project = LoadProjectFromRequest(file);
                if (project == null) return BadRequest("Invalid project");
                if (partNo < 0 || partNo >= project.parts.Count) return BadRequest("Invalid partNo");
                var part = project.parts[partNo] as UVoicePart;
                if (part == null) return BadRequest("Part is not voice part");

                var outTemp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".csv");
                OpenUtau.Core.DiffSinger.DiffSingerScript.SavePart(project, part, outTemp, true, true);

                var bytes = System.IO.File.ReadAllBytes(outTemp);
                System.IO.File.Delete(outTemp);

                return File(bytes, "text/csv", $"part_{partNo}_ds.csv");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("voicebank/{singerId}/oto")]
        public IActionResult ExportOto(string singerId)
        {
            try
            {
                if (!SingerManager.Inst.Singers.TryGetValue(singerId, out var singer))
                {
                    return NotFound(new { error = "Singer not found" });
                }

                if (singer.SingerType != USingerType.Classic && singer.SingerType != USingerType.Enunu)
                {
                     // Return empty or notify that only classic has traditional OTO
                }

                // Voicebank class holds the OtoSets
                var baseSinger = singer as OpenUtau.Core.Ustx.USinger;
                // Since USinger doesn't expose OtoSets directly, we have to look at the filesystem or parse.
                // However, singer.Location provides the root folder.
                var loc = singer.Location;
                var iniFiles = Directory.GetFiles(loc, "oto.ini", SearchOption.AllDirectories);

                if (iniFiles.Length == 0)
                    return NotFound("No oto.ini found in singer folder");

                if (iniFiles.Length == 1)
                {
                    var bytes = System.IO.File.ReadAllBytes(iniFiles[0]);
                    return File(bytes, "text/plain", "oto.ini");
                }
                
                // Return a ZIP if multiple
                var zipTemp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".zip");
                using (var archive = ZipFile.Open(zipTemp, ZipArchiveMode.Create))
                {
                    foreach(var ini in iniFiles)
                    {
                        var entryName = Path.GetRelativePath(loc, ini);
                        archive.CreateEntryFromFile(ini, entryName);
                    }
                }
                var zipBytes = System.IO.File.ReadAllBytes(zipTemp);
                System.IO.File.Delete(zipTemp);
                return File(zipBytes, "application/zip", $"{singerId}_otos.zip");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
