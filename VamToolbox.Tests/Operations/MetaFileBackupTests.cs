using System.IO.Abstractions.TestingHelpers;
using AutoFixture;
using FluentAssertions;
using VamToolbox.Operations.Abstract;
using VamToolbox.Operations.Backupers;
using Xunit;

namespace VamToolbox.Tests.Operations;
public class MetaFileBackupTests 
{
    private readonly MockFileSystem _fs;
    private readonly MetaFileRestorer _restorer;
    private const string AddondsDir = "C:/VaM/AddonPackages/";

    public MetaFileBackupTests()
    {
        _fs = new MockFileSystem(new Dictionary<string, MockFileData> {
            [AddondsDir + "a.var"] = CreateZipFile(metaFile: "test-meta-content")
        });
        var fixture = new CustomFixture();
        fixture.AddFileSystem(_fs);
        _restorer = fixture.Create<MetaFileRestorer>();
    }

    [Fact]
    public async Task Restore_ShouldRestoreMetaFile()
    {
        _fs.AddFile(AddondsDir + "a.var", CreateZipFile(metaFile: "test-meta-content", backupFile: "backup-content"));
        await Restore();

        var file = _fs.GetFile(AddondsDir + "a.var");
        var (metaFile, backupFile) = ReadZipFile(file);
        metaFile!.Should().Be("backup-content");
        backupFile!.Should().Be("backup-content");
    }

    [Fact]
    public async Task Restore_WhenBackupFileIsMissing_ShouldSkipIt()
    {
        _fs.AddFile(AddondsDir + "a.var", CreateZipFile(metaFile: "test-meta-content"));
        await Restore();

        var file = _fs.GetFile(AddondsDir + "a.var");
        var (metaFile, backupFile) = ReadZipFile(file);
        metaFile!.Should().Be("test-meta-content");
        backupFile.Should().BeNull();
    }

    [Fact]
    public async Task Restore_WhenDryRun_ShouldNotChangeAnything()
    {
        _fs.AddFile(AddondsDir + "a.var", CreateZipFile(metaFile: "test-meta-content", backupFile: "backup-content"));
        await Restore(dryRun: true);

        var file = _fs.GetFile(AddondsDir + "a.var");
        var (metaFile, backupFile) = ReadZipFile(file);
        metaFile!.Should().Be("test-meta-content");
        backupFile!.Should().Be("backup-content");
    }

    [Fact]
    public async Task Restore_WhenMetaFileIsMissing_ShouldRestoreMetaFile()
    {
        _fs.AddFile(AddondsDir + "a.var", CreateZipFile(backupFile: "backup-content"));
        await Restore();

        var file = _fs.GetFile(AddondsDir + "a.var");
        var (metaFile, backupFile) = ReadZipFile(file);
        metaFile!.Should().Be("backup-content");
        backupFile!.Should().Be("backup-content");
    }

    private static (string? metaFile, string? backupFile) ReadZipFile(MockFileData file)
    {
        var files = ZipTestHelpers.ReadZipFile(file);
        files.TryGetValue("meta.json", out var metaFile);
        files.TryGetValue("meta.json.toolboxbak", out var backupFile);

        return (metaFile, backupFile);
    }

    private static MockFileData CreateZipFile(string? metaFile = null, string? backupFile = null)
    {
        var files = new Dictionary<string, string>();
        if (metaFile is not null) {
            files["meta.json"] = metaFile;
        }

        if (backupFile is not null) {
            files["meta.json.toolboxbak"] = backupFile;
        }

        return ZipTestHelpers.CreateMockFile(files);
    }

    private Task Restore(bool dryRun = false) => _restorer.Restore(new OperationContext {
        DryRun = dryRun,
        VamDir = "C:/VaM",
        Threads = 1
    });
}
