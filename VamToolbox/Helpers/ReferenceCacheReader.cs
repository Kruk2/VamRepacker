using System.Collections.Frozen;
using VamToolbox.Logging;
using VamToolbox.Models;
using VamToolbox.Operations.Abstract;
using VamToolbox.Sqlite;

namespace VamToolbox.Helpers;

public interface IReferenceCache
{
    Task SaveCache(IEnumerable<VarPackage> varFiles, IEnumerable<FreeFile> freeFiles);
    Task ReadCache(List<PotentialJsonFile> potentialScenes);
}

public class ReferenceCache : IReferenceCache
{
    private readonly IDatabase _database;
    private readonly IProgressTracker _progressTracker;

    public ReferenceCache(IDatabase database, IProgressTracker progressTracker)
    {
        _database = database;
        _progressTracker = progressTracker;
    }

    public Task SaveCache(IEnumerable<VarPackage> varFiles, IEnumerable<FreeFile> freeFiles) => Task.Run(async () => {
        _progressTracker.Report("Saving file cache", forceShow: true);
        await _database.Save(SaveCacheSync(varFiles, freeFiles));
    });

    private IEnumerable<CachedFile> SaveCacheSync(IEnumerable<VarPackage> varFiles, IEnumerable<FreeFile> freeFiles)
    {
        var progress = 0;
        var filesFromFreeFiles = freeFiles
            .SelfAndChildren();
        var filesFromVars = varFiles.SelectMany(t => t.Files)
            .SelfAndChildren();

        var allFiles = filesFromVars.Cast<FileReferenceBase>().Concat(filesFromFreeFiles).ToList();
        var total = allFiles.Count + allFiles.Count;
        var allFilesGrouped = allFiles.GroupBy(t =>
            new DatabaseFileKey(
                t.IsVar ? Path.GetFileName(t.Var.FullPath) : t.Free.LocalPath,
                t.IsVar ? t.Var.Size : t.Size,
                t.IsVar ? t.Var.Modified : t.Free.ModifiedTimestamp,
                t.IsVar ? t.LocalPath : string.Empty));

        foreach (var files in allFilesGrouped) {
            var firstFile = files.First();
            var cacheFile = new CachedFile {
                LocalPath = files.Key.LocalPath,
                FileName = files.Key.FileName,
                Size = files.Key.Size,
                ModifiedTime = files.Key.ModifiedTime,
                CsFiles = firstFile.CsFiles,
                Uuid = firstFile.MorphName ?? firstFile.InternalId,
                VarLocalFileSize = firstFile.IsVar ? firstFile.Size : null,
                IsInvalidVar = firstFile.IsVar ? firstFile.Var.IsInvalid ? 1 : 0 : 0,
            };

            var uniqueMorphs = files.Select(t => t.MorphName ?? t.InternalId).Distinct();
            var uniqueSizes = files.Select(t => t.IsVar ? (long?)t.Size : null).Distinct();
            if (uniqueMorphs.Count() != 1) {
                throw new InvalidOperationException($"Mismatched morphs for {files.Key}");
            }
            if (uniqueSizes.Count() != 1) {
                throw new InvalidOperationException($"Mismatched sizes for {files.Key}");
            }

            var allFilesReferences = files
                .Select(t => t.JsonFile is null ? [] : t.JsonFile.References.Select(x => x.Reference).Concat(t.JsonFile.Missing))
                .Select(t => t.Select(x => new CachedJsonReference {
                    Index = x.Index,
                    Length = x.Length,
                    MorphName = x.MorphName,
                    InternalId = x.InternalId,
                    Value = x.Value
                }))
                .ToList();
            for (var i = 1; i < allFilesReferences.Count; i++)
            {
                if (allFilesReferences[i].Count() != allFilesReferences[0].Count())
                    throw new InvalidOperationException($"Mismatched references count for {files.Key}");
            }

            cacheFile.References = allFilesReferences[0].ToList();
            _progressTracker.Report(new ProgressInfo(Interlocked.Increment(ref progress), total, $"Caching {files.Key.FileName}"));
            yield return cacheFile;
        }

        foreach (var varPackage in varFiles.Where(t => t is { Files.Count: 0 }))
        {
            yield return new CachedFile
            {
                FileName = Path.GetFileName(varPackage.FullPath),
                LocalPath = "dummy",
                Size = varPackage.Size,
                ModifiedTime = varPackage.Modified,
                References = [],
                IsInvalidVar = 1
            };
        }
    }

    public Task ReadCache(List<PotentialJsonFile> potentialScenes) => Task.Run(async () => await ReadCacheAsync(potentialScenes));

    private async Task ReadCacheAsync(List<PotentialJsonFile> potentialScenes)
    {
        var progress = 0;
        HashSet<VarPackage> processedVars = [];
        HashSet<FreeFile> processedFreeFiles = [];

        _progressTracker.Report(new ProgressInfo(0, potentialScenes.Count, "Fetching cache from database", forceShow: true));

        var referenceCache = (await _database.Read())
            .ToFrozenDictionary(t => new DatabaseFileKey(t.FileName, t.Size, t.ModifiedTime, t.LocalPath), t => t.References);

        foreach (var json in potentialScenes) {
            switch (json.IsVar) {
                case true when processedVars.Add(json.Var):
                case false when processedFreeFiles.Add(json.Free):
                    ReadReferenceCache(json, referenceCache);
                    break;
            }

            _progressTracker.Report(new ProgressInfo(progress++, potentialScenes.Count, "Reading cache: " + (json.IsVar ? json.Var.ToString() : json.Free.ToString())));
        }
    }

    private static void ReadReferenceCache(PotentialJsonFile potentialJsonFile, FrozenDictionary<DatabaseFileKey, List<CachedJsonReference>?> globalReferenceCache)
    {
        if (potentialJsonFile.IsVar) {
            foreach (var varFile in potentialJsonFile.Var.Files
                         .SelfAndChildren()
                         .Where(t => t.FilenameLower != "meta.json" && KnownNames.IsPotentialJsonFile(t.ExtLower))
                         .Where(t => !t.Dirty)) {

                var varFileName = Path.GetFileName(varFile.ParentVar.FullPath);
                if (globalReferenceCache.TryGetValue(new DatabaseFileKey(varFileName, varFile.ParentVar.Size, varFile.ParentVar.Modified, varFile.LocalPath), out var references) && references != null) {
                    var mappedReferences = references.Where(x => x.Value is not null).Select(t => new Reference(t, varFile)).ToList();
                    potentialJsonFile.AddCachedReferences(varFile.LocalPath, mappedReferences);
                }
            }
        } else if (!potentialJsonFile.IsVar && !potentialJsonFile.Free.Dirty) {
            var free = potentialJsonFile.Free;
            if (globalReferenceCache.TryGetValue(new DatabaseFileKey(free.LocalPath, free.Size, free.ModifiedTimestamp, string.Empty), out var references) && references != null) {
                var mappedReferences = references.Where(x => x.Value is not null).Select(t => new Reference(t, free)).ToList();
                potentialJsonFile.AddCachedReferences(mappedReferences);
            }
        }
    }
}