using System.Collections.Generic;

namespace OpenUtau.Api.Models
{
    public class ProjectCreateRequest
    {
        public string Name { get; set; } = "New Project";
        public List<TrackRequest> Tracks { get; set; } = new List<TrackRequest>();
    }

    public class TrackRequest
    {
        public string? SingerId { get; set; }
        public string? Phonemizer { get; set; }
        public string? Renderer { get; set; }
        public List<NoteRequest> Notes { get; set; } = new List<NoteRequest>();
    }

    public class NoteRequest
    {
        public string? Lyric { get; set; }
        public int Tone { get; set; }
        public int Position { get; set; } // position in ticks
        public int Duration { get; set; } // duration in ticks
    }
}
