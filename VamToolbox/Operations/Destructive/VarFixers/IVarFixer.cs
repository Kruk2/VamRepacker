using Ionic.Zip;
using VamToolbox.Models;

namespace VamToolbox.Operations.Destructive.VarFixers;

public interface IVarFixer
{
    bool Process(VarPackage var, ZipFile zip, Lazy<IDictionary<string, object>?> metaFileLazy);
}