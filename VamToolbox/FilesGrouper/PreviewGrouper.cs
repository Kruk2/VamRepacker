using System.Collections.Frozen;
using System.IO.Abstractions;
using VamToolbox.Helpers;
using VamToolbox.Models;

namespace VamToolbox.FilesGrouper;

public interface IPreviewGrouper
{
    public void GroupsPreviews<T>(List<T> files) where T : FileReferenceBase;
}

public sealed class PreviewGrouper : IPreviewGrouper
{
    private readonly IFileSystem _fs;
    public PreviewGrouper(IFileSystem fs) => _fs = fs;

    public void GroupsPreviews<T>(List<T> files) where T : FileReferenceBase
    {
        var filesWithPreview = new[] { ".vam", ".var", ".vap", ".vaj", ".json", ".assetbundle" };
        var filesMovedAsChildren = new HashSet<T>();
        var possibleFilesWithPreview = files.Where(t => filesWithPreview.Contains(t.ExtLower));
        var possiblePreviews = files.Where(t => KnownNames.PreviewExtensions.Contains(t.ExtLower)).ToFrozenDictionary(t => t.LocalPath);

        foreach (var possibleFileWithPreview in possibleFilesWithPreview) {
            foreach (var ext in KnownNames.PreviewExtensions) {
                var previewFileName = _fs.Path.Combine(Path.GetDirectoryName(possibleFileWithPreview.LocalPath)!, possibleFileWithPreview.FilenameWithoutExt + ext).NormalizePathSeparators();
                if (!possiblePreviews.TryGetValue(previewFileName, out var child)) continue;

                possibleFileWithPreview.AddChildren(child);
                filesMovedAsChildren.Add(child);
            }
        }

        files.RemoveAll(filesMovedAsChildren.Contains);
    }
}