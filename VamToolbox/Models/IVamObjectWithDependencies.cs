namespace VamToolbox.Models;

public interface IVamObjectWithDependencies
{
    List<VarPackage> TrimmedResolvedVarDependencies { get; }
    List<VarPackage> AllResolvedVarDependencies { get; }
    List<FreeFile> TrimmedResolvedFreeDependencies { get; }
    List<FreeFile> AllResolvedFreeDependencies { get; }
    IEnumerable<string> UnresolvedDependencies { get; }
    bool AlreadyCalculatedDeps { get; }

    void ClearDependencies();
}