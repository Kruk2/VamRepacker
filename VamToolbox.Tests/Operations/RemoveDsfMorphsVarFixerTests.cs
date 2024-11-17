using AutoFixture;
using FluentAssertions;
using Ionic.Zip;
using VamToolbox.Models;
using VamToolbox.Operations.Destructive.VarFixers;
using Xunit;

namespace VamToolbox.Tests.Operations;

public class RemoveDsfMorphsVarFixerTests
{
    private readonly CustomFixture _fixture;
    private readonly RemoveDsfMorphsVarFixer _fixer;

    public RemoveDsfMorphsVarFixerTests()
    {
        _fixture = new CustomFixture();
        _fixer = _fixture.Create<RemoveDsfMorphsVarFixer>();
    }

    [Theory, CustomAutoData]
    public void Fix_WhenVarDoesntContainDsfMorphs_ShouldReturnFalse(VarPackage var)
    {
        using var zipFile = CreateZip("some_morph.vmb", "other_morph.vmi");

        var result = RunFixer(var, zipFile);

        result.Should().BeFalse();
        zipFile.EntryFileNames.Count.Should().Be(2);
    }

    [Theory, CustomAutoData]
    public void Fix_WhenVarContainsDsfMorphs_ShouldDeleteThem(VarPackage var)
    {
        var morphs = _fixture.CreateMany<string>().Select(t => t + ".DSF").ToList();
        morphs.AddRange(_fixture.CreateMany<string>().Select(t => t + ".dsf"));
        morphs.AddRange(["some_morph.vmb", "other_morph.vmi"]);
        using var zipFile = CreateZip(morphs.ToArray());

        var result = RunFixer(var, zipFile);

        result.Should().BeTrue();
        zipFile.EntryFileNames.Count.Should().Be(2);
        zipFile.EntryFileNames.Should().OnlyContain(t => t.EndsWith("some_morph.vmb") || t.EndsWith("other_morph.vmi"));
    }

    private bool RunFixer(VarPackage var, ZipFile zipFile) => _fixer.Process(var, zipFile, new Lazy<IDictionary<string, object>?>());

    private ZipFile CreateZip(params string[] morphs)
    {
        var files = morphs.ToDictionary(t => KnownNames.FemaleMorphsDir + $"/{t}");
        var zipFile = ZipTestHelpers.CreateZipFile(files);
        return ZipFile.Read(zipFile);
    }
}