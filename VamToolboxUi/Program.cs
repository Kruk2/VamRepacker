using System.IO.Abstractions;
using System.Text;
using Autofac;
using Ionic.Zip;
using MoreLinq;
using VamToolbox.FilesGrouper;
using VamToolbox.Hashing;
using VamToolbox.Helpers;
using VamToolbox.Logging;
using VamToolbox.Operations.Abstract;
using VamToolbox.Operations.Destructive;
using VamToolbox.Operations.Destructive.VarFixers;
using VamToolbox.Operations.NotDestructive;
using VamToolbox.Sqlite;

namespace VamToolboxUi;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        //var files = Directory.EnumerateFiles(@"D:\Gry\other\vam_small\AddonPackages", "*.var", SearchOption.AllDirectories);
        ////files = files.Concat(Directory.EnumerateFiles(@"D:\Gry\other\vam_test\AddonPackages", "*.var", SearchOption.AllDirectories));
        //files = files.Where(t => File.ResolveLinkTarget(t, true) == null);
        //foreach (var file in files) {
        //    var modified = File.GetLastWriteTimeUtc(file);
        //    var updated = false;
        //    {
        //        using var zipFile = ZipFile.Read(file);
        //        zipFile.CaseSensitiveRetrieval = true;
        //        var rmMorphs = zipFile.Entries.Where(t => t.FileName.NormalizePathSeparators().EndsWith("/RG InOut.vmi", StringComparison.Ordinal) ||
        //                                                  t.FileName.NormalizePathSeparators().EndsWith("/RG Side2Side.vmi", StringComparison.Ordinal) ||
        //                                                  t.FileName.NormalizePathSeparators().EndsWith("/RG UpDown2.vmi", StringComparison.Ordinal) ||
        //                                                  t.FileName.NormalizePathSeparators().EndsWith("/RG InOut.vmb", StringComparison.Ordinal) ||
        //                                                  t.FileName.NormalizePathSeparators().EndsWith("/RG Side2Side.vmb", StringComparison.Ordinal) ||
        //                                                  t.FileName.NormalizePathSeparators().EndsWith("/RG UpDown2.vmb", StringComparison.Ordinal)).ToArray();

        //        if (rmMorphs.Length > 0) {
        //            zipFile.RemoveEntries(rmMorphs);
        //            zipFile.Save();
        //            updated = true;
        //        }
        //    }
        //    if (updated) {
        //        File.SetLastWriteTimeUtc(file, modified);
        //    }

        //}

        Application.ThreadException += CatchUnhandled;
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        AppDomain.CurrentDomain.UnhandledException += CatchUnhandledDomain;

        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var container = Configure();
        EnsureDbCreated(container);
        Application.Run(container.Resolve<MainWindow>());
    }

    private static void EnsureDbCreated(IContainer container)
    {
        using var scope = container.BeginLifetimeScope();
        scope.Resolve<IDatabase>().EnsureCreated();
    }

    private static void CatchUnhandledDomain(object sender, UnhandledExceptionEventArgs e)
    {
        MessageBox.Show((e.ExceptionObject as Exception)!.ToString(), "Unhandled UI Exception");
    }

    private static void CatchUnhandled(object sender, ThreadExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.ToString(), "Unhandled Thread Exception");
    }

    private static IContainer Configure()
    {
        var builder = new ContainerBuilder();

        builder.RegisterType<FileSystem>().As<IFileSystem>().SingleInstance();
        builder.RegisterType<SoftLinker>().As<ISoftLinker>().SingleInstance();
        builder.RegisterType<JsonFileParser>().As<IJsonFileParser>().SingleInstance();
        builder.RegisterType<MainWindow>().As<IProgressTracker>().AsSelf().SingleInstance();

        builder.RegisterType<Logger>().As<ILogger>().InstancePerLifetimeScope();
        builder.Register(_ => new Database(System.AppContext.BaseDirectory)).As<IDatabase>().InstancePerLifetimeScope();
        builder.RegisterType<MD5Helper>().As<IHashingAlgo>().SingleInstance();

        builder.RegisterType<PresetGrouper>().As<IPresetGrouper>();
        builder.RegisterType<MorphGrouper>().As<IMorphGrouper>();
        builder.RegisterType<PreviewGrouper>().As<IPreviewGrouper>();
        builder.RegisterType<ScriptGrouper>().As<IScriptGrouper>();
        builder.RegisterType<FavAndHiddenGrouper>().As<IFavAndHiddenGrouper>();
        builder.RegisterType<FileGroupers>().As<IFileGroupers>();
        builder.RegisterType<ReferenceCache>().As<IReferenceCache>();
        builder.RegisterType<UuidReferencesResolver>().As<IUuidReferenceResolver>();
        builder.RegisterType<ReferencesResolver>().As<IReferencesResolver>();

        var assembly = typeof(ScanVarPackagesOperation).Assembly;
        builder
            .RegisterAssemblyTypes(assembly)
            .AssignableTo<IOperation>()
            .AsImplementedInterfaces()
            .InstancePerLifetimeScope();

        builder
            .RegisterAssemblyTypes(assembly)
            .AssignableTo<IVarFixer>()
            .AsImplementedInterfaces()
            .AsSelf()
            .InstancePerLifetimeScope();

        return builder.Build();
    }
}