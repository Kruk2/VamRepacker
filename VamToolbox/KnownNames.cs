using VamToolbox.Models;

namespace VamToolbox;

public static class KnownNames
{
    public static readonly string[] ExtReferencesToPresets = [".json", ".vap", ".vaj", ".vam"];
    public static readonly string[] PreviewExtensions = [".jpg", ".jpeg", ".png"];
    public static readonly string[] VirusMorphs = ["RG UpDown2", "RG Side2Side", "RG InOut"];

    public const string BackupExtension = ".toolboxbak";
    public const string AddonPackagesUserPrefs = "AddonPackagesUserPrefs";
    public const string AddonPackages = "AddonPackages";

    public const string MorphsDir = "Custom/Atom/Person/Morphs";
    public const string FemaleMorphsDir = "Custom/Atom/Person/Morphs/female";
    public const string FemaleGenMorphsDir = "Custom/Atom/Person/Morphs/female_genitalia";
    public const string MaleMorphsDir = "Custom/Atom/Person/Morphs/male";
    public const string MaleGenMorphsDir = "Custom/Atom/Person/Morphs/male_genitalia";
    public static readonly string[] MorphDirs = [FemaleMorphsDir, FemaleGenMorphsDir, MaleMorphsDir, MaleGenMorphsDir];

    public const string FemaleHairDir = "Custom/Hair/Female";
    public const string MaleHairDir = "Custom/Hair/Male";

    public const string FemaleClothDir = "Custom/Clothing/Female";
    public const string MaleClothDir = "Custom/Clothing/Male";
    public const string SharedClothDir = "Custom/Clothing/Shared";
    public const string NeutralClothDir = "Custom/Clothing/Neutral";

    public const string FemalePositionsDir = "Custom/Positions/Female";
    public const string MalePositionsDir = "Custom/Positions/Male";
    public const string TogetherPositionsDir = "Custom/Positions/Together";

    public const string GlutePhysicsDir = "Custom/Atom/Person/GlutePhysics";
    public const string BreastPhysicsDir = "Custom/Atom/Person/BreastPhysics";
    public const string PluginsPresetDir = "Custom/PluginPresets";
    public const string AppearancePresetsDir = "Custom/Atom/Person/Appearance";
    public const string ClothingPresetsDir = "Custom/Atom/Person/Clothing";
    public const string HairPresetsDir = "Custom/Atom/Person/Hair";
    public const string PosePresetsDir = "Custom/Atom/Person/Pose";
    public const string SkinPresetsDir = "Custom/Atom/Person/Skin";

    public static readonly string[] KnownDirs = [
        FemaleMorphsDir,FemaleGenMorphsDir,  MaleMorphsDir,  MaleGenMorphsDir, 
        FemaleHairDir,  MaleHairDir, 
        FemaleClothDir, MaleClothDir, SharedClothDir, NeutralClothDir,
        FemalePositionsDir, MalePositionsDir, TogetherPositionsDir,
        GlutePhysicsDir, BreastPhysicsDir, PluginsPresetDir,
        AppearancePresetsDir, ClothingPresetsDir,
        HairPresetsDir, PosePresetsDir, SkinPresetsDir
    ];

    public static bool IsPotentialJsonFile(string ext) => ext is ".json" or ".vap" or ".vaj" or ".uiap";

    public static bool IsOtherCloth(this string localPath) => localPath.IsInDir(SharedClothDir) || localPath.IsInDir(NeutralClothDir);
    private static bool IsInDir(this string localPath, string dir) => localPath.Contains(dir, StringComparison.OrdinalIgnoreCase);

    public static AssetType ClassifyType(this string ext, string localPath)
    {
        if (ext is ".vmi" or ".vmb" or ".dsf") {
            if (localPath.IsInDir(FemaleGenMorphsDir))
                return AssetType.FemaleGenMorph;
            if (localPath.IsInDir(FemaleMorphsDir))
                return AssetType.FemaleNormalMorph;
            if (localPath.IsInDir(MaleGenMorphsDir))
                return AssetType.MaleGenMorph;
            if (localPath.IsInDir(MaleMorphsDir))
                return AssetType.MaleNormalMorph;
            return AssetType.UnknownMorph;
        }

        if (ext is ".vaj" or ".vam" or ".vab") {
            if (localPath.IsInDir(FemaleHairDir))
                return AssetType.FemaleHair;
            if (localPath.IsInDir(FemaleClothDir))
                return AssetType.FemaleCloth;
            if (localPath.IsInDir(MaleHairDir))
                return AssetType.MaleHair;
            if (localPath.IsInDir(MaleClothDir))
                return AssetType.MaleCloth;
            if (localPath.IsOtherCloth())
                return AssetType.OtherCloth;
            return AssetType.UnknownClothOrHair;
        }
        if (ext is ".vap") {
            if (localPath.IsInDir(BreastPhysicsDir)) {
                return AssetType.BreastPhysics;
            }
            if (localPath.IsInDir(GlutePhysicsDir)) {
                return AssetType.GlutePhysics;
            }
            if (localPath.IsInDir(PluginsPresetDir)) {
                return AssetType.PluginsPreset;
            }
            if (localPath.IsInDir(FemalePositionsDir)) {
                return AssetType.FemalePosition;
            }
            if (localPath.IsInDir(MalePositionsDir)) {
                return AssetType.MalePosition;
            }
            if (localPath.IsInDir(TogetherPositionsDir)) {
                return AssetType.TogetherPosition;
            }
            if (localPath.IsInDir(AppearancePresetsDir)) {
                return AssetType.AppearancePreset;
            }
            if (localPath.IsInDir(ClothingPresetsDir)) {
                return AssetType.ClothingPreset;
            }
            if (localPath.IsInDir(HairPresetsDir)) {
                return AssetType.HairPreset;
            }
            if (localPath.IsInDir(MorphsDir)) {
                return AssetType.MorphPreset;
            }
            if (localPath.IsInDir(PosePresetsDir)) {
                return AssetType.PosePreset;
            }
            if (localPath.IsInDir(SkinPresetsDir)) {
                return AssetType.SkinPreset;
            }
            if (localPath.IsInDir(FemaleClothDir)) {
                return AssetType.FemaleClothPreset;
            }
            if (localPath.IsInDir(MaleClothDir)) {
                return AssetType.MaleClothPreset;
            }
            if (localPath.IsInDir(FemaleHairDir)) {
                return AssetType.FemaleHairPreset;
            }
            if (localPath.IsInDir(MaleHairDir)) {
                return AssetType.MaleHairPreset;
            }
            // glass preset? interesting
        }

        return AssetType.Unknown;
    }
}