using System.Collections.Concurrent;
using VamToolbox.Models;

namespace VamToolbox.Helpers;
public interface IUuidReferenceResolver
{
    (JsonReference? jsonReference, bool isDelayed) MatchVamJsonReferenceById(JsonFile jsonFile, Reference reference, FileReferenceBase? fallBackResolvedAsset);
    (JsonReference? jsonReference, bool isDelayed) MatchMorphJsonReferenceByName(JsonFile jsonFile, Reference reference, FileReferenceBase? fallBackResolvedAsset);
    Task<List<JsonReference>> ResolveDelayedReferences();
    Task InitLookups(IEnumerable<FreeFile> freeFiles, IEnumerable<VarPackage> varFiles);
}

public class UuidReferencesResolver : IUuidReferenceResolver
{
    private ILookup<string, FileReferenceBase> _vamFilesById = null!;
    private ILookup<string, FileReferenceBase> _morphFilesByName = null!;
    private readonly ConcurrentBag<(JsonFile jsonFile, Reference reference, List<FileReferenceBase> matchedAssets, string uuidOrName)> _delayedReferencesToResolve = [];
    private readonly Dictionary<(string uuidOrName, byte femaleOrMale), FileReferenceBase> _cachedDeleyedVam = new(new CustomUuidComparer());
    private readonly Dictionary<(string uuidOrName, byte femaleOrMale), FileReferenceBase> _cachedDeleyedMorphs = new(new CustomUuidComparer());

    private class CustomUuidComparer : IEqualityComparer<(string uuidOrName, byte femaleOrMale)>
    {
        public bool Equals((string uuidOrName, byte femaleOrMale) x, (string uuidOrName, byte femaleOrMale) y)
        {
            return string.Equals(x.uuidOrName, y.uuidOrName, StringComparison.OrdinalIgnoreCase) && x.femaleOrMale == y.femaleOrMale;
        }

        public int GetHashCode((string uuidOrName, byte femaleOrMale) obj)
        {
            var hashCode = new HashCode();
            hashCode.Add(obj.uuidOrName, StringComparer.OrdinalIgnoreCase);
            hashCode.Add(obj.femaleOrMale);
            return hashCode.ToHashCode();
        }
    }


    public async Task InitLookups(IEnumerable<FreeFile> freeFiles, IEnumerable<VarPackage> varFiles)
    {
        await Task.Run(() => InitVamFilesById(freeFiles, varFiles));
        await Task.Run(() => InitMorphNames(freeFiles, varFiles));
    }

    private void InitVamFilesById(IEnumerable<FreeFile> freeFiles, IEnumerable<VarPackage> varFiles)
    {
        var vamFilesFromVars = varFiles
            .SelectMany(t => t.Files.Where(x => x.InternalId != null));

        _vamFilesById = vamFilesFromVars.Cast<FileReferenceBase>()
            .Concat(freeFiles.Where(t => t.InternalId != null && (t.Type & AssetType.ValidClothOrHair) != 0))
            .Where(t => (t.Type & AssetType.ValidClothOrHair) != 0)
            .ToLookup(t => t.InternalId!);
    }

    private void InitMorphNames(IEnumerable<FreeFile> freeFiles, IEnumerable<VarPackage> varFiles)
    {
        var morphFilesFromVars = varFiles
            .SelectMany(t => t.Files.Where(x => x.MorphName != null && (x.Type & AssetType.ValidMorph) != 0));

        _morphFilesByName = freeFiles
            .Where(t => t.MorphName != null && (t.Type & AssetType.ValidMorph) != 0)
            .Cast<FileReferenceBase>()
            .Concat(morphFilesFromVars)
            .ToLookup(t => t.MorphName!);
    }

    public Task<List<JsonReference>> ResolveDelayedReferences() => Task.Run(ResolveDelayedReferencesSync);

