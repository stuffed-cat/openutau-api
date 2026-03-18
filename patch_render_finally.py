with open("src/OpenUtau.Api/Controllers/RenderController.cs", "r") as f:
    text = f.read()

# Replace part 1
old_part1 = """                // Wait for the render to complete
                var renderResult = await Task.Run(() => engine.RenderMixdown(DocManager.Inst.MainScheduler, ref tokenSource, true));
                var mix = renderResult.Item1;"""
new_part1 = """                // Wait for the render to complete
                Tuple<List<float>, List<float>> renderResult;
                try {
                    renderResult = await Task.Run(() => engine.RenderMixdown(DocManager.Inst.MainScheduler, ref tokenSource, true));
                } finally {
                    _activeRenders.TryRemove(taskId, out _);
                    tokenSource.Dispose();
                }
                var mix = renderResult.Item1;"""
text = text.replace(old_part1, new_part1)

with open("src/OpenUtau.Api/Controllers/RenderController.cs", "w") as f:
    f.write(text)

