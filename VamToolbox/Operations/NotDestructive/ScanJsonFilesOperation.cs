using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Threading.Tasks.Dataflow;
using VamToolbox.Helpers;
using VamToolbox.Logging;
using VamToolbox.Models;
using VamToolbox.Operations.Abstract;
using VamToolbox.Operations.Repo;

namespace VamToolbox.Operations.NotDestructive;

public sealed class ScanJsonFilesOperation : IScanJsonFilesOperation
{
    private readonly IProgressTracker _progressTracker;
    private readonly IFileSystem _fs;
    private readonly ILogger _logger;
    private readonly IJsonFileParser _jsonFileParser;
    private readonly IReferenceCache _referenceCache;
    private readonly IUuidReferenceResolver _uuidReferenceResolver;
    private readonly IReferencesResolver _referencesResolver;
    private readonly ConcurrentBag<JsonFile> _jsonFiles = [];
    private readonly ConcurrentBag<string> _errors = [];
    private int _scanned;
    private int _total;
    private int _unknownErrorsCount;

    private OperationContext _context = null!;
    private IVarFilters? _filters;

    public ScanJsonFilesOperation(
        IProgressTracker progressTracker,
        IFileSystem fs,
        ILogger logger,
        IJsonFileParser jsonFileParser,
        IReferenceCache referenceCache,
        IUuidReferenceResolver uuidReferenceResolver,
        IReferencesResolver referencesResolver)
    {
        _progressTracker = progressTracker;
        _fs = fs;
        _logger = logger;
        _jsonFileParser = jsonFileParser;
        _referenceCache = referenceCache;
        _uuidReferenceResolver = uuidReferenceResolver;
        _referencesResolver = referencesResolver;
    }

    public async Task<List<JsonFile>> ExecuteAsync(OperationContext context, IList<FreeFile> freeFiles, IList<VarPackage> varFiles, IVarFilters? filters = null)
    {
        var stopWatch = new Stopwatch();
        stopWatch.Start();

        _context = context;
        _filters = filters;
        await _logger.Init("scan_json_files.log");
        _progressTracker.InitProgress("Scanning scenes/presets references");

        var potentialScenes = await InitLookups(varFiles, freeFiles);
        await _referenceCache.ReadCache(potentialScenes);

        _total = potentialScenes.Count;
        await RunScenesScan(potentialScenes, varFiles, freeFiles);

        _total = varFiles.Count + freeFiles.Count;
        await Task.Run(async () => await CalculateDeps(varFiles, freeFiles));
        await _referenceCache.SaveCache(varFiles, freeFiles);

        var missingCount = _jsonFiles.Sum(s => s.Missing.Count);
        var resolvedCount = _jsonFiles.Sum(s => s.References.Count);
        var scenes = _jsonFiles.OrderBy(s => s.ToString()).ToList();
        await Task.Run(() => PrintWarnings(scenes, varFiles));

        _progressTracker.Complete($"Scanned {_scanned} json files for references. Got {_unknownErrorsCount} unknown errors - check logs.\r\n Found {missingCount} missing and {resolvedCount} resolved references.Took {stopWatch.Elapsed:hh\\:mm\\:ss}");
        return scenes;
    }

    private async Task<List<PotentialJsonFile>> InitLookups(IList<VarPackage> varFiles, IList<FreeFile> freeFiles)
    {
        await _uuidReferenceResolver.InitLookups(freeFiles, varFiles);
        await _referencesResolver.InitLookups(freeFiles, varFiles, _errors);

        return await Task.Run(() => {

            var varFilesWithScene = varFiles
                .Where(t => t.Files.SelfAndChildren()
                    .Any(x => x.FilenameLower != "meta.json" && KnownNames.IsPotentialJsonFile(x.ExtLower)));

            return freeFiles
                .SelfAndChildren()
                .Where(t => KnownNames.IsPotentialJsonFile(t.ExtLower))
                .Select(t => new PotentialJsonFile(t))
                .Concat(varFilesWithScene.Select(t => new PotentialJsonFile(t)))
                .ToList();
        });
    }

