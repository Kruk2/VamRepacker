using VamToolbox.Helpers;

namespace VamToolbox.Operations.Repo;

public class ProfileModel : ICloneable
{
    public string Name { get; }
    public SortedSet<string> Dirs { get; }
    public SortedSet<string> Files { get; }

    public ProfileModel(SortedSet<string> files, SortedSet<string> dirs, string name)
    {
        Files = files;
        Dirs = dirs;
        Name = name;
    }

    public object Clone() => new ProfileModel(new SortedSet<string>(Files), new SortedSet<string>(Dirs), Name);
    public override string ToString() => Name;
}

public interface IVarFilters
{
    bool Matches(string path);
    void FromProfile(ProfileModel profile);
}

public class VarFilters : IVarFilters
{
    private readonly List<string> _dirs = [];
    private readonly List<string> _files = [];

    public void FromProfile(ProfileModel profile)
    {
        _dirs.AddRange(profile.Dirs.Select(t => t.NormalizePathSeparators()));
        _files.AddRange(profile.Files.Select(t => t.NormalizePathSeparators()));
    }

    public bool Matches(string path)
    {
        return _dirs.Any(path.StartsWith) || _files.Contains(path);
    }
}