using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;

namespace VamToolbox.Sqlite;

public sealed class Database : IDatabase
{
    private const string CacheJsonFileName = "cache.json";
    private const string AppSettingsTable = "AppSettings";
    private const int SettingsVersion = 1;
    private readonly SqliteConnection _connection;
    private List<CachedFile>? _cachedFiles;

    public Database(string rootDir)
    {
        var currentDir = Path.Combine(rootDir, "vamToolbox.sqlite");
        _connection = new SqliteConnection($@"data source={currentDir}");
        _connection.Open();
    }

    public void ClearCache()
    {
        if(File.Exists(CacheJsonFileName))
            File.Delete(CacheJsonFileName);
    }

    public void EnsureCreated()
    {
        _connection.Query("PRAGMA journal_mode=WAL;PRAGMA synchronous = NORMAL; PRAGMA journal_size_limit = 6144000;");

        CreateSettingsTable();
    }

    private void CreateSettingsTable()
    {
        if (!TableExists(AppSettingsTable)) return;

        _connection.Execute($"Create Table {AppSettingsTable} (Version INTEGER NOT NULL PRIMARY KEY, Data JSON NOT NULL)");
    }

    private bool TableExists(string tableName)
    {
        var table = _connection.Query<string>("SELECT name FROM sqlite_master WHERE type='table' AND name = @tableName;", new { tableName });
        var foundTable = table.FirstOrDefault();
        return string.IsNullOrEmpty(tableName) || foundTable != tableName;
    }

    public async Task<List<CachedFile>> Read()
    {
        if(_cachedFiles != null)
            return _cachedFiles;

        if (!File.Exists(CacheJsonFileName)) {
            _cachedFiles = [];
            return _cachedFiles;
        }

        await using var openStream = File.OpenRead(CacheJsonFileName);
        _cachedFiles = (await JsonSerializer.DeserializeAsync(openStream, SourceGenerationContext.Default.ListCachedFile))!;
        return _cachedFiles;
    }

    public async Task Save(IEnumerable<CachedFile> files)
    {
        _cachedFiles = null;
        await using var openStream = File.Create(CacheJsonFileName);
        await JsonSerializer.SerializeAsync(openStream, files, SourceGenerationContext.Default.IEnumerableCachedFile);
    }

    public void Dispose() => _connection.Dispose();

    public void SaveSettings(AppSettings appSettings)
    {
        var json = JsonSerializer.Serialize(appSettings);
        _connection.Execute($"insert or replace into {AppSettingsTable} values (@SettingsVersion, @Data)", new { SettingsVersion, Data=json });
    }

    public AppSettings LoadSettings()
    {
        var json = _connection.ExecuteScalar<string?>($"select data from {AppSettingsTable} where Version = @SettingsVersion", new { SettingsVersion});
        if (json is null) {
            return new AppSettings();
        }

        return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }
}