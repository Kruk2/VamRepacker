﻿using System.Collections.Concurrent;
using System.Collections.Frozen;
using Dapper;
using Microsoft.Data.Sqlite;
using VamToolbox.Models;

namespace VamToolbox.Sqlite;

public sealed class Database : IDatabase
{
    private const string FilesTable = "Files";
    private const string RefTable = "JsonReferences";
    private const string AppSettingsTable = "AppSettings";
    private const int SettingsVersion = 1;
    private readonly SqliteConnection _connection;

    public Database(string rootDir)
    {
        var currentDir = Path.Combine(rootDir, "vamToolbox.sqlite");
        _connection = new SqliteConnection($@"data source={currentDir}");
        _connection.Open();
    }

    public void EnsureCreated()
    {
        _connection.Query("PRAGMA journal_mode=WAL");

        CreateHashTable();
        CreateFilesTable();
        CreateJsonReferencesTable();
        CreateSettingsTable();
        CreateIndexes();
    }

    private void CreateSettingsTable()
    {
        if (!TableExists(AppSettingsTable)) return;

        _connection.Execute($"Create Table {AppSettingsTable} (Version INTEGER NOT NULL PRIMARY KEY, Data JSON NOT NULL)");
    }

    private void CreateIndexes()
    {
        _connection.Execute($"Create Index if not exists IX_ParentFileId on {RefTable} (ParentFileId)");
    }

    private void CreateJsonReferencesTable()
    {
        if (!TableExists(RefTable)) return;

        _connection.Execute($"Create Table {RefTable} (" +
            "Value TEXT NOT NULL," +
            "MorphName TEXT," +
            "InternalId TEXT," +
            "[Index] INTEGER NOT NULL," +
            "Length INTEGER NOT NULL," +
            "ParentFileId integer NOT NULL," +
            $"CONSTRAINT FK_ParentJsonId FOREIGN KEY(ParentFileId) REFERENCES {FilesTable}(Id) ON DELETE CASCADE);");
    }

    private void CreateFilesTable()
    {
        if (!TableExists(FilesTable)) return;

        _connection.Execute($"Create Table {FilesTable} (" +
                            "Id integer PRIMARY KEY AUTOINCREMENT NOT NULL," +
                            "Path TEXT collate nocase NOT NULL," +
                            "LocalPath TEXT NOT NULL," +
                            "Uuid TEXT collate nocase," +
                            "FileSize integer NOT NULL," +
                            "ModifiedTime integer NOT NULL);");

        _connection.Execute($"CREATE UNIQUE INDEX IX_Files ON {FilesTable}(Path, LocalPath);");
    }

    private void CreateHashTable()
    {
        if (!TableExists("Hashes")) return;

        _connection.Execute("Create Table Hashes (" +
                            "FullPath TEXT collate nocase NOT NULL," +
                            "LocalAssetPath TEXT collate nocase NOT NULL," +
                            "Hash VARCHAR(42) collate nocase NOT NULL);");
    }

    private bool TableExists(string tableName)
    {
        var table = _connection.Query<string>("SELECT name FROM sqlite_master WHERE type='table' AND name = @tableName;", new { tableName });
        var foundTable = table.FirstOrDefault();
        return string.IsNullOrEmpty(tableName) || foundTable != tableName;
    }

    public async Task<FrozenDictionary<(string fullPath, string localAssetPath), string>> GetHashes()
    {
        var hashes = await _connection.QueryAsync<(string fullPath, string localAssethPath, string hash)>("SELECT * FROM hashes");

        var grouped = hashes
            .Select(t => new KeyValuePair<(string fullPath, string localAssetPath), string>((t.fullPath, t.localAssethPath), t.hash));
        return grouped.ToFrozenDictionary(t => t.Key, t => t.Value);
    }