    private async Task CalculateDeps(IList<VarPackage> varFiles, IList<FreeFile> freeFiles)
    {
        _progressTracker.Report("Calculating dependencies", forceShow: true);

        var dependencies = varFiles.Cast<IVamObjectWithDependencies>().Concat(freeFiles).ToList();
        dependencies.ForEach(t => t.ClearDependencies());

        var depScanBlock = new ActionBlock<IVamObjectWithDependencies>(t => {
            _ = t.ResolvedVarDependencies;
        },
            new ExecutionDataflowBlockOptions {
                MaxDegreeOfParallelism = _context.Threads
            });

        foreach (var d in dependencies)
            depScanBlock.Post(d);

        depScanBlock.Complete();
        await depScanBlock.Completion;
    }

    private async Task RunScenesScan(IEnumerable<PotentialJsonFile> potentialScenes, IList<VarPackage> varFiles, IList<FreeFile> freeFiles)
    {
        var scanSceneBlock = new ActionBlock<PotentialJsonFile>(
            ScanJsonAsync,
            new ExecutionDataflowBlockOptions {
                MaxDegreeOfParallelism = _context.Threads
            });

        foreach (var potentialScene in potentialScenes) {
            scanSceneBlock.Post(potentialScene);
        }

        scanSceneBlock.Complete();
        await scanSceneBlock.Completion;

        await Task.Run(async () => await CalculateDeps(varFiles, freeFiles));
        AnnotatePreferredVarsForDelayedResolver(varFiles, freeFiles);
        await _uuidReferenceResolver.ResolveDelayedReferences();
    }

    private void AnnotatePreferredVarsForDelayedResolver(IList<VarPackage> varFiles, IList<FreeFile> freeFiles)
    {
        IEnumerable<VarPackage>? varsToMove;
        IEnumerable<FreeFile>? filesToMove;
        if (_filters is not null) {
            (varsToMove, filesToMove) = DependencyCalculator.GetFilesToMove(_filters, varFiles);
           
        } else {
            (varsToMove, filesToMove) = DependencyCalculator.GetFilesToMove(varFiles, freeFiles);
        }

        var files = varsToMove.SelectMany(t => t.Files).SelfAndChildren()
            .Concat(filesToMove.SelfAndChildren().Cast<FileReferenceBase>());

        foreach (var file in files) {
            file.PreferredForDelayedResolver = true;
        }
    }

    private void PrintWarnings(List<JsonFile> scenes, IList<VarPackage> varPackages)
    {
        _progressTracker.Report("Saving logs", forceShow: true);
        _logger.Log("Errors");
        foreach (var error in _errors.OrderBy(t => t)) {
            _logger.Log(error);
        }

        _logger.Log("Unresolved references");
        foreach (var unableToParseVarName in scenes
                     .SelectMany(t => t.Missing)
                     .Where(t => KnownNames.VirusMorphs.All(x => t.MorphName?.Equals(x, StringComparison.Ordinal) != true))
                     .OrderBy(t => t.Value)) {
            _logger.Log($"'{unableToParseVarName.Value}' in {unableToParseVarName.ForJsonFile}");
        }

        _logger.Log("Missing vars");
        var varIndex = varPackages.ToLookup(t => t.Name.PackageNameWithoutVersion, StringComparer.OrdinalIgnoreCase);
        var missingVars = scenes
            .SelectMany(t => t.Missing.Where(x => x.EstimatedVarName != null).Select(x => x.EstimatedVarName!))
            .Distinct()
            .OrderBy(t => t.Filename);

        foreach (var varName in missingVars) {
            if (!varIndex.Contains(varName.PackageNameWithoutVersion)) {
                _logger.Log(varName.Filename);
                continue;
            }

            if (varName.Version == -1) continue; // we have and we want anything, ignore
            if (varIndex[varName.PackageNameWithoutVersion].Any(x => x.Name.Version == varName.Version)) continue; // we have it, ignore
            if (varName.MinVersion && varIndex[varName.PackageNameWithoutVersion].Any(x => x.Name.Version >= varName.Version)) continue; // we have it, ignore

            _logger.Log(varName.Filename);
        }

        //_logger.Log("Extensions");
        //foreach (var seenExtensionsKey in JsonFileParser.SeenExtensions.Keys) {
        //    _logger.Log(seenExtensionsKey);
        //}
    }

