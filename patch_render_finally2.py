with open("src/OpenUtau.Api/Controllers/RenderController.cs", "r") as f:
    text = f.read()

# Fix part 1 definition
text = text.replace("Tuple<List<float>, List<float>> renderResult;", "Tuple<OpenUtau.Core.SignalChain.WaveMix, System.Collections.Generic.List<OpenUtau.Core.SignalChain.Fader>> renderResult;")

# Patch RenderMixdown as well
mixdown_search = """                var engine = new RenderEngine(project);
                var tokenSource = new CancellationTokenSource();"""

mixdown_replacement = """                var engine = new RenderEngine(project);
                var tokenSource = new CancellationTokenSource();
                var taskId = Guid.NewGuid();
                _activeRenders.TryAdd(taskId, tokenSource);"""

text = text.replace(mixdown_search, mixdown_replacement)

mix_search2 = """                var renderResult = await Task.Run(() => engine.RenderMixdown(DocManager.Inst.MainScheduler, ref tokenSource, true));
                var mix = renderResult.Item1;"""
mix_repl2 = """                Tuple<OpenUtau.Core.SignalChain.WaveMix, System.Collections.Generic.List<OpenUtau.Core.SignalChain.Fader>> renderResult;
                try {
                    renderResult = await Task.Run(() => engine.RenderMixdown(DocManager.Inst.MainScheduler, ref tokenSource, true));
                } finally {
                    _activeRenders.TryRemove(taskId, out _);
                    tokenSource.Dispose();
                }
                var mix = renderResult.Item1;"""

text = text.replace(mix_search2, mix_repl2)

with open("src/OpenUtau.Api/Controllers/RenderController.cs", "w") as f:
    f.write(text)

