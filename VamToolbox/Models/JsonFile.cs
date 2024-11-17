namespace VamToolbox.Models;

public sealed class JsonFile
{
    private readonly List<JsonReference> _references = [];
    private readonly List<Reference> _missing = [];

    public FileReferenceBase File { get; }
    public IReadOnlyCollection<JsonReference> References => _references;
    public IReadOnlyCollection<Reference> Missing => _missing;

    public IReadOnlySet<VarPackage> VarReferences { get; } = new HashSet<VarPackage>();
    public IReadOnlySet<FreeFile> FreeReferences { get; } = new HashSet<FreeFile>();

    public JsonFile(OpenedPotentialJson openedJsonFile)
    {
        File = openedJsonFile.File;
    }

    public void AddReference(JsonReference reference)
    {
        _references.Add(reference);
        if (reference.IsVarReference)
            ((HashSet<VarPackage>)VarReferences).Add(reference.ToParentVar);
        else
            ((HashSet<FreeFile>)FreeReferences).Add(reference.ToFreeFile);

        if (reference.ToFile != File)
            reference.ToFile.UsedByJsonFiles[this] = true;
    }
    public void AddMissingReference(Reference reference) => _missing.Add(reference);

    public override string ToString() => File.ToString();


}