using System.Dynamic;
using System.IO.Abstractions;
using System.Threading.Tasks.Dataflow;
using Newtonsoft.Json;
using VamToolbox.Logging;
using VamToolbox.Operations.Abstract;

namespace VamToolbox.Operations.Destructive;

public sealed class TrustAllVarsOperation : ITrustAllVarsOperation
{
    private readonly IProgressTracker _progressTracker;
    private readonly IFileSystem _fs;
    private int _total;
    private int _progress;
    private int _trusted;
    private string _vamPrefsDir = null!;
    private OperationContext _context = null!;

    public TrustAllVarsOperation(IProgressTracker progressTracker, IFileSystem fs)
    {
        _progressTracker = progressTracker;
        _fs = fs;
    }

    public async Task ExecuteAsync(OperationContext context)
    {
        _context = context;
        _vamPrefsDir = Path.Combine(context.VamDir, KnownNames.AddonPackagesUserPrefs);
        if (!context.DryRun)
            _fs.Directory.CreateDirectory(_vamPrefsDir);

        _progressTracker.InitProgress("Trusting all vars");
        var depScanBlock = new ActionBlock<string>(TrustVar, new ExecutionDataflowBlockOptions {
            MaxDegreeOfParallelism = context.Threads
        });

        var vars = _fs.Directory.EnumerateFiles(_fs.Path.Combine(context.VamDir, KnownNames.AddonPackages), "*.var", SearchOption.AllDirectories).ToList();
        _total = vars.Count;
        foreach (var var in vars)
            depScanBlock.Post(var);

        depScanBlock.Complete();
        await depScanBlock.Completion;

        _progressTracker.Complete($"Trusted {_trusted} vars");
    }

    private void TrustVar(string varPath)
    {
        var varName = Path.GetFileNameWithoutExtension(varPath);
        var prefFile = Path.Combine(_vamPrefsDir, varName+ ".prefs");
        dynamic json;
        try {
            json = _fs.File.Exists(prefFile) ? JsonConvert.DeserializeObject<ExpandoObject>(_fs.File.ReadAllText(prefFile))! : new ExpandoObject();
        } catch (JsonReaderException) {
            return;
        }

        var hasPropertyDisabled = ((IDictionary<string, object>)json).ContainsKey("pluginsAlwaysDisabled");
        var hasPropertyEnabled = ((IDictionary<string, object>)json).ContainsKey("pluginsAlwaysEnabled");
        if (!hasPropertyDisabled || (hasPropertyDisabled && json.pluginsAlwaysDisabled != "true")) {
            if (!hasPropertyEnabled || (hasPropertyEnabled && json.pluginsAlwaysEnabled != "true")) {
                json.pluginsAlwaysEnabled = "true";
                json.pluginsAlwaysDisabled = "false";

                var serialized = JsonConvert.SerializeObject(json, Formatting.Indented);
                if (!_context.DryRun)
                    _fs.File.WriteAllText(prefFile, serialized);

                Interlocked.Increment(ref _trusted);
            }
        }

        _progressTracker.Report(new ProgressInfo(Interlocked.Increment(ref _progress), _total, $"Trusting {varName}"));
    }
}

public interface ITrustAllVarsOperation : IOperation
{
    Task ExecuteAsync(OperationContext context);
}