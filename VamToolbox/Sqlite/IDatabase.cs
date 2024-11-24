namespace VamToolbox.Sqlite;

public interface IDatabase : IDisposable
{
    public Task<List<CachedFile>> Read();
    public Task Save(IEnumerable<CachedFile> files);

    void ClearCache();
    void EnsureCreated();

    void SaveSettings(AppSettings appSettings);
    AppSettings LoadSettings();
}