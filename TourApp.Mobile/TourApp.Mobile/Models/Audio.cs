using System.Text.Json.Serialization;

namespace TourApp.Mobile.Models
{
    public class Audio
    {
        [JsonPropertyName("id")]
        public int AudioId { get; set; }

        [JsonPropertyName("poiId")]
        public int PoiId { get; set; }

        [JsonPropertyName("language")]
        public string Language { get; set; } = "vi";

        [JsonPropertyName("audioPath")]
        public string AudioPath { get; set; } = string.Empty;

        [JsonPropertyName("scriptText")]
        public string ScriptText { get; set; } = string.Empty;

        [JsonPropertyName("duration")]
        public int Duration { get; set; }

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Kiểm tra audio này là file MP3 thật (không phải TTS text-only).
        /// </summary>
        public bool HasAudioFile => !string.IsNullOrEmpty(AudioPath)
            && AudioPath != "TTS_ONLY"
            && (AudioPath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
                || AudioPath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)
                || AudioPath.StartsWith("http", StringComparison.OrdinalIgnoreCase));
    }
}