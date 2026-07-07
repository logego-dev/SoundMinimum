using SoundMinimum.Models;
using SoundMinimum.Services;
using SoundMinimum.Themes;

namespace SoundMinimum.Forms;

using SoundMinimum;

public class ProjectManagerForm : Form
{
    private readonly ProjectManager _project;
    private readonly ListBox _listBox;
    private readonly TextBox _nameBox;

    public ProjectManagerForm(ProjectManager project)
    {
        _project = project;
        Text = Loc.Get("Project_Title");
        Size = new Size(500, 450);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = DarkTheme.BgMain;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;

        var label = new Label
        {
            Text = Loc.Get("Project_List"), Location = new Point(12, 12), Size = new Size(460, 20),
            ForeColor = DarkTheme.FgText, BackColor = DarkTheme.BgMain, Font = DarkTheme.FontBold
        };
        Controls.Add(label);

        _listBox = new ListBox
        {
            Location = new Point(12, 36), Size = new Size(360, 220),
            BackColor = DarkTheme.BgInput, ForeColor = DarkTheme.FgText,
            Font = DarkTheme.FontNormal, IntegralHeight = false
        };
        RefreshList();
        Controls.Add(_listBox);

        _nameBox = new TextBox
        {
            Location = new Point(12, 270), Size = new Size(360, 24),
            BackColor = DarkTheme.BgInput, ForeColor = DarkTheme.FgText,
            Font = DarkTheme.FontNormal, BorderStyle = BorderStyle.FixedSingle,
            Text = Loc.Get("Project_New")
        };
        Controls.Add(_nameBox);

        int y = 36;
        AddBtn(Loc.Get("Project_Open"), 385, y, () => OpenSelected()); y += 36;
        AddBtn(Loc.Get("Project_NewBtn"), 385, y, () => CreateNew()); y += 36;
        AddBtn(Loc.Get("Project_SaveAs"), 385, y, () => SaveCurrentAs()); y += 36;
        AddBtn(Loc.Get("Project_Delete"), 385, y, () => DeleteSelected()); y += 36;
        AddBtn(Loc.Get("Project_Backups"), 385, y, () => ShowBackups());

        var closeBtn = new Button { Text = Loc.Get("Project_Close"), Location = new Point(200, 370), Size = new Size(100, 30) };
        closeBtn.Click += (_, _) => DialogResult = DialogResult.OK;
        StyleBtn(closeBtn);
        Controls.Add(closeBtn);

        _listBox.DoubleClick += (_, _) => OpenSelected();
    }

    private void AddBtn(string text, int x, int y, Action action)
    {
        var btn = new Button { Text = text, Location = new Point(x, y), Size = new Size(95, 30) };
        btn.Click += (_, _) => action();
        StyleBtn(btn);
        Controls.Add(btn);
    }

    private void StyleBtn(Button btn)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderColor = DarkTheme.BorderColor;
        btn.BackColor = DarkTheme.BgInput;
        btn.ForeColor = DarkTheme.FgText;
        btn.Font = DarkTheme.FontNormal;
    }

    private void RefreshList()
    {
        _listBox.Items.Clear();
        foreach (var f in _project.GetProjectFiles())
        {
            var name = Path.GetFileNameWithoutExtension(f);
            _listBox.Items.Add(name);
        }
        if (_project.Current != null)
        {
            var idx = _listBox.Items.IndexOf(_project.Current.Name);
            if (idx >= 0) _listBox.SelectedIndex = idx;
        }
    }

    private void OpenSelected()
    {
        if (_listBox.SelectedIndex < 0) return;
        var files = _project.GetProjectFiles();
        if (_listBox.SelectedIndex < files.Length)
            _project.Open(files[_listBox.SelectedIndex]);
    }

    private void CreateNew()
    {
        var name = _nameBox.Text.Trim();
        if (string.IsNullOrEmpty(name)) name = Loc.Get("Project_NewDefault");
        _project.New();
        _project.Current.Name = name;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void SaveCurrentAs()
    {
        using var sfd = new SaveFileDialog
        {
            Filter = "Project files|*.smproj",
            InitialDirectory = _project.ProjectsDir,
            FileName = $"{_project.Current.Name}.smproj"
        };
        if (sfd.ShowDialog(this) == DialogResult.OK)
        {
            _project.SaveAs(sfd.FileName);
            RefreshList();
        }
    }

    private void DeleteSelected()
    {
        if (_listBox.SelectedIndex < 0) return;
        var files = _project.GetProjectFiles();
        if (_listBox.SelectedIndex >= files.Length) return;
        var path = files[_listBox.SelectedIndex];
        var result = MessageBox.Show(Loc.Get("Project_ConfirmDelete", Path.GetFileNameWithoutExtension(path)),
            Loc.Get("Project_ConfirmTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result == DialogResult.Yes)
        {
            File.Delete(path);
            RefreshList();
        }
    }

    private void ShowBackups()
    {
        var projectName = _project.Current?.Name ?? "project";
        var backups = _project.Backup.GetBackups(projectName);
        if (backups.Count == 0)
        {
            MessageBox.Show(Loc.Get("Project_NoBackups"), Loc.Get("Project_BackupTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var menu = new ContextMenuStrip();
        foreach (var b in backups.Take(20))
        {
            var fileName = Path.GetFileName(b);
            var item = menu.Items.Add(Loc.Get("Project_Restore", fileName));
            item.Tag = b;
            item.Click += (_, _) =>
            {
                var path = (string)((ToolStripMenuItem)item!).Tag!;
                _project.Open(path);
                MessageBox.Show(Loc.Get("Project_Restored", fileName), Loc.Get("Project_RestoreTitle"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
        }
        menu.Show(Cursor.Position);
    }
}
