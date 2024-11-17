using FluentAssertions;
using FluentAssertions.Execution;
using VamToolbox.Models;
using Xunit;

namespace VamToolbox.Tests.Models;
public class FreeFileTests
{
    [Theory, CustomAutoData]
    public void Create_ShouldInitAllProperties(long size, DateTime modificationDate, bool isInVamDir)
    {
        var fakePath = @"C:/a\q/e\smtH.assetbundlE";
        var fakeLocalPath = @"q\e/smtH.assetbundlE";
        var file = new FreeFile(fakePath, fakeLocalPath, size, isInVamDir, modificationDate, softLinkPath: null);

        using var _ = new AssertionScope();
        file.FullPath.Should().Be("C:/a/q/e/smtH.assetbundlE");
        file.LocalPath.Should().Be("q/e/smtH.assetbundlE");
        file.Size.Should().Be(size);
        file.IsInVaMDir.Should().Be(true);
        file.IsVar.Should().BeFalse();
        file.Children.Should().BeEmpty();
        file.SelfAndChildren().Should().BeEquivalentTo([file]);
        file.Dirty.Should().BeFalse();
        file.FilenameLower.Should().Be("smth.assetbundle");
        file.ExtLower.Should().Be(".assetbundle");
        file.FilenameWithoutExt.Should().Be("smtH");
        file.InternalId.Should().BeNullOrEmpty();
        file.MorphName.Should().BeNullOrEmpty();
        file.ModifiedTimestamp.Should().Be(modificationDate);
        file.Var.Should().BeNull();
        file.VarFile.Should().BeNull();
        file.Free.Should().Be(file);
        file.ToString().Should().Be("C:/a/q/e/smtH.assetbundlE");
    }

    [Theory, CustomAutoData]
    public void Create_AddingChildFile(FreeFile freeFile, FreeFile childFile)
    {
        freeFile.AddChildren(childFile);

        freeFile.Children.Should().BeEquivalentTo([childFile]);
        freeFile.SelfAndChildren().Should().BeEquivalentTo([childFile, freeFile]);
        freeFile.SizeWithChildren.Should().Be(freeFile.Size + childFile.Size);
    }

    [Theory, CustomAutoData]
    public void Create_NestedChildrenShouldIterateCorrectly(FreeFile freeFile, FreeFile childFile, FreeFile childChildFile)
    {
        freeFile.AddChildren(childFile);
        childFile.AddChildren(childChildFile);

        freeFile.Children.Should().BeEquivalentTo([childFile]);
        freeFile.SelfAndChildren().Should().BeEquivalentTo([childFile, freeFile, childChildFile]);
        freeFile.SizeWithChildren.Should().Be(freeFile.Size + childFile.Size + childChildFile.Size);

        childFile.Children.Should().BeEquivalentTo([childChildFile]);
        childFile.SelfAndChildren().Should().BeEquivalentTo([childFile, childChildFile]);
        childFile.SizeWithChildren.Should().Be(childFile.Size + childChildFile.Size);
    }

    [Theory, CustomAutoData]
    public void Create_AddingMissingChildren(FreeFile freeFile, string missingChild)
    {
        freeFile.AddMissingChildren(missingChild);

        freeFile.MissingChildren.Should().BeEquivalentTo(missingChild);
    }
}
