using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Threading.Tasks.Dataflow;
using Ionic.Zip;
using Newtonsoft.Json;
using VamToolbox.FilesGrouper;
using VamToolbox.Helpers;
using VamToolbox.Logging;
using VamToolbox.Models;
using VamToolbox.Operations.Abstract;
using VamToolbox.Sqlite;

namespace VamToolbox.Operations.NotDestructive;

public sealed class ScanVarPackagesOperation : IScanVarPackagesOperation
{
    private readonly IFileSystem _fs;
    private readonly IProgressTracker _reporter;
    private readonly ILogger _logger;
    private readonly IFileGroupers _groupers;
    private readonly ISoftLinker _softLinker;
    private IFavAndHiddenGrouper _favHideenGrouper;
    private readonly ConcurrentBag<VarPackage> _packages = new();
    private readonly VarScanResults _result = new();

    private int _scanned;
    private int _totalVarsCount;
    private OperationContext _context = null!;
    private readonly IDatabase _database;
    private FrozenDictionary<DatabaseFileKey, FrozenDictionary<string, string?>> _uuidCache = null!;

    public ScanVarPackagesOperation(IFileSystem fs, IProgressTracker progressTracker, ILogger logger, IFileGroupers groupers, ISoftLinker softLinker, IDatabase database, IFavAndHiddenGrouper favHideenGrouper)
    {
        _fs = fs;
        _reporter = progressTracker;
        _logger = logger;
        _groupers = groupers;
        _softLinker = softLinker;
        _database = database;
        _favHideenGrouper = favHideenGrouper;
    }

    public async Task<List<VarPackage>> ExecuteAsync(OperationContext context, List<FreeFile> freeFiles)
    {
        _context = context;
        _reporter.InitProgress("Scanning var files");
        await _logger.Init("var_scan.log");

        var stopWatch = new Stopwatch();
        stopWatch.Start();
        var packageFiles = await InitLookups();

        var scanPackageBlock = CreateBlock();
        foreach (var packageFile in packageFiles) {
            if (!VarPackageName.TryGet(_fs.Path.GetFileName(packageFile.path), out var name)) {
                _result.InvalidVarName.Add(packageFile.path);
                continue;
            }

            scanPackageBlock.Post((packageFile.path, packageFile.softLink, name));
        }

        scanPackageBlock.Complete();
        await scanPackageBlock.Completion;

        _result.Vars = _packages
            .GroupBy(t => t.Name.Filename)
            .Select(t => {
                var sortedVars = t.OrderBy(t => t.FullPath).ToList();
                if (sortedVars.Count == 1) return sortedVars[0];

                var fromVamDir = sortedVars.Where(t => t.IsInVaMDir);
                var notFromVamDir = sortedVars.Where(t => !t.IsInVaMDir);
                if (fromVamDir.Count() > 1) {
                    _result.DuplicatedVars.Add(fromVamDir.Select(t => t.FullPath).ToList());
                }
                if (notFromVamDir.Count() > 1) {
                    _result.DuplicatedVars.Add(notFromVamDir.Select(t => t.FullPath).ToList());
                }

                return fromVamDir.FirstOrDefault() ?? sortedVars.First();
            })
            .ToList();

        _reporter.Report("Grouping fav/hidden files", forceShow: true);
        await _favHideenGrouper.Group(freeFiles, _result.Vars);

        var endingMessage = $"Found {_result.Vars.SelectMany(t => t.Files).Count()} files in {_result.Vars.Count} var packages. Took {stopWatch.Elapsed:hh\\:mm\\:ss}. Check var_scan.log";
        _reporter.Complete(endingMessage);

        foreach (var err in _result.InvalidVarName.OrderBy(t => t))
            _logger.Log($"[INVALID-VAR-NAME] {err}");
        foreach (var err in _result.MissingMetaJson.OrderBy(t => t))
            _logger.Log($"[MISSING-META-JSON] {err}");
        foreach (var err in _result.InvalidVars.OrderBy(t => t))
            _logger.Log($"[INVALID-VAR] {err}");
        foreach (var err in _result.DuplicatedVars)
            _logger.Log($"[DUPLICATED-VARS] {Environment.NewLine} {string.Join(Environment.NewLine, err)}");
        return _result.Vars;
    }

    private Task<IEnumerable<(string path, string? softLink)>> InitLookups()
    {
        return Task.Run(() => {
            var packageFiles = _fs.Directory
                .GetFiles(_fs.Path.Combine(_context.VamDir, KnownNames.AddonPackages), "*.var", SearchOption.AllDirectories)
                .ToList();

            if (!string.IsNullOrEmpty(_context.RepoDir))
                packageFiles.AddRange(_fs.Directory.GetFiles(_context.RepoDir, "*.var", SearchOption.AllDirectories));

            _totalVarsCount = packageFiles.Count;

            _uuidCache = _database.ReadVarFilesCache()
                .GroupBy(t => new DatabaseFileKey(t.fileName, t.size, t.modifiedTime))
                .ToFrozenDictionary(
                    t => t.Key,
                    t => t.ToFrozenDictionary(x => x.localPath, x => x.uuid));

            return packageFiles
                .Select(t => (path: t, softLink: _softLinker.GetSoftLink(t)))
                .Where(t => t.softLink is null || _fs.File.Exists(t.softLink));
        });
    }

