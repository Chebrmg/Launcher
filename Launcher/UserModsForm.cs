using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
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
        private PictureBox _previewBox;
        private Button _btnLoadPreview;
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
            this.Size = new Size(1100, 720);
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
                Size = new Size(680, 620),
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

            _modsGrid.RowTemplate.Height = 60;

            var colPreview = new DataGridViewImageColumn
            {
                HeaderText = "",
                Name = "colPreview",
                Width = 56,
                ImageLayout = DataGridViewImageCellLayout.Zoom,
                ReadOnly = true
            };

            var colName = new DataGridViewTextBoxColumn
            {
                HeaderText = "Название",
                Name = "colName",
                Width = 190,
                ReadOnly = true
            };

            var colDesc = new DataGridViewTextBoxColumn
            {
                HeaderText = "Краткое описание",
                Name = "colDesc",
                Width = 260,
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

            _modsGrid.Columns.AddRange(colPreview, colName, colDesc, colChebovka, colInstall);

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
                Location = new Point(700, 50),
                Size = new Size(370, 620),
                BackColor = Color.FromArgb(35, 35, 50),
                AutoScroll = true
            };
            this.Controls.Add(_detailPanel);

            int y = 10;

            // Превью картинка
            _previewBox = new PictureBox
            {
                Location = new Point(10, y),
                Size = new Size(345, 190),
                BackColor = Color.FromArgb(25, 25, 35),
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                AllowDrop = true
            };
            _previewBox.DragEnter += PreviewBox_DragEnter;
            _previewBox.DragDrop += PreviewBox_DragDrop;
            _detailPanel.Controls.Add(_previewBox);
            y += 190;

            // Кнопка загрузки превью
            _btnLoadPreview = new Button
            {
                Text = "Загрузить превью",
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(40, 40, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8, FontStyle.Regular),
                Size = new Size(345, 28),
                Location = new Point(10, y)
            };
            _btnLoadPreview.FlatAppearance.BorderSize = 0;
            _btnLoadPreview.Click += BtnLoadPreview_Click;
            _detailPanel.Controls.Add(_btnLoadPreview);
            y += 35;

            AddDetailLabel("Название:", ref y);
            _txtName = AddDetailTextBox(ref y, false);

            AddDetailLabel("Краткое описание:", ref y);
            _txtShortDesc = AddDetailTextBox(ref y, false);

            AddDetailLabel("Полное описание:", ref y);
            _txtFullDesc = new TextBox
            {
                Location = new Point(10, y),
                Size = new Size(345, 110),
                BackColor = Color.FromArgb(45, 45, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                BorderStyle = BorderStyle.FixedSingle,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical
            };
            _detailPanel.Controls.Add(_txtFullDesc);
            y += 110;

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
                Size = new Size(345, 35),
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
                Size = new Size(345, multiline ? 80 : 25),
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

        private Image GetThumbnail(string h5uPath, int width, int height)
        {
            var preview = UserModConfig.ReadPreviewFromArchive(h5uPath);
            if (preview != null)
            {
                var thumb = new Bitmap(width, height);
                using (var g = Graphics.FromImage(thumb))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(preview, 0, 0, width, height);
                }
                preview.Dispose();
                return thumb;
            }

            var empty = new Bitmap(width, height);
            using (var g = Graphics.FromImage(empty))
            {
                g.Clear(Color.FromArgb(40, 40, 55));
                using var font = new Font("Segoe UI", 7);
                using var brush = new SolidBrush(Color.FromArgb(100, 100, 120));
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("Нет\nфото", font, brush, new RectangleF(0, 0, width, height), sf);
            }
            return empty;
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

                var thumb = GetThumbnail(file, 52, 52);

                int rowIdx = _modsGrid.Rows.Add(
                    thumb,
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

            // Загружаем превью
            _previewBox.Image?.Dispose();
            _previewBox.Image = UserModConfig.ReadPreviewFromArchive(path);

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

        private void BtnLoadPreview_Click(object? sender, EventArgs e)
        {
            if (_selectedModPath == null)
                return;

            using var dlg = new OpenFileDialog
            {
                Title = "Выберите картинку для превью",
                Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp|Все файлы|*.*"
            };

            if (dlg.ShowDialog() != DialogResult.OK)
                return;

            UserModConfig.SavePreviewToArchive(_selectedModPath, dlg.FileName);

            // Обновляем превью в панели деталей
            _previewBox.Image?.Dispose();
            _previewBox.Image = UserModConfig.ReadPreviewFromArchive(_selectedModPath);

            // Обновляем миниатюру в таблице
            int idx = _mods.FindIndex(m => m.path == _selectedModPath);
            if (idx >= 0)
            {
                var oldThumb = _modsGrid.Rows[idx].Cells["colPreview"].Value as Image;
                oldThumb?.Dispose();
                _modsGrid.Rows[idx].Cells["colPreview"].Value = GetThumbnail(_selectedModPath, 52, 52);
            }

            MessageBox.Show("Превью загружено!", "UserMods", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void PreviewBox_DragEnter(object? sender, DragEventArgs e)
        {
            if (_selectedModPath == null || e.Data == null)
                return;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
                if (files.Length > 0)
                {
                    string ext = Path.GetExtension(files[0]).ToLower();
                    if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp")
                    {
                        e.Effect = DragDropEffects.Copy;
                        return;
                    }
                }
            }
            e.Effect = DragDropEffects.None;
        }

        private void PreviewBox_DragDrop(object? sender, DragEventArgs e)
        {
            if (_selectedModPath == null || e.Data == null)
                return;

            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            if (files.Length == 0)
                return;

            string imagePath = files[0];
            UserModConfig.SavePreviewToArchive(_selectedModPath, imagePath);

            _previewBox.Image?.Dispose();
            _previewBox.Image = UserModConfig.ReadPreviewFromArchive(_selectedModPath);

            int idx = _mods.FindIndex(m => m.path == _selectedModPath);
            if (idx >= 0)
            {
                var oldThumb = _modsGrid.Rows[idx].Cells["colPreview"].Value as Image;
                oldThumb?.Dispose();
                _modsGrid.Rows[idx].Cells["colPreview"].Value = GetThumbnail(_selectedModPath, 52, 52);
            }
        }
    }
}
