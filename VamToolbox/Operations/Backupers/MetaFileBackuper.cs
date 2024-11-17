using System.IO.Abstractions;
using System.Threading.Tasks.Dataflow;
using Ionic.Zip;
using VamToolbox.Helpers;
using VamToolbox.Logging;
using VamToolbox.Operations.Abstract;

namespace VamToolbox.Operations.Backupers;
public interface IMetaFileRestorer : IOperation
{
    Task Restore(OperationContext context);
}

public class MetaFileRestorer : IMetaFileRestorer
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger _logger;
    private readonly IProgressTracker _progressTracker;
    private OperationContext _context = null!;
    private int _total, _progress, _successfullyProcessed;
    private readonly ISoftLinker _softLinker;

    public MetaFileRestorer(IFileSystem fileSystem, ILogger logger, IProgressTracker progress, ISoftLinker softLinker)
    {
        _fileSystem = fileSystem;
        _logger = logger;
        _progressTracker = progress;
        _softLinker = softLinker;
    }

    public async Task Restore(OperationContext context)
    {
        _context = context;
        await _logger.Init("meta_file_backup.txt");
        _progressTracker.InitProgress("Restoring meta.json");

        await RunInParallel(GetAddonDirs(), RestoreMeta);
        _progressTracker.Complete($"Restored {_successfullyProcessed} meta.json");
    }

    private IEnumerable<string> GetAddonDirs()
    {
        var addonDir = _fileSystem.Path.Combine(_context.VamDir, KnownNames.AddonPackages);
        if (_fileSystem.Directory.Exists(addonDir)) {
            yield return addonDir;
        }
        if (_context.RepoDir is not null && _fileSystem.Directory.Exists(_context.RepoDir)) {
            yield return _context.RepoDir;
        }
    }

    private async Task RestoreMeta(string varPath)
    {
        var oldModifiedDate = _fileSystem.FileInfo.New(varPath).LastWriteTimeUtc;
        var varTmpPath = varPath + ".tmp";

        try {
            {
                await using var stream = _fileSystem.File.OpenRead(varPath);
                using var zip = ZipFile.Read(stream);
                zip.CaseSensitiveRetrieval = true;

                var backupFile = zip["meta.json" + KnownNames.BackupExtension];

                if (backupFile is null) {
                    return;
                }

                var metaFile = zip["meta.json"];
                if (_context.DryRun) {
                    return;
                }

                var oldMetaCreationTime = DateTime.MinValue; 
                var oldMetaModifiedTime = DateTime.MinValue;
                if (metaFile is not null) {
                    zip.RemoveEntry(metaFile);
                    oldMetaCreationTime = metaFile.CreationTime;
                    oldMetaModifiedTime = metaFile.LastModified;
                }

                using (var reader = new MemoryStream()) {
                    backupFile.Extract(reader);
                    reader.Position = 0;
                    zip.AddEntry("meta.json", reader.ToArray());
                }

                if (oldMetaCreationTime != default) {
                    zip["meta.json"].CreationTime = oldMetaCreationTime;
                }
                if (oldMetaModifiedTime != default) {
                    zip["meta.json"].LastModified = oldMetaModifiedTime;
                }

                _logger.Log($"Restoring meta.json in {varPath}");
                Interlocked.Increment(ref _successfullyProcessed);

                await using var outputStream = _fileSystem.File.OpenWrite(varTmpPath);
                zip.Save(outputStream);
            }

            if (_fileSystem.File.Exists(varTmpPath)) {
                _fileSystem.File.Move(varTmpPath, varPath, true);
            }

        } catch (Exception e) {
            _logger.Log($"Unable to process {varPath}. Error: {e.Message}");
        } finally {
            if (!_context.DryRun) {
                _fileSystem.File.SetLastWriteTimeUtc(varPath, oldModifiedDate);
            }
            Interlocked.Increment(ref _progress);
            _progressTracker.Report(new ProgressInfo(_progress, _total, $"Restoring up {_fileSystem.Path.GetFileName(varPath)}"));
        }
    }

    private async Task RunInParallel(IEnumerable<string> varDirs, Func<string, Task> act)
    {
        var depScanBlock = new ActionBlock<string>(async t => await act(t), new ExecutionDataflowBlockOptions {
            MaxDegreeOfParallelism = _context.Threads
        });

        var files = varDirs
            .SelectMany(t => _fileSystem.Directory.EnumerateFiles(t, "*.var", SearchOption.AllDirectories))
            .Where(t => !_softLinker.IsSoftLink(t))
            .ToList();
        _total = files.Count;
        foreach (var file in files) {
            depScanBlock.Post(file);
        }

        depScanBlock.Complete();
        await depScanBlock.Completion;
    }
}
