using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using RestEase;
using VamToolbox.Logging;
using VamToolbox.Models;
using VamToolbox.Operations.Abstract;

namespace VamToolbox.Operations.Repo;

public sealed class DownloadMissingVars : IDownloadMissingVars
{
    private readonly IProgressTracker _reporter;
    private readonly ILogger _logger;

    public DownloadMissingVars(IProgressTracker progressTracker, ILogger logger)
    {
        _reporter = progressTracker;
        _logger = logger;
    }

    public async Task ExecuteAsync(OperationContext context, IList<VarPackage> vars, IList<FreeFile> freeFiles)
    {
        _reporter.InitProgress("Downloading missing vars from vam hub");
        await _logger.Init("download_missing_from_vam.log");
        int processed = 0;
        var unresolvedVars = await Task.Run(() => FindMissingReferences(vars, freeFiles));

        var vamResult = await QueryVam(unresolvedVars, vars.Select(t => t.Name));
        if (vamResult.Count == 0) {
            _reporter.Complete("Downloaded 0 packages");
            return;
        }

        var folderDestination = Path.Combine(context.VamDir, KnownNames.AddonPackages, "other");
        if (!context.DryRun)
            Directory.CreateDirectory(folderDestination);
        var count = vamResult.Count;

        using var handler = new HttpClientHandler { UseCookies = false, CheckCertificateRevocationList = true};
        using var client = new HttpClient(handler);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.51 Safari/537.36 Edg/99.0.1150.30");

        foreach (var (packageInfo, i) in vamResult.Zip(Enumerable.Range(0, count))) {
            var varDestination = Path.Combine(folderDestination, packageInfo.Filename);
            if (File.Exists(varDestination))
                continue;
            if (context.DryRun)
                continue;

            _logger.Log($"Downloading {packageInfo.Filename} {packageInfo.DownloadUrl}");
            _reporter.Report(new ProgressInfo(processed, count, $"Downloading {i}/{count} " + packageInfo.Filename));

            if (await DownloadVar(packageInfo, client, varDestination)) {
                _logger.Log($"Downloaded {packageInfo.Filename} {packageInfo.DownloadUrl}");
            }

            _reporter.Report(new ProgressInfo(++processed, count, $"Downloaded {i}/{count} " + packageInfo.Filename));
        }

        _reporter.Complete($"Downloaded {processed} vars. Check download_missing_from_vam.log");
    }

