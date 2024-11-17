
namespace VamToolboxUi
{
    partial class MainWindow
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            label1 = new Label();
            vamDirTxt = new TextBox();
            selectVamDirBtn = new Button();
            label2 = new Label();
            additionalVarsDir = new TextBox();
            additionalVarsBtn = new Button();
            progressBar = new ProgressBar();
            operationStatusLabel = new Label();
            copyVarsFromRepoBtn = new Button();
            dryRunCheckbox = new CheckBox();
            copyMissingDepsFromRepoBtn = new Button();
            scanJsonFilesBtn = new Button();
            clearCacheBtn = new Button();
            groupBox2 = new GroupBox();
            restoreMetaJsonBtn = new Button();
            downloadFromHubBtn = new Button();
            trustAllVarsBtn = new Button();
            groupBox3 = new GroupBox();
            profilesListBox = new CheckedListBox();
            manageProfilesBtn = new Button();
            removeAllSoftLinkBeforeChk = new CheckBox();
            label4 = new Label();
            comboThreads = new ComboBox();
            clearRepoDirBtn = new Button();
            stageTxt = new Label();
            groupBox4 = new GroupBox();
            modeComboBox = new ComboBox();
            label3 = new Label();
            groupBox1 = new GroupBox();
            removeDsfMorphsChk = new CheckBox();
            removeVirusMorphsChk = new CheckBox();
            runVarFixersBtn = new Button();
            removeDependenciesFromMetaChk = new CheckBox();
            disableMorphPreloadChk = new CheckBox();
            groupBox2.SuspendLayout();
            groupBox3.SuspendLayout();
            groupBox4.SuspendLayout();
            groupBox1.SuspendLayout();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(14, 11);
            label1.Name = "label1";
            label1.Size = new Size(104, 19);
            label1.TabIndex = 0;
            label1.Text = "VAM directory: ";
            // 
            // vamDirTxt
            // 
            vamDirTxt.Location = new Point(121, 8);
            vamDirTxt.Margin = new Padding(3, 4, 3, 4);
            vamDirTxt.Name = "vamDirTxt";
            vamDirTxt.ReadOnly = true;
            vamDirTxt.Size = new Size(343, 26);
            vamDirTxt.TabIndex = 1;
            // 
            // selectVamDirBtn
            // 
            selectVamDirBtn.Location = new Point(472, 6);
            selectVamDirBtn.Margin = new Padding(3, 4, 3, 4);
            selectVamDirBtn.Name = "selectVamDirBtn";
            selectVamDirBtn.Size = new Size(138, 29);
            selectVamDirBtn.TabIndex = 2;
            selectVamDirBtn.Text = "Select";
            selectVamDirBtn.UseVisualStyleBackColor = true;
            selectVamDirBtn.Click += selectVamDirBtn_Click;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(14, 51);
            label2.Name = "label2";
            label2.Size = new Size(81, 19);
            label2.TabIndex = 3;
            label2.Text = "VARs REPO:";
            // 
            // additionalVarsDir
            // 
            additionalVarsDir.Location = new Point(121, 46);
            additionalVarsDir.Margin = new Padding(3, 4, 3, 4);
            additionalVarsDir.Name = "additionalVarsDir";
            additionalVarsDir.ReadOnly = true;
            additionalVarsDir.Size = new Size(343, 26);
            additionalVarsDir.TabIndex = 4;
            // 
            // additionalVarsBtn
            // 
            additionalVarsBtn.Location = new Point(472, 46);
            additionalVarsBtn.Margin = new Padding(3, 4, 3, 4);
            additionalVarsBtn.Name = "additionalVarsBtn";
            additionalVarsBtn.Size = new Size(70, 29);
            additionalVarsBtn.TabIndex = 5;
            additionalVarsBtn.Text = "Select";
            additionalVarsBtn.UseVisualStyleBackColor = true;
            additionalVarsBtn.Click += additionalVarsBtn_Click;
            // 
            // progressBar
            // 
            progressBar.Location = new Point(3, 765);
            progressBar.Margin = new Padding(3, 4, 3, 4);
            progressBar.Name = "progressBar";
            progressBar.Size = new Size(711, 53);
            progressBar.TabIndex = 7;
            // 
            // operationStatusLabel
            // 
            operationStatusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            operationStatusLabel.AutoSize = true;
            operationStatusLabel.Location = new Point(3, 723);
            operationStatusLabel.Name = "operationStatusLabel";
            operationStatusLabel.Size = new Size(46, 38);
            operationStatusLabel.TabIndex = 8;
            operationStatusLabel.Text = "status\r\nstatus";
            operationStatusLabel.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // copyVarsFromRepoBtn
            // 
            copyVarsFromRepoBtn.Location = new Point(550, 24);
            copyVarsFromRepoBtn.Margin = new Padding(3, 4, 3, 4);
            copyVarsFromRepoBtn.Name = "copyVarsFromRepoBtn";
            copyVarsFromRepoBtn.Size = new Size(154, 56);
            copyVarsFromRepoBtn.TabIndex = 9;
            copyVarsFromRepoBtn.Text = "Soft-link selected profile(s) to VAM";
            copyVarsFromRepoBtn.UseVisualStyleBackColor = true;
            copyVarsFromRepoBtn.Click += softLinkVarsBtn_Click;
            // 
            // dryRunCheckbox
            // 
            dryRunCheckbox.AutoSize = true;
            dryRunCheckbox.Location = new Point(574, 28);
            dryRunCheckbox.Margin = new Padding(3, 4, 3, 4);
            dryRunCheckbox.Name = "dryRunCheckbox";
            dryRunCheckbox.Size = new Size(75, 23);
            dryRunCheckbox.TabIndex = 12;
            dryRunCheckbox.Text = "Dry run";
            dryRunCheckbox.UseVisualStyleBackColor = true;
            // 
            // copyMissingDepsFromRepoBtn
            // 
            copyMissingDepsFromRepoBtn.Location = new Point(545, 25);
            copyMissingDepsFromRepoBtn.Margin = new Padding(3, 4, 3, 4);
            copyMissingDepsFromRepoBtn.Name = "copyMissingDepsFromRepoBtn";
            copyMissingDepsFromRepoBtn.Size = new Size(154, 95);
            copyMissingDepsFromRepoBtn.TabIndex = 13;
            copyMissingDepsFromRepoBtn.Text = "Search for missing dependencies in VAM and copy/soft-link them from REPO";
            copyMissingDepsFromRepoBtn.UseVisualStyleBackColor = true;
            copyMissingDepsFromRepoBtn.Click += copyMissingDepsFromRepoBtn_Click;
            // 
            // scanJsonFilesBtn
            // 
            scanJsonFilesBtn.Location = new Point(617, 6);
            scanJsonFilesBtn.Margin = new Padding(3, 4, 3, 4);
            scanJsonFilesBtn.Name = "scanJsonFilesBtn";
            scanJsonFilesBtn.Size = new Size(97, 68);
            scanJsonFilesBtn.TabIndex = 15;
            scanJsonFilesBtn.Text = "Scan";
            scanJsonFilesBtn.UseVisualStyleBackColor = true;
            scanJsonFilesBtn.Click += scanJsonFilesBtn_Click;
            // 
            // clearCacheBtn
            // 
            clearCacheBtn.Location = new Point(238, 66);
            clearCacheBtn.Margin = new Padding(3, 4, 3, 4);
            clearCacheBtn.Name = "clearCacheBtn";
            clearCacheBtn.Size = new Size(147, 29);
            clearCacheBtn.TabIndex = 17;
            clearCacheBtn.Text = "Clear internal cache";
            clearCacheBtn.UseVisualStyleBackColor = true;
            clearCacheBtn.Click += clearCache_Click;
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(restoreMetaJsonBtn);
            groupBox2.Controls.Add(downloadFromHubBtn);
            groupBox2.Controls.Add(copyMissingDepsFromRepoBtn);
            groupBox2.Controls.Add(trustAllVarsBtn);
            groupBox2.Location = new Point(3, 336);
            groupBox2.Margin = new Padding(3, 4, 3, 4);
            groupBox2.Name = "groupBox2";
            groupBox2.Padding = new Padding(3, 4, 3, 4);
            groupBox2.Size = new Size(711, 129);
            groupBox2.TabIndex = 19;
            groupBox2.TabStop = false;
            groupBox2.Text = "Tools";
            // 
            // restoreMetaJsonBtn
            // 
            restoreMetaJsonBtn.Location = new Point(143, 28);
            restoreMetaJsonBtn.Margin = new Padding(3, 4, 3, 4);
            restoreMetaJsonBtn.Name = "restoreMetaJsonBtn";
            restoreMetaJsonBtn.Size = new Size(127, 62);
            restoreMetaJsonBtn.TabIndex = 21;
            restoreMetaJsonBtn.Text = "Restore meta.json";
            restoreMetaJsonBtn.UseVisualStyleBackColor = true;
            restoreMetaJsonBtn.Click += restoreMetaJsonBtn_Click;
            // 
            // downloadFromHubBtn
            // 
            downloadFromHubBtn.Location = new Point(403, 25);
            downloadFromHubBtn.Margin = new Padding(3, 4, 3, 4);
            downloadFromHubBtn.Name = "downloadFromHubBtn";
            downloadFromHubBtn.Size = new Size(135, 95);
            downloadFromHubBtn.TabIndex = 28;
            downloadFromHubBtn.Text = "Download missing and updated VARs from Virt-a HUB";
            downloadFromHubBtn.UseVisualStyleBackColor = true;
            downloadFromHubBtn.Click += downloadFromHubBtn_Click;
            // 
            // trustAllVarsBtn
            // 
            trustAllVarsBtn.Location = new Point(7, 28);
            trustAllVarsBtn.Margin = new Padding(3, 4, 3, 4);
            trustAllVarsBtn.Name = "trustAllVarsBtn";
            trustAllVarsBtn.Size = new Size(129, 62);
            trustAllVarsBtn.TabIndex = 18;
            trustAllVarsBtn.Text = "Trust scripts for all VARs in .prefs files";
            trustAllVarsBtn.UseVisualStyleBackColor = true;
            trustAllVarsBtn.Click += trustAllVarsBtn_Click;
            // 
            // groupBox3
            // 
            groupBox3.Controls.Add(profilesListBox);
            groupBox3.Controls.Add(manageProfilesBtn);
            groupBox3.Controls.Add(copyVarsFromRepoBtn);
            groupBox3.Location = new Point(3, 472);
            groupBox3.Margin = new Padding(3, 4, 3, 4);
            groupBox3.Name = "groupBox3";
            groupBox3.Padding = new Padding(3, 4, 3, 4);
            groupBox3.Size = new Size(711, 196);
            groupBox3.TabIndex = 20;
            groupBox3.TabStop = false;
            groupBox3.Text = "Profiles";
            // 
            // profilesListBox
            // 
            profilesListBox.FormattingEnabled = true;
            profilesListBox.Items.AddRange(new object[] { "test1", "test1", "test1", "test1", "test1", "test1", "test1", "test1", "test1", "test1", "test1" });
            profilesListBox.Location = new Point(7, 24);
            profilesListBox.Margin = new Padding(3, 4, 3, 4);
            profilesListBox.MultiColumn = true;
            profilesListBox.Name = "profilesListBox";
            profilesListBox.Size = new Size(535, 151);
            profilesListBox.TabIndex = 17;
            // 
            // manageProfilesBtn
            // 
            manageProfilesBtn.Location = new Point(550, 87);
            manageProfilesBtn.Margin = new Padding(3, 4, 3, 4);
            manageProfilesBtn.Name = "manageProfilesBtn";
            manageProfilesBtn.Size = new Size(154, 30);
            manageProfilesBtn.TabIndex = 16;
            manageProfilesBtn.Text = "Manage profiles";
            manageProfilesBtn.UseVisualStyleBackColor = true;
            manageProfilesBtn.Click += manageProfilesBtn_Click;
            // 
            // removeAllSoftLinkBeforeChk
            // 
            removeAllSoftLinkBeforeChk.AutoSize = true;
            removeAllSoftLinkBeforeChk.Location = new Point(271, 28);
            removeAllSoftLinkBeforeChk.Margin = new Padding(3, 4, 3, 4);
            removeAllSoftLinkBeforeChk.Name = "removeAllSoftLinkBeforeChk";
            removeAllSoftLinkBeforeChk.Size = new Size(295, 23);
            removeAllSoftLinkBeforeChk.TabIndex = 18;
            removeAllSoftLinkBeforeChk.Text = "Remove all soft-links before applying profile";
            removeAllSoftLinkBeforeChk.UseVisualStyleBackColor = true;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(10, 71);
            label4.Name = "label4";
            label4.Size = new Size(128, 19);
            label4.TabIndex = 21;
            label4.Text = "Number of threads:";
            // 
            // comboThreads
            // 
            comboThreads.DropDownStyle = ComboBoxStyle.DropDownList;
            comboThreads.FormattingEnabled = true;
            comboThreads.Location = new Point(143, 67);
            comboThreads.Margin = new Padding(3, 4, 3, 4);
            comboThreads.Name = "comboThreads";
            comboThreads.Size = new Size(77, 27);
            comboThreads.TabIndex = 22;
            // 
            // clearRepoDirBtn
            // 
            clearRepoDirBtn.Location = new Point(549, 46);
            clearRepoDirBtn.Margin = new Padding(3, 4, 3, 4);
            clearRepoDirBtn.Name = "clearRepoDirBtn";
            clearRepoDirBtn.Size = new Size(62, 29);
            clearRepoDirBtn.TabIndex = 23;
            clearRepoDirBtn.Text = "Clear";
            clearRepoDirBtn.UseVisualStyleBackColor = true;
            clearRepoDirBtn.Click += clearRepoDirBtn_Click;
            // 
            // stageTxt
            // 
            stageTxt.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            stageTxt.AutoSize = true;
            stageTxt.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            stageTxt.Location = new Point(3, 685);
            stageTxt.Name = "stageTxt";
            stageTxt.Size = new Size(28, 15);
            stageTxt.TabIndex = 26;
            stageTxt.Text = "Idle";
            stageTxt.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // groupBox4
            // 
            groupBox4.Controls.Add(modeComboBox);
            groupBox4.Controls.Add(label3);
            groupBox4.Controls.Add(clearCacheBtn);
            groupBox4.Controls.Add(removeAllSoftLinkBeforeChk);
            groupBox4.Controls.Add(dryRunCheckbox);
            groupBox4.Controls.Add(label4);
            groupBox4.Controls.Add(comboThreads);
            groupBox4.Location = new Point(3, 82);
            groupBox4.Margin = new Padding(3, 4, 3, 4);
            groupBox4.Name = "groupBox4";
            groupBox4.Padding = new Padding(3, 4, 3, 4);
            groupBox4.Size = new Size(711, 124);
            groupBox4.TabIndex = 27;
            groupBox4.TabStop = false;
            groupBox4.Text = "General Options";
            // 
            // modeComboBox
            // 
            modeComboBox.FormattingEnabled = true;
            modeComboBox.Items.AddRange(new object[] { "", "SoftLink", "Copy", "Move" });
            modeComboBox.Location = new Point(65, 26);
            modeComboBox.Name = "modeComboBox";
            modeComboBox.Size = new Size(155, 27);
            modeComboBox.TabIndex = 24;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(11, 29);
            label3.Name = "label3";
            label3.Size = new Size(48, 19);
            label3.TabIndex = 23;
            label3.Text = "Mode:";
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(removeDsfMorphsChk);
            groupBox1.Controls.Add(removeVirusMorphsChk);
            groupBox1.Controls.Add(runVarFixersBtn);
            groupBox1.Controls.Add(removeDependenciesFromMetaChk);
            groupBox1.Controls.Add(disableMorphPreloadChk);
            groupBox1.Location = new Point(3, 214);
            groupBox1.Margin = new Padding(3, 4, 3, 4);
            groupBox1.Name = "groupBox1";
            groupBox1.Padding = new Padding(3, 4, 3, 4);
            groupBox1.Size = new Size(711, 125);
            groupBox1.TabIndex = 31;
            groupBox1.TabStop = false;
            groupBox1.Text = "Var Fixers";
            // 
            // removeDsfMorphsChk
            // 
            removeDsfMorphsChk.AutoSize = true;
            removeDsfMorphsChk.Location = new Point(341, 52);
            removeDsfMorphsChk.Margin = new Padding(3, 4, 3, 4);
            removeDsfMorphsChk.Name = "removeDsfMorphsChk";
            removeDsfMorphsChk.Size = new Size(150, 23);
            removeDsfMorphsChk.TabIndex = 32;
            removeDsfMorphsChk.Text = "Remove dsf morphs";
            removeDsfMorphsChk.UseVisualStyleBackColor = true;
            // 
            // removeVirusMorphsChk
            // 
            removeVirusMorphsChk.AutoSize = true;
            removeVirusMorphsChk.Location = new Point(341, 28);
            removeVirusMorphsChk.Margin = new Padding(3, 4, 3, 4);
            removeVirusMorphsChk.Name = "removeVirusMorphsChk";
            removeVirusMorphsChk.Size = new Size(244, 23);
            removeVirusMorphsChk.TabIndex = 31;
            removeVirusMorphsChk.Text = "Remove virus-morphs (RG morphs)";
            removeVirusMorphsChk.UseVisualStyleBackColor = true;
            // 
            // runVarFixersBtn
            // 
            runVarFixersBtn.Location = new Point(281, 84);
            runVarFixersBtn.Margin = new Padding(3, 4, 3, 4);
            runVarFixersBtn.Name = "runVarFixersBtn";
            runVarFixersBtn.Size = new Size(130, 30);
            runVarFixersBtn.TabIndex = 30;
            runVarFixersBtn.Text = "Run var fixers";
            runVarFixersBtn.UseVisualStyleBackColor = true;
            runVarFixersBtn.Click += runVarFixersBtn_Click;
            // 
            // removeDependenciesFromMetaChk
            // 
            removeDependenciesFromMetaChk.AutoSize = true;
            removeDependenciesFromMetaChk.Location = new Point(7, 60);
            removeDependenciesFromMetaChk.Margin = new Padding(3, 4, 3, 4);
            removeDependenciesFromMetaChk.Name = "removeDependenciesFromMetaChk";
            removeDependenciesFromMetaChk.Size = new Size(304, 23);
            removeDependenciesFromMetaChk.TabIndex = 1;
            removeDependenciesFromMetaChk.Text = "Remove dependencies from all meta.json files";
            removeDependenciesFromMetaChk.UseVisualStyleBackColor = true;
            // 
            // disableMorphPreloadChk
            // 
            disableMorphPreloadChk.AutoSize = true;
            disableMorphPreloadChk.Location = new Point(7, 28);
            disableMorphPreloadChk.Margin = new Padding(3, 4, 3, 4);
            disableMorphPreloadChk.Name = "disableMorphPreloadChk";
            disableMorphPreloadChk.Size = new Size(310, 23);
            disableMorphPreloadChk.TabIndex = 0;
            disableMorphPreloadChk.Text = "Disable morphPreload for all non-morphpacks";
            disableMorphPreloadChk.UseVisualStyleBackColor = true;
            // 
            // MainWindow
            // 
            AutoScaleDimensions = new SizeF(8F, 19F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(733, 833);
            Controls.Add(groupBox1);
            Controls.Add(scanJsonFilesBtn);
            Controls.Add(groupBox4);
            Controls.Add(stageTxt);
            Controls.Add(clearRepoDirBtn);
            Controls.Add(groupBox3);
            Controls.Add(groupBox2);
            Controls.Add(additionalVarsDir);
            Controls.Add(operationStatusLabel);
            Controls.Add(label2);
            Controls.Add(progressBar);
            Controls.Add(additionalVarsBtn);
            Controls.Add(selectVamDirBtn);
            Controls.Add(vamDirTxt);
            Controls.Add(label1);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Margin = new Padding(3, 4, 3, 4);
            Name = "MainWindow";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "VamToolbox ";
            FormClosing += MainWindow_FormClosing;
            groupBox2.ResumeLayout(false);
            groupBox3.ResumeLayout(false);
            groupBox4.ResumeLayout(false);
            groupBox4.PerformLayout();
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox vamDirTxt;
        private System.Windows.Forms.Button selectVamDirBtn;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox additionalVarsDir;
        private System.Windows.Forms.Button additionalVarsBtn;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Label operationStatusLabel;
        private System.Windows.Forms.Button copyVarsFromRepoBtn;
        private System.Windows.Forms.CheckBox dryRunCheckbox;
        private System.Windows.Forms.Button copyMissingDepsFromRepoBtn;
        private System.Windows.Forms.Button scanJsonFilesBtn;
        private System.Windows.Forms.Button clearCacheBtn;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Button manageProfilesBtn;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ComboBox comboThreads;
        private System.Windows.Forms.CheckedListBox profilesListBox;
        private System.Windows.Forms.CheckBox removeAllSoftLinkBeforeChk;
        private System.Windows.Forms.Button clearRepoDirBtn;
        private System.Windows.Forms.Button trustAllVarsBtn;
        private System.Windows.Forms.Label stageTxt;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.Button downloadFromHubBtn;
        private Button restoreMetaJsonBtn;
        private GroupBox groupBox1;
        private Button runVarFixersBtn;
        private CheckBox removeDependenciesFromMetaChk;
        private CheckBox disableMorphPreloadChk;
        private CheckBox removeVirusMorphsChk;
        private CheckBox removeDsfMorphsChk;
        private ComboBox modeComboBox;
        private Label label3;
    }
}

