using System.IO.Abstractions.TestingHelpers;
using AutoFixture;
using FluentAssertions;
using VamToolbox.Operations.Abstract;
using VamToolbox.Operations.Destructive;
using Xunit;

namespace VamToolbox.Tests.Operations;

public class TrustAllVarsOperationTests
{
    private const string FakeVamDir = "C:/VAM/";
    private const string AddonPackagesPath = FakeVamDir + "AddonPackages";
    private const string PrefsDirPath = FakeVamDir + "AddonPackagesUserPrefs";
    private readonly CustomFixture _fixture = new();
    private readonly TrustAllVarsOperation _operation;
    private readonly MockFileSystem _fs;

    public TrustAllVarsOperationTests()
    {
        _fs = _fixture.Create<MockFileSystem>();
        _fs.AddDirectory(FakeVamDir);
        CreateVarFile("a.1.var");

        _operation = _fixture.Create<TrustAllVarsOperation>();
    }

    [Fact]
    public async Task Trust_WhenPrefDirectoryDoesnExist_CreatesDirectory()
    {
        var context = GetContext();

        await _operation.ExecuteAsync(context);

        _fs.Directory.Exists(PrefsDirPath).Should().BeTrue();
    }

    [Fact]
    public async Task Trust_WhenPrefFileDoesntExist_CreatesNew()
    {
        var context = GetContext();

        await _operation.ExecuteAsync(context);

        var prefPath = GetPrefPath();
        _fs.FileExists(prefPath).Should().BeTrue();
        var content = _fs.GetFile(prefPath).TextContents;
        content.Should().Be("{\r\n  \"pluginsAlwaysEnabled\": \"true\",\r\n  \"pluginsAlwaysDisabled\": \"false\"\r\n}");
    }

    [Fact]
    public async Task Trust_WhenPrefFileDoesntExist_ButInDryMode_SkipCreation()
    {
        var context = GetContext(dryRun: true);

        await _operation.ExecuteAsync(context);

        var prefPath = GetPrefPath();
        _fs.FileExists(prefPath).Should().BeFalse();
        _fs.Directory.Exists(PrefsDirPath).Should().BeFalse();
    }

    [Fact]
    public async Task Trust_WhenPrefFileExistsAndHasPluginsAlwaysEnabledToFalse_ChangesItToTrue()
    {
        var context = GetContext();
        var prefPath = GetPrefPath();
        _fs.AddFile(prefPath, new MockFileData("{\"pluginsAlwaysEnabled\": \"false\", \"pluginsAlwaysDisabled\": \"false\"}"));

        await _operation.ExecuteAsync(context);

        var content = _fs.GetFile(prefPath).TextContents;
        content.Should().Be("{\r\n  \"pluginsAlwaysEnabled\": \"true\",\r\n  \"pluginsAlwaysDisabled\": \"false\"\r\n}");
    }

    [Fact]
    public async Task Trust_WhenPrefFilExistsAndHasPluginsAlwaysDisabledSetToTrue_DoesntChangeIt()
    {
        var context = GetContext();
        var prefPath = GetPrefPath();
        var content = "{\"pluginsAlwaysEnabled\": \"false\", \"pluginsAlwaysDisabled\": \"true\"}";
        _fs.AddFile(prefPath, new MockFileData(content));

        await _operation.ExecuteAsync(context);

        var actualContent = _fs.GetFile(prefPath).TextContents;
        actualContent.Should().Be(content);
    }

    [Fact]
    public async Task Trust_WhenPrefFilExistsAndHasExtraProperties_DoesntChangeThemWhenUpdatingPluginsSetting()
    {
        var context = GetContext();
        var prefPath = GetPrefPath();
        var content = "{ \"whatever\":\"wtf\", \"pluginsAlwaysEnabled\": \"false\", \"pluginsAlwaysDisabled\": \"false\", \"deep\":{\"wtf\": true} }";
        _fs.AddFile(prefPath, new MockFileData(content));

        await _operation.ExecuteAsync(context);

        var actualContent = _fs.GetFile(prefPath).TextContents;
        actualContent.Should().Be("{\r\n  \"whatever\": \"wtf\",\r\n  \"pluginsAlwaysEnabled\": \"true\",\r\n  \"pluginsAlwaysDisabled\": \"false\",\r\n  \"deep\": {\r\n    \"wtf\": true\r\n  }\r\n}");
    }

    [Fact]
    public async Task Trust_WhenPrefFilExistsAndContainsMalformedData_SkipIt()
    {
        var context = GetContext();
        var prefPath = GetPrefPath();
        var content = "{ whatever\":\"wtf }";
        _fs.AddFile(prefPath, new MockFileData(content));

        await _operation.ExecuteAsync(context);

        var actualContent = _fs.GetFile(prefPath).TextContents;
        actualContent.Should().Be(content);
    }

    private static string GetPrefPath() => Path.Combine(PrefsDirPath, "a.1.prefs");
    private string CreateVarFile(string file)
    {
        var path = Path.Combine(AddonPackagesPath, file);
        _fs.AddFile(path, new MockFileData("test"));
        return path;
    }

    private static OperationContext GetContext(bool dryRun = false) => new() { DryRun = dryRun, VamDir = FakeVamDir, Threads = 6 };
}
