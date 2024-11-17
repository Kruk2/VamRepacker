namespace VamToolbox.Sqlite;

public record DatabaseFileKey(string FileName, long Size, DateTime ModifiedTime, string? LocalPath)
{
    public virtual bool Equals(DatabaseFileKey? other)
    {
        if (other is null) {
            return false;
        }

        if (ReferenceEquals(this, other)) {
            return true;
        }

        return Size == other.Size && 
               ModifiedTime.Equals(other.ModifiedTime) &&
               string.Equals(FileName, other.FileName, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(LocalPath, other.LocalPath, StringComparison.Ordinal);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(FileName, StringComparer.OrdinalIgnoreCase);
        hashCode.Add(LocalPath, StringComparer.Ordinal);
        hashCode.Add(Size);
        hashCode.Add(ModifiedTime);
        return hashCode.ToHashCode();
    }
}

public record DatabaseVarKey(string FileName, long Size, DateTime ModifiedTime)
{
    public virtual bool Equals(DatabaseVarKey? other)
    {
        if (other is null) {
            return false;
        }

        if (ReferenceEquals(this, other)) {
            return true;
        }

        return Size == other.Size &&
               ModifiedTime.Equals(other.ModifiedTime) &&
               string.Equals(FileName, other.FileName, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(FileName, StringComparer.OrdinalIgnoreCase);
        hashCode.Add(Size);
        hashCode.Add(ModifiedTime);
        return hashCode.ToHashCode();
    }
}