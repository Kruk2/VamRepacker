using VamToolbox.Models;

namespace VamToolbox.Sqlite;

public interface IDatabase : IDisposable
{
    IEnumerable<ReferenceEntry> ReadReferenceCache();
    IEnumerable<(string fileName, string localPath, long size, DateTime modifiedTime, string? uuid, long varLocalFileSize, string? parentLocalPath)> ReadVarFilesCache();
    IEnumerable<(string fileName, long size, DateTime modifiedTime, string? uuid, string? parentLocalPath)> ReadFreeFilesCache();

    public Dictionary<DatabaseFileKey, long> SaveFiles(
        Dictionary<DatabaseFileKey, (string? uuid, long? varLocalFileSizeVal, string? parentFile)> files);
    void UpdateReferences(Dictionary<DatabaseFileKey, long> batch,
        List<(DatabaseFileKey file, List<Reference> references)> jsonFiles);

    Task ClearCache();
    void EnsureCreated();

    void SaveSettings(AppSettings appSettings);
    AppSettings LoadSettings();
    void Vaccum();
}