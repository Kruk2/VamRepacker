using System.IO.Abstractions.TestingHelpers;
using AutoFixture;
using FluentAssertions;
using VamToolbox.Models;
using VamToolbox.Operations.Abstract;
using VamToolbox.Operations.Destructive;
using VamToolbox.Operations.Destructive.VarFixers;
using Xunit;

namespace VamToolbox.Tests.Operations;
public class VarFixerOperationTests 
{
    private readonly MockFileSystem _fs;
    private readonly VarFixerOperation _operation;
    private CustomFixture _fixture;
    private const string AddondsDir = "C:/VaM/AddonPackages/";

    public VarFixerOperationTests()
    {
        _fixture = new CustomFixture();
        _fs = _fixture.Create<MockFileSystem>();
        _operation = _fixture.Create<VarFixerOperation>();
    }

    [Fact]
    public async Task Execute_WhenMetaIsMissing_ShouldSkip()
    {
        CreateZipFile();
        await Execute(removeDeps: true, disableMorphs: true);

        var metaFile = ReadMetaJson();
        metaFile.Should().BeNull();
    }


    [Fact]
    public async Task Execute_WhenMetaIsBroken_ShouldSkip()
    {
        CreateZipFile("broken-json");
        await Execute(removeDeps: true, disableMorphs: true);

        var metaFile = ReadMetaJson();
        metaFile.Should().Be("broken-json");
    }

    [Fact]
    public async Task Backup_ShouldNotBackupMetaIfItExists()
    {
        CreateZipFile(TestMetaWithDepsOnly, metaBackup: "old-backup");
        await Execute(removeDeps: true);

        var backupFile = ReadMetaBackupJson();
        var metaFile = ReadMetaJson();
        backupFile.Should().Be("old-backup");
        metaFile.Should().Be(@"{
  ""licenseType"": ""PC"",
  ""creatorName"": ""Chill_PopRun"",
  ""contentList"": [
    ""Saves/scene/private_clubII1.402.json""
  ],
  ""dependencies"": {}
}");
    }

    [Fact]
    public async Task Backup_WhenDryRun_ShouldNotChangeAnything()
    {
        CreateZipFile(TestMetaWithDepsOnly);
        await Execute(removeDeps: true, dryRun: true);

        var backupFile = ReadMetaBackupJson();
        backupFile.Should().BeNull();
    }

    [Fact]
    public async Task Backup_ShouldBackupMetaFile()
    {
        CreateZipFile(TestMetaWithDepsOnly);
        await Execute(removeDeps: true);

        var backupFile = ReadMetaBackupJson();
        backupFile.Should().Be(TestMetaWithDepsOnly);
    }

    [Fact]
    public async Task RemoveDeps_WhenMetaHasDependencies_ShouldClearThem()
    {
        CreateZipFile(TestMetaWithDepsOnly);
        await Execute(removeDeps: true);

        var metaFile = ReadMetaJson();
        metaFile.Should().Be(@"{
  ""licenseType"": ""PC"",
  ""creatorName"": ""Chill_PopRun"",
  ""contentList"": [
    ""Saves/scene/private_clubII1.402.json""
  ],
  ""dependencies"": {}
}");
    }

    [Fact]
    public async Task RemoveDeps_WhenMetaHasDependenciesAndInNestedDir_ShouldClearThem()
    {
        CreateZipFile(TestMetaWithDepsOnly, path: AddondsDir + "test/a.var");
        await Execute(removeDeps: true);

        var metaFile = ReadMetaJson(AddondsDir + "test/a.var");
        metaFile.Should().Be(@"{
  ""licenseType"": ""PC"",
  ""creatorName"": ""Chill_PopRun"",
  ""contentList"": [
    ""Saves/scene/private_clubII1.402.json""
  ],
  ""dependencies"": {}
}");
    }


    [Fact]
    public async Task RemoveDeps_WhenMetaHasBrokenReferences_ShouldClearThem()
    {
        CreateZipFile(TestMetaWithBrokenReferences);
        await Execute(removeDeps: true);

        var metaFile = ReadMetaJson();
        metaFile.Should().Be(@"{
  ""licenseType"": ""PC"",
  ""creatorName"": ""Chill_PopRun"",
  ""contentList"": [
    ""Saves/scene/private_clubII1.402.json"",
    ""Saves/scene/private_clubII1.402.jpg""
  ],
  ""dependencies"": {},
  ""customOptions"": {
    ""preloadMorphs"": ""false""
  }
}");
    }

