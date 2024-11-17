using VamToolbox.Models;

namespace VamToolbox.FilesGrouper;

public class FileGroupers : IFileGroupers
{
    private readonly IScriptGrouper _scriptGrouper;
    private readonly IMorphGrouper _morphGrouper;
    private readonly IPresetGrouper _presetGrouper;
    private readonly IPreviewGrouper _previewGrouper;

    public FileGroupers(IScriptGrouper scriptGrouper, 
        IMorphGrouper morphGrouper, 
        IPresetGrouper presetGrouper, 
        IPreviewGrouper previewGrouper)
    {
        _scriptGrouper = scriptGrouper;
        _morphGrouper = morphGrouper;
        _presetGrouper = presetGrouper;
        _previewGrouper = previewGrouper;
    }

    public async Task Group<T>(List<T> files, Func<string, Stream> openFileStream) where T : FileReferenceBase
    {
        await _scriptGrouper.GroupCslistRefs(files, openFileStream);
        await _morphGrouper.GroupMorphsVmi(files, openFileStream: openFileStream);
        await _presetGrouper.GroupPresets(files, openFileStream);
        _previewGrouper.GroupsPreviews(files);
    }
}

public interface IFileGroupers
{
    Task Group<T>(List<T> files, Func<string, Stream> openFileStream) where T : FileReferenceBase;
}
