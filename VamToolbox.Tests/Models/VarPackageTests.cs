using FluentAssertions;
using FluentAssertions.Execution;
using VamToolbox.Models;
using Xunit;

namespace VamToolbox.Tests.Models;
public class VarPackageTests
{
    [Theory, CustomAutoData]
    public void Create_ShouldInitAllProperties(VarPackageName varName, long size, bool isInVamDir)
    {
        var fakePath = @"C:/a\q/e\smtH.assetbundlE";
        var now = DateTime.UtcNow;
        var varPackage = new VarPackage(varName, fakePath, softLinkPath: null, isInVamDir, size, now);

        using var _ = new AssertionScope();
        varPackage.FullPath.Should().Be("C:/a/q/e/smtH.assetbundlE");
        varPackage.Size.Should().Be(size);
        varPackage.IsInVaMDir.Should().Be(true);
        varPackage.Modified.Should().Be(now);
    }

    [Theory, CustomAutoData]
    public void Create_AddingVarFiles(VarPackage varPackage)
    {
        var varFile1 = CreateFile("a", varPackage);
        var varFile2 = CreateFile("A", varPackage);

        using var _ = new AssertionScope();
        varPackage.Files.Should().BeEquivalentTo(new[] { varFile1, varFile2 });
        varPackage.FilesDict.Should().HaveCount(1);
        varPackage.FilesDict.Should().ContainKey("a");
        varPackage.FilesDict.Should().ContainValue(varFile1);
    }

    [Theory, CustomAutoData]
    public void IsMorphPack_WhenOnlyContainsMorphs_ShouldBeTrue(VarPackage varPackage)
    {
        CreateFile(KnownNames.MaleMorphsDir + "/test.vmb", varPackage);
        CreateFile(KnownNames.FemaleGenMorphsDir + "/test.vmi", varPackage);

        using var _ = new AssertionScope();
        varPackage.IsMorphPack.Should().BeTrue();
    }

    [Theory, CustomAutoData]
    public void IsMorphPack_WhenContainsMorphsAndOtherFiles_ShouldBeTrue(VarPackage varPackage)
    {
        CreateFile(KnownNames.MaleMorphsDir + "/test.vmb", varPackage);
        CreateFile(KnownNames.FemaleGenMorphsDir + "/test.q", varPackage);

        using var _ = new AssertionScope();
        varPackage.IsMorphPack.Should().BeFalse();
    }

    private static VarPackageFile CreateFile(string localPath, VarPackage varPackage)
    {
        return new VarPackageFile(localPath, false, varPackage, 1);
    }
}
