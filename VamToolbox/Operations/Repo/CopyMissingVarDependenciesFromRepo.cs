using VamToolbox.Helpers;
using VamToolbox.Logging;
using VamToolbox.Models;
using VamToolbox.Operations.Abstract;

namespace VamToolbox.Operations.Repo;

public sealed class CopyMissingVarDependenciesFromRepo : ICopyMissingVarDependenciesFromRepo
{
    private readonly IProgressTracker _reporter;
    private readonly ILogger _logger;
    private readonly ISoftLinker _linker;
    private OperationContext _context = null!;

    public CopyMissingVarDependenciesFromRepo(IProgressTracker progressTracker, ILogger logger, ISoftLinker linker)
    {
        _reporter = progressTracker;
        _logger = logger;
        _linker = linker;
    }

    public async Task ExecuteAsync(OperationContext context, IList<VarPackage> vars, IList<FreeFile> freeFiles,
        CopyMode mode)
    {
        _reporter.InitProgress("Copying missing dependencies from REPO to VAM");
        _context = context;
        await _logger.Init("copy_missing_deps_from_repo.log");
        if (string.IsNullOrEmpty(_context.RepoDir)) {
            _reporter.Complete("Missing repo dir. Aborting");
            return;
        }

        await Task.Run(() => {
            var (varsToMove, filesToMove) = DependencyCalculator.GetFilesToMove(vars, freeFiles);
            LinkFiles(mode, filesToMove.ToList(), varsToMove.ToList());
        });
    }

    private void LinkFiles(
        CopyMode mode,
        List<FreeFile> existingFiles, List<VarPackage> exitingVars)
    {
        var count = existingFiles.Count + exitingVars.Count;
        var processed = 0;

        var varFolderDestination = Path.Combine(_context.VamDir, KnownNames.AddonPackages, "other");
        if (!_context.DryRun)
            Directory.CreateDirectory(varFolderDestination);

        foreach (var existingVar in exitingVars.OrderBy(t => t.Name.Filename)) {
            var varDestination = Path.Combine(varFolderDestination, Path.GetFileName(existingVar.FullPath));
            if (File.Exists(varDestination)) {
                _logger.Log($"Skipping {varDestination} source: {existingVar.FullPath}. Already exists.");
                _reporter.Report(new ProgressInfo(++processed, count, existingVar.Name.Filename));
                continue;
            }
            if (mode == CopyMode.Move && !_context.DryRun)
                File.Move(existingVar.FullPath, varDestination);
            else if(mode == CopyMode.Copy && !_context.DryRun)
                File.Copy(existingVar.FullPath, varDestination);
            else if (mode == CopyMode.SoftLink)
            {
                var success = _linker.SoftLink(varDestination, existingVar.FullPath, _context.DryRun);
                if (!success) {
                    _logger.Log($"Error soft-link. You didn't run the program as admin Dest: {varDestination} source: {existingVar.FullPath}");
                    _reporter.Complete("Failed. Unable to create symlink. Probably missing admin privilege.");
                    continue;
                }
            } else {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            _logger.Log($"{mode}: {Path.GetFileName(varDestination)}");
            _reporter.Report(new ProgressInfo(++processed, count, existingVar.Name.Filename));
        }

        foreach (var file in existingFiles.OrderBy(t => t.FilenameLower)) {
            var relativeToRoot = file.FullPath.RelativeTo(_context.RepoDir!);
            var destinationPath = Path.Combine(_context.VamDir, relativeToRoot);
            if (File.Exists(destinationPath)) {
                _logger.Log($"SkippingDest: {destinationPath} source: {file.FullPath}. Already exists.");
                _reporter.Report(new ProgressInfo(++processed, count, file.FilenameWithoutExt));
                continue;
            }
            if (!_context.DryRun)
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            if (mode == CopyMode.Move && !_context.DryRun) {
                File.Move(file.FullPath, destinationPath);
            } else if (mode == CopyMode.Copy && !_context.DryRun) {
                File.Copy(file.FullPath, destinationPath);
            } else if (mode == CopyMode.SoftLink) {
                var success = _linker.SoftLink(destinationPath, file.FullPath, _context.DryRun);
                if (!success) {
                    _logger.Log($"Error soft-link. Code {success} Dest: {destinationPath} source: {file.FullPath}");
                    _reporter.Complete(
                        $"Failed. Unable to create symlink. Probably missing admin privilege. Error code: {success}.");
                    continue;
                }
            } else {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }


            _logger.Log($"{mode}: {Path.GetFileName(file.FilenameWithoutExt)}");
            _reporter.Report(new ProgressInfo(++processed, count, file.FilenameWithoutExt));
        }

        _reporter.Complete($"Copied {count} vars/files. Check copy_missing_deps_from_repo.log");
    }
}

public interface ICopyMissingVarDependenciesFromRepo : IOperation
{
    Task ExecuteAsync(OperationContext context, IList<VarPackage> vars, IList<FreeFile> freeFiles, CopyMode mode);
}