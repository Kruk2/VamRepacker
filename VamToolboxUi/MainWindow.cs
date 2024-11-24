using System.Diagnostics;
using System.Reflection;
using Autofac;
using MoreLinq;
using VamToolbox;
using VamToolbox.Helpers;
using VamToolbox.Logging;
using VamToolbox.Models;
using VamToolbox.Operations.Abstract;
using VamToolbox.Operations.Backupers;
using VamToolbox.Operations.Destructive;
using VamToolbox.Operations.Destructive.VarFixers;
using VamToolbox.Operations.NotDestructive;
using VamToolbox.Operations.Repo;
using VamToolbox.Sqlite;

namespace VamToolboxUi;

public partial class MainWindow : Form, IProgressTracker
{
    public const long ReportEveryTicks = 500 * TimeSpan.TicksPerMillisecond;
    private readonly Stopwatch _stopwatch = new();
    private long _nextReport;
    private int _stage, _totalStages;

    private readonly ILifetimeScope _ctx;
    private Dictionary<string, bool> _buttonsState = [];
    private List<ProfileModel> _profiles = [];

    private bool _working;
    private readonly Stopwatch _sw = new();

    public MainWindow(ILifetimeScope ctx)
    {
        _ctx = ctx;
        InitializeComponent();

        Text += Assembly.GetExecutingAssembly().GetName().Version;
        comboThreads.Items.AddRange(Enumerable.Range(1, Environment.ProcessorCount).Cast<object>().ToArray());
        operationStatusLabel.Text = string.Empty;

        LoadSettings();
    }

    private void selectVamDirBtn_Click(object sender, EventArgs e)
    {
        var (selected, vamDir) = AskFirDirectory();
        if (!selected)
            return;
        if (!Directory.Exists(Path.Combine(vamDir, KnownNames.AddonPackages))) {
            MessageBox.Show("VaM dir doesn't contain AddonPackages");
            vamDirTxt.Text = string.Empty;
        } else if (Directory.Exists(additionalVarsDir.Text) && !ArePathsExclusive(additionalVarsDir.Text, vamDir)) {
            MessageBox.Show("VaM dir has to be outside REPO and vice-versa");
            vamDirTxt.Text = string.Empty;
        } else {
            vamDirTxt.Text = vamDir;
        }
    }

    private void additionalVarsBtn_Click(object sender, EventArgs e)
    {
        var (selected, dir) = AskFirDirectory();
        if (!selected)
            return;

        if (Directory.Exists(vamDirTxt.Text) && !ArePathsExclusive(vamDirTxt.Text, dir)) {
            MessageBox.Show("VaM dir has to be outside REPO and vice-versa");
            additionalVarsDir.Text = string.Empty;
        } else {
            additionalVarsDir.Text = dir;
        }
    }

    private async void copyMissingDepsFromRepoBtn_Click(object sender, EventArgs e)
    {
        if (!ValidateSettings()) return;

        var ctx = GetContext(stages: 5);

        await using var scope = _ctx.BeginLifetimeScope();
        await RemoveOldLinks(scope, ctx);
        var (vars, freeFiles) = await ScanJsonFiles(scope, ctx);
        var mode = Enum.Parse<CopyMode>(modeComboBox.Text);
        await scope.Resolve<ICopyMissingVarDependenciesFromRepo>()
            .ExecuteAsync(ctx, vars, freeFiles, mode);

        if (MessageBox.Show("Do you want to try to download missing vars from HUB?", "Hub",
                MessageBoxButtons.YesNo) == DialogResult.Yes) {
            _totalStages++;
            await scope.Resolve<IDownloadMissingVars>().ExecuteAsync(ctx, vars, freeFiles);
        }

        SwitchUI(false);
    }