    private List<JsonReference> ResolveDelayedReferencesSync()
    {
        if (_delayedReferencesToResolve.IsEmpty)
            return [];

        var createdReferences = new List<JsonReference>(_delayedReferencesToResolve.Count);
        foreach (var (jsonFile, reference, matchedAssets, uuidOrName) in _delayedReferencesToResolve.OrderByDescending(t => t.jsonFile.File.PreferredForDelayedResolver)) {
            var isVam = reference.Value.EndsWith(".vam", StringComparison.OrdinalIgnoreCase);
           
            void AddReference(FileReferenceBase fileToAdd)
            {
                var referenceToAdd = new JsonReference(fileToAdd, reference);
                jsonFile.AddReference(referenceToAdd);
                fileToAdd.PreferredForDelayedResolver |= jsonFile.File.PreferredForDelayedResolver;
                if (fileToAdd.IsVar && jsonFile.File.PreferredForDelayedResolver) {
                    foreach (var varPackageFile in fileToAdd.Var.Files.SelfAndChildren()) {
                        // mark all files in current var as preferred
                        varPackageFile.PreferredForDelayedResolver |= jsonFile.File.PreferredForDelayedResolver;
                    }
                }
            }

            var isFemaleReference = reference.EstimatedAssetType.IsFemale();
            var isMaleReference = reference.EstimatedAssetType.IsMale();
            var femaleOrMaleReference = (byte)(isFemaleReference ? 2 : isMaleReference ? 1 : 0);

            if (isVam && _cachedDeleyedVam.TryGetValue((uuidOrName, femaleOrMaleReference), out var cachedReference)) {
                AddReference(cachedReference);
                continue;
            }

            if (!isVam && _cachedDeleyedMorphs.TryGetValue((uuidOrName, femaleOrMaleReference), out var cachedMorphReference)) {
                AddReference(cachedMorphReference);
                continue;
            }

            // prefer files that will be moved anyway because of the profile
            if (matchedAssets.Any(t => t.PreferredForDelayedResolver)) {
                matchedAssets.RemoveAll(t => !t.PreferredForDelayedResolver);
            }

            // prefer vars/json with least dependencies
            var dependencies = CalculateDependencies(matchedAssets);
            var dependenciesCount = dependencies.Select(t => t.ResolvedVarDependencies.Count + t.ResolvedFreeDependencies.Count);
            var minCount = dependenciesCount.Min();
            var zipped = matchedAssets.Zip(dependenciesCount);

            var bestMatches = zipped.Where(t => t.Second == minCount).Select(t => t.First);
            FileReferenceBase bestMatch;

            if (bestMatches.Take(2).Count() == 1) {
                bestMatch = bestMatches.First();
            } else {
                bestMatch = bestMatches
                    .OrderByDescending(t => t.IsInVaMDir ? 1 : 0)
                    .ThenBy(t => t.Var?.FullPath.Length ?? 0)
                    .ThenByDescending(t => t.UsedByVarPackagesOrFreeFilesCount) // most used
                    .ThenBy(t => t is VarPackageFile varFile ? varFile.ParentVar.Size : ((FreeFile)t).SizeWithChildren) // smallest
                    .ThenByDescending(t => t.IsVar ? t.Var.Name.Version : int.MaxValue)
                    .First();
            }

            AddReference(bestMatch);

            var isFemaleAsset = bestMatch.Type.IsFemale();
            var isMaleAsset = bestMatch.Type.IsMale();
            var femaleOrMale = (byte)(isFemaleAsset ? 2 : isMaleAsset ? 1 : 0);
            if (isVam)
                _cachedDeleyedVam[(uuidOrName, femaleOrMale)] = bestMatch;
            else
                _cachedDeleyedMorphs[(uuidOrName, femaleOrMale)] = bestMatch;
        }

        _delayedReferencesToResolve.Clear();
        return createdReferences;
    }

    private static IVamObjectWithDependencies[] CalculateDependencies(IEnumerable<FileReferenceBase> matchedAssets)
    {
        return matchedAssets
            .Select(t => t is VarPackageFile varFile ? varFile.ParentVar : (IVamObjectWithDependencies)t)
            .ToArray();
    }

