import re

with open("src/OpenUtau.Api/Controllers/RenderController.cs", "r") as f:
    code = f.read()

# Add using System.Collections.Concurrent;
if "using System.Collections.Concurrent;" not in code:
    code = "using System.Collections.Concurrent;\n" + code

# Add _activeRenders and CancelAllRenders
add_code = """
        private static readonly ConcurrentDictionary<Guid, CancellationTokenSource> _activeRenders = new ConcurrentDictionary<Guid, CancellationTokenSource>();

        [HttpPost("cancel")]
        public IActionResult CancelAllRenders()
        {
            int count = 0;
            foreach (var kvp in _activeRenders)
            {
                try {
                    if (!kvp.Value.IsCancellationRequested) {
                        kvp.Value.Cancel();
                        count++;
                    }
                } catch { }
            }
            _activeRenders.Clear();
            return Ok(new { message = $"Cancelled {count} active render tasks." });
        }
"""
code = code.replace("public class RenderController : ControllerBase\n    {", "public class RenderController : ControllerBase\n    {" + add_code)

# Patch RenderPart
part_search = "var tokenSource = new CancellationTokenSource();"
part_replacement = """var tokenSource = new CancellationTokenSource();
                var taskId = Guid.NewGuid();
                _activeRenders.TryAdd(taskId, tokenSource);"""

code = code.replace(part_search, part_replacement)

# We need to wrap the rest of the method logic in a try-finally block unfortunately, but the easiest way is to add it just before returning.
# Wait, let's use Regex to find the return File(...); and add the TryRemove.
# Let's see how RenderPart handles returns:
# return File(streamRet, "audio/" + format, "rendered_part." + format);
# return BadRequest
# To be safe, we can just replace `return File` with `_activeRenders.TryRemove(taskId, out _); return File` but we also need to handle errors.

with open("src/OpenUtau.Api/Controllers/RenderController.cs", "w") as f:
    f.write(code)

