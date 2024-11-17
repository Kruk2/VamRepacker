namespace VamToolbox.Models;

public sealed class VarPackageFile : FileReferenceBase
{
    public VarPackage ParentVar { get; }

    private readonly List<VarPackageFile> _children = [];
    public override IReadOnlyCollection<VarPackageFile> Children => _children.AsReadOnly();

    public VarPackageFile(string localPath, bool isInVamDir, VarPackage varPackage, long size)
        : base(localPath, size, isInVamDir)
    {
        ParentVar = varPackage;
        ParentVar.AddVarFile(this);
    }

    public override void AddChildren(FileReferenceBase children)
    {
        _children.Add((VarPackageFile)children);
        children.ParentFile = this;
    }

    public override IEnumerable<VarPackageFile> SelfAndChildren() => base.SelfAndChildren().Cast<VarPackageFile>();
    public override string ToString() => base.ToString() + $" Var: {ParentVar.FullPath}";
}