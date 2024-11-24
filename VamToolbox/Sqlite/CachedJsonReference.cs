using System.Text.Json.Serialization;

namespace VamToolbox.Sqlite;

public sealed class CachedJsonReference
{
    [JsonPropertyName("v")]
    public string Value { get; set; } = null!;
    [JsonPropertyName("m")]
    public string? MorphName { get; set; }
    [JsonPropertyName("i")]
    public string? InternalId { get; set; }
    [JsonPropertyName("x")]
    public int Index { get; set; }
    [JsonPropertyName("l")]
    public int Length { get; set; }
}