    private async void softLinkVarsBtn_Click(object sender, EventArgs e)
    {
        if (!ValidateSettings()) return;

        if (profilesListBox.CheckedItems.Count == 0 &&
            MessageBox.Show("Nothing was selected, everything from repo will be linked. Continue?", "", MessageBoxButtons.YesNo) == DialogResult.No) {
            return;
        }

        var ctx = GetContext(stages: 5);
        await using var scope = _ctx.BeginLifetimeScope();
        await RemoveOldLinks(scope, ctx);
        var filters = BuildFilters();
        var (vars, _) = await ScanJsonFiles(scope, ctx, filters);
        await scope.Resolve<ICopySelectedVarsWithDependenciesFromRepo>()
            .ExecuteAsync(ctx, vars, filters);

        SwitchUI(false);
    }

    private static (bool, string) AskFirDirectory(string? root = null)
    {
        using var odf = new FolderBrowserDialog();
        if (root != null)
            odf.SelectedPath = root;
        var result = odf.ShowDialog();
        return (result == DialogResult.OK, odf.SelectedPath.NormalizePathSeparators());
    }

    public void InitProgress(string startingMessage) => RunInvokedInvoke(() => {
        _stopwatch.Start();
        progressBar.Value = 0;
        progressBar.Style = ProgressBarStyle.Blocks;
        MoveToStage(startingMessage);
        SwitchUI(true);
    });

    public void Report(ProgressInfo progress)
    {
        if (_stopwatch.ElapsedTicks <= _nextReport && !progress.ForceShow) {
            return;
        }

        _nextReport = _stopwatch.ElapsedTicks + ReportEveryTicks;
        RunInvokedInvoke(() => {
            operationStatusLabel.Text = progress.Current;
            if (progress.Total == 0) {
                progressBar.Style = ProgressBarStyle.Marquee;
                return;
            } else if (progressBar.Style != ProgressBarStyle.Blocks) {
                progressBar.Style = ProgressBarStyle.Blocks;
            }

            if (progressBar.Maximum != progress.Total)
                progressBar.Maximum = progress.Total;
            progressBar.Value = progress.Processed;
        });
    }

    public void Report(string message, bool forceShow) => Report(new ProgressInfo(message, forceShow));

    public void Complete(string endingMessage) => RunInvokedInvoke(() => {
        progressBar.Style = ProgressBarStyle.Blocks;
        progressBar.Value = progressBar.Maximum;
        operationStatusLabel.Text = endingMessage;
    });


    private void SwitchUI(bool working)
    {
        if (_working == working) return;
        if (working) {
            operationStatusLabel.Text = string.Empty;
            _sw.Restart();
        }

        _working = working;
        var controls = Controls
            .OfType<Control>()
            .Where(t => t is not Label)
            .ToList();

        if (_buttonsState.Count > 0 && working)
            throw new InvalidOperationException();

        if (working) {
            _buttonsState = controls
                .ToDictionary(t => t.Name, t => t.Enabled);
            controls.ForEach(t => t.Enabled = false);
        } else {
            controls.ForEach(t => t.Enabled = _buttonsState[t.Name]);
            _buttonsState.Clear();
        }

        if (!working) {
            _sw.Stop();
            stageTxt.Text = $"Finished in {_sw.Elapsed.Minutes}min and {_sw.Elapsed.Seconds}s";
        }
    }

    private OperationContext GetContext(int stages)
    {
        _totalStages = stages;
        _stage = 0;

        var ctx = new OperationContext {
            DryRun = dryRunCheckbox.Checked,
            Threads = (int)comboThreads.SelectedItem!,
            RepoDir = additionalVarsDir.Text,
            VamDir = vamDirTxt.Text
        };
        return ctx;
    }

    private void RunInvokedInvoke(Action action)
    {
        if (InvokeRequired)
            Invoke(action);
        else
            action();
    }

