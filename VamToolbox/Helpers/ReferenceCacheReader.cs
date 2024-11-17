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

    public Task SaveCache(IEnumerable<VarPackage> varFiles, IEnumerable<FreeFile> freeFiles) => Task.Run(() => SaveCacheSync(varFiles, freeFiles));

    private void SaveCacheSync(IEnumerable<VarPackage> varFiles, IEnumerable<FreeFile> freeFiles)
    {
        _progressTracker.Report("Generating cache", forceShow: true);

        var progress = 0;
        var filesFromFreeFiles = freeFiles
            .SelfAndChildren();
        var filesFromVars = varFiles.SelectMany(t => t.Files)
            .SelfAndChildren();

        var allFiles = filesFromVars.Cast<FileReferenceBase>().Concat(filesFromFreeFiles).ToList();
        var total = allFiles.Count + allFiles.Count;

        var bulkInsertFiles = new Dictionary<DatabaseFileKey, (string? uuid, long? varLocalFileSizeVal, string? csFiles)>();
        var bulkInsertReferences = new List<(DatabaseFileKey file, List<Reference> references)>();
        var allFilesGrouped = allFiles.GroupBy(t =>
            new DatabaseFileKey(
                t.IsVar ? Path.GetFileName(t.Var.FullPath) : t.Free.LocalPath,
                t.IsVar ? t.Var.Size : t.Size,
                t.IsVar ? t.Var.Modified : t.Free.ModifiedTimestamp,
                t.IsVar ? t.LocalPath : string.Empty));

        foreach (var files in allFilesGrouped) {
            var firstFile = files.First();
            bulkInsertFiles[files.Key] = (
                firstFile.MorphName ?? firstFile.InternalId, 
                firstFile.IsVar ? firstFile.Size : null,
                firstFile.CsFiles);

            var uniqueMorphs = files.Select(t => t.MorphName ?? t.InternalId).Distinct();
            var uniqueSizes = files.Select(t => t.IsVar ? (long?)t.Size : null).Distinct();
            if (uniqueMorphs.Count() != 1) {
                throw new InvalidOperationException($"Mismatched morphs for {files.Key}");
            }
            if (uniqueSizes.Count() != 1) {
                throw new InvalidOperationException($"Mismatched sizes for {files.Key}");
            }

            var allFilesReferences = files.Select(t => t.JsonFile is null ? [] : t.JsonFile.References.Select(x => x.Reference).Concat(t.JsonFile.Missing).ToList()).ToList();
            for (var i = 1; i < allFilesReferences.Count; i++)
            {
                if (allFilesReferences[i].Count != allFilesReferences[0].Count)
                    throw new InvalidOperationException($"Mismatched references count for {files.Key}");
            }

            if (allFilesReferences[0].Count > 0) {
                bulkInsertReferences.Add((files.Key, allFilesReferences[0]));
            }

            _progressTracker.Report(new ProgressInfo(Interlocked.Increment(ref progress), total, $"Caching {files.Key.FileName}"));
        }

        _progressTracker.Report("Saving file cache", forceShow: true);
        var filesIds = _database.SaveFiles(bulkInsertFiles);
        _progressTracker.Report("Saving references cache", forceShow: true);
        _database.UpdateReferences(filesIds, bulkInsertReferences);
        _database.Vaccum();
    }

    public Task ReadCache(List<PotentialJsonFile> potentialScenes) => Task.Run(() => ReadCacheSync(potentialScenes));

    private void ReadCacheSync(List<PotentialJsonFile> potentialScenes)
    {
        var progress = 0;
        HashSet<VarPackage> processedVars = [];
        HashSet<FreeFile> processedFreeFiles = [];

        _progressTracker.Report(new ProgressInfo(0, potentialScenes.Count, "Fetching cache from database", forceShow: true));

        var referenceCache = _database.ReadReferenceCache()
            .GroupBy(t => new DatabaseFileKey(t.FileName, t.FileSize, t.FileModifiedTime, t.LocalPath))
            .ToFrozenDictionary(t => t.Key, t => t.ToList());

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

    private static void ReadReferenceCache(PotentialJsonFile potentialJsonFile, FrozenDictionary<DatabaseFileKey, List<ReferenceEntry>> globalReferenceCache)
    {
        if (potentialJsonFile.IsVar) {
            foreach (var varFile in potentialJsonFile.Var.Files
                         .SelfAndChildren()
                         .Where(t => t.FilenameLower != "meta.json" && KnownNames.IsPotentialJsonFile(t.ExtLower))
                         .Where(t => !t.Dirty)) {

                var varFileName = Path.GetFileName(varFile.ParentVar.FullPath);
                if (globalReferenceCache.TryGetValue(new DatabaseFileKey(varFileName, varFile.ParentVar.Size, varFile.ParentVar.Modified, varFile.LocalPath), out var references)) {
                    var mappedReferences = references.Where(x => x.Value is not null).Select(t => new Reference(t, varFile)).ToList();
                    potentialJsonFile.AddCachedReferences(varFile.LocalPath, mappedReferences);
                }
            }
        } else if (!potentialJsonFile.IsVar && !potentialJsonFile.Free.Dirty) {
            var free = potentialJsonFile.Free;
            if (globalReferenceCache.TryGetValue(new DatabaseFileKey(free.LocalPath, free.Size, free.ModifiedTimestamp, string.Empty), out var references)) {
                var mappedReferences = references.Where(x => x.Value is not null).Select(t => new Reference(t, free)).ToList();
                potentialJsonFile.AddCachedReferences(mappedReferences);
            }
        }
    }
}