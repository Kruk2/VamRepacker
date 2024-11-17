using System.IO.Abstractions.TestingHelpers;
using AutoFixture;
using FluentAssertions;
using VamToolbox.Operations.Backupers;
using Xunit;

namespace VamToolbox.Tests.Operations;
public class UserPrefsBackuperTests 
{
    private readonly MockFileSystem _fs;
    private readonly UserPrefsBackuper _backuper;
    private const string VamDir = "C:/VaM/";
    private const string PrefsDir = VamDir + "AddonPackagesUserPrefs/";

    public UserPrefsBackuperTests()
    {
        _fs = new MockFileSystem(new Dictionary<string, MockFileData> {
            [PrefsDir + "a.prefs"] = new ("test"),
            [PrefsDir + "b.prefs"] = new ("test2")
        });
        var fixture = new CustomFixture();
        fixture.AddFileSystem(_fs);
        _backuper = fixture.Create<UserPrefsBackuper>();
    }

    [Fact]
    public async Task Backup_WhenUserPrefsDirNotExists_ShouldSkip()
    {
        await _backuper.Backup(PrefsDir, false);

        _fs.AllFiles.Should().HaveCount(2);
    }

    [Fact]
    public async Task Backup_ShouldBackupBothFiles()
    {
        await _backuper.Backup(VamDir, false);

        _fs.AllFiles.Should().HaveCount(4);
        _fs.GetFile(PrefsDir + "a.prefs").TextContents.Should().Be(_fs.GetFile(PrefsDir + "a.prefs.toolboxbak").TextContents);
        _fs.GetFile(PrefsDir + "b.prefs").TextContents.Should().Be(_fs.GetFile(PrefsDir + "b.prefs.toolboxbak").TextContents);
    }


    [Fact]
    public async Task Backup_WhenDryRun_ShouldSkipBackup()
    {
        await _backuper.Backup(VamDir, true);

        _fs.AllFiles.Should().HaveCount(2);
        _fs.FileExists(PrefsDir + "a.prefs");
        _fs.FileExists(PrefsDir + "b.prefs");
    }

    [Fact]
    public async Task Backup_WhenBackupExitst_ShouldOverrideIt()
    {
        _fs.AddFile(PrefsDir + "a.prefs.toolboxbak", new MockFileData("old_backup"));
        await _backuper.Backup(VamDir, false);

        _fs.AllFiles.Should().HaveCount(4);
        _fs.GetFile(PrefsDir + "a.prefs").TextContents.Should().Be(_fs.GetFile(PrefsDir + "a.prefs.toolboxbak").TextContents);
        _fs.GetFile(PrefsDir + "b.prefs").TextContents.Should().Be(_fs.GetFile(PrefsDir + "b.prefs.toolboxbak").TextContents);
    }

    [Fact]
    public async Task Restore_WhenUserPrefsDirNotExists_ShouldSkip()
    {
        await _backuper.Restore(PrefsDir, false);

        _fs.AllFiles.Should().HaveCount(2);
    }

    [Fact]
    public async Task Restore_ShouldRestoreBothFiles()
    {
        _fs.AddFile(PrefsDir + "a.prefs.toolboxbak", new MockFileData("backup"));
        _fs.AddFile(PrefsDir + "b.prefs.toolboxbak", new MockFileData("backup1"));

        await _backuper.Restore(VamDir, false);

        _fs.AllFiles.Should().HaveCount(4);
        _fs.GetFile(PrefsDir + "a.prefs").TextContents.Should().Be("backup");
        _fs.GetFile(PrefsDir + "b.prefs").TextContents.Should().Be("backup1");
        _fs.GetFile(PrefsDir + "a.prefs.toolboxbak").TextContents.Should().Be("backup");
        _fs.GetFile(PrefsDir + "b.prefs.toolboxbak").TextContents.Should().Be("backup1");
    }

    [Fact]
    public async Task Restore_WhenDryRun_ShouldSkipRestore()
    {
        _fs.AddFile(PrefsDir + "a.prefs.toolboxbak", new MockFileData("backup"));
        _fs.AddFile(PrefsDir + "b.prefs.toolboxbak", new MockFileData("backup1"));

        await _backuper.Restore(VamDir, true);

        _fs.AllFiles.Should().HaveCount(4);
        _fs.GetFile(PrefsDir + "a.prefs").TextContents.Should().Be("test");
        _fs.GetFile(PrefsDir + "b.prefs").TextContents.Should().Be("test2");
        _fs.GetFile(PrefsDir + "a.prefs.toolboxbak").TextContents.Should().Be("backup");
        _fs.GetFile(PrefsDir + "b.prefs.toolboxbak").TextContents.Should().Be("backup1");
    }
}