    private void MainWindow_FormClosing(object sender, FormClosingEventArgs e)
    {
        var appSettings = new AppSettings 
        {
            AdditionalVars = additionalVarsDir.Text,
            VamDir = vamDirTxt.Text,
            Threads = (int)comboThreads.SelectedItem!,
            RemoveSoftLinksBefore = removeAllSoftLinkBeforeChk.Checked,
            Profiles = _profiles,
            CopyMode = string.IsNullOrEmpty(modeComboBox.Text) ? null : Enum.Parse<CopyMode>(modeComboBox.Text)
        };

        {
            using var scope = _ctx.BeginLifetimeScope();
            var database = scope.Resolve<IDatabase>();
            database.SaveSettings(appSettings);
        }
        
        _ctx.Dispose();
    }

    private void LoadSettings()
    {
        using var scope = _ctx.BeginLifetimeScope();
        var database = scope.Resolve<IDatabase>();
        var appSettings = database.LoadSettings();

        additionalVarsDir.Text = appSettings.AdditionalVars;
        vamDirTxt.Text = appSettings.VamDir;
        comboThreads.SelectedItem = appSettings.Threads == 0 ? Environment.ProcessorCount : appSettings.Threads;
        removeAllSoftLinkBeforeChk.Checked = appSettings.RemoveSoftLinksBefore;
        modeComboBox.Text = appSettings.CopyMode is null ? string.Empty : appSettings.CopyMode.ToString();
        _profiles = appSettings.Profiles;

        ReloadProfiles();
    }

    private static async Task<(List<VarPackage> vars, List<FreeFile> freeFiles)> RunIndexing(ILifetimeScope scope, OperationContext operationContext)
    {
        var freeFiles = await scope.Resolve<IScanFilesOperation>()
            .ExecuteAsync(operationContext);
        var vars = await scope.Resolve<IScanVarPackagesOperation>()
            .ExecuteAsync(operationContext, freeFiles);

        return (vars, freeFiles);
    }

    private async void scanJsonFilesBtn_Click(object sender, EventArgs e)
    {
        if (!ValidateSettings()) return;

        await using var scope = _ctx.BeginLifetimeScope();
        var ctx = GetContext(stages: 3);
        await ScanJsonFiles(scope, ctx);
        SwitchUI(false);
    }

    private bool ValidateSettings()
    {
        if (string.IsNullOrEmpty(vamDirTxt.Text) || !Directory.Exists(vamDirTxt.Text)) {
            MessageBox.Show("Select VAM dir first");
            return false;
        }
        if (!string.IsNullOrEmpty(additionalVarsDir.Text) && !Directory.Exists(additionalVarsDir.Text)) {
            MessageBox.Show("REPO dir doesn't exist");
            return false;
        }

        return true;
    }

