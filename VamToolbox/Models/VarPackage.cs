using System.Collections.Frozen;
using VamToolbox.Helpers;

namespace VamToolbox.Models;

public sealed class VarPackage : IVamObjectWithDependencies
{
    public VarPackageName Name { get; }
    public long Size { get; }
    public string FullPath { get; }
    public string? SourcePathIfSoftLink { get; }
    public bool IsInVaMDir { get; }
    public DateTime Modified { get; }

    private readonly List<VarPackageFile> _files = [];
    public IReadOnlyList<VarPackageFile> Files => _files;

    private AssetType? _assetType;
    public AssetType Type => _assetType ??= Files
        .SelfAndChildren()
        .Aggregate(AssetType.None, (a, b) => a | b.Type);

    public bool IsMorphPack => (Type & ~AssetType.Morph) == AssetType.None;

    private List<JsonFile>? _jsonFiles;
    public IReadOnlyList<JsonFile> JsonFiles => _jsonFiles ??= Files
        .SelfAndChildren()
        .Where(t => t.JsonFile != null)
        .Select(t => t.JsonFile!)
        .ToList();

    private List<VarPackage>? _trimmedResolvedVarDependencies;
    private List<FreeFile>? _trimmedResolvedFreeDependencies;
    public IReadOnlyList<VarPackage> ResolvedVarDependencies => CalculateDeps().Var;
    public IReadOnlyList<FreeFile> ResolvedFreeDependencies => CalculateDeps().Free;
    public bool AlreadyCalculatedDeps => _trimmedResolvedVarDependencies is not null;

    public IEnumerable<string> UnresolvedDependencies => JsonFiles
        .SelectMany(t => t.Missing.Select(x => x.EstimatedReferenceLocation + " from " + t))
        .Distinct();

    private FrozenDictionary<string, VarPackageFile>? _filesDict;

    public FrozenDictionary<string, VarPackageFile> FilesDict => _filesDict ??= Files
        .SelfAndChildren()
        .GroupBy(t => t.LocalPath, StringComparer.InvariantCultureIgnoreCase)
        .ToFrozenDictionary(t => t.Key, t => t.First(), StringComparer.InvariantCultureIgnoreCase);

    public bool IsInvalid { get; set; }

    public VarPackage(
        VarPackageName name,
        string fullPath,
        string? softLinkPath,
        bool isInVamDir,
        long size,
        DateTime modified)
    {
        Name = name;
        FullPath = fullPath.NormalizePathSeparators();
        IsInVaMDir = isInVamDir;
        Size = size;
        SourcePathIfSoftLink = softLinkPath?.NormalizePathSeparators() ?? null;
        Modified = modified;
    }

    internal void AddVarFile(VarPackageFile varFile) => _files.Add(varFile);

    public override string ToString() => FullPath;

    private (List<VarPackage> Var, List<FreeFile> Free) CalculateDeps()
    {
        if (_trimmedResolvedFreeDependencies is not null && _trimmedResolvedVarDependencies is not null)
            return (_trimmedResolvedVarDependencies, _trimmedResolvedFreeDependencies);
        return (_trimmedResolvedVarDependencies, _trimmedResolvedFreeDependencies) = DependencyCalculator.CalculateTrimmedDeps(JsonFiles);
    }

    public void ClearDependencies()
    {
        _trimmedResolvedFreeDependencies = null;
        _trimmedResolvedVarDependencies = null;
    }
}