    [Fact]
    public async Task DisableMorphs_WhenMorphsAreEnabledAndItsNotMorphpack_ShouldDisable()
    {
        CreateZipFile(TestMetaWithMorphPreloadEnabled);
        await Execute(disableMorphs: true, addMorphFile: false);

        var metaFile = ReadMetaJson();
        metaFile.Should().Be(@"{
  ""licenseType"": ""PC"",
  ""creatorName"": ""Chill_PopRun"",
  ""contentList"": [
    ""Saves/scene/private_clubII1.402.json""
  ],
  ""dependencies"": {},
  ""customOptions"": {
    ""test2"": true,
    ""preloadMorphs"": ""false""
  }
}");
    }

    [Fact]
    public async Task DisableMorphs_WhenMorphsAreEnabledAndItsMorphpack_ShouldKeepEnabled()
    {
        CreateZipFile(TestMetaWithMorphPreloadEnabled);
        await Execute(disableMorphs: true, addMorphFile: true);

        var metaFile = ReadMetaJson();
        metaFile.Should().Be(TestMetaWithMorphPreloadEnabled);
    }

    [Fact]
    public async Task DisableMorphs_WhenMorphsAreDisabled_ShouldNotChangeAnything()
    {
        CreateZipFile(TestMetaWithMorphPreloadDisabled);
        await Execute(disableMorphs: true);

        var metaFile = ReadMetaJson();
        metaFile.Should().Be(TestMetaWithMorphPreloadDisabled);
    }

    [Fact]
    public async Task DisableMorphs_WhenCustomOptionsAreMissing_ShouldNotChangeAnything()
    {
        CreateZipFile(TestMetaWithMorphPreloadMissingCustomOptions);
        await Execute(disableMorphs: true);

        var metaFile = ReadMetaJson();
        metaFile.Should().Be(TestMetaWithMorphPreloadMissingCustomOptions);
    }

    [Fact]
    public async Task Execute_WhenThereIsNothingToFix_SkipWriting()
    {
        CreateZipFile(TestMetaWithNothingToFix);
        await Execute(removeDeps: true, disableMorphs: true);

        var metaFile = ReadMetaJson();
        metaFile.Should().Be(TestMetaWithNothingToFix);
    }

    private string? ReadMetaJson(string? path = null)
    {

        var file = _fs.GetFile(path ?? (AddondsDir + "a.var"));
        var files = ZipTestHelpers.ReadZipFile(file);
        files.TryGetValue("meta.json", out var metaContent);
        return metaContent;
    }

    private string? ReadMetaBackupJson(string? path = null)
    {
        var file = _fs.GetFile(path ?? (AddondsDir + "a.var"));
        var files = ZipTestHelpers.ReadZipFile(file);
        files.TryGetValue("meta.json.toolboxbak", out var metaBackup);

        return metaBackup;
    }

    private void CreateZipFile(string? metaContent = null, string? metaBackup = null, string? path = null)
    {
        var files = new Dictionary<string, string>();
        if (metaContent is not null) {
            files["meta.json"] = metaContent;
        }
        if (metaBackup is not null) {
            files["meta.json.toolboxbak"] = metaBackup;
        }

        var zipFile = ZipTestHelpers.CreateMockFile(files);
        _fs.AddFile(path ?? (AddondsDir + "a.var"), zipFile);
    }

    private Task Execute(bool dryRun = false, bool removeDeps = false, bool disableMorphs = false, bool addMorphFile = false)
    {
        var varFiles = _fs.AllFiles.Select(t => new VarPackage(_fixture.Create<VarPackageName>(), t, null, false, 0, DateTime.UtcNow)).ToList();
        if (addMorphFile) {
            varFiles.ForEach(t => _ = new VarPackageFile(KnownNames.MaleMorphsDir + "/a.vmb", 0, false, t, DateTime.Now));
        } else {
            varFiles.ForEach(t => _ = new VarPackageFile(KnownNames.HairPresetsDir + "/a.vam", 0, false, t, DateTime.Now));
        }

        var fixers = new List<IVarFixer>();
        if(disableMorphs) fixers.Add(_fixture.Create<DisableMorphPreloadVarFixer>());
        if(removeDeps) fixers.Add(_fixture.Create<RemoveDependenciesVarFixer>());

        return _operation.Execute(new OperationContext {
            DryRun = dryRun,
            VamDir = "C:/VaM",
            Threads = 1
        }, varFiles, fixers);
    }

