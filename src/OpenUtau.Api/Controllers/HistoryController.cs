using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;
using OpenUtau.Core.Format;
using System.Reflection;
using System.Linq;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HistoryController : ControllerBase
    {
        [HttpPost("undo")]
        public IActionResult Undo()
        {
            DocManager.Inst.Undo();
            return Ok(new { message = "Undo successful" });
        }

        [HttpPost("redo")]
        public IActionResult Redo()
        {
            DocManager.Inst.Redo();
            return Ok(new { message = "Redo successful" });
        }

        [HttpGet("state")]
        public IActionResult GetHistoryState()
        {
            try
            {
                var docManager = DocManager.Inst;
                var undoQueueField = docManager.GetType().GetField("undoQueue", BindingFlags.Instance | BindingFlags.NonPublic);
                var redoQueueField = docManager.GetType().GetField("redoQueue", BindingFlags.Instance | BindingFlags.NonPublic);

                var undoQueue = undoQueueField?.GetValue(docManager) as System.Collections.IEnumerable;
                var redoQueue = redoQueueField?.GetValue(docManager) as System.Collections.IEnumerable;

                var undoList = undoQueue?.Cast<object>().Select(cmd => {
                    var nameKey = cmd.GetType().GetProperty("NameKey")?.GetValue(cmd) as string;
                    return nameKey ?? "Unknown";
                }).ToList() ?? new System.Collections.Generic.List<string>();

                var redoList = redoQueue?.Cast<object>().Select(cmd => {
                    var nameKey = cmd.GetType().GetProperty("NameKey")?.GetValue(cmd) as string;
                    return nameKey ?? "Unknown";
                }).ToList() ?? new System.Collections.Generic.List<string>();

                return Ok(new
                {
                    UndoStack = undoList,
                    RedoStack = redoList,
                    CanUndo = undoList.Count > 0,
                    CanRedo = redoList.Count > 0
                });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
