using System.Diagnostics.CodeAnalysis;
using MoreLinq;
using VamToolbox.Sqlite;
using VamToolbox.Helpers;

namespace VamToolbox.Models;

public sealed class Reference
{
    public string Value { get; }
    public int Index { get; }
    public int Length { get; }

    // these are read from next line in JSON file
    public string? MorphName { get; set; }
    public string? InternalId { get; set; }

    public FileReferenceBase ForJsonFile { get; internal set; }
    public override string ToString() => $"{Value} at index {Index}";

    public Reference(string value, int index, int length, FileReferenceBase forJsonFile)
    {
        Value = value;
        Index = index;
        Length = length;
        ForJsonFile = forJsonFile;

        ParseReference();
    }

    public Reference(ReferenceEntry referenceEntry, FileReferenceBase forJsonFile)
    {
        Value = referenceEntry.Value!;
        InternalId = referenceEntry.InternalId;
        MorphName = referenceEntry.MorphName;
        Index = referenceEntry.Index;
        Length = referenceEntry.Length;
        ForJsonFile = forJsonFile;

        ParseReference();
    }

    public string EstimatedExtension { get; private set; } = null!;
    public string EstimatedReferenceLocation { get; private set; } = null!;
    public AssetType EstimatedAssetType { get; private set; }
    public VarPackageName? EstimatedVarName { get; private set; }
    public bool IsLocal { get; private set; }
    public bool IsSelf { get; private set; }
    [MemberNotNullWhen(true, nameof(EstimatedVarName))]
    public bool IsVar { get; private set; }

    private void ParseReference()
    {
        var refPathSplit = Value.Split(':');
        EstimatedReferenceLocation = refPathSplit.Last().NormalizeAssetPath();
        EstimatedExtension = '.' + EstimatedReferenceLocation.Split('.').Last().ToLowerInvariant();
        EstimatedAssetType = EstimatedExtension.ClassifyType(EstimatedReferenceLocation);

        if (refPathSplit[0].Equals("clothing", StringComparison.OrdinalIgnoreCase) ||
            refPathSplit[0].Equals("toggle", StringComparison.OrdinalIgnoreCase)) {
            refPathSplit = refPathSplit[1..];
        }

        // SELF:something or something
        if (refPathSplit.Length == 1) {
            IsLocal = true;
            return;
        }
        if (refPathSplit[0].Equals("SELF", StringComparison.OrdinalIgnoreCase)) {
            IsSelf = true;
            return;
        }
        // wtf?
        if (refPathSplit.Length > 2) {
            return;
        }

        // var
        string varName;
        if (refPathSplit[0].StartsWith(KnownNames.AddonPackages + '/', StringComparison.OrdinalIgnoreCase)) {
            varName = refPathSplit[0].Replace(KnownNames.AddonPackages + '/', "", StringComparison.OrdinalIgnoreCase);
        } else {
            varName = refPathSplit[0];
        }

        if (!varName.EndsWith(".var", StringComparison.OrdinalIgnoreCase)) {
            varName += ".var";
        }

        IsVar = VarPackageName.TryGet(varName, out var tmp);
        EstimatedVarName = tmp;
    }
}