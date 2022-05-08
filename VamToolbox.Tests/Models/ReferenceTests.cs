﻿using FluentAssertions;
using VamToolbox.Helpers;
using VamToolbox.Models;
using Xunit;

namespace VamToolbox.Tests.Models;
public class ReferenceTests 
{

    [Theory]
    [InlineData("a.1:/Custom/a.png", ".png")]
    [InlineData("SELF:/Custom/a.Png", ".png")]
    [InlineData("Custom/a.pNg", ".png")]
    void Create_FileExtension(string value, string ext)
    {
        var reference = Create(value);

        reference.EstimatedExtension.Should().Be(ext);
    }

    [Theory]
    [InlineData("a.1:/Custom/a.vmi", AssetType.UnknownMorph)]
    [InlineData("SELF:/Custom/a.vaj", AssetType.UnknownClothOrHair)]
    [InlineData("Custom/a.vmb", AssetType.UnknownMorph)]
    [InlineData("Custom/a.xxx", AssetType.Unknown)]
    void Create_AssetType(string value, AssetType expectedType)
    {
        var reference = Create(value);

        reference.EstimatedAssetType.Should().Be(expectedType);
    }

    [Theory]
    [InlineData("a.1:/Custom\\a.vmi", "Custom/a.vmi")]
    [InlineData("AddonPackages/a.1.var:/Custom\\a.vmi", "Custom/a.vmi")]
    [InlineData("SELF:/Custom\\a.vmi", "Custom/a.vmi")]
    [InlineData("SELF:\\Custom\\a.vmi", "Custom/a.vmi")]
    [InlineData("SELF:/SELF:/a.jpg", "a.jpg")]
    [InlineData("clothing:JaxZoa.JaxEffects.latest:/Custom/Jax_Effects_CumCornerRight.vam", "Custom/Jax_Effects_CumCornerRight.vam")]
    void Create_ReferenceLocation(string value, string expectedLocation)
    {
        var reference = Create(value);

        reference.EstimatedReferenceLocation.Should().Be(expectedLocation);
    }

    [Theory]
    [InlineData("a.b.1:/Custom\\a.vmi", "a.b.1.var")]
    [InlineData("AddonPackages/a.b.1.var:/Custom\\a.vmi", "a.b.1.var")]
    [InlineData("SELF:/Custom\\a.vmi", null)]
    [InlineData("SELF:\\Custom\\a.vmi", null)]
    [InlineData("SELF:/SELF:/a.jpg", null)]
    [InlineData("clothing:JaxZoa.JaxEffects.latest:/Custom/Jax_Effects_CumCornerRight.vam", "JaxZoa.JaxEffects.latest.var")]
    [InlineData("toggle:JaxZoa.JaxEffects.latest:/Custom/Jax_Effects_CumCornerRight.vam", "JaxZoa.JaxEffects.latest.var")]
    void Create_VarName(string value, string? expectedVarName)
    {
        var reference = Create(value);

        if (expectedVarName is null) {
            reference.EstimatedVarName.Should().BeNull();
        } else {
            reference.EstimatedVarName!.Filename.Should().Be(expectedVarName);
        }
    }

    Reference Create(string value) => new Reference(value, 0, 0, new FreeFile("", "", 1, false, DateTime.Now));
}