    private void manageProfilesBtn_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(additionalVarsDir.Text)) {
            MessageBox.Show("Select repo dir virs");
            return;
        }

        using var manageProfiles = new ManageProfiles(_profiles, additionalVarsDir.Text);
        if (manageProfiles.ShowDialog() == DialogResult.OK) {
            _profiles = manageProfiles.Profiles;
            ReloadProfiles();
        }
    }

    private void ReloadProfiles()
    {
        profilesListBox.Items.Clear();
        _profiles.ForEach(t => profilesListBox.Items.Add(t));
    }

    private VarFilters BuildFilters()
    {
        var filters = new VarFilters();
        profilesListBox.CheckedItems
            .Cast<ProfileModel>()
            .ForEach(filters.FromProfile);

        return filters;
    }

    private async void runVarFixersBtn_Click(object sender, EventArgs e)
    {
        if (!ValidateSettings()) return;
        if (removeDependenciesFromMetaChk.Checked && MessageBox.Show("Removing dependencies can be very time and I/O consuming operation (every zip will be rewriten) Continue?", "Warning", MessageBoxButtons.OKCancel) == DialogResult.Cancel) {
            return;
        }

        var ctx = GetContext(stages: 2);
        await using var scope = _ctx.BeginLifetimeScope();
        var vars = await scope.Resolve<IScanVarPackagesOperation>().ExecuteAsync(ctx, []);
        var fixers = new List<IVarFixer>();
        if(disableMorphPreloadChk.Checked) fixers.Add(scope.Resolve<DisableMorphPreloadVarFixer>());
        if(removeDependenciesFromMetaChk.Checked) fixers.Add(scope.Resolve<RemoveDependenciesVarFixer>());
        if(removeVirusMorphsChk.Checked) fixers.Add(scope.Resolve<RemoveVirusMorphsVarFixer>());
        if(removeDsfMorphsChk.Checked) fixers.Add(scope.Resolve<RemoveDsfMorphsVarFixer>());

        await scope.Resolve<IVarFixerOperation>().Execute(ctx, vars, fixers);

        SwitchUI(false);
    }

    private async void clearCache_Click(object sender, EventArgs e)
    {
        SwitchUI(true);
        await using var scope = _ctx.BeginLifetimeScope();
        var db = scope.Resolve<IDatabase>();
         db.ClearCache();
        SwitchUI(false);
    }

    private void clearRepoDirBtn_Click(object sender, EventArgs e) => additionalVarsDir.Text = string.Empty;

    private async void trustAllVarsBtn_Click(object sender, EventArgs e)
    {
        if (!ValidateSettings()) return;

        await using var scope = _ctx.BeginLifetimeScope();
        var ctx = GetContext(stages: 1);
        await scope.Resolve<ITrustAllVarsOperation>().ExecuteAsync(ctx);

        SwitchUI(false);
    }

    private Task RemoveOldLinks(ILifetimeScope scope, OperationContext ctx)
    {
        if (!removeAllSoftLinkBeforeChk.Checked) {
            _stage++;
            return Task.CompletedTask;
        }
        return scope.Resolve<IRemoveSoftLinksAndEmptyDirs>().ExecuteAsync(ctx);
    }

    private async void downloadFromHubBtn_Click(object sender, EventArgs e)
    {
        if (!ValidateSettings()) return;

        var ctx = GetContext(stages: 4);

        await using var scope = _ctx.BeginLifetimeScope();
        var (vars, freeFiles) = await ScanJsonFiles(scope, ctx);
        await scope.Resolve<IDownloadMissingVars>().ExecuteAsync(ctx, vars, freeFiles);
        SwitchUI(false);
    }

    private static async Task<(List<VarPackage> vars, List<FreeFile> freeFiles)> ScanJsonFiles(ILifetimeScope scope, OperationContext ctx, IVarFilters? filters = null)
    {
        var (vars, freeFiles) = await RunIndexing(scope, ctx);
        await scope.Resolve<IScanJsonFilesOperation>().ExecuteAsync(ctx, freeFiles, vars, filters);

        return (vars, freeFiles);
    }

    private async void restoreMetaJsonBtn_Click(object sender, EventArgs e)
    {
        if (!ValidateSettings()) return;

        var ctx = GetContext(stages: 1);

        await using var scope = _ctx.BeginLifetimeScope();
        await scope.Resolve<IMetaFileRestorer>().Restore(ctx);
        SwitchUI(false);
    }

    private void MoveToStage(string text) => stageTxt.Text = $"{_stage++ + 1}/{_totalStages} {text}";

    private static bool ArePathsExclusive(string subPath, string subPath1)
    {
        return !IsSubPathOf(subPath, subPath1) && !IsSubPathOf(subPath1, subPath) && !string.Equals(subPath, subPath1, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSubPathOf(string subPath, string basePath)
    {
        var rel = Path.GetRelativePath(basePath, subPath);
        return rel != "."
               && rel != ".."
               && !rel.StartsWith("../", StringComparison.OrdinalIgnoreCase)
               && !rel.StartsWith(@"..\", StringComparison.OrdinalIgnoreCase)
               && !Path.IsPathRooted(rel);
    }
}