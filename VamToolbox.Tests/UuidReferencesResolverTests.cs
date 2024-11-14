using FluentAssertions;
using VamToolbox.Helpers;
using VamToolbox.Models;
using Xunit;
using AutoFixture;

namespace VamToolbox.Tests;

public class UuidReferencesResolverTests
{
    private readonly UuidReferencesResolver _resolver = new();

    private readonly Reference _reference;
    private readonly JsonFile _jsonFile;
    private readonly List<FreeFile> _freeFiles;
    private readonly List<VarPackage> _vars;

    public UuidReferencesResolverTests()
    {
        var fixture = new CustomFixture();
        _reference = fixture.Create<Reference>();
        _jsonFile = fixture.Create<JsonFile>();
        _freeFiles = fixture.CreateMany<FreeFile>().ToList();
        _vars = fixture.CreateMany<VarPackage>().ToList();

        _resolver.InitLookups(Enumerable.Empty<FreeFile>(), Enumerable.Empty<VarPackage>()).GetAwaiter().GetResult();
    }

    [Fact]
    public void Resolve_NoMatchingUuids_ShouldReturnNothing()
    {
        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, _reference, fallBackResolvedAsset: null);

        jsonReference.Should().BeNull();
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_MatchingMaleMorphAgainstFemale_ShouldReturnNothing()
    {
        var reference = new Reference(KnownNames.FemaleMorphsDir + "morph.vmi", 0, 0, _freeFiles.First());
        reference.MorphName = "internal id";
        var matchedFile = CreateFile(KnownNames.MaleMorphsDir + "/morph.vmi");
        matchedFile.MorphName = reference.MorphName!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, reference, fallBackResolvedAsset: null);

        jsonReference.Should().BeNull();
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_MatchingMaleAgainstFemale_WithFallbackReference_ShouldReturnFallbackReference()
    {
        var fallbackReference = CreateFile(KnownNames.MaleMorphsDir + "/morph.vmi", isInVamDir: false);
        var reference = new Reference(KnownNames.FemaleMorphsDir + "morph.vmi", 0, 0, _freeFiles.First());
        reference.MorphName = "internal id";
        var matchedFile = CreateFile(KnownNames.MaleMorphsDir + "/morph.vmi");
        matchedFile.MorphName = reference.MorphName!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, reference, fallBackResolvedAsset: fallbackReference);

        jsonReference!.ToFile.Should().Be(fallbackReference);
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_MatchingFemaleMorphAgainstMale_ShouldReturnNothing()
    {
        var reference = new Reference(KnownNames.MaleMorphsDir + "morph.vmi", 0, 0, _freeFiles.First());
        reference.MorphName = "internal id";
        var matchedFile = CreateFile(KnownNames.FemaleMorphsDir + "/morph.vmi");
        matchedFile.MorphName = reference.MorphName!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, reference, fallBackResolvedAsset: null);

        jsonReference.Should().BeNull();
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_MatchingMaleGenMorphAgainstMale_ShouldMatch()
    {
        var reference = new Reference(KnownNames.MaleGenMorphsDir + "morph.vmi", 0, 0, _freeFiles.First());
        reference.MorphName = "internal id";
        var matchedFile = CreateFile(KnownNames.MaleMorphsDir + "/morph.vmi");
        matchedFile.MorphName = reference.MorphName!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, reference, fallBackResolvedAsset: null);

        jsonReference!.ToFile.Should().Be(matchedFile);
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_MatchingFemaleGenMorphAgainstFemale_ShouldReturnNothing()
    {
        var reference = new Reference(KnownNames.FemaleGenMorphsDir + "morph.vmi", 0, 0, _freeFiles.First());
        reference.MorphName = "internal id";
        var matchedFile = CreateFile(KnownNames.FemaleMorphsDir + "/morph.vmi");
        matchedFile.MorphName = reference.MorphName!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, reference, fallBackResolvedAsset: null);

        jsonReference!.ToFile.Should().Be(matchedFile);
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_NoMatchingUuids_WhenFileInputMorphOutsideMorphDirectory_ShouldReturnNothing()
    {
        var matchedFile = CreateFile(KnownNames.FemaleClothDir + "/morph.vmi");
        matchedFile.MorphName = _reference.MorphName!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, _reference, fallBackResolvedAsset: null);

        jsonReference.Should().BeNull();
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_SingleMatchingUuid_WhenReferencedAssetHasUnknownGender_ShouldReturnMatchingUuid()
    {
        var reference = new Reference(string.Empty, 0, 0, _freeFiles.First());
        reference.MorphName = "internal id";
        var matchedFile = CreateFile(KnownNames.MaleMorphsDir + "/morph.vmi");
        matchedFile.MorphName = reference.MorphName!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, reference, fallBackResolvedAsset: null);

