using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace VamToolbox.Models;

[ExcludeFromCodeCoverage]
public sealed class MetaFileJson
{
    [JsonProperty("dependencies")]
    public Dictionary<string, Dependency> Dependencies { get; private set; } = [];

}

[ExcludeFromCodeCoverage]
public sealed class Dependency
{
    [JsonProperty("dependencies")]
    public Dictionary<string, Dependency> Dependencies { get; private set; } = [];
}