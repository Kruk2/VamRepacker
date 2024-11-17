using System.Collections.Frozen;
using System.IO.Abstractions;
using System.Text;
using VamToolbox.Helpers;
using VamToolbox.Models;

namespace VamToolbox.FilesGrouper;

public interface IScriptGrouper
{
    Task GroupCslistRefs<T>(List<T> files, Func<string, Stream?> openFileStream) where T : FileReferenceBase;
}

public sealed class ScriptGrouper : IScriptGrouper
{
    private readonly IFileSystem _fs;

    public ScriptGrouper(IFileSystem fs)
    {
        _fs = fs;
    }

    public async Task GroupCslistRefs<T>(List<T> files, Func<string, Stream?> openFileStream) where T : FileReferenceBase
    {
        var filesMovedAsChildren = new HashSet<T>();
        var filesIndex = files
            .Where(f => f.ExtLower == ".cs")
            .ToFrozenDictionary(f => f.LocalPath);
        foreach (var cslist in files.Where(f => f.ExtLower == ".cslist")) {
            var cslistFolder = _fs.Path.GetDirectoryName(cslist.LocalPath)!;
            var csFiles = cslist.CsFiles;

            if (csFiles is null) {
                await using var stream = openFileStream(cslist.LocalPath) ?? throw new ArgumentNullException(nameof(openFileStream), $"Failed to read vam uuid for {cslist}");
                using var streamReader = new StreamReader(stream, Encoding.UTF8);
                csFiles = await streamReader.ReadToEndAsync();
                cslist.CsFiles = csFiles;
            }

            var stringStream = new StringReader(csFiles);
            string? cslistRef;
            while ((cslistRef = await stringStream.ReadLineAsync()) != null)
            {
                cslistRef = cslistRef.Trim();
                if (string.IsNullOrWhiteSpace(cslistRef)) continue;
                if (filesIndex.TryGetValue(_fs.Path.Combine(cslistFolder, cslistRef).NormalizePathSeparators(), out var f1))
                {
                    cslist.AddChildren(f1);
                    filesMovedAsChildren.Add(f1);
                }
                else
                {
                    cslist.AddMissingChildren(cslistRef);
                }
            }
        }

        files.RemoveAll(filesMovedAsChildren.Contains);
    }
}