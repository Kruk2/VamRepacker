using System.IO.Abstractions;
using VamToolbox.Logging;
using VamToolbox.Operations.Abstract;

namespace VamToolbox.Operations.Backupers;
public interface IUserPrefsBackuper : IOperation
{
    public Task Backup(string vamDir, bool dryRun);
    public Task Restore(string vamDir, bool dryRun);
}

public class UserPrefsBackuper : IUserPrefsBackuper
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger _logger;
    private readonly IProgressTracker _progressTracker;

    public UserPrefsBackuper(IFileSystem fileSystem, ILogger logger, IProgressTracker progressTracker)
    {
        _fileSystem = fileSystem;
        _logger = logger;
        _progressTracker = progressTracker;
    }

    public async Task Backup(string vamDir, bool dryRun)
    {
        await _logger.Init("user_prefs_backup.txt");
        _progressTracker.InitProgress("Backing up prefs files.");

        var userPrefsDir = UserPrefsDir(vamDir);
        if (!_fileSystem.Directory.Exists(userPrefsDir)) {
            _logger.Log("UserPrefsDir doesn't exist. Exiting");
            return;
        }

        int counter = 0;
        foreach (var file in _fileSystem.Directory.EnumerateFiles(userPrefsDir, "*.prefs")) {
            var fileName = _fileSystem.Path.GetFileName(file) + KnownNames.BackupExtension;
            var backupDestination = _fileSystem.Path.Combine(userPrefsDir, fileName);

            if (!dryRun) {
                _fileSystem.File.Copy(file, backupDestination, true);
            }

            _logger.Log($"Backing up {file} to {backupDestination}");
            _progressTracker.Report($"Backing up {Path.GetFileName(file)}");
            counter++;
        }

        _progressTracker.Complete($"Backed up {counter} files");
    }

    public async Task Restore(string vamDir, bool dryRun)
    {
        _progressTracker.InitProgress("Restoring prefs files.");
        await _logger.Init("user_prefs_backup.txt");

        var userPrefsDir = UserPrefsDir(vamDir);
        if (!_fileSystem.Directory.Exists(userPrefsDir)) {
            _logger.Log("UserPrefsDir doesn't exist. Exiting");
            return;
        }

        int counter = 0;
        foreach (var file in _fileSystem.Directory.EnumerateFiles(userPrefsDir, "*" + KnownNames.BackupExtension)) {
            var fileName = _fileSystem.Path.GetFileNameWithoutExtension(file);
            var restoreDestination = _fileSystem.Path.Combine(userPrefsDir, fileName);

            if (!dryRun) {
                _fileSystem.File.Copy(file, restoreDestination, true);
            }

            _logger.Log($"Restoring {file} to {restoreDestination}");
            _progressTracker.Report($"Restoring {Path.GetFileName(restoreDestination)}");
            counter++;
        }

        _progressTracker.Complete($"Restored {counter} files");
    }

    private string UserPrefsDir(string vamDir) => _fileSystem.Path.Combine(vamDir, KnownNames.AddonPackagesUserPrefs);
}