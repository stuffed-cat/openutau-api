using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/project/expressions")]
    public class ProjectExpressionsController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetExpressions()
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project is currently loaded.");

            return Ok(project.expressions.Values.Select(e => new
            {
                e.name,
                e.abbr,
                type = e.type.ToString(),
                e.min,
                e.max,
                e.defaultValue,
                e.isFlag,
                e.flag,
                e.options,
                e.skipOutputIfDefault
            }));
        }

        [HttpPost]
        public IActionResult AddOrUpdateExpression([FromBody] UExpressionDescriptor descriptor)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project is currently loaded.");
            if (descriptor == null || string.IsNullOrEmpty(descriptor.abbr))
                return BadRequest("Invalid expression descriptor.");

            try
            {
                // Prepare new descriptors list
                var oldDescriptors = project.expressions.Values.ToList();
                var newDescriptors = new List<UExpressionDescriptor>();

                bool found = false;
                foreach (var exp in oldDescriptors)
                {
                    if (exp.abbr.ToLower() == descriptor.abbr.ToLower())
                    {
                        newDescriptors.Add(descriptor);
                        found = true;
                    }
                    else
                    {
                        newDescriptors.Add(exp);
                    }
                }

                if (!found)
                {
                    newDescriptors.Add(descriptor);
                }

                DocManager.Inst.ExecuteCmd(new ConfigureExpressionsCommand(project, newDescriptors.ToArray()));
                return Ok(new { message = "Expression added/updated successfully", expression = descriptor });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpDelete("{abbr}")]
        public IActionResult DeleteExpression(string abbr)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest("No project is currently loaded.");
            if (string.IsNullOrEmpty(abbr)) return BadRequest("abbr is required.");

            try
            {
                var removeAbbr = abbr.ToLower();
                var oldDescriptors = project.expressions.Values.ToList();
                var newDescriptors = oldDescriptors.Where(e => e.abbr.ToLower() != removeAbbr).ToArray();

                if (oldDescriptors.Count == newDescriptors.Length)
                {
                    return NotFound(new { message = "Expression not found" });
                }

                DocManager.Inst.ExecuteCmd(new ConfigureExpressionsCommand(project, newDescriptors));
                return Ok(new { message = $"Expression {abbr} deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
