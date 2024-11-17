using AutoFixture;
using FluentAssertions;
using VamToolbox.Helpers;
using VamToolbox.Models;
using Xunit;

namespace VamToolbox.Tests.Helpers;
public class ReferencesResolverTests
{
    private readonly CustomFixture _fixture;
    private readonly ReferencesResolver _resolver;
    private const string LocalJsonFolder = "saves/scenes";
    private const string ResourceLocalPath = "Custom/torsoDecal.png";

    private VarPackageFile SceneVarFile => Build("MeshedVR.BonusScenes.9.var", "saves/scenes/scene.json", isInVamDir: true);
    private PotentialJsonFile PotentialJson => new(SceneVarFile.Var!);
    private Reference ReferenceVersionTwo => new("DJ.TanLines.2:/" + ResourceLocalPath, 0, 0, SceneVarFile);
    private Reference ReferenceVersionOne => new("DJ.TanLines.1:/" + ResourceLocalPath, 0, 0, SceneVarFile);
    private Reference ReferenceVersionLatest => new("DJ.TanLines.latest:/" + ResourceLocalPath, 0, 0, SceneVarFile);

    private VarPackageFile DJTanLinesOneVarFile => Build("DJ.TanLines.1.var", ResourceLocalPath, isInVamDir: true);
    private VarPackageFile DJTanLinesTwoVarFile => Build("DJ.TanLines.2.var", ResourceLocalPath, isInVamDir: true);

    public ReferencesResolverTests()
    {
        _fixture = new CustomFixture();
        _resolver = _fixture.Create<ReferencesResolver>();
    }

    [Fact]
    public async Task Scan_ShouldReturnNull_WhenReferencedVarDoesntExistAtAll()
    {
        await Init();

        var result = _resolver.ScanPackageSceneReference(PotentialJson, ReferenceVersionOne, varToSearch: null, localSceneFolder: LocalJsonFolder);
        result.Should().BeNull();

        result = _resolver.ScanPackageSceneReference(PotentialJson, ReferenceVersionTwo, varToSearch: null, localSceneFolder: LocalJsonFolder);
        result.Should().BeNull();

        result = _resolver.ScanPackageSceneReference(PotentialJson, ReferenceVersionLatest, varToSearch: null, localSceneFolder: LocalJsonFolder);
        result.Should().BeNull();
    }

    [Fact]
    public async Task Scan_ShouldReturnVersionTwo_WhenReferencedByLatest_BothVersionsExist()
    {
        await Init(DJTanLinesOneVarFile.Var!, DJTanLinesTwoVarFile.Var!);

        var result = _resolver.ScanPackageSceneReference(PotentialJson, ReferenceVersionLatest, varToSearch: null, localSceneFolder: LocalJsonFolder);
        result!.ToVarFile!.Var!.Name.Should().Be(DJTanLinesTwoVarFile.Var!.Name);
    }

    [Fact]
    public async Task Scan_ShouldReturnVersionTwo_WhenReferencedByVersionTwo_BothVersionsExist()
    {
        await Init(DJTanLinesOneVarFile.Var!, DJTanLinesTwoVarFile.Var!);

        var result = _resolver.ScanPackageSceneReference(PotentialJson, ReferenceVersionTwo, varToSearch: null, localSceneFolder: LocalJsonFolder);
        result!.ToVarFile!.Var!.Name.Should().Be(DJTanLinesTwoVarFile.Var!.Name);
    }

    [Fact]
    public async Task Scan_ShouldReturnVersionOne_WhenReferencedByVersionOne_BothVersionsExist()
    {
        await Init(DJTanLinesOneVarFile.Var!, DJTanLinesTwoVarFile.Var!);

        var result = _resolver.ScanPackageSceneReference(PotentialJson, ReferenceVersionOne, varToSearch: null, localSceneFolder: LocalJsonFolder);
        result!.ToVarFile!.Var!.Name.Should().Be(DJTanLinesOneVarFile.Var!.Name);
    }

    [Fact]
    public async Task Scan_ShouldReturnVersionOne_WhenReferencedByVersionOne_OnlyFirstVersionExists()
    {
        await Init(DJTanLinesOneVarFile.Var!);

        var result = _resolver.ScanPackageSceneReference(PotentialJson, ReferenceVersionOne, varToSearch: null, localSceneFolder: LocalJsonFolder);
        result!.ToVarFile!.Var!.Name.Should().Be(DJTanLinesOneVarFile.Var!.Name);
    }

    [Fact]
    public async Task Scan_ShouldReturnVersionOne_WhenReferencedByVersionTwo_OnlyFirstVersionExists()
    {
        await Init(DJTanLinesOneVarFile.Var!);

        var result = _resolver.ScanPackageSceneReference(PotentialJson, ReferenceVersionTwo, varToSearch: null, localSceneFolder: LocalJsonFolder);
        result!.ToVarFile!.Var!.Name.Should().Be(DJTanLinesOneVarFile.Var!.Name);
    }

    [Fact]
    public async Task Scan_ShouldReturnVersionOne_WhenReferencedByLatest_OnlyFirstVersionExists()
    {
        await Init(DJTanLinesOneVarFile.Var!);

        var result = _resolver.ScanPackageSceneReference(PotentialJson, ReferenceVersionLatest, varToSearch: null, localSceneFolder: LocalJsonFolder);
        result!.ToVarFile!.Var!.Name.Should().Be(DJTanLinesOneVarFile.Var!.Name);
    }

    [Fact]
    public async Task Scan_ShouldReturnVersionTwo_WhenReferencedByVersionOne_OnlySecondVersionExists()
    {
        await Init(DJTanLinesTwoVarFile.Var!);

        var result = _resolver.ScanPackageSceneReference(PotentialJson, ReferenceVersionOne, varToSearch: null, localSceneFolder: LocalJsonFolder);
        result!.ToVarFile!.Var!.Name.Should().Be(DJTanLinesTwoVarFile.Var!.Name);
    }

    [Fact]
    public async Task Scan_ShouldReturnVersionTwo_WhenReferencedByVersionTwo_OnlySecondVersionExists()
    {
        await Init(DJTanLinesTwoVarFile.Var!);

        var result = _resolver.ScanPackageSceneReference(PotentialJson, ReferenceVersionTwo, varToSearch: null, localSceneFolder: LocalJsonFolder);
        result!.ToVarFile!.Var!.Name.Should().Be(DJTanLinesTwoVarFile.Var!.Name);
    }

    [Fact]
    public async Task Scan_ShouldReturnVersionTwo_WhenReferencedByLatest_OnlySecondVersionExists()
    {
        await Init(DJTanLinesTwoVarFile.Var!);

        var result = _resolver.ScanPackageSceneReference(PotentialJson, ReferenceVersionLatest, varToSearch: null, localSceneFolder: LocalJsonFolder);

        result!.ToVarFile!.Var!.Name.Should().Be(DJTanLinesTwoVarFile.Var!.Name);
    }

    private static VarPackageFile Build(string varName, string localFilePath, bool isInVamDir)
    {
        VarPackageName.TryGet(varName, out var name);
        var varPackage = new VarPackage(name!, "D:/" + name!.Filename, null, isInVamDir, 1, DateTime.UtcNow);
        return new VarPackageFile(localFilePath, isInVamDir, varPackage, 1);
    }

    private Task Init(params VarPackage[] packages)
    {
        return _resolver.InitLookups([], new List<VarPackage>(packages), []);
    }
}
