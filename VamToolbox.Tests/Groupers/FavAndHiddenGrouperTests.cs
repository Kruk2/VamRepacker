using AutoFixture;
using FluentAssertions;
using FluentAssertions.Execution;
using VamToolbox.FilesGrouper;
using VamToolbox.Models;
using Xunit;

namespace VamToolbox.Tests.Groupers;

public class FavAndHiddenGrouperTests
{
    private readonly FavAndHiddenGrouper _grouper;
    private readonly CustomFixture _fixture;

    public FavAndHiddenGrouperTests()
    {
        _fixture = new();
        _grouper = _fixture.Create<FavAndHiddenGrouper>();
    }

    [Fact]
    public async Task GroupMorphs_WhenFavMorphFileIsMatching_ShouldGroupIt()
    {
        var freeFiles = new List<FreeFile>();
        var varPackage = _fixture.Create<VarPackage>();
        var varFile1 = CreateVarFile(varPackage, KnownNames.MaleMorphsDir + "/differntName.vmi", morphName: "Ass");
        var varFileWithoutFav = CreateVarFile(varPackage, KnownNames.MaleMorphsDir + "/Ass1.vmi");
        CreateFile(freeFiles, KnownNames.MaleMorphsDir + "/favorites/Ass.fav");
        var varFile2 = CreateVarFile(varPackage, KnownNames.FemaleMorphsDir + "/Ass.vmb");
        CreateFile(freeFiles, KnownNames.FemaleMorphsDir + "/favorites/Ass.fav");
        var freeFileMorph = CreateFile(freeFiles, KnownNames.FemaleMorphsDir + "/Ass.vmb");
        var freeFileMorphWithoutFav = CreateFile(freeFiles, KnownNames.FemaleGenMorphsDir + "/Ass1.vmb");

        await _grouper.Group(freeFiles, new List<VarPackage> { varPackage });

        using var _ = new AssertionScope();
        freeFiles.Should().NotContain(t => t.ExtLower == ".fav");
        varFile1.FavFilePath.Should().Be(KnownNames.MaleMorphsDir + "/favorites/Ass.fav");
        varFile2.FavFilePath.Should().Be(KnownNames.FemaleMorphsDir + "/favorites/Ass.fav");
        freeFileMorph.FavFilePath.Should().Be(KnownNames.FemaleMorphsDir + "/favorites/Ass.fav");
        freeFileMorphWithoutFav.FavFilePath.Should().BeNull();
        varFileWithoutFav.FavFilePath.Should().BeNull();
    }

    private VarPackageFile CreateVarFile(VarPackage package, string localFile, string? morphName = null) => new VarPackageFile(localFile, false, package, 0) {
        MorphName = morphName ?? Path.GetFileNameWithoutExtension(localFile)
    };

    private FreeFile CreateFile(List<FreeFile> freeFiles, string localFile)
    {
        var file = new FreeFile("", localFile, 0, false, DateTime.Now, null);
        file.MorphName = file.FilenameWithoutExt;
        freeFiles.Add(file);
        return file;
    }
}