    private async Task<bool> DownloadVar(PackageInfo packageInfo, HttpClient client, string destt)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, packageInfo.DownloadUrl);
        message.Headers.Add("Cookie", "vamhubconsent=yes");
        var response = await client.SendAsync(message);
        if (!response.IsSuccessStatusCode) {
            _logger.Log($"Unable to download {packageInfo.DownloadUrl}. Status code: {response.StatusCode}");
            return false;
        }

        if (!response.Content.Headers.ContentLength.HasValue || response.Content.Headers.ContentType is not
            {
                MediaType: "application/octet-stream"
            }) {
            _logger.Log(
                $"Unable to download {packageInfo.DownloadUrl}. Invalid size: {response.Content.Headers.ContentLength ?? 0} or content-type: {response.Content.Headers.ContentType?.MediaType ?? string.Empty}");
            return false;
        }

        await using var fs = new FileStream(destt, FileMode.CreateNew);
        await response.Content.CopyToAsync(fs);
        return true;
    }

    private static List<VarPackageName> FindMissingReferences(IList<VarPackage> vars, IList<FreeFile> freeFiles)
    {
        var jsonFiles = vars.Where(t => t.IsInVaMDir).SelectMany(t => t.JsonFiles)
            .Concat(freeFiles.Where(t => t.IsInVaMDir && t.JsonFile != null).Select(t => t.JsonFile!));
        var varIndex = vars.ToLookup(t => t.Name.PackageNameWithoutVersion, StringComparer.OrdinalIgnoreCase);
        var unresolvedVars = jsonFiles
            .SelectMany(t => t.Missing)
            .Select(t => t.EstimatedVarName)
            .Where(t => t != null)
            .Select(t => t!)
            .DistinctBy(t => t.Filename)
            .Where(t => {
                if (!varIndex.Contains(t.PackageNameWithoutVersion)) return true; // we don't have it at all
                if (t.Version == -1) return false; // we have and we want anything, ignore
                if (varIndex[t.PackageNameWithoutVersion].Any(x => x.Name.Version == t.Version)) return false; // we have it, ignore
                if (t.MinVersion && varIndex[t.PackageNameWithoutVersion].Any(x => x.Name.Version >= t.Version)) return false; // we have it, ignore

                return true;

            })
            .ToList();
        return unresolvedVars;
    }

    private static async Task<List<PackageInfo>> QueryVam(IReadOnlyCollection<VarPackageName> unresolvedVars, IEnumerable<VarPackageName> existingVarsList)
    {
        using var httpClient = new HttpClient {
            BaseAddress = new Uri("https://hub.virtamate.com"),
            Timeout = TimeSpan.FromMinutes(5)
        };
        var service = RestClient.For<IVamService>(httpClient);
        var packagesToQuery = unresolvedVars.Select(t => t.Filename);
        packagesToQuery = packagesToQuery
            .Concat(existingVarsList.Select(t => t.PackageNameWithoutVersion))
            .Distinct(StringComparer.InvariantCultureIgnoreCase);

        var query = new VamQuery { Packages = string.Join(',', packagesToQuery) };
        var result = await service.FindPackages(query);
        var packagesToDownload = result.Packages.Values
            .Where(t => !string.IsNullOrEmpty(t.DownloadUrl) && t.DownloadUrl != "null")
            .ToList();
        var validDownloads = packagesToDownload.Where(t => !t.DownloadUrl.EndsWith("?file=", StringComparison.OrdinalIgnoreCase)).ToList();
        var invalidDownloadsToReQuery = packagesToDownload.Except(validDownloads).Select(t => t.Filename).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct();

        query = new VamQuery { Packages = string.Join(',', invalidDownloadsToReQuery) };
        result = await service.FindPackages(query);
        packagesToDownload = result.Packages.Values
            .Where(t => !string.IsNullOrEmpty(t.DownloadUrl) && t.DownloadUrl != "null")
            .ToList();
        validDownloads.AddRange(packagesToDownload.Where(t => !t.DownloadUrl.EndsWith("?file=", StringComparison.OrdinalIgnoreCase)));

        var existingVarsSet = existingVarsList.Select(t => t.Filename).ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        validDownloads.RemoveAll(t => existingVarsSet.Contains(t.Filename));

        return validDownloads.DistinctBy(t => t.DownloadUrl).ToList();
    }
}

#pragma warning disable CA2227 // Collection properties should be read only
#pragma warning disable CA1056
[ExcludeFromCodeCoverage]
public class VamResult
{
    [JsonProperty("packages")]
    public Dictionary<string, PackageInfo> Packages { get; set; } = null!;
}

[ExcludeFromCodeCoverage]
public class PackageInfo
{
    [JsonProperty("filename")]
    public string Filename { get; set; } = string.Empty;
    [JsonProperty("downloadUrl")]
    public string DownloadUrl { get; set; } = string.Empty;
}

[ExcludeFromCodeCoverage]
public class VamQuery
{
    [JsonProperty("source")]
    public string Source { get; set; } = "VaM";
    [JsonProperty("action")]
    public string Action { get; set; } = "findPackages";
    [JsonProperty("packages")]
    public string Packages { get; set; } = string.Empty;
}


[BaseAddress("https://hub.virtamate.com")]
[Header("User-Agent", "UnityPlayer/2018.1.9f1 (UnityWebRequest/1.0, libcurl/7.51.0-DEV)")]
[Header("X-Unity-Version", "2018.1.9f1")]
public interface IVamService
{
    [Post("citizenx/api.php")]
    Task<VamResult> FindPackages([Body] VamQuery query);
}
#pragma warning restore CA2227 // Collection properties should be read only

public interface IDownloadMissingVars : IOperation
{
    Task ExecuteAsync(OperationContext context, IList<VarPackage> vars, IList<FreeFile> freeFiles);
}