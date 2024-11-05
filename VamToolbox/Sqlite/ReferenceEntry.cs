using System.Diagnostics.CodeAnalysis;

namespace VamToolbox.Sqlite;

[ExcludeFromCodeCoverage]
public sealed class ReferenceEntry
{
    public string? Value { get; init; }
    public int Index { get; init; }
    public int Length { get; init; }
    public string? MorphName { get; set; }
    public string? InternalId { get; set; }
    public string FileName { get; set; } = null!;
    public DateTime FileModifiedTime { get; set; }
    public long FileSize { get; set; }
    public string LocalPath { get; set; } = null!;
}