        jsonReference!.ToFile.Should().Be(matchedFile);
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_SingleMatchingUuid_WhenReferencedAssetHasFemaleGenderInWeirdDirectory_ShouldReturnMatchingUuid()
    {
        var reference = new Reference("custom/female/morph", 0, 0, _freeFiles.First());
        reference.MorphName = "internal id";
        var matchedFile = CreateFile(KnownNames.FemaleMorphsDir + "/morph.vmi");
        matchedFile.MorphName = reference.MorphName!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, reference, fallBackResolvedAsset: null);

        jsonReference!.ToFile.Should().Be(matchedFile);
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_SingleMatchingUuid_WhenReferencedAssetHasMaleGenderInWeirdDirectory_ShouldReturnMatchingUuid()
    {
        var reference = new Reference("custom/male/morph", 0, 0, _freeFiles.First());
        reference.MorphName = "internal id";
        var matchedFile = CreateFile(KnownNames.MaleMorphsDir + "/morph.vmi");
        matchedFile.MorphName = reference.MorphName!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, reference, fallBackResolvedAsset: null);

        jsonReference!.ToFile.Should().Be(matchedFile);
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_FemaleAndMaleMatchingUuids_WhenReferencedAssetHasUnknownGender_ShouldReturnFemaleUuid()
    {
        var reference = new Reference(string.Empty, 0, 0, _freeFiles.First());
        reference.MorphName = "internal id";
        var matchedFile = CreateFile(KnownNames.FemaleMorphsDir + "/morph.vmi");
        matchedFile.MorphName = reference.MorphName!;
        _freeFiles.Add(matchedFile);
        var notMatchedFile = CreateFile(KnownNames.MaleMorphsDir + "/morph.vmi");
        notMatchedFile.MorphName = reference.MorphName!;
        _freeFiles.Add(notMatchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, reference, fallBackResolvedAsset: null);

        jsonReference!.ToFile.Should().Be(matchedFile);
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public void Resolve_NoMatchingUuidsWithFallBackReference_ShouldReturnFallbackReference()
    {
        var fallbackReference = CreateFile(KnownNames.FemaleGenMorphsDir + "morph.vmi", isInVamDir: false);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, _reference, fallbackReference);

        jsonReference.Should().NotBeNull();
        jsonReference!.ToFile.Should().Be(fallbackReference);
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_MatchingUuidsHaveDifferentSizeThanFallback_ShouldReturnFallbackReference()
    {
        var fallbackReference = CreateFile(KnownNames.FemaleGenMorphsDir + "morph.vmi", size: 10);
        var matchedFile = CreateFile(KnownNames.FemaleGenMorphsDir + "morph.vmi", size: 5);
        matchedFile.MorphName = _reference.MorphName!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, _reference, fallbackReference);

        jsonReference!.ToFile.Should().Be(fallbackReference);
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_FallbackReference_WhenInVamDir_ShouldReturnFallbackReference()
    {
        var fallbackReference = CreateFile(KnownNames.FemaleGenMorphsDir + "morph.vmi");
        var matchedFile = CreateFile(KnownNames.FemaleGenMorphsDir + "morph.vmi", isInVamDir: false);
        matchedFile.MorphName = _reference.MorphName!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, _reference, fallbackReference);

        jsonReference!.ToFile.Should().Be(fallbackReference);
        isDelayed.Should().BeFalse();
    }


    [Theory, CustomAutoData]
    public async Task Resolve_FallbackVarFileReference_WhenInVamDir_ShouldReturnFallbackReference(VarPackage package)
    {
        var fallbackReference = CreateVarFile(KnownNames.FemaleGenMorphsDir + "morph.vmi", package);
        var matchedFile = CreateFile(KnownNames.FemaleGenMorphsDir + "morph.vmi", isInVamDir: false);
        matchedFile.MorphName = _reference.MorphName!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, _reference, fallbackReference);

        jsonReference!.ToFile.Should().Be(fallbackReference);
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_MatchingOneUuid_ShouldReturnExactMatch()
    {
        var matchedFile = CreateFile(KnownNames.FemaleMorphsDir + "/morph.vmi");
        matchedFile.MorphName = _reference.MorphName!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, _reference, fallBackResolvedAsset: null);

