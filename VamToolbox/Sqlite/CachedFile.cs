using System.Text.Json.Serialization;

namespace VamToolbox.Sqlite;

public sealed class CachedFile
{
    [JsonPropertyName("f")]
    public string FileName { get; set; } = null!;
    [JsonPropertyName("l")]
    public string? LocalPath { get; set; } = null!;
    [JsonPropertyName("s")]
    public long Size { get; set; }
    [JsonPropertyName("m")]
    public DateTime ModifiedTime { get; set; }
    [JsonPropertyName("u")]
    public string? Uuid { get; set; }
    [JsonPropertyName("v")]
    public long? VarLocalFileSize { get; set; }
    [JsonPropertyName("c")]
    public string? CsFiles { get; set; }
    [JsonPropertyName("b")]
    public int IsInvalidVar { get; set; }

    [JsonPropertyName("r")]
    public List<CachedJsonReference>? References { get; set;  }
}