    private async Task ScanJsonAsync(PotentialJsonFile potentialJson)
    {
        try {
            _progressTracker.Report(new ProgressInfo(Interlocked.Increment(ref _scanned), _total, potentialJson.Name));
            foreach (var openedJson in potentialJson.OpenJsons())
                await ScanJsonAsync(openedJson, potentialJson);

        } catch (Exception ex) {
            Interlocked.Increment(ref _unknownErrorsCount);
            _errors.Add($"[UNKNOWN-ERROR] Unable to process {potentialJson.Name} because: {ex}");
        } finally {
            potentialJson.Dispose();
        }
    }

    private async Task ScanJsonAsync(OpenedPotentialJson openedJson, PotentialJsonFile potentialJson)
    {
        using var streamReader = openedJson.Stream == null ? null : new StreamReader(openedJson.Stream);
        var localJsonPath = _fs.Path.GetDirectoryName(openedJson.File.LocalPath)!.NormalizePathSeparators();

        Reference? nextScanForUuidOrMorphName = null;
        JsonReference? resolvedReferenceWhenUuidMatchingFails = null;

        var jsonFile = new JsonFile(openedJson);
        var offset = 0;
        var hasDelayedReferences = false;

        if (openedJson.CachedReferences != null) {
            foreach (var reference in openedJson.CachedReferences) {
                (nextScanForUuidOrMorphName, resolvedReferenceWhenUuidMatchingFails) = ProcessJsonReference(reference);

                if (reference.InternalId != null) {
                    if (nextScanForUuidOrMorphName is null) throw new ArgumentException("Uuid reference is null but got internal id");
                    ProcessVamReference(reference);
                } else if (reference.MorphName != null) {
                    if (nextScanForUuidOrMorphName is null) throw new ArgumentException("morph reference is null but got morph name");
                    ProcessMorphReference(reference);
                }
            }
        }

        while (streamReader is { EndOfStream: false }) {
            var line = await streamReader.ReadLineAsync();
            if (string.IsNullOrEmpty(line))
                continue;

            if (nextScanForUuidOrMorphName != null) {
                if (line.Contains("\"internalId\"", StringComparison.Ordinal)) {
                    var internalId = line.Replace("\"internalId\"", "", StringComparison.Ordinal);
                    nextScanForUuidOrMorphName.InternalId = internalId[(internalId.IndexOf('\"', StringComparison.Ordinal) + 1)..internalId.LastIndexOf('\"')];
                    ProcessVamReference(nextScanForUuidOrMorphName);

                    nextScanForUuidOrMorphName = null;
                    resolvedReferenceWhenUuidMatchingFails = null;
                    offset += line.Length;
                    continue;
                }

                if (line.Contains("\"name\"", StringComparison.Ordinal)) {
                    var morphName = line.Replace("\"name\"", "", StringComparison.Ordinal);
                    nextScanForUuidOrMorphName.MorphName = morphName[(morphName.IndexOf('\"', StringComparison.Ordinal) + 1)..morphName.LastIndexOf('\"')];
                    ProcessMorphReference(nextScanForUuidOrMorphName);

                    nextScanForUuidOrMorphName = null;
                    resolvedReferenceWhenUuidMatchingFails = null;
                    offset += line.Length;
                    continue;
                }

                if (resolvedReferenceWhenUuidMatchingFails != null) {
                    jsonFile.AddReference(resolvedReferenceWhenUuidMatchingFails);
                    resolvedReferenceWhenUuidMatchingFails = null;
                } else {
                    jsonFile.AddMissingReference(nextScanForUuidOrMorphName);
                }

                nextScanForUuidOrMorphName = null;
            }

            Reference? reference;
            string? referenceParseError;
            try {
                reference = _jsonFileParser.GetAsset(line, offset, openedJson.File, out referenceParseError);
            } catch (Exception e) {
                _logger.Log($"[ERROR] {e.Message} Unable to parse asset '{line}' in {openedJson.File}");
                throw;
            } finally {
                offset += line.Length;
            }

            if (reference is null) {
                if (referenceParseError != null)
                    _errors.Add(referenceParseError);
                continue;
            }

            (nextScanForUuidOrMorphName, resolvedReferenceWhenUuidMatchingFails) = ProcessJsonReference(reference);
        }

        if (jsonFile.References.Count > 0 || jsonFile.Missing.Count > 0 || hasDelayedReferences) {
            _jsonFiles.Add(jsonFile);
            openedJson.File.JsonFile = jsonFile;
        }

        (Reference? nextScanForUuidOrMorphName, JsonReference? jsonReference) ProcessJsonReference(Reference reference)
        {
            JsonReference? jsonReference = null;
            if (reference.IsVar) {
                // 1. reference to var, just scan vars
                jsonReference = _referencesResolver.ScanPackageSceneReference(potentialJson, reference, varToSearch: null, localJsonPath);
            } else {
                if (potentialJson.IsVar && (reference.IsSelf || reference.IsLocal)) {
                    // 2. we're in VAR but the reference is not to VAR (so it's SELF: or local)
                    jsonReference = _referencesResolver.ScanPackageSceneReference(potentialJson, reference, potentialJson.Var, localJsonPath);
                }
                if (jsonReference == null && (reference.IsLocal || (reference.IsSelf && !potentialJson.IsVar))) {
                    // 3. it's local (we can be in var or free file), just scan free files
                    // 4. we're not in var and it's self reference 
                    jsonReference = _referencesResolver.ScanFreeFileSceneReference(localJsonPath, reference);
                }
            }

            if (reference.Value.EndsWith(".vam", StringComparison.OrdinalIgnoreCase))
                nextScanForUuidOrMorphName = reference;
            else if (reference.Value.EndsWith(".vmi", StringComparison.OrdinalIgnoreCase))
                nextScanForUuidOrMorphName = reference;
            else if (jsonReference != null)
                jsonFile.AddReference(jsonReference);
            else
                jsonFile.AddMissingReference(reference);

            return (nextScanForUuidOrMorphName, jsonReference);
        }

        void ProcessMorphReference(Reference morphReference)
        {
            var (jsonReferenceByMorphName, delayedReference) = _uuidReferenceResolver.MatchMorphJsonReferenceByName(jsonFile, morphReference, resolvedReferenceWhenUuidMatchingFails?.ToFile);
            if (jsonReferenceByMorphName != null)
                jsonFile.AddReference(jsonReferenceByMorphName);
            else if (!delayedReference)
                jsonFile.AddMissingReference(morphReference);
            else
                hasDelayedReferences = true;
        }

        void ProcessVamReference(Reference vamReference)
        {
            var (jsonReferenceById, delayedReference) = _uuidReferenceResolver.MatchVamJsonReferenceById(jsonFile, vamReference, resolvedReferenceWhenUuidMatchingFails?.ToFile);
            if (jsonReferenceById != null)
                jsonFile.AddReference(jsonReferenceById);
            else if (!delayedReference)
                jsonFile.AddMissingReference(vamReference);
            else
                hasDelayedReferences = true;
        }
    }
}

public interface IScanJsonFilesOperation : IOperation
{
    Task<List<JsonFile>> ExecuteAsync(OperationContext context, IList<FreeFile> freeFiles, IList<VarPackage> varFiles, IVarFilters? filters = null);
}