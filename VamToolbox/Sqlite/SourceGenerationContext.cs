using System.Text.Json.Serialization;

namespace VamToolbox.Sqlite;

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(List<CachedFile>))]
[JsonSerializable(typeof(IEnumerable<CachedFile>))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}