with open("src/OpenUtau.Api/Controllers/PartPropertiesExtController.cs", "r") as f:
    text = f.read()

merge_api = """
        /// <summary>
        /// 合并相邻音符 (Merge adjacent note into this note)
        /// </summary>
        [HttpPost("part/{partNo}/note/{noteIndex}/merge")]
        public IActionResult MergeNotes(int partNo, int noteIndex)
        {
            var project = DocManager.Inst.Project;
            if (project == null) return BadRequest(new { error = "Project not loaded" });
            if (partNo < 0 || partNo >= project.parts.Count) return BadRequest(new { error = "Invalid part index" });

            var part = project.parts[partNo];
            if (!(part is UVoicePart voicePart))
                return BadRequest(new { error = "Can only merge notes in voice parts" });

            if (noteIndex < 0 || noteIndex >= voicePart.notes.Count - 1)
                return BadRequest(new { error = "Invalid note index. Cannot merge because there is no next note." });

            var note1 = voicePart.notes.ElementAt(noteIndex);
            var note2 = voicePart.notes.ElementAt(noteIndex + 1);

            DocManager.Inst.StartUndoGroup("Merge notes");

            int newDuration = note2.End - note1.position;

            DocManager.Inst.ExecuteCmd(new RemoveNoteCommand(voicePart, note2));
            DocManager.Inst.ExecuteCmd(new ResizeNoteCommand(voicePart, note1, newDuration - note1.duration));

            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = "Notes merged successfully", newNoteDuration = note1.duration });
        }
"""

# Insert before "public IActionResult MergeParts"
text = text.replace("        [HttpPost(\"parts/merge\")]", merge_api + "\n        [HttpPost(\"parts/merge\")]")

with open("src/OpenUtau.Api/Controllers/PartPropertiesExtController.cs", "w") as f:
    f.write(text)

