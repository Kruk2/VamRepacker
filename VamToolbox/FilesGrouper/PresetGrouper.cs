using System.Diagnostics;
using System.IO.Abstractions;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using VamToolbox.Helpers;
using VamToolbox.Logging;
using VamToolbox.Models;

namespace VamToolbox.FilesGrouper;

public interface IPresetGrouper
{
    public Task GroupPresets<T>(List<T> files, Func<string, Stream?> openFileStream) where T : FileReferenceBase;
}

public sealed partial class PresetGrouper : IPresetGrouper
{
    private readonly IFileSystem _fs;
    private readonly ILogger _logger;

    public PresetGrouper(IFileSystem fs, ILogger logger)
    {
        _fs = fs;
        _logger = logger;
    }

    public async Task GroupPresets<T>(List<T> files, Func<string, Stream?> openFileStream)
        where T : FileReferenceBase
    {
        var presetFiles = files
            .Where(t => t.ExtLower == ".vap" || KnownNames.PreviewExtensions.Contains(t.ExtLower))
            .ToLookup(t => _fs.Path.GetDirectoryName(t.LocalPath)!.NormalizePathSeparators());

        var grouped = files
            .Where(f => f.ExtLower is ".vaj" or ".vam" or ".vab" || KnownNames.PreviewExtensions.Contains(f.ExtLower))
            .Select(f => (basePath: f.LocalPath[..^f.ExtLower.Length], file: f))
            .GroupBy(x => x.basePath)
            .Select(g => {
                return (vaj: (T?)g.SingleOrDefault(f => f.file.ExtLower == ".vaj").file,
                    vam: (T?)g.SingleOrDefault(f => f.file.ExtLower == ".vam").file,
                    vab: (T?)g.SingleOrDefault(f => f.file.ExtLower == ".vab").file,
                    preview: (T?)g.FirstOrDefault(f => KnownNames.PreviewExtensions.Contains(f.file.ExtLower)).file);
            });


        var filesMovedAsChildren = new HashSet<T>();
        foreach (var (vaj, vam, vab, preview) in grouped) {
            var notNullPreset = vam ?? vaj ?? vab;
            if (notNullPreset == null)
                continue;

            if (vam is { InternalId: null }) {
                vam.InternalId = await ReadVamInternalId(vam, openFileStream);
            }

            var localDir = _fs.Path.GetDirectoryName(notNullPreset.LocalPath)!.NormalizePathSeparators();
            var pathWithoutExtension = _fs.Path.Combine(localDir, _fs.Path.GetFileNameWithoutExtension(notNullPreset.LocalPath)).NormalizePathSeparators();
            GroupAssetPresets(notNullPreset, notNullPreset.FilenameWithoutExt, presetFiles[localDir], filesMovedAsChildren);

            if (vam == null) {
                _logger.Log($"[MISSING-PRESET-FILE] Missing vam toFile for {notNullPreset}");
                notNullPreset.AddMissingChildren(pathWithoutExtension + ".vam");
            }

            if (vaj == null) {
                _logger.Log($"[MISSING-PRESET-FILE] Missing vaj toFile for {notNullPreset}");
                notNullPreset.AddMissingChildren(pathWithoutExtension + ".vaj");
            } else if (notNullPreset != vaj) {
                notNullPreset.AddChildren(vaj);
                filesMovedAsChildren.Add(vaj);
            }

            if (vab == null) {
                _logger.Log($"[MISSING-PRESET-FILE] Missing vab toFile for {notNullPreset}");
                notNullPreset.AddMissingChildren(pathWithoutExtension + ".vab");
            } else if (notNullPreset != vab) {
                notNullPreset.AddChildren(vab);
                filesMovedAsChildren.Add(vab);
            }

            if (preview != null) {
                notNullPreset.AddChildren(preview);
                filesMovedAsChildren.Add(preview);
            }
        }

        files.RemoveAll(filesMovedAsChildren.Contains);
    }

    private static void GroupAssetPresets<T>(T notNullPreset, string fileNameWithoutExtensions, IEnumerable<T> presetFilesWithPreviews, HashSet<T> filesMovedAsChildren) where T : FileReferenceBase
    {
        var allowedPresetName = fileNameWithoutExtensions + "_";
        foreach (var additionalPreset in presetFilesWithPreviews.Where(t => t.ExtLower == ".vap" && t.FilenameLower.StartsWith(allowedPresetName, StringComparison.OrdinalIgnoreCase))) {
            var additionalPresetPreview = presetFilesWithPreviews.FirstOrDefault(t => 
                t.FilenameWithoutExt.Equals(additionalPreset.FilenameWithoutExt, StringComparison.OrdinalIgnoreCase) && 
                KnownNames.PreviewExtensions.Contains(t.ExtLower));

            additionalPreset.AddChildren(notNullPreset);

            if (additionalPresetPreview != null) {
                additionalPreset.AddChildren(additionalPresetPreview);
                filesMovedAsChildren.Add(additionalPresetPreview);
            }
        }
    }

    [GeneratedRegex("['\"]uid[\"']\\s*:\\s*['\"](.*?)[\"']")]
    private static partial Regex UuidRegex { get; }

    private async Task<string?> ReadVamInternalId<T>(T vam, Func<string, Stream?> openFileStream) where T : FileReferenceBase
    {
        await using var streamReader = openFileStream(vam.LocalPath) ?? throw new ArgumentNullException(nameof(openFileStream), $"Failed to read vam uuid for {vam}");

        using var stream = new StreamReader(streamReader, Encoding.UTF8);
        var matchedUuid = UuidRegex.Match(await stream.ReadToEndAsync());
        if (matchedUuid.Success)
            return matchedUuid.Groups[1].Value;

        _logger.Log($"[MISSING-UUID-VAM] missing uuid in {vam}");
        return string.Empty;
    }
}