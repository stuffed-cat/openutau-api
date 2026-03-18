using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core.Util;
using OpenUtau.Core.Ustx;
using System;

namespace OpenUtau.Api.Controllers {

    public class NoteDefaultsRequest {
        public string? DefaultLyric { get; set; }
        public string? SplittedLyric { get; set; }
        public int? CurrentPortamentoLength { get; set; }
        public int? CurrentPortamentoStart { get; set; }
        public int? CurrentPitchShape { get; set; }
        public float? CurrentVibratoLength { get; set; }
        public float? CurrentVibratoPeriod { get; set; }
        public float? CurrentVibratoDepth { get; set; }
        public float? CurrentVibratoIn { get; set; }
        public float? CurrentVibratoOut { get; set; }
        public float? CurrentVibratoShift { get; set; }
        public float? CurrentVibratoDrift { get; set; }
        public float? CurrentVibratoVolLink { get; set; }
        public float? AutoVibratoNoteLength { get; set; }
        public bool? AutoVibratoToggle { get; set; }
    }

    [ApiController]
    [Route("api/notes")]
    public class NoteDefaultsController : ControllerBase {
        
        [HttpPost("setdefaults")]
        public IActionResult SetDefaults([FromBody] NoteDefaultsRequest request) {
            bool modified = false;

            if (request.DefaultLyric != null) {
                NotePresets.Default.DefaultLyric = request.DefaultLyric;
                modified = true;
            }
            if (request.SplittedLyric != null) {
                NotePresets.Default.SplittedLyric = request.SplittedLyric;
                modified = true;
            }
            if (request.CurrentPortamentoLength.HasValue) {
                NotePresets.Default.DefaultPortamento.PortamentoLength = request.CurrentPortamentoLength.Value;
                modified = true;
            }
            if (request.CurrentPortamentoStart.HasValue) {
                NotePresets.Default.DefaultPortamento.PortamentoStart = request.CurrentPortamentoStart.Value;
                modified = true;
            }
            if (request.CurrentPitchShape.HasValue) {
                if (Enum.IsDefined(typeof(PitchPointShape), request.CurrentPitchShape.Value)) {
                    NotePresets.Default.DefaultPitchShape = (PitchPointShape)request.CurrentPitchShape.Value;
                    modified = true;
                }
            }
            if (request.CurrentVibratoLength.HasValue) {
                NotePresets.Default.DefaultVibrato.VibratoLength = Math.Max(0, Math.Min(100, request.CurrentVibratoLength.Value));
                modified = true;
            }
            if (request.CurrentVibratoPeriod.HasValue) {
                NotePresets.Default.DefaultVibrato.VibratoPeriod = Math.Max(5, Math.Min(500, request.CurrentVibratoPeriod.Value));
                modified = true;
            }
            if (request.CurrentVibratoDepth.HasValue) {
                NotePresets.Default.DefaultVibrato.VibratoDepth = Math.Max(5, Math.Min(200, request.CurrentVibratoDepth.Value));
                modified = true;
            }
            if (request.CurrentVibratoIn.HasValue) {
                NotePresets.Default.DefaultVibrato.VibratoIn = Math.Max(0, Math.Min(100, request.CurrentVibratoIn.Value));
                modified = true;
            }
            if (request.CurrentVibratoOut.HasValue) {
                float vIn = request.CurrentVibratoIn.HasValue ? request.CurrentVibratoIn.Value : NotePresets.Default.DefaultVibrato.VibratoIn;
                NotePresets.Default.DefaultVibrato.VibratoOut = Math.Max(0, Math.Min(100 - vIn, request.CurrentVibratoOut.Value));
                modified = true;
            }
            if (request.CurrentVibratoShift.HasValue) {
                NotePresets.Default.DefaultVibrato.VibratoShift = Math.Max(0, Math.Min(100, request.CurrentVibratoShift.Value));
                modified = true;
            }
            if (request.CurrentVibratoDrift.HasValue) {
                NotePresets.Default.DefaultVibrato.VibratoDrift = Math.Max(-100, Math.Min(100, request.CurrentVibratoDrift.Value));
                modified = true;
            }
            if (request.CurrentVibratoVolLink.HasValue) {
                NotePresets.Default.DefaultVibrato.VibratoVolLink = Math.Max(-100, Math.Min(100, request.CurrentVibratoVolLink.Value));
                modified = true;
            }
            if (request.AutoVibratoNoteLength.HasValue) {
                NotePresets.Default.AutoVibratoNoteDuration = (int)Math.Max(10, request.AutoVibratoNoteLength.Value);
                modified = true;
            }
            if (request.AutoVibratoToggle.HasValue) {
                NotePresets.Default.AutoVibratoToggle = request.AutoVibratoToggle.Value;
                modified = true;
            }
            
            if (modified) {
                NotePresets.Save();
            }

            return Ok(new { message = "Note defaults updated successfully.", defaults = NotePresets.Default });
        }
    }
}
