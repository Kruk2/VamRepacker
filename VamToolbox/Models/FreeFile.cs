using VamToolbox.Helpers;

namespace VamToolbox.Models;

public sealed class FreeFile : FileReferenceBase, IVamObjectWithDependencies
{
    public string FullPath { get; }
    public string? SourcePathIfSoftLink { get; }
    public DateTime ModifiedTimestamp { get; }

    private readonly List<FreeFile> _children = [];
    public override IReadOnlyCollection<FreeFile> Children => _children.AsReadOnly();

    private List<VarPackage>? _trimmedResolvedVarDependencies;
    private List<FreeFile>? _trimmedResolvedFreeDependencies;
    public IReadOnlyList<VarPackage> ResolvedVarDependencies => CalculateDeps().Var;
    public IReadOnlyList<FreeFile> ResolvedFreeDependencies => CalculateDeps().Free;
    public bool AlreadyCalculatedDeps => _trimmedResolvedVarDependencies is not null;
    public IEnumerable<string> UnresolvedDependencies => JsonFile?.Missing.Select(x => x.EstimatedReferenceLocation + " from " + this) ?? [];

    public FreeFile(string path, string localPath, long size, bool isInVamDir, DateTime modifiedTimestamp, string? softLinkPath)
        : base(localPath, size, isInVamDir)
    {
        FullPath = path.NormalizePathSeparators();
        SourcePathIfSoftLink = softLinkPath?.NormalizePathSeparators() ?? null;
        ModifiedTimestamp = modifiedTimestamp;
    }

    public override IEnumerable<FreeFile> SelfAndChildren() => base.SelfAndChildren().Cast<FreeFile>();

    public override string ToString() => FullPath;

    public override void AddChildren(FileReferenceBase children) => _children.Add((FreeFile)children);

    private (List<VarPackage> Var, List<FreeFile> Free) CalculateDeps()
    {
        if (_trimmedResolvedFreeDependencies is not null && _trimmedResolvedVarDependencies is not null)
            return (_trimmedResolvedVarDependencies, _trimmedResolvedFreeDependencies);
        if (JsonFile is null)
            return (_trimmedResolvedVarDependencies, _trimmedResolvedFreeDependencies) = (Enumerable.Empty<VarPackage>().ToList(), Enumerable.Empty<FreeFile>().ToList());

        return (_trimmedResolvedVarDependencies, _trimmedResolvedFreeDependencies) = DependencyCalculator.CalculateTrimmedDeps([JsonFile]);
    }

    public void ClearDependencies()
    {
        _trimmedResolvedFreeDependencies = null;
        _trimmedResolvedVarDependencies = null;
    }
}