using VamToolbox.Helpers;

namespace VamToolbox.Models;

public sealed class FreeFile : FileReferenceBase, IVamObjectWithDependencies
{
    public string FullPath { get; }
    public string? SourcePathIfSoftLink { get; }

    private readonly List<FreeFile> _children = new();
    public override IReadOnlyCollection<FreeFile> Children => _children.AsReadOnly();

    private List<VarPackage>? _trimmedResolvedVarDependencies;
    private List<FreeFile>? _trimmedResolvedFreeDependencies;
    public IReadOnlyList<VarPackage> ResolvedVarDependencies => CalculateDeps().Var;
    public IReadOnlyList<FreeFile> ResolvedFreeDependencies => CalculateDeps().Free;
    public bool AlreadyCalculatedDeps => _trimmedResolvedVarDependencies is not null;
    public IEnumerable<string> UnresolvedDependencies => JsonFile?.Missing.Select(x => x.EstimatedReferenceLocation + " from " + this) ?? Enumerable.Empty<string>();

    public FreeFile(string path, string localPath, long size, bool isInVamDir, DateTime modifiedTimestamp, string? softLinkPath)
        : base(localPath, size, isInVamDir, modifiedTimestamp)
    {
        FullPath = path.NormalizePathSeparators();
        SourcePathIfSoftLink = softLinkPath?.NormalizePathSeparators() ?? null;
    }

    public IEnumerable<FreeFile> SelfAndChildren() => Children.SelectMany(t => t.SelfAndChildren()).Append(this).Distinct();

    public override string ToString() => FullPath;

    public override void AddChildren(FileReferenceBase children)
    {
        _children.Add((FreeFile)children);
    }

    private (List<VarPackage> Var, List<FreeFile> Free) CalculateDeps()
    {
        if (_trimmedResolvedFreeDependencies is not null && _trimmedResolvedVarDependencies is not null)
            return (_trimmedResolvedVarDependencies, _trimmedResolvedFreeDependencies);
        if (JsonFile is null)
            return (_trimmedResolvedVarDependencies, _trimmedResolvedFreeDependencies) = (Enumerable.Empty<VarPackage>().ToList(), Enumerable.Empty<FreeFile>().ToList());

        return (_trimmedResolvedVarDependencies, _trimmedResolvedFreeDependencies) = DependencyCalculator.CalculateTrimmedDeps(new List<JsonFile> { JsonFile });
    }

    public void ClearDependencies()
    {
        _trimmedResolvedFreeDependencies = null;
        _trimmedResolvedVarDependencies = null;
    }
}