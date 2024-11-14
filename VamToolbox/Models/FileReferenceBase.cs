using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using VamToolbox.Hashing;
using VamToolbox.Helpers;

namespace VamToolbox.Models;

public abstract class FileReferenceBase
{
    public string LocalPath { get; }
    public string FilenameLower { get; }
    public string FilenameWithoutExt { get; }
    public string ExtLower { get; }


    public string? InternalId { get; internal set; }
    public string? MorphName { get; internal set; }

    [MemberNotNullWhen(true, nameof(VarFile))]
    [MemberNotNullWhen(true, nameof(Var))]
    [MemberNotNullWhen(false, nameof(Free))]
    public bool IsVar => this is VarPackageFile;
    public VarPackage? Var => this is VarPackageFile varFile ? varFile.ParentVar : null;
    public VarPackageFile? VarFile => this as VarPackageFile;
    public FreeFile? Free => this as FreeFile;
    public FileReferenceBase? ParentFile { get; protected internal set; }

    public bool IsInVaMDir { get; }
    public AssetType Type { get; }

    public bool Dirty {
        get => _dirty || Children.Any(t => t.Dirty);
        set => _dirty = value;
    }

    public string? FavFilePath { get; set; }
    public long Size { get; }

    public JsonFile? JsonFile { get; internal set; }
    public ConcurrentDictionary<JsonFile, bool> UsedByJsonFiles { get; } = new();
    public int UsedByVarPackagesOrFreeFilesCount => UsedByJsonFiles.Keys.Select(t => t.File.IsVar ? (object)t.File.Var : t.File.Free).Distinct().Count();
    public abstract IReadOnlyCollection<FileReferenceBase> Children { get; }
    public List<string> MissingChildren { get; } = new();

    private long? _sizeWithChildren;
    private bool _dirty;
    public long SizeWithChildren => _sizeWithChildren ??= Size + Children.Sum(t => t.SizeWithChildren);
    public bool PreferredForDelayedResolver { get; internal set; }

    protected FileReferenceBase(string localPath, long size, bool isInVamDir)
    {
        LocalPath = localPath.NormalizePathSeparators();
        FilenameLower = Path.GetFileName(localPath).ToLowerInvariant();
        FilenameWithoutExt = Path.GetFileNameWithoutExtension(localPath);
        ExtLower = Path.GetExtension(FilenameLower);
        IsInVaMDir = isInVamDir;
        Type = ExtLower.ClassifyType(LocalPath);
        Size = size;
    }

    public override string ToString() => LocalPath;

    public abstract void AddChildren(FileReferenceBase children);

    public void AddMissingChildren(string localChildrenPath) => MissingChildren.Add(localChildrenPath);

    public virtual IEnumerable<FileReferenceBase> SelfAndChildren() => Children.SelectMany(t => t.SelfAndChildren()).Append(this);
}