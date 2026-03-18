using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using OpenUtau.Core;
using OpenUtau.Core.Voicevox;

namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/voicevox")]
    public class VoicevoxController : ControllerBase
    {
        [HttpGet("speakers")]
        public IActionResult GetSpeakers()
        {
            try
            {
                var response = VoicevoxClient.Inst.SendRequest(new VoicevoxURL() { method = "GET", path = "/singers" });
                var jObj = JObject.Parse(response.Item1);
                
                if (jObj.ContainsKey("detail"))
                {
                    return StatusCode(500, new { error = "Voicevox engine returned an error", detail = jObj["detail"]?.ToString() });
                }

                if (jObj.ContainsKey("json"))
                {
                    // The engine might return the structure wrapped in a 'json' property depending on the version
                    return Ok(jObj["json"]);
                }

                return Ok(jObj); // Return the raw list of singers
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to communicate with Voicevox engine", details = ex.Message });
            }
        }

        [HttpGet("speakers/{speakerUuid}")]
        public IActionResult GetSpeakerInfo(string speakerUuid)
        {
            try
            {
                var queryurl = new VoicevoxURL() 
                { 
                    method = "GET", 
                    path = "/singer_info", 
                    query = new Dictionary<string, string> { { "speaker_uuid", speakerUuid } } 
                };
                
                var response = VoicevoxClient.Inst.SendRequest(queryurl);
                var jObj = JObject.Parse(response.Item1);

                if (jObj.ContainsKey("detail"))
                {
                    return NotFound(new { error = "Speaker not found or error occurred", detail = jObj["detail"]?.ToString() });
                }

                return Ok(jObj);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to communicate with Voicevox engine", details = ex.Message });
            }
        }

        [HttpGet("models")]
        public IActionResult GetModels()
        {
            try
            {
                var response = VoicevoxClient.Inst.SendRequest(new VoicevoxURL() { method = "GET", path = "/engine_manifest" });
                var jObj = JObject.Parse(response.Item1);

                if (jObj.ContainsKey("detail"))
                {
                    return StatusCode(500, new { error = "Voicevox engine returned an error", detail = jObj["detail"]?.ToString() });
                }

                return Ok(jObj);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to communicate with Voicevox engine", details = ex.Message });
            }
        }

        public class VoicevoxTestRequest
        {
            public string Text { get; set; } = "こんにちは";
            public int SpeakerId { get; set; } = 0;
        }

        [HttpPost("test")]
        public IActionResult TestRender([FromBody] VoicevoxTestRequest request)
        {
            try
            {
                // Note: Actual rendering requires generating audio queries and then synthesis.
                // This is a minimal ping test to the API.
                var queryUrl = new VoicevoxURL() 
                { 
                    method = "POST", 
                    path = "/audio_query", 
                    query = new Dictionary<string, string> 
                    { 
                        { "text", request.Text },
                        { "speaker", request.SpeakerId.ToString() }
                    } 
                };

                var queryResponse = VoicevoxClient.Inst.SendRequest(queryUrl);
                if (string.IsNullOrEmpty(queryResponse.Item1)) {
                    return StatusCode(500, new { error = "Failed to generate audio query" });
                }

                var synthUrl = new VoicevoxURL()
                {
                    method = "POST",
                    path = "/synthesis",
                    query = new Dictionary<string, string> { { "speaker", request.SpeakerId.ToString() } },
                    accept = "audio/wav"
                };

                synthUrl.body = queryResponse.Item1;
                var synthResponse = VoicevoxClient.Inst.SendRequest(synthUrl);

                if (synthResponse.Item2 != null && synthResponse.Item2.Length > 0)
                {
                    return File(synthResponse.Item2, "audio/wav");
                }

                return StatusCode(500, new { error = "Failed to synthesize audio payload" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to communicate with Voicevox engine", details = ex.Message });
            }
        }
    }
}