        jsonReference!.ToFile.Should().Be(matchedFile);
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task Resolve_TwoMatchingUuid_WhenOneIsOutsideVamDir_ShouldReturnExactMatch()
    {
        var matchedFile = CreateFile(KnownNames.FemaleMorphsDir + "/morph.vmi");
        matchedFile.MorphName = _reference.MorphName!;
        var matchedFile2 = CreateFile(KnownNames.FemaleGenMorphsDir + "/morph.vmi", isInVamDir: false);
        matchedFile2.MorphName = _reference.MorphName!;

        _freeFiles.Add(matchedFile);
        _freeFiles.Add(matchedFile2);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, _reference, fallBackResolvedAsset: null);

        jsonReference!.ToFile.Should().Be(matchedFile);
        isDelayed.Should().BeFalse();
    }


    [Fact]
    public async Task Resolve_TwoMatchingUuid_ShouldReturnDelayedFlag()
    {
        var matchedFile = CreateFile(KnownNames.FemaleMorphsDir + "/morph.vmi");
        matchedFile.MorphName = _reference.MorphName!;
        var matchedFile2 = CreateFile(KnownNames.FemaleGenMorphsDir + "/morph.vmi");
        matchedFile2.MorphName = _reference.MorphName!;

        _freeFiles.Add(matchedFile);
        _freeFiles.Add(matchedFile2);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchMorphJsonReferenceByName(_jsonFile, _reference, fallBackResolvedAsset: null);

        jsonReference.Should().BeNull();
        isDelayed.Should().BeTrue();
    }

    [Fact]
    public async Task ResolvePreset_MaleHairReferenceMatchedInFemaleHairDirectory_ShouldReturnNothing()
    {
        var matchedFile = CreateFile(KnownNames.MaleHairDir + "/hair.vam");
        var reference = new Reference(KnownNames.FemaleHairDir + "hair.vam", 0, 0, _freeFiles.First());
        reference.InternalId = "internal id";
        matchedFile.InternalId = reference.InternalId!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchVamJsonReferenceById(_jsonFile, reference, fallBackResolvedAsset: null);

        jsonReference.Should().BeNull();
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task ResolvePreset_MaleClothReferenceMatchedInFemaleClothDirectory_ShouldReturnNothing()
    {
        var matchedFile = CreateFile(KnownNames.MaleClothDir + "/cloth.vam");
        var reference = new Reference(KnownNames.FemaleClothDir + "cloth.vam", 0, 0, _freeFiles.First());
        reference.InternalId = "internal id";
        matchedFile.InternalId = reference.InternalId!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchVamJsonReferenceById(_jsonFile, reference, fallBackResolvedAsset: null);

        jsonReference.Should().BeNull();
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task ResolvePreset_HairFileMatchedOutsideHairDirectory_ShouldReturnNothing()
    {
        var matchedFile = CreateFile(KnownNames.FemaleMorphsDir + "/hair.vam");
        matchedFile.InternalId = _reference.InternalId!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchVamJsonReferenceById(_jsonFile, _reference, fallBackResolvedAsset: null);

        jsonReference.Should().BeNull();
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task ResolvePreset_ClothFileMatchedOutsideClothDirectory_ShouldReturnNothing()
    {
        var matchedFile = CreateFile(KnownNames.FemaleMorphsDir + "/cloth.vam");
        matchedFile.InternalId = _reference.InternalId!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchVamJsonReferenceById(_jsonFile, _reference, fallBackResolvedAsset: null);

        jsonReference.Should().BeNull();
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task ResolvePreset_SingleHairFileMatched_ShouldReturnIt()
    {
        var matchedFile = CreateFile(KnownNames.FemaleHairDir + "/hair.vam");
        matchedFile.InternalId = _reference.InternalId!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchVamJsonReferenceById(_jsonFile, _reference, fallBackResolvedAsset: null);

        jsonReference!.ToFile.Should().Be(matchedFile);
        isDelayed.Should().BeFalse();
    }

    [Fact]
    public async Task ResolvePreset_SingleClothFileMatched_ShouldReturnIt()
    {
        var matchedFile = CreateFile(KnownNames.FemaleClothDir + "/cloth.vam");
        matchedFile.InternalId = _reference.InternalId!;
        _freeFiles.Add(matchedFile);
        await _resolver.InitLookups(_freeFiles, _vars);

        var (jsonReference, isDelayed) = _resolver.MatchVamJsonReferenceById(_jsonFile, _reference, fallBackResolvedAsset: null);

        jsonReference!.ToFile.Should().Be(matchedFile);
        isDelayed.Should().BeFalse();
    }

    private FreeFile CreateFile(string localPath, bool isInVamDir = true, long size = 1) => new("a", localPath, size, isInVamDir, DateTime.Now, softLinkPath: null);
    private VarPackageFile CreateVarFile(string localPath, VarPackage varPackage, bool isInVamDir = true) => new(localPath, isInVamDir, varPackage, 1);
}