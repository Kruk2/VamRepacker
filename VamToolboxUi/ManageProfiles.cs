using MoreLinq;
using VamToolbox.Helpers;
using VamToolbox.Operations.Repo;

namespace VamToolboxUi;

public partial class ManageProfiles : Form
{
    private readonly List<ProfileModel> _originalProfiles;
    private readonly string _repoDir;

    private ProfileModel SelectedProfile => (profileList.SelectedItem as ProfileModel)!;
    public List<ProfileModel> Profiles => profileList.Items.Cast<ProfileModel>().ToList();

    public ManageProfiles(List<ProfileModel> profileModels, string repoDir)
    {
        InitializeComponent();
        _originalProfiles = profileModels;
        _repoDir = repoDir.NormalizePathSeparators();

        var profiles = new List<ProfileModel>(_originalProfiles.Select(t => (ProfileModel)t.Clone()));
        profileList.Items.AddRange(profiles.ToArray());
    }

    private void deleteBtn_Click(object sender, System.EventArgs e)
    {
        if (SelectedProfile != null && MessageBox.Show("Do you want to delete profile?", SelectedProfile.Name, MessageBoxButtons.YesNo) == DialogResult.Yes) {
            profileList.Items.Remove(SelectedProfile);
        }

        if (profileList.Items.Count == 0) {
            filesList.Items.Clear();
            dirList.Items.Clear();
        }
    }

    private void cancelBtn_Click(object sender, System.EventArgs e)
    {
        if (MessageBox.Show("Are you sure you want to quit without saving?", "", MessageBoxButtons.YesNoCancel) == DialogResult.Yes)
            Close();
    }

    private void saveBtn_Click(object sender, System.EventArgs e)
    {
        DialogResult = DialogResult.OK;
        Close();
    }

    private void addNewBtn_Click(object sender, System.EventArgs e)
    {
        var name = AskForName();
        if (string.IsNullOrWhiteSpace(name)) return;
        var id = profileList.Items.Add(new ProfileModel([], [], name));
        profileList.SelectedIndex = id;
    }

    private void addDirBtn_Click(object sender, System.EventArgs e)
    {
        if (SelectedProfile is null) return;

        using var odf = new FolderBrowserDialog { SelectedPath = _repoDir };
        var result = odf.ShowDialog();
        if (result != DialogResult.OK) return;

        var dir = odf.SelectedPath.NormalizePathSeparators();
        if (!ValidatePaths(dir))
            return;

        SelectedProfile.Dirs.Add(dir);
        dirList.Items.Clear();
        SelectedProfile.Dirs.ForEach(t => dirList.Items.Add(t));
    }

    private bool ValidatePaths(params string[] paths)
    {
        if (paths.Any(t => !t.StartsWith(_repoDir, StringComparison.Ordinal))) {
            MessageBox.Show($"Invalid paths. Verify if they are located in {_repoDir}");
            return false;
        }

        return true;
    }

    private void addFileBtn_Click(object sender, System.EventArgs e)
    {
        if (SelectedProfile is null) return;

        using var odf = new OpenFileDialog { InitialDirectory = _repoDir, Filter = "Var Files|*.var", Multiselect = true };
        var result = odf.ShowDialog();
        if (result != DialogResult.OK) return;

        var files = odf.FileNames.Select(t => t.NormalizePathSeparators()).ToArray();
        if (!ValidatePaths(files))
            return;

        files.ForEach(t => SelectedProfile.Files.Add(t));
        filesList.Items.Clear();
        SelectedProfile.Files.ForEach(t => filesList.Items.Add(t));
    }

    private void removeDirsBtn_Click(object sender, System.EventArgs e) => RemoveSelected(dirList);
    private void removeFilesBtn_Click(object sender, System.EventArgs e) => RemoveSelected(filesList);

    private void RemoveSelected(ListBox listBox)
    {
        if (SelectedProfile is null) return;

        while (listBox.SelectedItems.Count > 0) {
            var selected = listBox.SelectedItems[0]!;
            listBox.Items.Remove(selected);

            var list = ReferenceEquals(listBox, dirList) ? SelectedProfile.Dirs : SelectedProfile.Files;
            list.Remove((selected as string)!);
        }
    }

    private void profileList_SelectedIndexChanged(object sender, System.EventArgs e)
    {
        filesList.Items.Clear();
        dirList.Items.Clear();

        if (SelectedProfile is null) return;
        SelectedProfile.Files.ForEach(t => filesList.Items.Add(t));
        SelectedProfile.Dirs.ForEach(t => dirList.Items.Add(t));
    }

    private void renameBtn_Click(object sender, System.EventArgs e)
    {
        if (SelectedProfile is null) return;

        var name = AskForName(SelectedProfile.Name);
        if (string.IsNullOrWhiteSpace(name)) return;

        var selectedIndex = profileList.SelectedIndex;
        var profile = SelectedProfile;
        var profiles = Profiles;
        profiles.Remove(SelectedProfile);

        var newProfile = new ProfileModel(profile.Files, profile.Dirs, name);
        profiles.Insert(selectedIndex, newProfile);

        profileList.Items.Clear();
        profiles.ForEach(t => profileList.Items.Add(t));
        profileList.SelectedIndex = selectedIndex;

    }

    private static string? AskForName(string initialValue = "")
    {
        var size = new Size(200, 70);
        var inputBox = new Form { FormBorderStyle = FormBorderStyle.FixedDialog, ClientSize = size, Text = "Name", StartPosition = FormStartPosition.CenterScreen };

        var profileNameTxt = new TextBox {
            Size = new Size(size.Width - 10, 23), Location = new Point(5, 5), Text = initialValue
        };
        inputBox.Controls.Add(profileNameTxt);

        var okButton = new Button {
            DialogResult = DialogResult.OK,
            Size = new Size(75, 23),
            Text = "OK",
            Location = new Point(size.Width - 80 - 80, 39)
        };
        inputBox.Controls.Add(okButton);

        var cancelButton = new Button {
            DialogResult = DialogResult.Cancel,
            Size = new Size(75, 23),
            Text = "Cancel",
            Location = new Point(size.Width - 80, 39)
        };
        inputBox.Controls.Add(cancelButton);

        inputBox.AcceptButton = okButton;
        inputBox.CancelButton = cancelButton;

        return inputBox.ShowDialog() == DialogResult.OK ? profileNameTxt.Text : null;
    }
}