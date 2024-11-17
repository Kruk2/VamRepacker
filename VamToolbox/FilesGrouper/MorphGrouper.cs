using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using VamToolbox.Helpers;
using VamToolbox.Logging;
using VamToolbox.Models;

namespace VamToolbox.FilesGrouper;

public interface IMorphGrouper
{
    public Task GroupMorphsVmi<T>(List<T> files, Func<string, Stream?> openFileStream)
        where T : FileReferenceBase;
}

public sealed partial class MorphGrouper : IMorphGrouper
{
    private readonly IFileSystem _fs;
    private readonly ILogger _logger;

    public MorphGrouper(IFileSystem fs, ILogger logger)
    {
        _fs = fs;
        _logger = logger;
    }

    public async Task GroupMorphsVmi<T>(List<T> files, Func<string, Stream?> openFileStream)
        where T : FileReferenceBase
    {
        var filesMovedAsChildren = new HashSet<T>();
        var pairs = GroupMorphs(files);
        foreach (var (vmi, vmb) in pairs) {
            var notNullPreset = vmi ?? vmb;
            if (notNullPreset == null)
                continue;

            if (vmi is not null && vmi.MorphName is null) {
                vmi.MorphName = await ReadVmiName(vmi, openFileStream);
            }

            var localDir = _fs.Path.Combine(_fs.Path.GetDirectoryName(notNullPreset.LocalPath)!, _fs.Path.GetFileNameWithoutExtension(notNullPreset.LocalPath)).NormalizePathSeparators();
            if (vmi == null) {
                _logger.Log($"[MISSING-MORPH-FILE] Missing vmi for {notNullPreset}");
                notNullPreset.AddMissingChildren(localDir + ".vmi");
            } else if (notNullPreset != vmi) {
                notNullPreset.AddChildren(vmi);
                filesMovedAsChildren.Add(vmi);
            }

            if (vmb == null) {
                _logger.Log($"[MISSING-MORPH-FILE] Missing vmb for {notNullPreset}");
                notNullPreset.AddMissingChildren(localDir + ".vmb");
            } else if (notNullPreset != vmb) {
                notNullPreset.AddChildren(vmb);
                filesMovedAsChildren.Add(vmb);
            }
        }

        files.RemoveAll(filesMovedAsChildren.Contains);
    }

    private static IEnumerable<(T?, T?)> GroupMorphs<T>(IEnumerable<T> files) where T : FileReferenceBase
    {
        return files
            .Where(f => f.ExtLower is ".vmi" or ".vmb")
            .Select(f => (basePath: f.LocalPath[..^f.ExtLower.Length], file: f))
            .GroupBy(x => x.basePath)
            .Select(g => {
                return (vmi: (T?)g.SingleOrDefault(f => f.file.ExtLower == ".vmi").file,
                    vmb: (T?)g.SingleOrDefault(f => f.file.ExtLower == ".vmb").file);
            });
    }

    [GeneratedRegex("['\"]displayName[\"']\\s*:\\s*['\"](.*?)[\"']")]
    private static partial Regex UuidRegex();

    private async Task<string?> ReadVmiName<T>(T vam, Func<string, Stream?> openFileStream) where T : FileReferenceBase
    {
        await using var streamReader = openFileStream(vam.LocalPath) ?? throw new ArgumentNullException(nameof(openFileStream), $"Failed to read vam uuid for {vam}");

        using var stream = new StreamReader(streamReader, Encoding.UTF8);
        var matchedUuid = UuidRegex().Match(await stream.ReadToEndAsync());
        if (matchedUuid.Success)
            return matchedUuid.Groups[1].Value;

        _logger.Log($"[MISSING-DISPLAYNAME-VMI] missing name in {vam}");
        return string.Empty;
    }
}