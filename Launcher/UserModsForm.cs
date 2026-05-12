using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Launcher
{
    public class UserModsForm : Form
    {
        private readonly string _modsFolder;
        private DataGridView _modsGrid;
        private Panel _detailPanel;
        private TextBox _txtName;
        private TextBox _txtShortDesc;
        private TextBox _txtFullDesc;
        private CheckBox _chkChebovka;
        private CheckBox _chkInstall;
        private Button _btnSave;
        private string? _selectedModPath;

        private List<(string path, UserModConfig config)> _mods = new();

        public UserModsForm(string modsFolder)
        {
            _modsFolder = modsFolder;
            Directory.CreateDirectory(_modsFolder);
            InitUI();
            LoadMods();
        }

        private void InitUI()
        {
            this.Text = "UserMods";
            this.Size = new Size(850, 550);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(30, 30, 40);
            this.ForeColor = Color.White;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Кнопка "Открыть папку с модами"
            var btnOpenFolder = new Button
            {
                Text = "Открыть папку с модами",
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(40, 40, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                Size = new Size(180, 30),
                Location = new Point(10, 10)
            };
            btnOpenFolder.FlatAppearance.BorderSize = 0;
            btnOpenFolder.Click += (s, e) =>
            {
                Directory.CreateDirectory(_modsFolder);
                Process.Start(new ProcessStartInfo
                {
                    FileName = _modsFolder,
                    UseShellExecute = true
                });
            };
            this.Controls.Add(btnOpenFolder);

            // Кнопка "Обновить"
            var btnRefresh = new Button
            {
                Text = "Обновить",
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(40, 40, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                Size = new Size(100, 30),
                Location = new Point(200, 10)
            };
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Click += (s, e) => LoadMods();
            this.Controls.Add(btnRefresh);

            // Таблица модов
            _modsGrid = new DataGridView
            {
                Location = new Point(10, 50),
                Size = new Size(510, 450),
                BackgroundColor = Color.FromArgb(25, 25, 35),
                ForeColor = Color.White,
                GridColor = Color.FromArgb(60, 60, 80),
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Segoe UI", 9)
            };

            _modsGrid.DefaultCellStyle.BackColor = Color.FromArgb(30, 30, 40);
            _modsGrid.DefaultCellStyle.ForeColor = Color.White;
            _modsGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(60, 60, 90);
            _modsGrid.DefaultCellStyle.SelectionForeColor = Color.White;
            _modsGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(40, 40, 55);
            _modsGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _modsGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            _modsGrid.EnableHeadersVisualStyles = false;

            var colName = new DataGridViewTextBoxColumn
            {
                HeaderText = "Название",
                Name = "colName",
                Width = 150,
                ReadOnly = true
            };

            var colDesc = new DataGridViewTextBoxColumn
            {
                HeaderText = "Краткое описание",
                Name = "colDesc",
                Width = 200,
                ReadOnly = true
            };

            var colChebovka = new DataGridViewCheckBoxColumn
            {
                HeaderText = "Chebovka",
                Name = "colChebovka",
                Width = 75
            };

            var colInstall = new DataGridViewCheckBoxColumn
            {
                HeaderText = "Установка",
                Name = "colInstall",
                Width = 75
            };

            _modsGrid.Columns.AddRange(colName, colDesc, colChebovka, colInstall);

            _modsGrid.CellClick += ModsGrid_CellClick;
            _modsGrid.CellValueChanged += ModsGrid_CellValueChanged;
            _modsGrid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_modsGrid.IsCurrentCellDirty)
                    _modsGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            this.Controls.Add(_modsGrid);

            // Панель деталей мода (справа)
            _detailPanel = new Panel
            {
                Location = new Point(530, 50),
                Size = new Size(300, 450),
                BackColor = Color.FromArgb(35, 35, 50)
            };
            this.Controls.Add(_detailPanel);

            int y = 10;
            AddDetailLabel("Название:", ref y);
            _txtName = AddDetailTextBox(ref y, false);

            AddDetailLabel("Краткое описание:", ref y);
            _txtShortDesc = AddDetailTextBox(ref y, false);

            AddDetailLabel("Полное описание:", ref y);
            _txtFullDesc = new TextBox
            {
                Location = new Point(10, y),
                Size = new Size(275, 120),
                BackColor = Color.FromArgb(45, 45, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                BorderStyle = BorderStyle.FixedSingle,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical
            };
            _detailPanel.Controls.Add(_txtFullDesc);
            y += 130;

            _chkChebovka = new CheckBox
            {
                Text = "Для Chebovka",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Location = new Point(10, y),
                AutoSize = true
            };
            _detailPanel.Controls.Add(_chkChebovka);
            y += 30;

            _chkInstall = new CheckBox
            {
                Text = "Установить",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Location = new Point(10, y),
                AutoSize = true
            };
            _detailPanel.Controls.Add(_chkInstall);
            y += 40;

            _btnSave = new Button
            {
                Text = "Сохранить",
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 80, 0),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Size = new Size(275, 35),
                Location = new Point(10, y)
            };
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.Click += BtnSave_Click;
            _detailPanel.Controls.Add(_btnSave);

            SetDetailPanelVisible(false);
        }

        private void AddDetailLabel(string text, ref int y)
        {
            var lbl = new Label
            {
                Text = text,
                ForeColor = Color.FromArgb(180, 180, 200),
                Font = new Font("Segoe UI", 8),
                Location = new Point(10, y),
                AutoSize = true
            };
            _detailPanel.Controls.Add(lbl);
            y += 18;
        }

        private TextBox AddDetailTextBox(ref int y, bool multiline)
        {
            var txt = new TextBox
            {
                Location = new Point(10, y),
                Size = new Size(275, multiline ? 80 : 25),
                BackColor = Color.FromArgb(45, 45, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                BorderStyle = BorderStyle.FixedSingle,
                Multiline = multiline
            };
            _detailPanel.Controls.Add(txt);
            y += multiline ? 90 : 35;
            return txt;
        }

        private void SetDetailPanelVisible(bool visible)
        {
            foreach (Control c in _detailPanel.Controls)
                c.Visible = visible;
        }

        private void LoadMods()
        {
            _mods.Clear();
            _modsGrid.Rows.Clear();

            if (!Directory.Exists(_modsFolder))
                return;

            foreach (string file in Directory.GetFiles(_modsFolder, "*.h5u"))
            {
                var config = UserModConfig.ReadFromArchive(file);
                _mods.Add((file, config));

                int rowIdx = _modsGrid.Rows.Add(
                    config.Name,
                    config.ShortDescription,
                    config.ForChebovka,
                    config.Install
                );
                _modsGrid.Rows[rowIdx].Tag = file;
            }

            SetDetailPanelVisible(false);
            _selectedModPath = null;
        }

        private void ModsGrid_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _mods.Count)
                return;

            // Если нажали на чекбокс — не открываем детали
            if (e.ColumnIndex == _modsGrid.Columns["colChebovka"]!.Index ||
                e.ColumnIndex == _modsGrid.Columns["colInstall"]!.Index)
                return;

            var (path, config) = _mods[e.RowIndex];
            _selectedModPath = path;

            _txtName.Text = config.Name;
            _txtShortDesc.Text = config.ShortDescription;
            _txtFullDesc.Text = config.FullDescription;
            _chkChebovka.Checked = config.ForChebovka;
            _chkInstall.Checked = config.Install;

            SetDetailPanelVisible(true);
        }

        private void ModsGrid_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _mods.Count)
                return;

            var (path, config) = _mods[e.RowIndex];

            if (e.ColumnIndex == _modsGrid.Columns["colChebovka"]!.Index)
            {
                bool val = (bool)(_modsGrid.Rows[e.RowIndex].Cells["colChebovka"].Value ?? false);
                config.ForChebovka = val;
                config.SaveToArchive(path);
            }
            else if (e.ColumnIndex == _modsGrid.Columns["colInstall"]!.Index)
            {
                bool val = (bool)(_modsGrid.Rows[e.RowIndex].Cells["colInstall"].Value ?? false);
                config.Install = val;
                config.SaveToArchive(path);
            }
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            if (_selectedModPath == null)
                return;

            int idx = _mods.FindIndex(m => m.path == _selectedModPath);
            if (idx < 0)
                return;

            var config = _mods[idx].config;
            config.Name = _txtName.Text;
            config.ShortDescription = _txtShortDesc.Text;
            config.FullDescription = _txtFullDesc.Text;
            config.ForChebovka = _chkChebovka.Checked;
            config.Install = _chkInstall.Checked;

            config.SaveToArchive(_selectedModPath);

            // Обновляем строку в таблице
            var row = _modsGrid.Rows[idx];
            row.Cells["colName"].Value = config.Name;
            row.Cells["colDesc"].Value = config.ShortDescription;
            row.Cells["colChebovka"].Value = config.ForChebovka;
            row.Cells["colInstall"].Value = config.Install;

            MessageBox.Show("Сохранено!", "UserMods", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
