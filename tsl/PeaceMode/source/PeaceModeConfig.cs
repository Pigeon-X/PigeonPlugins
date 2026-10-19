using System.Text.Json.Serialization;

namespace PeaceMode;

public sealed class PeaceModeConfig
{
    [JsonPropertyName("启用和平模式")]
    public bool Enabled { get; set; } = true;
}
