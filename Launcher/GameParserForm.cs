using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Launcher
{
    public class GameParserForm : Form
    {
        private readonly string _gameRoot;

        // Экран выбора фракций
        private Panel _selectionPanel;
        private ComboBox _cmbFaction1;
        private ComboBox _cmbFaction2;
        private Button _btnStart;
        private Label _selectionStatus;

        // Основной контент (после выбора фракций)
        private TabControl _tabs;

        // Вкладки Игрок 1 и Игрок 2
        private CreatureTabContent _player1Tab;
        private CreatureTabContent _player2Tab;

        private string _faction1 = "";
        private string _faction2 = "";

        public GameParserForm(string gameRoot)
        {
            _gameRoot = gameRoot;
            InitUI();
        }

        private void InitUI()
        {
            Text = "Парсер игровых данных";
            Size = new Size(1150, 750);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(30, 30, 40);
            ForeColor = Color.White;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            // === Экран выбора фракций ===
            _selectionPanel = new Panel
            {
                Parent = this,
                Location = new Point(0, 0),
                Size = new Size(Width, Height),
                BackColor = Color.FromArgb(30, 30, 40),
            };

            var titleLabel = new Label
            {
                Parent = _selectionPanel,
                Text = "Выберите фракции",
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 220, 100),
                AutoSize = true,
                Location = new Point(400, 120),
            };

            // Игрок 1
            var lbl1 = new Label
            {
                Parent = _selectionPanel,
                Text = "Игрок 1:",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 180, 255),
                AutoSize = true,
                Location = new Point(350, 220),
            };

            _cmbFaction1 = new ComboBox
            {
                Parent = _selectionPanel,
                Location = new Point(460, 218),
                Size = new Size(250, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 11),
                BackColor = Color.FromArgb(50, 50, 65),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
            };
            foreach (string f in GameDataParser.SelectableFactions)
                _cmbFaction1.Items.Add(f);
            _cmbFaction1.SelectedIndex = 0;

            // Игрок 2
            var lbl2 = new Label
            {
                Parent = _selectionPanel,
                Text = "Игрок 2:",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 130, 130),
                AutoSize = true,
                Location = new Point(350, 280),
            };

            _cmbFaction2 = new ComboBox
            {
                Parent = _selectionPanel,
                Location = new Point(460, 278),
                Size = new Size(250, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 11),
                BackColor = Color.FromArgb(50, 50, 65),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
            };
            foreach (string f in GameDataParser.SelectableFactions)
                _cmbFaction2.Items.Add(f);
            _cmbFaction2.SelectedIndex = 1;

            // Кнопка Начать
            _btnStart = new Button
            {
                Parent = _selectionPanel,
                Text = "Начать",
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                Size = new Size(200, 45),
                Location = new Point(450, 370),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 120, 50),
                ForeColor = Color.White,
            };
            _btnStart.FlatAppearance.BorderSize = 0;
            _btnStart.Click += BtnStart_Click;

            // Статус загрузки
            _selectionStatus = new Label
            {
                Parent = _selectionPanel,
                Text = "",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.Gray,
                Size = new Size(600, 100),
                Location = new Point(260, 440),
                TextAlign = ContentAlignment.TopCenter,
            };

            // === Основной контент (скрыт до выбора) ===
            _tabs = new TabControl
            {
                Parent = this,
                Location = new Point(10, 10),
                Size = new Size(Width - 36, Height - 60),
                Appearance = TabAppearance.FlatButtons,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Visible = false,
            };
        }

        private void BtnStart_Click(object? sender, EventArgs e)
        {
            _faction1 = _cmbFaction1.SelectedItem?.ToString() ?? "";
            _faction2 = _cmbFaction2.SelectedItem?.ToString() ?? "";

            if (_faction1 == _faction2)
            {
                MessageBox.Show("Выберите разные фракции!", "Парсер",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _btnStart.Enabled = false;
            _selectionStatus.Text = "Загрузка данных...";
            _selectionStatus.ForeColor = Color.Gray;

            System.Threading.Tasks.Task.Run(() =>
            {
                var parser = new GameDataParser(_gameRoot);
                parser.BuildVfs();

                var factions1 = new List<string> { _faction1 };
                var factions2 = new List<string> { _faction2 };

                var creatures1 = parser.ParseCreatures(factions1);
                var creatures2 = parser.ParseCreatures(factions2);

                return (creatures1, creatures2, parser.DiagInfo);
            }).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    _selectionStatus.Text = "Ошибка: " + task.Exception?.InnerException?.Message;
                    _selectionStatus.ForeColor = Color.Red;
                    _btnStart.Enabled = true;
                    return;
                }

                var (creatures1, creatures2, diagInfo) = task.Result;

                if (creatures1.Count == 0 && creatures2.Count == 0)
                {
                    _selectionStatus.Text = "Юниты не найдены.\n\nДиагностика:\n" + diagInfo;
                    _selectionStatus.ForeColor = Color.OrangeRed;
                    _btnStart.Enabled = true;
                    return;
                }

                ShowMainContent(creatures1, creatures2);
            }, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void ShowMainContent(List<CreatureInfo> creatures1, List<CreatureInfo> creatures2)
        {
            _selectionPanel.Visible = false;
            _tabs.Visible = true;

            _tabs.TabPages.Clear();

            // Вкладка Игрок 1
            var tabP1 = new TabPage($"Игрок 1: {_faction1}")
            {
                BackColor = Color.FromArgb(30, 30, 40),
                ForeColor = Color.White,
            };
            _player1Tab = new CreatureTabContent(tabP1, creatures1);
            _tabs.TabPages.Add(tabP1);

            // Вкладка Игрок 2
            var tabP2 = new TabPage($"Игрок 2: {_faction2}")
            {
                BackColor = Color.FromArgb(30, 30, 40),
                ForeColor = Color.White,
            };
            _player2Tab = new CreatureTabContent(tabP2, creatures2);
            _tabs.TabPages.Add(tabP2);

            // Артефакты (заглушка)
            var tabArtifacts = new TabPage("Артефакты")
            {
                BackColor = Color.FromArgb(30, 30, 40),
                ForeColor = Color.White,
            };
            tabArtifacts.Controls.Add(new Label
            {
                Text = "В разработке...",
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 14),
                AutoSize = true,
                Location = new Point(20, 20),
            });
            _tabs.TabPages.Add(tabArtifacts);

            // Заклинания (заглушка)
            var tabSpells = new TabPage("Заклинания")
            {
                BackColor = Color.FromArgb(30, 30, 40),
                ForeColor = Color.White,
            };
            tabSpells.Controls.Add(new Label
            {
                Text = "В разработке...",
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 14),
                AutoSize = true,
                Location = new Point(20, 20),
            });
            _tabs.TabPages.Add(tabSpells);
        }
    }

    /// <summary>
    /// Содержимое вкладки с юнитами (ListView + панель деталей).
    /// </summary>
    internal class CreatureTabContent
    {
        private readonly TabPage _tab;
        private readonly List<CreatureInfo> _creatures;
        private ListView _creatureList;
        private Panel _detailPanel;
        private PictureBox _detailIcon;
        private Label _detailName;
        private Label _detailStats;
        private Label _detailUpgrades;
        private Label _detailAbilities;

        public CreatureTabContent(TabPage tab, List<CreatureInfo> creatures)
        {
            _tab = tab;
            _creatures = creatures;
            BuildUI();
            PopulateList();
        }

        private void BuildUI()
        {
            // Список юнитов
            _creatureList = new ListView
            {
                Parent = _tab,
                Location = new Point(10, 10),
                Size = new Size(620, 620),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(35, 35, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                OwnerDraw = true,
                BorderStyle = BorderStyle.FixedSingle,
            };

            _creatureList.Columns.Add("", 68);
            _creatureList.Columns.Add("Имя", 150);
            _creatureList.Columns.Add("Атк", 42);
            _creatureList.Columns.Add("Защ", 42);
            _creatureList.Columns.Add("Урон", 60);
            _creatureList.Columns.Add("HP", 45);
            _creatureList.Columns.Add("Скор", 42);
            _creatureList.Columns.Add("Иниц", 42);
            _creatureList.Columns.Add("Золото", 55);
            _creatureList.Columns.Add("Рост", 42);

            _creatureList.DrawColumnHeader += DrawColumnHeader;
            _creatureList.DrawSubItem += DrawSubItem;
            _creatureList.SelectedIndexChanged += SelectedChanged;

            // Панель деталей
            _detailPanel = new Panel
            {
                Parent = _tab,
                Location = new Point(640, 10),
                Size = new Size(470, 620),
                BackColor = Color.FromArgb(40, 40, 55),
                BorderStyle = BorderStyle.FixedSingle,
            };

            _detailIcon = new PictureBox
            {
                Parent = _detailPanel,
                Location = new Point(15, 15),
                Size = new Size(128, 128),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(30, 30, 45),
                BorderStyle = BorderStyle.FixedSingle,
            };

            _detailName = new Label
            {
                Parent = _detailPanel,
                Location = new Point(155, 15),
                Size = new Size(300, 30),
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 220, 100),
            };

            _detailStats = new Label
            {
                Parent = _detailPanel,
                Location = new Point(155, 50),
                Size = new Size(300, 95),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.White,
            };

            _detailUpgrades = new Label
            {
                Parent = _detailPanel,
                Location = new Point(15, 155),
                Size = new Size(440, 60),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(150, 200, 255),
            };

            _detailAbilities = new Label
            {
                Parent = _detailPanel,
                Location = new Point(15, 220),
                Size = new Size(440, 380),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(180, 255, 180),
            };
        }

        private void PopulateList()
        {
            _creatureList.Items.Clear();
            _creatureList.SmallImageList?.Dispose();

            var sorted = _creatures.OrderBy(c => c.Gold).ToList();

            var imageList = new ImageList
            {
                ImageSize = new Size(64, 64),
                ColorDepth = ColorDepth.Depth32Bit,
            };

            foreach (var creature in sorted)
            {
                string imgKey = creature.Id;
                if (creature.Icon != null)
                    imageList.Images.Add(imgKey, creature.Icon);
                else
                    imageList.Images.Add(imgKey, CreatePlaceholder(64, 64));

                var item = new ListViewItem("") { ImageKey = imgKey, Tag = creature };
                item.SubItems.Add(creature.Name);
                item.SubItems.Add(creature.AttackSkill.ToString());
                item.SubItems.Add(creature.DefenceSkill.ToString());
                item.SubItems.Add($"{creature.MinDamage}-{creature.MaxDamage}");
                item.SubItems.Add(creature.Health.ToString());
                item.SubItems.Add(creature.Speed.ToString());
                item.SubItems.Add(creature.Initiative.ToString());
                item.SubItems.Add(creature.Gold.ToString());
                item.SubItems.Add(creature.WeeklyGrowth.ToString());

                _creatureList.Items.Add(item);
            }

            _creatureList.SmallImageList = imageList;
        }

        private void DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(40, 40, 55)), e.Bounds);
            TextRenderer.DrawText(e.Graphics, e.Header?.Text ?? "",
                new Font("Segoe UI", 9, FontStyle.Bold), e.Bounds, Color.White,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
        }

        private void DrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
        {
            if (e.Item == null) return;

            var bgColor = e.Item.Selected
                ? Color.FromArgb(60, 60, 90)
                : (e.ItemIndex % 2 == 0
                    ? Color.FromArgb(35, 35, 50)
                    : Color.FromArgb(42, 42, 58));

            e.Graphics.FillRectangle(new SolidBrush(bgColor), e.Bounds);

            if (e.ColumnIndex == 0)
            {
                var creature = e.Item.Tag as CreatureInfo;
                if (creature?.Icon != null)
                {
                    int imgSize = Math.Min(e.Bounds.Width - 4, e.Bounds.Height - 4);
                    if (imgSize < 16) imgSize = 64;
                    int x = e.Bounds.X + (e.Bounds.Width - imgSize) / 2;
                    int y = e.Bounds.Y + (e.Bounds.Height - imgSize) / 2;
                    e.Graphics.DrawImage(creature.Icon, x, y, imgSize, imgSize);
                }
            }
            else
            {
                var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left;
                if (e.ColumnIndex >= 2)
                    flags = TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter;

                TextRenderer.DrawText(e.Graphics, e.SubItem?.Text ?? "",
                    _creatureList.Font, e.Bounds, Color.White, flags);
            }
        }

        private void SelectedChanged(object? sender, EventArgs e)
        {
            if (_creatureList.SelectedItems.Count == 0)
                return;

            var creature = _creatureList.SelectedItems[0].Tag as CreatureInfo;
            if (creature == null)
                return;

            _detailIcon.Image?.Dispose();
            _detailIcon.Image = creature.Icon != null ? new Bitmap(creature.Icon) : null;

            _detailName.Text = creature.Name;

            string ranged = creature.Shots > 0 ? $"  |  Выстрелы: {creature.Shots}" : "";
            string flying = creature.Flying ? "  |  Летает" : "";
            _detailStats.Text =
                $"Атака: {creature.AttackSkill}  |  Защита: {creature.DefenceSkill}\n" +
                $"Урон: {creature.MinDamage}-{creature.MaxDamage}  |  HP: {creature.Health}\n" +
                $"Скорость: {creature.Speed}  |  Инициатива: {creature.Initiative}{ranged}{flying}\n" +
                $"Золото: {creature.Gold}  |  Рост в неделю: {creature.WeeklyGrowth}\n" +
                $"Фракция: {creature.Faction}";

            _detailUpgrades.Text = creature.Upgrades.Count > 0
                ? "Улучшения: " + string.Join(", ", creature.Upgrades)
                : "Улучшения: нет";

            _detailAbilities.Text = creature.Abilities.Count > 0
                ? "Способности:\n" + string.Join("\n", creature.Abilities.Select(a => "  " + a))
                : "Способности: нет";
        }

        private static Image CreatePlaceholder(int w, int h)
        {
            var bmp = new Bitmap(w, h);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.FromArgb(50, 50, 65));
            g.DrawString("?", new Font("Segoe UI", 20, FontStyle.Bold),
                Brushes.Gray, w / 2 - 10, h / 2 - 16);
            return bmp;
        }
    }
}