    private ActionBlock<(string path, string? softLink, VarPackageName varName)> CreateBlock()
    {
        var scanPackageBlock = new ActionBlock<(string path, string? softLink, VarPackageName varName)>(
            f => ExecuteOneAsync(f.path, f.softLink, f.varName),
            new ExecutionDataflowBlockOptions {
                MaxDegreeOfParallelism = _context.Threads
            });
        return scanPackageBlock;
    }

    private async Task ExecuteOneAsync(string varFullPath, string? softLink, VarPackageName name)
    {
        try {
            varFullPath = varFullPath.NormalizePathSeparators();
            var isInVamDir = varFullPath.StartsWith(_context.VamDir, StringComparison.Ordinal);
            var fileInfo = softLink != null ? _fs.FileInfo.New(softLink) : _fs.FileInfo.New(varFullPath);
            var varPackage = new VarPackage(name, varFullPath, softLink, isInVamDir, fileInfo.Length, fileInfo.LastWriteTimeUtc);

            await using var stream = _fs.File.OpenRead(varFullPath);
            using var archive = ZipFile.Read(stream);
            archive.CaseSensitiveRetrieval = true;

            var foundMetaFile = false;
            foreach (var entry in archive.Entries) {
                if (entry.IsDirectory) continue;
                if (entry.FileName == "meta.json" + KnownNames.BackupExtension) continue;
                if (entry.FileName == "meta.json") {
                    try {
                        await ReadMetaFile(entry);
                    } catch (Exception e) when (e is ArgumentException or JsonReaderException or JsonSerializationException) {
                        var message = $"{varFullPath}: {e.Message}";
                        _result.InvalidVars.Add(message);
                    }

                    foundMetaFile = true;
                    continue;
                }

                CreatePackageFileAsync(entry, isInVamDir, entry.LastModified, varPackage);
            }
            if (!foundMetaFile) {
                _result.MissingMetaJson.Add(varFullPath);
                return;
            }

            var varFilesList = (List<VarPackageFile>)varPackage.Files;
            _packages.Add(varPackage);

            var entries = archive.Entries.ToFrozenDictionary(t => t.FileName.NormalizePathSeparators());
            Stream OpenFileStream(string p) => entries[p].OpenReader();

            LookupDirtyPackages(varPackage);

            _reporter.Report("Grouping files", forceShow: true);
            await _groupers.Group(varFilesList, OpenFileStream);

        } catch (Exception exc) {
            var message = $"{varFullPath}: {exc.Message}";
            _result.InvalidVars.Add(message);
        }

        _reporter.Report(new ProgressInfo(Interlocked.Increment(ref _scanned), _totalVarsCount, name.Filename));
    }

    private void LookupDirtyPackages(VarPackage varPackage)
    {
        foreach (var varFile in varPackage.Files
                     .SelfAndChildren()
                     .Where(t => t.ExtLower is ".vmi" or ".vam" || KnownNames.IsPotentialJsonFile(t.ExtLower) && t.FilenameLower != "meta.json")) {
            var varPackageFileName = Path.GetFileName(varPackage.SourcePathIfSoftLink ?? varPackage.FullPath);
            if (!_uuidCache.TryGetValue(new DatabaseFileKey(varPackageFileName, varPackage.Size, varPackage.Modified), out var cacheEntry) ||
                !cacheEntry.TryGetValue(varFile.LocalPath, out var uuidEntry)) {
                varFile.Dirty = true;
                continue;
            }

            if (!string.IsNullOrEmpty(uuidEntry)) {
                if (varFile.ExtLower == ".vmi") {
                    varFile.MorphName = uuidEntry;
                } else if (varFile.ExtLower == ".vam") {
                    varFile.InternalId = uuidEntry;
                }
            }
        }
    }

    private static async Task<MetaFileJson?> ReadMetaFile(ZipEntry metaEntry)
    {
        await using var metaStream = metaEntry.OpenReader();
        using var sr = new StreamReader(metaStream);
        using var reader = new JsonTextReader(sr);
        var serializer = new JsonSerializer();
        return serializer.Deserialize<MetaFileJson>(reader);
    }

    private static void CreatePackageFileAsync(ZipEntry entry, bool isInVamDir, DateTime modifiedTimestamp, VarPackage varPackage)
    {
        _ = new VarPackageFile(entry.FileName.NormalizePathSeparators(), entry.UncompressedSize, isInVamDir, varPackage, modifiedTimestamp);
    }
}

public interface IScanVarPackagesOperation : IOperation
{
    Task<List<VarPackage>> ExecuteAsync(OperationContext context, List<FreeFile> freeFiles);
}

[ExcludeFromCodeCoverage]
public class VarScanResults
{
#pragma warning disable CA2227 // Collection properties should be read only
    public List<VarPackage> Vars { get; set; } = new();
    public ConcurrentBag<string> InvalidVars { get; } = new();
    public ConcurrentBag<string> InvalidVarName { get; } = new();
    public ConcurrentBag<string> MissingMetaJson { get; } = new();

    public ConcurrentBag<string> MissingMorphsFiles { get; } = new();
    public ConcurrentBag<string> MissingPresetsFiles { get; } = new();
    public ConcurrentBag<string> MissingScriptFiles { get; } = new();
    public List<List<string>> DuplicatedVars { get; set; } = new();
#pragma warning restore CA2227 // Collection properties should be read only
}