    public async Task AddHashes(ConcurrentDictionary<(string fullPath, string localAssetPath), string> hashes)
    {
        await using var transaction = _connection.BeginTransaction();
        var command = _connection.CreateCommand();
        command.CommandText =
            @"INSERT INTO hashes VALUES($fullPath, $localAssetPath, $hash) ";

        var parameterFullPath = command.CreateParameter();
        parameterFullPath.ParameterName = "$fullPath";
        command.Parameters.Add(parameterFullPath);
        var parameterAsset = command.CreateParameter();
        parameterAsset.ParameterName = "$localAssetPath";
        command.Parameters.Add(parameterAsset);
        var parameterHash = command.CreateParameter();
        parameterHash.ParameterName = "$hash";
        command.Parameters.Add(parameterHash);

        // Insert a lot of data
        foreach (var hash in hashes) {
            parameterFullPath.Value = hash.Key.fullPath;
            parameterAsset.Value = hash.Key.localAssetPath;
            parameterHash.Value = hash.Value;
            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    public IEnumerable<ReferenceEntry> ReadReferenceCache()
    {
        return _connection.Query<ReferenceEntry>(
            $"select file.Path as FilePath, file.LocalPath as LocalPath, ref.Value, ref.[Index], ref.Length, ref.MorphName, ref.InternalId from {FilesTable} file " +
            $"left join {RefTable} ref on file.Id = ref.ParentFileId ");
    }

    public IEnumerable<(string fullPath, string localPath, long size, DateTime modifiedTime, string? uuid)> ReadVarFilesCache()
    {
        return _connection.Query<(string, string, long, DateTime, string?)>(
            $"select Path, LocalPath, FileSize, ModifiedTime, Uuid from {FilesTable} where LocalPath is not ''");
    }

    public IEnumerable<(string fullPath, long size, DateTime modifiedTime, string? uuid)> ReadFreeFilesCache()
    {
        return _connection.Query<(string, long, DateTime, string?)>(
            $"select Path, FileSize, ModifiedTime, Uuid from {FilesTable} where LocalPath is ''");
    }

    public void UpdateReferences(Dictionary<FileReferenceBase, long> batch, List<(FileReferenceBase file, IEnumerable<Reference> references)> jsonFiles)
    {
        using var transaction = _connection.BeginTransaction();
        var command = _connection.CreateCommand();
        command.CommandText =
            $"insert into {RefTable} (Value, [Index], Length, MorphName, InternalId, ParentFileId) VALUES " +
            "($Value, $Index, $Length, $MorphName, $InternalId, $fileId)";

        var parameterValue = command.CreateParameter();
        parameterValue.ParameterName = "$Value";
        command.Parameters.Add(parameterValue);
        var parameterIndex = command.CreateParameter();
        parameterIndex.ParameterName = "$Index";
        command.Parameters.Add(parameterIndex);
        var parameterLength = command.CreateParameter();
        parameterLength.ParameterName = "$Length";
        command.Parameters.Add(parameterLength);
        var parameterMorph = command.CreateParameter();
        parameterMorph.ParameterName = "$MorphName";
        command.Parameters.Add(parameterMorph);
        var paramInternalId = command.CreateParameter();
        paramInternalId.ParameterName = "$InternalId";
        command.Parameters.Add(paramInternalId);
        var paramFileId = command.CreateParameter();
        paramFileId.ParameterName = "$fileId";
        command.Parameters.Add(paramFileId);

        foreach (var (file, references) in jsonFiles) {
            foreach (var reference in references) {
                paramFileId.Value = batch[file];
                parameterValue.Value = reference.Value;
                parameterIndex.Value = reference.Index;
                parameterLength.Value = reference.Length;
                parameterMorph.Value = (object?)reference.MorphName ?? DBNull.Value;
                paramInternalId.Value = (object?)reference.InternalId ?? DBNull.Value;
                command.ExecuteNonQuery();
            }
        }

        transaction.Commit();
    }

    public async Task ClearCache()
    {
        await _connection.QueryAsync($"DELETE FROM {RefTable}");
        await _connection.QueryAsync($"DELETE FROM {FilesTable}");
        await _connection.QueryAsync("VACUUM");
    }

    public void SaveFiles(Dictionary<FileReferenceBase, long> files)
    {
        using var transaction = _connection.BeginTransaction();
        var commandInsert = _connection.CreateCommand();
        commandInsert.CommandText = $"insert or replace into {FilesTable} (Path, LocalPath, Uuid, FileSize, ModifiedTime) VALUES ($fullPath, $localPath, $uuid, $size, $timestamp); SELECT last_insert_rowid();";

        var paramFullPath = commandInsert.CreateParameter();
        paramFullPath.ParameterName = "$fullPath";
        commandInsert.Parameters.Add(paramFullPath);
        var localPath = commandInsert.CreateParameter();
        localPath.ParameterName = "$localPath";
        commandInsert.Parameters.Add(localPath);
        var uuid = commandInsert.CreateParameter();
        uuid.ParameterName = "$uuid";
        commandInsert.Parameters.Add(uuid);
        var paramSize = commandInsert.CreateParameter();
        paramSize.ParameterName = "$size";
        commandInsert.Parameters.Add(paramSize);
        var paramTimestamp = commandInsert.CreateParameter();
        paramTimestamp.ParameterName = "$timestamp";
        commandInsert.Parameters.Add(paramTimestamp);

        foreach (var file in files.Keys) {
            paramFullPath.Value = file.IsVar ? file.Var.SourcePathIfSoftLink ?? file.Var.FullPath : file.Free.SourcePathIfSoftLink ?? file.Free.FullPath;
            localPath.Value = (object?)(file.IsVar ? file.VarFile.LocalPath : null) ?? string.Empty;
            uuid.Value = (object?)(file.MorphName ?? file.InternalId) ?? DBNull.Value;
            paramSize.Value = file.Size;
            paramTimestamp.Value = file.ModifiedTimestamp;
            files[file] = (long)commandInsert.ExecuteScalar()!;
        }

        transaction.Commit();
    }

    public void Dispose() => _connection.Dispose();

    public void SaveSettings(AppSettings appSettings)
    {
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(appSettings)!;
        _connection.Execute($"insert or replace into {AppSettingsTable} values (@SettingsVersion, @Data)", new { SettingsVersion, Data=json });
    }

    public AppSettings LoadSettings()
    {
        var json = _connection.ExecuteScalar<string?>($"select data from {AppSettingsTable} where Version = @SettingsVersion", new { SettingsVersion});
        if (json is null) {
            return new AppSettings();
        }

        return Newtonsoft.Json.JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
    }
}