using FluentAssertions;
using FluentAssertions.Execution;
using VamToolbox.Models;
using Xunit;

namespace VamToolbox.Tests.Models;
public class VarPackageFileTests
{
    [Theory, CustomAutoData]
    public void Create_ShouldInitAllProperties(long size, bool isInVamDir, VarPackage varPackage)
    {
        var fakeLocalPath = @"q\e/smtH.assetbundlE";
        var file = new VarPackageFile(fakeLocalPath, isInVamDir, varPackage, size);

        using var _ = new AssertionScope();
        file.ParentVar.Should().Be(varPackage);
        file.LocalPath.Should().Be("q/e/smtH.assetbundlE");
        file.Size.Should().Be(size);
        file.IsInVaMDir.Should().Be(true);
        file.IsVar.Should().BeTrue();
        file.Children.Should().BeEmpty();
        file.SelfAndChildren().Should().BeEquivalentTo(new[] { file });
        file.Dirty.Should().BeFalse();
        file.FilenameLower.Should().Be("smth.assetbundle");
        file.ExtLower.Should().Be(".assetbundle");
        file.FilenameWithoutExt.Should().Be("smtH");
        file.InternalId.Should().BeNullOrEmpty();
        file.MorphName.Should().BeNullOrEmpty();
        file.Var.Should().Be(varPackage);
        file.VarFile.Should().Be(file);
        file.Free.Should().BeNull();
        file.ToString().Should().Be($@"q/e/smtH.assetbundlE Var: {varPackage.FullPath}");
        file.ParentVar.Files.Should().BeEquivalentTo(new[] { file });
    }

    [Theory, CustomAutoData]
    public void Create_AddingChildFile(VarPackageFile varFile, VarPackageFile childFile)
    {
        varFile.AddChildren(childFile);

        varFile.Children.Should().BeEquivalentTo(new[] { childFile });
        varFile.SelfAndChildren().Should().BeEquivalentTo(new[] { childFile, varFile });
        varFile.SizeWithChildren.Should().Be(varFile.Size + childFile.Size);
    }

    [Theory, CustomAutoData]
    public void Create_NestedChildrenShouldIterateCorrectly(VarPackageFile freeFile, VarPackageFile childFile, VarPackageFile childChildFile)
    {
        freeFile.AddChildren(childFile);
        childFile.AddChildren(childChildFile);

        freeFile.Children.Should().BeEquivalentTo(new[] { childFile });
        freeFile.SelfAndChildren().Should().BeEquivalentTo(new[] { childFile, freeFile, childChildFile });
        freeFile.SizeWithChildren.Should().Be(freeFile.Size + childFile.Size + childChildFile.Size);

        childFile.Children.Should().BeEquivalentTo(new[] { childChildFile });
        childFile.SelfAndChildren().Should().BeEquivalentTo(new[] { childFile, childChildFile });
        childFile.SizeWithChildren.Should().Be(childFile.Size + childChildFile.Size);
    }

    [Theory, CustomAutoData]
    public void Create_AddingMissingChildren(VarPackageFile varFile, string missingChild)
    {
        varFile.AddMissingChildren(missingChild);

        varFile.MissingChildren.Should().BeEquivalentTo(missingChild);
    }
}
