// Native WinForms configuration panel: both roots, both launcher regions and labels.
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace WowRegionSwitcher
{
    internal sealed class SettingsForm : Form
    {
        private readonly AppSettings working;
        private readonly TextBox[] names = new TextBox[2];
        private readonly TextBox[] paths = new TextBox[2];
        private readonly ComboBox[] regions = new ComboBox[2];
        private static readonly string[] RegionOptions = {
            "CN - 中国大陆", "TW - 亚洲（台湾入口）", "KR - 亚洲（韩国入口）",
            "US - 美洲", "EU - 欧洲"
        };

        public AppSettings SavedSettings { get; private set; }

        public SettingsForm(AppSettings current)
        {
            working = current.Clone();
            Text = "配置两个启动入口";
            Width = 840; Height = 610;
            MinimumSize = new Size(760, 560);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Microsoft YaHei UI", 9.5f);
            BackColor = Color.FromArgb(26, 31, 42);
            ForeColor = Color.White;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            BuildUi();
        }

        private Label MakeInfo(string value, int height)
        {
            return new Label {
                Text = value, ForeColor = Color.FromArgb(200, 207, 219),
                Height = height, Dock = DockStyle.Top, AutoSize = false
            };
        }

        private Control MakeProfile(LaunchProfile profile, int index)
        {
            GroupBox group = new GroupBox {
                Text = index == 0 ? "入口一" : "入口二", Dock = DockStyle.Fill,
                ForeColor = Color.White, Padding = new Padding(13, 19, 13, 12)
            };
            TableLayoutPanel content = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
            for (int i = 0; i < 3; i++) content.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            names[index] = new TextBox { Dock = DockStyle.Fill, Text = profile.Name, Margin = new Padding(0, 8, 6, 9) };
            paths[index] = new TextBox { Dock = DockStyle.Fill, Text = profile.GameFolder, Margin = new Padding(0, 8, 6, 9) };
            regions[index] = new ComboBox {
                Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 8, 6, 9)
            };
            regions[index].Items.AddRange(RegionOptions);
            regions[index].SelectedIndex = Array.FindIndex(RegionOptions,
                opt => opt.StartsWith(profile.LoginRegion + " - ", StringComparison.Ordinal));
            if (regions[index].SelectedIndex < 0) regions[index].SelectedIndex = 0;
            Label nameLabel = MakeInfo("按钮名称", 25);
            Label pathLabel = MakeInfo("游戏根目录", 25);
            Label regionLabel = MakeInfo("战网登录区域", 25);
            nameLabel.Margin = pathLabel.Margin = regionLabel.Margin = new Padding(0, 12, 0, 0);
            content.Controls.Add(nameLabel, 0, 0);
            content.Controls.Add(pathLabel, 0, 1);
            content.Controls.Add(regionLabel, 0, 2);
            content.Controls.Add(names[index], 1, 0);
            content.Controls.Add(paths[index], 1, 1);
            content.Controls.Add(regions[index], 1, 2);
            Button browse = new Button { Text = "浏览...", Dock = DockStyle.Fill, Margin = new Padding(0, 6, 0, 10) };
            browse.Click += delegate {
                using (FolderBrowserDialog picker = new FolderBrowserDialog()) {
                    picker.Description = "请选择包含 Data、_retail_ 或 _forever_ 的魔兽世界游戏根目录";
                    picker.SelectedPath = Directory.Exists(paths[index].Text) ? paths[index].Text : "";
                    if (picker.ShowDialog(this) == DialogResult.OK) paths[index].Text = picker.SelectedPath;
                }
            };
            content.Controls.Add(browse, 2, 1);
            group.Controls.Add(content);
            return group;
        }

        private void BuildUi()
        {
            TableLayoutPanel layout = new TableLayoutPanel {
                Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 5
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 51));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
            Label intro = MakeInfo("分别配置两个入口的名称、安装目录和战网登录区域。\r\n注意：登录区域不等于角色所在的游戏服务器；不会修改账号或 product.db 的 play_region 字段。", 60);
            intro.Dock = DockStyle.Fill;
            layout.Controls.Add(intro, 0, 0);
            layout.Controls.Add(MakeProfile(working.First, 0), 0, 1);
            layout.Controls.Add(MakeProfile(working.Second, 1), 0, 2);
            Label note = MakeInfo("修改配置只会保存设置；点击启动入口时，程序才会检查并尝试更新游戏安装记录。", 39);
            note.Dock = DockStyle.Fill; note.Padding = new Padding(4, 12, 0, 0);
            layout.Controls.Add(note, 0, 3);
            FlowLayoutPanel buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
            Button save = new Button { Text = "保存配置", Width = 130, Height = 36 };
            Button cancel = new Button { Text = "取消", Width = 95, Height = 36 };
            save.Click += delegate { SaveAndClose(); };
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            buttons.Controls.Add(save); buttons.Controls.Add(cancel);
            layout.Controls.Add(buttons, 0, 4);
            Controls.Add(layout);
            AcceptButton = save;
            CancelButton = cancel;
        }

        private void SaveAndClose()
        {
            try
            {
                LaunchProfile[] profiles = { working.First, working.Second };
                for (int i = 0; i < 2; i++)
                {
                    profiles[i].Name = names[i].Text;
                    profiles[i].GameFolder = paths[i].Text;
                    profiles[i].LoginRegion = regions[i].Text.Substring(0, 2);
                }
                working.Save();
                SavedSettings = working.Clone();
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception error)
            {
                MessageBox.Show(this, "配置未保存：\r\n" + error.Message, "配置错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
