using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Format;
using OpenUtau.Core.Ustx;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DiffSingerController : ControllerBase
    {
        private IActionResult ExecuteEdit(IFormFile file, Action<UProject> modifier)
        {
            if (file == null || file.Length == 0) return BadRequest("No file uploaded");
            try
            {
                var tempFile = Path.GetTempFileName();
                using (var stream = new FileStream(tempFile, FileMode.Create)) { file.CopyTo(stream); }
                var project = Ustx.Load(tempFile);
                if (project == null) {
                    System.IO.File.Delete(tempFile);
                    return BadRequest("Failed to load project.");
                }

                modifier(project);

                var outTemp = Path.GetTempFileName() + ".ustx";
                Ustx.Save(outTemp, project);
                System.IO.File.Delete(tempFile);

                //var streamRet = new FileStream(outTemp, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);
                return PhysicalFile(outTemp, "application/json", "edited.ustx");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("singers")]
        public IActionResult GetSingers()
        {
            var singers = SingerManager.Inst.Singers.Values
                .Where(s => s.SingerType == USingerType.DiffSinger);
            var response = singers.Select(s => {
                bool hasVariancePredictor = System.IO.File.Exists(Path.Join(s.Location, "dsvariance", "dsconfig.yaml"));
                return new {
                    Id = s.Id,
                    Name = s.Name,
                    HasVariancePredictor = hasVariancePredictor,
                    Subbanks = s.Subbanks.Select((b, index) => new {
                        Id = index,
                        Name = b.Color,
                        Suffix = b.Suffix
                    })
                };
            });
            return Ok(response);
        }

        [HttpPost("part/{partNo}/speakerMix")]
        public IActionResult MixSpeaker(IFormFile file, int partNo, [FromForm] string speakerCurvesJson, [FromForm] int pointsCount)
        {
            return ExecuteEdit(file, project =>
            {
                if (partNo < 0 || partNo >= project.parts.Count) throw new Exception("Invalid part index");
                var partBase = project.parts[partNo];
                if (partBase is UVoicePart part)
                {
                    if (string.IsNullOrEmpty(speakerCurvesJson)) return;
                    var speakerCurves = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int[]>>(speakerCurvesJson);
                    if (speakerCurves == null) return;
                    
                    var track = project.tracks[part.trackNo];
                    var singer = track.Singer;
                    if (singer == null || singer.SingerType != USingerType.DiffSinger) throw new Exception("Track is not using a DiffSinger singer");

                    int length = pointsCount;

                    foreach (var kvp in speakerCurves)
                    {
                        string subbankColor = kvp.Key;
                        int subBankId = singer.Subbanks.ToList().FindIndex(sb => sb.Color == subbankColor || sb.Suffix == subbankColor);
                        if (subBankId == -1) continue; // unknown speaker

                        string abbr = "vc" + (subBankId + 1).ToString();
                        
                        var curve = part.curves.FirstOrDefault(c => c.abbr == abbr);
                        if (curve == null)
                        {
                            project.expressions.TryGetValue(abbr, out var descriptor);
                            if (descriptor == null) descriptor = new UExpressionDescriptor(abbr, abbr, 0, 100, 0) { type = UExpressionType.Curve };
                            curve = new UCurve(descriptor);
                            part.curves.Add(curve);
                        }

                        curve.xs = Enumerable.Range(0, kvp.Value.Length).Select(i => i * 10).ToList();
                        curve.ys = kvp.Value.ToList();
                    }
                }
            });
        }

        [HttpPost("part/{partNo}/applyVariance")]
        public IActionResult ApplyVariance(IFormFile file, int partNo, [FromQuery] string varianceType, [FromForm] string curvePointsJson)
        {
            // varianceType can be "ene" (energy), "brec" (breathiness), "tenc" (tension), "voic" (voicing)
            return ExecuteEdit(file, project =>
            {
                if (partNo < 0 || partNo >= project.parts.Count) throw new Exception("Invalid part index");
                var partBase = project.parts[partNo];
                if (partBase is UVoicePart part)
                {
                    string abbr = varianceType;
                    var curvePoints = System.Text.Json.JsonSerializer.Deserialize<int[]>(curvePointsJson);
                    if (curvePoints == null) return;

                    var curve = part.curves.FirstOrDefault(c => c.abbr == abbr);
                    if (curve == null)
                    {
                        project.expressions.TryGetValue(abbr, out var descriptor);
                        if (descriptor == null) descriptor = new UExpressionDescriptor(abbr, abbr, -100, 100, 0) { type = UExpressionType.Curve };
                        curve = new UCurve(descriptor);
                        part.curves.Add(curve);
                    }

                    curve.xs = Enumerable.Range(0, curvePoints.Length).Select(i => i * 10).ToList();
                    curve.ys = curvePoints.ToList();
                }
            });
        }
    }
}
