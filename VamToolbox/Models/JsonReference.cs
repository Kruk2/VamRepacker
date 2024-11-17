using System.Diagnostics.CodeAnalysis;

namespace VamToolbox.Models;

public sealed class JsonReference
{
    [MemberNotNullWhen(true, nameof(ToParentVar))]
    [MemberNotNullWhen(true, nameof(ToVarFile))]
    [MemberNotNullWhen(false, nameof(ToFreeFile))]
    public bool IsVarReference => ToFile is VarPackageFile;
    public FileReferenceBase ToFile { get; }

    public VarPackage? ToParentVar => ToFile is VarPackageFile varFile ? varFile.ParentVar : null;
    public FreeFile? ToFreeFile => ToFile as FreeFile;
    public VarPackageFile? ToVarFile => ToFile as VarPackageFile;
    public Reference Reference { get; }

    public JsonReference(FileReferenceBase toFile, Reference reference)
    {
        ToFile = toFile;
        Reference = reference;
    }

    public override string ToString() => ToFile.ToString();
}