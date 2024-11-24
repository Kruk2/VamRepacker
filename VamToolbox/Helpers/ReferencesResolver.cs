using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.IO.Abstractions;
using MoreLinq;
using VamToolbox.Models;

namespace VamToolbox.Helpers;

public interface IReferencesResolver
{
    JsonReference? ScanPackageSceneReference(PotentialJsonFile potentialJson, Reference reference, VarPackage? varToSearch, string localSceneFolder);
    JsonReference? ScanFreeFileSceneReference(string localSceneFolder, Reference reference);
    Task InitLookups(IList<FreeFile> freeFiles, IList<VarPackage> varFiles, ConcurrentBag<string> errors);
}

public class ReferencesResolver : IReferencesResolver
{
    private readonly IFileSystem _fs;
    private FrozenDictionary<string, ImmutableList<FreeFile>> _freeFilesIndex = null!;
    private FrozenDictionary<string, ImmutableList<VarPackage>> _varFilesIndex = null!;
    private ConcurrentBag<string> _errors = null!;

    public ReferencesResolver(IFileSystem fs) => _fs = fs;

    public Task InitLookups(IList<FreeFile> freeFiles, IList<VarPackage> varFiles, ConcurrentBag<string> errors) => Task.Run(() => InitLookupsSync(freeFiles, varFiles, errors));

    private void InitLookupsSync(IList<FreeFile> freeFiles, IList<VarPackage> varFiles, ConcurrentBag<string> errors)
    {
        _freeFilesIndex = freeFiles
            .GroupBy(f => f.LocalPath, f => f, StringComparer.InvariantCultureIgnoreCase)
            .ToFrozenDictionary(t => t.Key, t => t.ToImmutableList());
        _varFilesIndex = varFiles
            .GroupBy(t => t.Name.PackageNameWithoutVersion, StringComparer.InvariantCultureIgnoreCase)
            .ToFrozenDictionary(t => t.Key, t => t.ToImmutableList());
        _errors = errors;
    }

    public JsonReference? ScanFreeFileSceneReference(string localSceneFolder, Reference reference)
    {
        var refPath = reference.EstimatedReferenceLocation;
        // searching in localSceneFolder for var json files is handled in ScanPackageSceneReference
        if (!reference.ForJsonFile.IsVar && _freeFilesIndex.TryGetValue(_fs.SimplifyRelativePath(localSceneFolder, refPath), out var f1) && f1.Count > 0) {
            var matches = f1.OrderByDescending(t => t.UsedByVarPackagesOrFreeFilesCount).ThenBy(t => t.FullPath);
            var x = matches.FirstOrDefault(t => t.IsInVaMDir) ?? matches.First();
            return new JsonReference(x, reference);
        }
        if (_freeFilesIndex.TryGetValue(refPath, out var f2) && f2.Count > 0) {
            var matches = f2.OrderByDescending(t => t.UsedByVarPackagesOrFreeFilesCount).ThenBy(t => t.FullPath);
            var x = matches.FirstOrDefault(t => t.IsInVaMDir) ?? matches.First();
            return new JsonReference(x, reference);
        }

        return default;
    }

    public JsonReference? ScanPackageSceneReference(PotentialJsonFile potentialJson, Reference reference, VarPackage? varToSearch, string localSceneFolder)
    {
        if (varToSearch is null) {
            var varFile = reference.EstimatedVarName;
            if (varFile is null) {
                _errors.Add($"[ASSET-PARSE-ERROR] {reference.Value} was neither a SELF reference or VAR in {potentialJson}");
                return default;
            }

            varToSearch = FindVar(varFile);
        }

        if (varToSearch != null) {
            var varAssets = varToSearch.FilesDict;
            var assetName = reference.EstimatedReferenceLocation;

            if (potentialJson.Var == varToSearch) {
                var refInScene = _fs.SimplifyRelativePath(localSceneFolder, assetName);
                if (varAssets.TryGetValue(refInScene, out var f1)) {
                    //_logger.Log($"[RESOLVER] Found f1 {f1.ToParentVar.Name.Filename} for reference {refer}")}");ence.Value} from {(potentialJson.IsVar ? $"var: {potentialJson.Var.Name.Filename}" : $"file: {potentialJson.Free.FullPath
                    return new JsonReference(f1, reference);
                }
            }

            if (varAssets.TryGetValue(assetName, out var f2)) {
                //_logger.Log($"[RESOLVER] Found f2 {f2.ToParentVar.Name.Filename} for reference {reference.Value} from {(potentialJson.IsVar ? $"var: {potentialJson.Var.Name.Filename}" : $"file: {potentialJson.Free.FullPath}")}");
                return new JsonReference(f2, reference);
            }
        }

        return null;
    }

    private VarPackage? FindVar(VarPackageName varFile)
    {
        if (!_varFilesIndex.TryGetValue(varFile.PackageNameWithoutVersion, out var possibleVarsToSearchList)) {
            return null;
        }

        IEnumerable<VarPackage> possibleVarsToSearch = possibleVarsToSearchList;

        if (varFile.MinVersion)
        {
            possibleVarsToSearch = _varFilesIndex[varFile.PackageNameWithoutVersion].Where(t => t.Name.Version >= varFile.Version);
        }
        else if (varFile.Version != -1)
        {
            possibleVarsToSearch = _varFilesIndex[varFile.PackageNameWithoutVersion].Where(t => t.Name.Version == varFile.Version);
        }

        // VAM will use latest available version when exact match was not found
        return possibleVarsToSearch.Maxima(t => t.Name.Version).MinBy(t => t.FullPath.Length) ??
                      _varFilesIndex[varFile.PackageNameWithoutVersion].Maxima(t => t.Name.Version).MinBy(t => t.FullPath.Length);
    }
}