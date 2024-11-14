using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Formats.Tar;
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
    private IFavAndHiddenGrouper _favHiddenGrouper;
    private readonly ConcurrentBag<VarPackage> _packages = new();
    private readonly VarScanResults _result = new();

    private int _scanned;
    private int _totalVarsCount;
    private OperationContext _context = null!;
    private readonly IDatabase _database;
    private FrozenDictionary<DatabaseVarKey, List<(string fileName, string localPath, long size, DateTime modifiedTime, string? uuid, long varLocalFileSize, string? parentLocalPath)>> _varCache = null!;

    public ScanVarPackagesOperation(IFileSystem fs, IProgressTracker progressTracker, ILogger logger, IFileGroupers groupers, ISoftLinker softLinker, IDatabase database, IFavAndHiddenGrouper favHiddenGrouper)
    {
        _fs = fs;
        _reporter = progressTracker;
        _logger = logger;
        _groupers = groupers;
        _softLinker = softLinker;
        _database = database;
        _favHiddenGrouper = favHiddenGrouper;
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
        await _favHiddenGrouper.Group(freeFiles, _result.Vars);

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

            _varCache = _database.ReadVarFilesCache()
                .GroupBy(t => new DatabaseVarKey(t.fileName, t.size, t.modifiedTime))
                .ToFrozenDictionary(
                    t => t.Key,
                    t => t.ToList());

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
            var varPackageFileName = Path.GetFileName(varPackage.FullPath);
            var varCacheKey = new DatabaseVarKey(varPackageFileName, varPackage.Size, varPackage.Modified);
            _varCache.TryGetValue(varCacheKey, out var varCache);

            if (varCache != null) {
                ReadVarFromCache(varCache, varPackage);
            } else {
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

                    CreatePackageFileAsync(entry, varPackage);
                }
                if (!foundMetaFile) {
                    _result.MissingMetaJson.Add(varFullPath);
                    return;
                }

                var entries = archive.Entries.ToFrozenDictionary(t => t.FileName.NormalizePathSeparators());
                Stream OpenFileStream(string p) => entries[p].OpenReader();
                await _groupers.Group((List<VarPackageFile>)varPackage.Files, OpenFileStream);
            }

            _packages.Add(varPackage);
            _reporter.Report("Grouping files", forceShow: true);

        } catch (Exception exc) {
            var message = $"{varFullPath}: {exc.Message}";
            _result.InvalidVars.Add(message);
        }

        _reporter.Report(new ProgressInfo(Interlocked.Increment(ref _scanned), _totalVarsCount, name.Filename));
    }

    private static void ReadVarFromCache(List<(string fileName, string localPath, long size, DateTime modifiedTime, string? uuid, long varLocalFileSize, string? parentLocalPath)> varCache, VarPackage varPackage)
    {
        foreach (var (_, localPath, size, _, uuid, _, _) in varCache)
        {
            var varFile = new VarPackageFile(localPath.NormalizePathSeparators(), varPackage.IsInVaMDir, varPackage, size);
            if (!string.IsNullOrEmpty(uuid)) {
                if (varFile.ExtLower == ".vmi") {
                    varFile.MorphName = uuid;
                } else if (varFile.ExtLower == ".vam") {
                    varFile.InternalId = uuid;
                }
            }
        }

        var filesMovedAsChildren = new List<VarPackageFile>();
        foreach (var (_, localPath, _, _, _, _, parentLocalPath) in varCache) {
            if(string.IsNullOrEmpty(parentLocalPath))
                continue;

            var childFile = varPackage.FilesDict[localPath];
            var parentFile = varPackage.FilesDict[parentLocalPath];
            parentFile.AddChildren(childFile);
            filesMovedAsChildren.Add(childFile);
        }

        ((List<VarPackageFile>)varPackage.Files).RemoveAll(t => filesMovedAsChildren.Contains(t));
    }

    private static async Task<MetaFileJson?> ReadMetaFile(ZipEntry metaEntry)
    {
        await using var metaStream = metaEntry.OpenReader();
        using var sr = new StreamReader(metaStream);
        using var reader = new JsonTextReader(sr);
        var serializer = new JsonSerializer();
        return serializer.Deserialize<MetaFileJson>(reader);
    }

    private static void CreatePackageFileAsync(ZipEntry entry, VarPackage varPackage)
    {
        var varPackageFile = new VarPackageFile(entry.FileName.NormalizePathSeparators(), varPackage.IsInVaMDir, varPackage, entry.UncompressedSize);
        varPackageFile.Dirty = true;
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