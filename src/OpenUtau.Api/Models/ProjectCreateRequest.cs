using System.Collections.Generic;

namespace OpenUtau.Api.Models
{
    public class ProjectCreateRequest
    {
        public string Name { get; set; } = "New Project";
        public int BPM { get; set; } = 120;
        public int TimeSignatureNumerator { get; set; } = 4;
        public int TimeSignatureDenominator { get; set; } = 4;
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
        public int Position { get; set; } // in ticks
        public int Duration { get; set; } // in ticks
        public VibratoRequest? Vibrato { get; set; }
        public PitchRequest? Pitch { get; set; }
        public Dictionary<string, float>? Expressions { get; set; }
        public List<PhonemeRequest>? Phonemes { get; set; }
    }

    public class VibratoRequest
    {
        public float Length { get; set; } = 0;
        public float Period { get; set; } = 175;
        public float Depth { get; set; } = 25;
        public float In { get; set; } = 10;
        public float Out { get; set; } = 10;
        public float Shift { get; set; } = 0;
        public float Drift { get; set; } = 0;
    }

    public class PitchRequest
    {
        public List<PitchPointRequest> Data { get; set; } = new List<PitchPointRequest>();
    }

    public class PitchPointRequest
    {
        public float X { get; set; }
        public float Y { get; set; }
    }

    public class PhonemeRequest
    {
        public string Phoneme { get; set; } = "";
        public float Position { get; set; } = 0;
    }
}
