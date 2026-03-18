with open("src/OpenUtau.Api/Controllers/RenderController.cs", "r") as f:
    text = f.read()

text = text.replace("                var taskId = Guid.NewGuid();\n                _activeRenders.TryAdd(taskId, tokenSource);\n                var taskId = Guid.NewGuid();\n                _activeRenders.TryAdd(taskId, tokenSource);", "                var taskId = Guid.NewGuid();\n                _activeRenders.TryAdd(taskId, tokenSource);")

with open("src/OpenUtau.Api/Controllers/RenderController.cs", "w") as f:
    f.write(text)

