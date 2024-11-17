using System.Collections.Frozen;
using VamToolbox.Models;

namespace VamToolbox.Sqlite;

public interface IDatabase : IDisposable
{
    IEnumerable<ReferenceEntry> ReadReferenceCache();
    IEnumerable<(string fileName, string localPath, long size, DateTime modifiedTime, string? uuid, long varLocalFileSize, string? csFiles)> ReadVarFilesCache();
    IEnumerable<(string fileName, long size, DateTime modifiedTime, string? uuid, string? csFiles)> ReadFreeFilesCache();

    public FrozenDictionary<DatabaseFileKey, long> SaveFiles(
        Dictionary<DatabaseFileKey, (string? uuid, long? varLocalFileSizeVal, string? csFiles)> files);
    void UpdateReferences(FrozenDictionary<DatabaseFileKey, long> batch,
        List<(DatabaseFileKey file, List<Reference> references)> jsonFiles);

    Task ClearCache();
    void EnsureCreated();

    void SaveSettings(AppSettings appSettings);
    AppSettings LoadSettings();
    void Vaccum();
}