    private const string TestMetaWithMorphPreloadMissingCustomOptions = @"{ 
   ""licenseType"" : ""PC"", 
   ""creatorName"" : ""Chill_PopRun"", 
   ""contentList"" : [ 
      ""Saves/scene/private_clubII1.402.json""
   ], 
   ""dependencies"" : { },
   ""customOptions"" : { 
      ""preloadMorphs"" : ""false""
   }
}";

    private const string TestMetaWithMorphPreloadDisabled = @"{ 
   ""licenseType"" : ""PC"", 
   ""creatorName"" : ""Chill_PopRun"", 
   ""contentList"" : [ 
      ""Saves/scene/private_clubII1.402.json""
   ], 
   ""dependencies"" : { },
   ""customOptions"" : { 
      ""preloadMorphs"" : ""false"",
      ""test1"" : ""qqq"",
   }
}";

    private const string TestMetaWithMorphPreloadEnabled = @"{ 
   ""licenseType"" : ""PC"", 
   ""creatorName"" : ""Chill_PopRun"", 
   ""contentList"" : [ 
      ""Saves/scene/private_clubII1.402.json""
   ], 
   ""dependencies"" : { },
   ""customOptions"" : { 
      ""test2"": true,
      ""preloadMorphs"" : ""true""
   }
}";

    private const string TestMetaWithNothingToFix = @"{ 
   ""licenseType"" : ""PC"", 
   ""creatorName"" : ""Chill_PopRun"", 
   ""contentList"" : [ 
      ""Saves/scene/private_clubII1.402.json"", 
      ""Saves/scene/private_clubII1.402.jpg""
   ], 
   ""dependencies"" : { },
   ""customOptions"" : { 
      ""preloadMorphs"" : ""false""
   }
}";

    private const string TestMetaWithDepsOnly = @"{ 
   ""licenseType"" : ""PC"", 
   ""creatorName"" : ""Chill_PopRun"", 
   ""contentList"" : [ 
      ""Saves/scene/private_clubII1.402.json""
   ], 
   ""dependencies"" : { 
      ""ToumeiHitsuji.SlapStuffAudioPack.latest"" : { 
         ""licenseType"" : ""CC BY-SA"", 
         ""dependencies"" : { 
         }
      }, 
      ""rz67vr.cyber_room_2.latest"" : { 
         ""licenseType"" : ""CC BY"", 
         ""dependencies"" : { 
         }
      }, 
      ""MacGruber.LogicBricks.12"" : { 
         ""licenseType"" : ""PC EA"", 
         ""dependencies"" : { 
         }
      }, 
      ""AcidBubbles.Timeline.252"" : { 
         ""licenseType"" : ""CC BY-SA"", 
         ""dependencies"" : { 
         }
      }, 
      ""ToumeiHitsuji.SlapStuff.2"" : { 
         ""licenseType"" : ""CC BY-SA"", 
         ""dependencies"" : { 
            ""ToumeiHitsuji.SlapStuffAudioPack.2"" : { 
               ""licenseType"" : ""CC BY-SA"", 
               ""dependencies"" : { 
               }
            }
         }
      }
   }
}";

    private const string TestMetaWithBrokenReferences = @"{ 
   ""licenseType"" : ""PC"", 
   ""creatorName"" : ""Chill_PopRun"", 
   ""contentList"" : [ 
      ""Saves/scene/private_clubII1.402.json"", 
      ""Saves/scene/private_clubII1.402.jpg""
   ], 
   ""dependencies"" : { 
      ""ToumeiHitsuji.SlapStuffAudioPack.latest"" : { 
         ""licenseType"" : ""CC BY-SA"", 
         ""dependencies"" : { 
         }
      }, 
      ""rz67vr.cyber_room_2.latest"" : { 
         ""licenseType"" : ""CC BY"", 
         ""dependencies"" : { 
         }
      }, 
      ""MacGruber.LogicBricks.12"" : { 
         ""licenseType"" : ""PC EA"", 
         ""dependencies"" : { 
         }
      }, 
      ""AcidBubbles.Timeline.252"" : { 
         ""licenseType"" : ""CC BY-SA"", 
         ""dependencies"" : { 
         }
      }, 
      ""ToumeiHitsuji.SlapStuff.2"" : { 
         ""licenseType"" : ""CC BY-SA"", 
         ""dependencies"" : { 
            ""ToumeiHitsuji.SlapStuffAudioPack.2"" : { 
               ""licenseType"" : ""CC BY-SA"", 
               ""dependencies"" : { 
               }
            }
         }
      }
   }, 
   ""customOptions"" : { 
      ""preloadMorphs"" : ""false""
   }, 
   ""hadReferenceIssues"" : ""true"", 
   ""referenceIssues"" : [ 
      { 
         ""reference"" : ""Chill_PopRun.private_clubII1_2.1:/private_clubII/f_start_pose"", 
         ""issue"" : ""BROKEN: Missing package that is referenced""
      }, 
      { 
         ""reference"" : ""Chill_PopRun.private_clubII1_2.1:/Custom/SubScene/Chill_PopRun/Chill_PopRun/3-light/chill_light.json"", 
         ""issue"" : ""BROKEN: Missing package that is referenced""
      }
   ]
}";
}
