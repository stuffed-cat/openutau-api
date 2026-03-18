with open("src/OpenUtau.Api/Controllers/SelectionController.cs", "r") as f:
    text = f.read()

find_replace_api = """
        [HttpPost("notes/lyrics/replace")]
        public IActionResult ReplaceLyrics([FromBody] LyricsReplaceRequest request)
        {
            if (DocManager.Inst.Project == null) return BadRequest("No project loaded");
            var part = SelectionManager.GetActivePart(DocManager.Inst.Project);
            if (part == null) return NotFound("Active part not found");
            
            if (string.IsNullOrEmpty(request.Search)) return BadRequest("Search pattern is required");

            // If ApplyToSelection is true, only search in selected notes, else all notes in the active part
            var notes = request.ApplyToSelection ? SelectionManager.GetSelectedNotes(part) : part.notes.ToList();
            if (notes.Count == 0) return BadRequest("No notes to process");

            int count = 0;
            DocManager.Inst.StartUndoGroup("Replace lyrics");
            
            foreach (var note in notes)
            {
                if (note.lyric != null)
                {
                    string newLyric = note.lyric;
                    
                    if (request.UseRegex) 
                    {
                        try {
                            newLyric = System.Text.RegularExpressions.Regex.Replace(note.lyric, request.Search, request.Replace ?? "");
                        } catch (Exception ex) {
                            DocManager.Inst.EndUndoGroup();
                            return BadRequest($"Regex error: {ex.Message}");
                        }
                    } 
                    else 
                    {
                        newLyric = note.lyric.Replace(request.Search, request.Replace ?? "");
                    }

                    if (newLyric != note.lyric) {
                        DocManager.Inst.ExecuteCmd(new ChangeNoteLyricCommand(part, note, newLyric));
                        count++;
                    }
                }
            }
            
            DocManager.Inst.EndUndoGroup();

            return Ok(new { message = "Lyrics replaced", modifiedCount = count, totalProcessed = notes.Count });
        }
"""

request_class = """
    public class LyricsReplaceRequest
    {
        public string Search { get; set; } = "";
        public string Replace { get; set; } = "";
        public bool UseRegex { get; set; } = false;
        public bool ApplyToSelection { get; set; } = false; // true = selection, false = whole active part
    }
"""

text = text.replace("    public class NotesLyricsRequest", request_class + "\n    public class NotesLyricsRequest")

text = text.replace("        [HttpPost(\"notes/lyrics\")]", find_replace_api + "\n        [HttpPost(\"notes/lyrics\")]")

with open("src/OpenUtau.Api/Controllers/SelectionController.cs", "w") as f:
    f.write(text)

