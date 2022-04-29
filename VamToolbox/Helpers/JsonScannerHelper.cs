using System.Runtime.CompilerServices;
using VamToolbox.Models;
using VamToolbox.Sqlite;

namespace VamToolbox.Helpers;

public sealed class Reference : IEquatable<Reference>
{
    public bool Equals(Reference? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Value == other.Value;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((Reference) obj);
    }

    public override int GetHashCode() => Value.GetHashCode();


    public string NormalizedLocalPath => Value.Split(':').Last().NormalizeAssetPath();
    public string Value { get; init; }
    public int Index { get; init; }
    public int Length { get; init; }

    // these are read from next line in JSON file
    public string? MorphName { get; set; }
    public string? InternalId { get; set; }

    public override string ToString() => $"{Value} at index {Index}";

    private string? _estimatedReferenceLocation;

    public Reference(string value, int index, int length, FileReferenceBase fromJsonFile)
    {
        Value = value;
        Index = index;
        Length = length;
        FromJsonFile = fromJsonFile;
    }

    public Reference(ReferenceEntry referenceEntry, FileReferenceBase fromJsonFile)
    {
        Value = referenceEntry.Value!;
        InternalId = referenceEntry.InternalId;
        MorphName = referenceEntry.MorphName;
        Index = referenceEntry.Index;
        Length = referenceEntry.Length;
        FromJsonFile = fromJsonFile;
    }

    public string EstimatedReferenceLocation => _estimatedReferenceLocation ??= GetEstimatedReference();
    public string? EstimatedVarName => Value.StartsWith("SELF:", StringComparison.Ordinal) || !Value.Contains(':') ? null : Value.Split(':').First();
    public FileReferenceBase FromJsonFile { get; internal set; }

    private string GetEstimatedReference()
    {
        if (Value.StartsWith("SELF:", StringComparison.Ordinal) || !Value.Contains(':'))
            return Value.Split(':').Last().NormalizeAssetPath();
        return Value.Split(':')[0].NormalizeAssetPath();
    }
}

public interface IJsonFileParser
{
    public Reference? GetAsset(ReadOnlySpan<char> line, int offset, FileReferenceBase fromFile, out string? outputError);
}

public sealed class JsonScannerHelper : IJsonFileParser
{
    private static readonly HashSet<int> Extensions = new[]{
        "vmi", "vam", "vaj", "vap", "jpg", "jpeg", "tif", "png", "mp3", "ogg", "wav", "assetbundle", "scene",
        "cs", "cslist", "tiff", "dll"
    }.Select(t => string.GetHashCode(t, StringComparison.OrdinalIgnoreCase)).ToHashSet();

    //public static readonly ConcurrentDictionary<string, string> SeenExtensions = new();

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public Reference? GetAsset(ReadOnlySpan<char> line, int offset, FileReferenceBase fromFile, out string? outputError)
    {
        outputError = null;
        var lastQuoteIndex = line.LastIndexOf('"');
        if (lastQuoteIndex == -1)
            return null;

        var prevQuoteIndex = line[..lastQuoteIndex].LastIndexOf('"');
        if (prevQuoteIndex == -1)
            return null;

        var okToParse = false;
        if (prevQuoteIndex - 3 >= 0 && line[prevQuoteIndex - 1] == ' ')
        {
            if (line[prevQuoteIndex - 2] == ':')
            {
                // '" : ' OR '": '
                if (line[prevQuoteIndex - 3] == '"' || (prevQuoteIndex - 4 >= 0 && line[prevQuoteIndex - 3] == ' ' && line[prevQuoteIndex - 4] == '"'))
                    okToParse = true;
            }
        }
        else if (prevQuoteIndex - 2 >= 0 && line[prevQuoteIndex - 1] == ':')
        {
            // '":' OR '" :'
            if (line[prevQuoteIndex - 2] == '"' || (prevQuoteIndex - 3 >= 0 && line[prevQuoteIndex - 2] == ' ' && line[prevQuoteIndex - 3] == '"'))
                okToParse = true;
        }

        if (!okToParse)
            return null;

        var assetName = line[(prevQuoteIndex + 1)..lastQuoteIndex];
        var lastDot = assetName.LastIndexOf('.');
        if (lastDot == -1 || lastDot == assetName.Length - 1)
            return null;
        var assetExtension = assetName[^(assetName.Length - lastDot - 1)..];
        //var ext = assetExtension.ToString();
        //SeenExtensions.GetOrAdd(ext, ext);

        var endsWithExtension = Extensions.Contains(string.GetHashCode(assetExtension, StringComparison.OrdinalIgnoreCase));
        if (!endsWithExtension || !IsUrl(assetName, line, ref outputError))
            return null;

        return new Reference(assetName.ToString(), index: offset + prevQuoteIndex + 1, length: lastQuoteIndex - prevQuoteIndex - 1, fromFile);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static bool IsUrl(ReadOnlySpan<char> reference, ReadOnlySpan<char> line, ref string? error)
    {
        const StringComparison c = StringComparison.OrdinalIgnoreCase;

        if (reference.StartsWith("http://") || reference.StartsWith("https://"))
            return false;

        bool isURL;
        if (reference.Contains("\"simTexture\"", c))
        {
            return false;
        }
        else if (reference.EndsWith(".vam", c))
        {
            isURL = line.Contains("\"id\"", c);
        }
        else if (reference.EndsWith(".vap", c))
        {
            isURL = line.Contains("\"presetFilePath\"", c);
        }
        else if (reference.EndsWith(".vmi", c))
        {
            isURL = line.Contains("\"uid\"", c);
        }
        else
        {
            isURL = line.Contains("tex\"", c) || line.Contains("texture\"", c) || line.Contains("url\"", c) ||
                    line.Contains("bumpmap\"", c) || line.Contains("\"url", c) || line.Contains("LUT\"", c) ||
                    line.Contains("\"plugin#", c);
        }

        if (!isURL)
        {
            if (line.Contains("\"displayName\"", c) || line.Contains("\"audioClip\"", c) ||
                line.Contains("\"selected\"", c) || line.Contains("\"audio\"", c))
            {
                return false;
            }

            error = string.Concat("Invalid type in json scanner: ", line);
            return false;
            //throw new VamToolboxException("Invalid type in json scanner: " + line);
        }

        return true;
    }
}