    public (JsonReference? jsonReference, bool isDelayed) MatchVamJsonReferenceById(JsonFile jsonFile, Reference reference, FileReferenceBase? fallBackResolvedAsset)
    {
        return MatchAssetByUuidOrName(jsonFile, reference.InternalId, reference, _vamFilesById, fallBackResolvedAsset);
    }

    public (JsonReference? jsonReference, bool isDelayed) MatchMorphJsonReferenceByName(JsonFile jsonFile, Reference reference, FileReferenceBase? fallBackResolvedAsset)
    {
        if (reference.MorphName is not null && KnownNames.VirusMorphs.Any(t => reference.MorphName.Equals(t, StringComparison.Ordinal))) {
            return (default, false);
        }

        return MatchAssetByUuidOrName(jsonFile, reference.MorphName, reference, _morphFilesByName, fallBackResolvedAsset);
    }

    private (JsonReference? jsonReference, bool isDelayed) MatchAssetByUuidOrName(JsonFile jsonFile, string? uuidOrName,
        Reference reference, ILookup<string, FileReferenceBase> lookup, FileReferenceBase? fallBackResolvedAsset)
    {
        if (string.IsNullOrWhiteSpace(uuidOrName))
            throw new VamToolboxException("Invalid displayNameOrUuid");

        var matchedAssets = lookup[uuidOrName].ToList();
        if (fallBackResolvedAsset is not null && !matchedAssets.Contains(fallBackResolvedAsset)) matchedAssets.Add(fallBackResolvedAsset);
        FilterAssetsByGender(reference, matchedAssets);

        if (fallBackResolvedAsset is not null && matchedAssets.Any(t => t.SizeWithChildren != fallBackResolvedAsset.SizeWithChildren)) {
            // could be optimized, we can trigger late resolver and see what can be moved to VAM dir
            // different size, use fallbackAsset
            return (new JsonReference(fallBackResolvedAsset, reference), false);
        }

        switch (matchedAssets.Count) {
            case 0:
                if (fallBackResolvedAsset is not null) return (new JsonReference(fallBackResolvedAsset, reference), false);
                return (null, false);
            case 1:
                return (new JsonReference(matchedAssets[0], reference), false);
        }

        // prefer files inside VAM dir
        if (matchedAssets.Any(t => t.IsInVaMDir)) {
            matchedAssets.RemoveAll(t => !t.IsInVaMDir);
        }

        if (matchedAssets.Count == 1)
            return (new JsonReference(matchedAssets[0], reference), false);

        _delayedReferencesToResolve.Add((jsonFile, reference, matchedAssets, uuidOrName));

        return (null, true);
    }

    private static void FilterAssetsByGender(Reference reference, List<FileReferenceBase> matchedAssets)
    {
        var isSupportedType = (reference.EstimatedAssetType & AssetType.ValidClothOrHairOrMorph) != 0;
        var isFemaleAsset = reference.EstimatedAssetType.IsFemale();
        var isMaleAsset = reference.EstimatedAssetType.IsMale();

        if (!isSupportedType) {
            if (reference.EstimatedReferenceLocation.Contains("female", StringComparison.OrdinalIgnoreCase)) {
                isFemaleAsset = true;
            } else if (reference.EstimatedReferenceLocation.Contains("male", StringComparison.OrdinalIgnoreCase)) {
                isMaleAsset = true;
            }
        }

        if (isFemaleAsset) {
            matchedAssets.RemoveAll(x => x.Type.IsMale());
        } else if (isMaleAsset) {
            matchedAssets.RemoveAll(x => x.Type.IsFemale());
        } else {
            //very rare case where we don't know the gender, prefer females
            if (matchedAssets.Any(x => x.Type.IsFemale()))
                matchedAssets.RemoveAll(x => x.Type.IsMale());
        }
    }
}