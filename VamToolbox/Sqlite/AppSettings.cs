using System.Diagnostics.CodeAnalysis;
using VamToolbox.Operations.Repo;

namespace VamToolbox.Sqlite;

#pragma warning disable CA2227 // Collection properties should be read only
[ExcludeFromCodeCoverage]
public class AppSettings 
{
    public string? AdditionalVars { get; set; }
    public string? VamDir { get; set; }
    public int Threads { get; set; }
    public bool RemoveSoftLinksBefore { get; set; }
    public List<ProfileModel> Profiles { get; set; } = [];
    public CopyMode? CopyMode { get; set; }
}
#pragma warning restore CA2227 // Collection properties should be read only