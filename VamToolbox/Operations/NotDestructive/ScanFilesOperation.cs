using System.Collections.Frozen;
using System.IO.Abstractions;
using MoreLinq;
using VamToolbox.FilesGrouper;
using VamToolbox.Helpers;
using VamToolbox.Logging;
using VamToolbox.Models;
using VamToolbox.Operations.Abstract;
using VamToolbox.Sqlite;

namespace VamToolbox.Operations.NotDestructive;

public sealed class ScanFilesOperation : IScanFilesOperation
{
    private readonly IProgressTracker _reporter;
    private readonly IFileSystem _fs;
    private readonly ILogger _logger;
    private readonly IFileGroupers _groupers;
    private readonly IDatabase _database;
    private OperationContext _context = null!;
    private readonly ISoftLinker _softLinker;

    public ScanFilesOperation(IProgressTracker reporter, IFileSystem fs, ILogger logger, IFileGroupers groupers, IDatabase database, ISoftLinker softLinker)
    {
        _reporter = reporter;
        _fs = fs;
        _logger = logger;
        _groupers = groupers;
        _database = database;
        _softLinker = softLinker;
    }

    public async Task<List<FreeFile>> ExecuteAsync(OperationContext context)
    {
        _reporter.InitProgress("Scanning files");
        await _logger.Init("scan_files.log");
        _context = context;

        var files = await ScanFolder(_context.VamDir);
        if (!string.IsNullOrEmpty(_context.RepoDir)) {
            files.AddRange(await ScanFolder(_context.RepoDir));
        }

        _reporter.Complete($"Scanned {files.Count} files in the Saves and Custom folders. Check scan_files.log");

        return files;
    }

    private async Task<List<FreeFile>> ScanFolder(string rootDir)
    {
        var files = new List<FreeFile>();

        await Task.Run(async () => {
            var freeFileCache = _database
                .ReadFreeFilesCache()
                .ToFrozenDictionary(t => new DatabaseFileKey(t.fileName, t.size, t.modifiedTime, string.Empty), t => t.uuid);

            _reporter.Report("Scanning Custom folder", forceShow: true);
            files.AddRange(ScanFolder(rootDir, "Custom"));
            _reporter.Report("Scanning Saves folder", forceShow: true);
            files.AddRange(ScanFolder(rootDir, "Saves"));

            _reporter.Report("Updating local database", forceShow: true);
            await GroupFiles(freeFileCache, files, rootDir);

        });

        return files;
    }

    private List<FreeFile> ScanFolder(string rootDir, string folder)
    {
        var searchDir = _fs.Path.Combine(rootDir, folder);
        if (!Directory.Exists(searchDir))
            return [];

        var isVamDir = _context.VamDir == rootDir;
        var files = _fs.Directory
            .EnumerateFiles(searchDir, "*.*", SearchOption.AllDirectories)
            .Where(f => !f.Contains(@"\.", StringComparison.Ordinal) && !f.NormalizePathSeparators().Contains("/Saves/PluginData/JayJayWon/BrowserAssist", StringComparison.Ordinal))
            .Select(f => (path: f, softLink: _softLinker.GetSoftLink(f)))
            .Where(f => f.softLink is null || File.Exists(f.softLink))
            .Select(f => (f.path, fileInfo: _fs.FileInfo.New(f.softLink ?? f.path), f.softLink))
            .Select(f => new FreeFile(f.path, f.path.RelativeTo(rootDir), f.fileInfo.Length, isVamDir, f.fileInfo.LastWriteTimeUtc, f.softLink))
            .ToList();

        return files;
    }

    private async Task GroupFiles(
        FrozenDictionary<DatabaseFileKey, string?> freeFileCache,
        List<FreeFile> files, 
        string rootDir)
    {
        foreach (var freeFile in files)
        {
            if (!freeFileCache.TryGetValue(new DatabaseFileKey(freeFile.LocalPath, freeFile.Size, freeFile.ModifiedTimestamp, string.Empty), out var uuid)) {
                freeFile.Dirty = true;
                continue;
            }

            if (!string.IsNullOrEmpty(uuid)) {
                if (freeFile.ExtLower == ".vmi") {
                    freeFile.MorphName = uuid;
                } else if (freeFile.ExtLower == ".vam") {
                    freeFile.InternalId = uuid;
                }
            }
        }

        // it is possible that one of the child files was modified and we will not merge them so let's skip cache
        Stream OpenFileStream(string p) => _fs.File.OpenRead(_fs.Path.Combine(rootDir, p));
        await _groupers.Group(files, OpenFileStream);
    }
}

public interface IScanFilesOperation : IOperation
{
    Task<List<FreeFile>> ExecuteAsync(OperationContext context);
}