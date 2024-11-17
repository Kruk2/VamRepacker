using Ionic.Zip;
using VamToolbox.Logging;
using VamToolbox.Models;

namespace VamToolbox.Operations.Destructive.VarFixers;

public class RemoveVirusMorphsVarFixer : IVarFixer
{
    public static readonly string[] VirusMorphs = [
        "RG InOut.vmi", "RG InOut.vmb",
        "RG Side2Side.vmi", "RG Side2Side.vmb",
        "RG UpDown2.vmi", "RG UpDown2.vmb"
    ];

    private readonly ILogger _logger;
    public RemoveVirusMorphsVarFixer(ILogger logger) => _logger = logger;

    public bool Process(VarPackage var, ZipFile zip, Lazy<IDictionary<string, object>?> metaFileLazy)
    {
        var rmMorphs = zip.Entries.Where(IsVirusMorph).ToArray();

        if (rmMorphs.Length > 0) {
            _logger.Log($"Removing {rmMorphs.Length} virus morphs from {var.FullPath}");
            zip.RemoveEntries(rmMorphs);
            return true;
        }

        return false;
    }

    private static bool IsVirusMorph(ZipEntry zipFile)
    {
        if (zipFile.IsDirectory) return false;
        var fileName = Path.GetFileName(zipFile.FileName);
        return VirusMorphs.Any(t => t.Equals(fileName, StringComparison.OrdinalIgnoreCase));
    }
}