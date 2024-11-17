using System.Text;
using AutoFixture;
using FluentAssertions;
using FluentAssertions.Execution;
using VamToolbox.FilesGrouper;
using VamToolbox.Models;
using Xunit;

namespace VamToolbox.Tests.Groupers;
public class PresetGrouperTests
{
    private readonly PresetGrouper _grouper;

    public PresetGrouperTests()
    {
        var fixture = new CustomFixture();
        _grouper = fixture.Create<PresetGrouper>();
    }

    [Fact]
    public async Task Group_GivenTwoSetOfFilesGroupThem()
    {
        var fileGroup1 = CreateGroup("a");
        var fileGroup2 = CreateGroup("b");
        var files = fileGroup1.Concat(fileGroup2).ToList();

        await _grouper.GroupPresets(files, StreamOpener);

        using var _ = new AssertionScope();
        var parentFile = files.Single(t => t.FilenameLower == "a.vam");
        var parentFile2 = files.Single(t => t.FilenameLower == "b.vam");
        
        parentFile.Children.Should().HaveCount(3);
        parentFile.Children.Should().ContainSingle(t => t.FilenameLower == "a.vaj");
        parentFile.Children.Should().ContainSingle(t => t.FilenameLower == "a.vab");
        parentFile.Children.Should().ContainSingle(t => t.FilenameLower == "a.jpg");

        parentFile2.Children.Should().HaveCount(3);
        parentFile2.Children.Should().ContainSingle(t => t.FilenameLower == "b.vaj");
        parentFile2.Children.Should().ContainSingle(t => t.FilenameLower == "b.vab");
        parentFile2.Children.Should().ContainSingle(t => t.FilenameLower == "b.jpg");

        var preset1File1 = files.Single(t => t.FilenameLower == "a_preset1.vap");
        var preset1File2 = files.Single(t => t.FilenameLower == "a_preset2.vap");
        preset1File1.Children.Should().HaveCount(2);
        preset1File1.Children.Should().ContainSingle(t => t.FilenameLower == "a_preset1.png");
        preset1File1.Children.Should().Contain(parentFile);
        preset1File2.Children.Should().HaveCount(2);
        preset1File2.Children.Should().ContainSingle(t => t.FilenameLower == "a_preset2.jpeg");
        preset1File2.Children.Should().Contain(parentFile);

        var preset2File1 = files.Single(t => t.FilenameLower == "b_preset1.vap");
        var preset2File2 = files.Single(t => t.FilenameLower == "b_preset2.vap");
        preset2File1.Children.Should().HaveCount(2);
        preset2File1.Children.Should().ContainSingle(t => t.FilenameLower == "b_preset1.png");
        preset2File1.Children.Should().Contain(parentFile2);
        preset2File2.Children.Should().HaveCount(2);
        preset2File2.Children.Should().ContainSingle(t => t.FilenameLower == "b_preset2.jpeg");
        preset2File2.Children.Should().Contain(parentFile2);
    }

    [Fact]
    public async Task Group_MissingVajAndVabFiles_ShouldGroupNothing()
    {
        var fileGroup = CreateGroup("a");
        fileGroup.RemoveAll(t => t.ExtLower != ".vam");

        await _grouper.GroupPresets(fileGroup, StreamOpener);

        using var _ = new AssertionScope();
        var parentFile = fileGroup.Single(t => t.FilenameLower == "a.vam");

        parentFile.Children.Should().BeEmpty();
    }

    [Fact]
    public async Task Group_MissingVamFileShouldGroupTwoRemaining()
    {
        var fileGroup = CreateGroup("a");
        fileGroup.RemoveAll(t => t.ExtLower == ".vam");

        await _grouper.GroupPresets(fileGroup, StreamOpener);

        using var _ = new AssertionScope();
        var parentFile = fileGroup.Single(t => t.FilenameLower == "a.vaj");

        parentFile.Children.Should().HaveCount(2);
        parentFile.Children.Should().ContainSingle(t => t.FilenameLower == "a.vab");
        parentFile.Children.Should().ContainSingle(t => t.FilenameLower == "a.jpg");
    }

    [Fact]
    public async Task Group_ShouldReadInternalId()
    {
        var fileGroup = CreateGroup("a");

        await _grouper.GroupPresets(fileGroup, _ => StreamOpener(@"{""uid"": ""test""}"));

        using var _ = new AssertionScope();
        var parentFile = fileGroup.Single(t => t.FilenameLower == "a.vam");
        parentFile.InternalId.Should().Be("test");
    }


    [Fact]
    public async Task Group_WhenVamFileIsInvalid_ShouldNotCrash()
    {
        var fileGroup = CreateGroup("a");

        await _grouper.GroupPresets(fileGroup, _ => StreamOpener(@"invalid"));

        using var _ = new AssertionScope();
        var parentFile = fileGroup.Single(t => t.FilenameLower == "a.vam");
        parentFile.InternalId.Should().BeEmpty();
    }

    private static Stream StreamOpener(string? data = null)
    {
        var ms = new MemoryStream();
        ms.Write(Encoding.UTF8.GetBytes(data ?? "{}"));
        ms.Position = 0;
        return ms;
    }

    private List<FreeFile> CreateGroup(string fileName)
    {
        return [
            new FreeFile("whatever", fileName + ".vab", 0, false, DateTime.Now, null),
            new FreeFile("whatever", fileName + ".vaj", 0, false, DateTime.Now, null),
            new FreeFile("whatever", fileName + ".vam", 0, false, DateTime.Now, null),
            new FreeFile("whatever", fileName + ".jpg", 0, false, DateTime.Now, null),
            new FreeFile("whatever", fileName + "_preset1.vap", 0, false, DateTime.Now, null),
            new FreeFile("whatever", fileName + "_preset1.png", 0, false, DateTime.Now, null),
            new FreeFile("whatever", fileName + "_preset2.vap", 0, false, DateTime.Now, null),
            new FreeFile("whatever", fileName + "_preset2.jpeg", 0, false, DateTime.Now, null)
        ];
    }
}
