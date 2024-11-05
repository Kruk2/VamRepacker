namespace VamToolbox.Sqlite;

public record DatabaseFileKey(string FileName, long Size, DateTime ModifiedTime)
{
    public virtual bool Equals(DatabaseFileKey? other)
    {
        if (other is null) {
            return false;
        }

        if (ReferenceEquals(this, other)) {
            return true;
        }

        return string.Equals(FileName, other.FileName, StringComparison.OrdinalIgnoreCase) && Size == other.Size && ModifiedTime.Equals(other.ModifiedTime);
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