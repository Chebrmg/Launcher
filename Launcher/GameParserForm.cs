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
        private TabControl _tabs;
        private ListView _creatureList;
        private Panel _detailPanel;
        private ComboBox _factionFilter;
        private Label _loadingLabel;

        private List<CreatureInfo> _allCreatures = new();
        private CreatureInfo? _selectedCreature;

        // Детальная панель
        private PictureBox _detailIcon;
        private Label _detailName;
        private Label _detailStats;
        private Label _detailAbilities;
        private Label _detailUpgrades;

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

            _tabs = new TabControl
            {
                Parent = this,
                Location = new Point(10, 10),
                Size = new Size(Width - 36, Height - 60),
                Appearance = TabAppearance.FlatButtons,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
            };

            // Вкладка 1: Юниты
            var tabCreatures = new TabPage("Юниты")
            {
                BackColor = Color.FromArgb(30, 30, 40),
                ForeColor = Color.White,
            };
            _tabs.TabPages.Add(tabCreatures);

            // Вкладка 2: Прокачка (заглушка)
            var tabUpgrades = new TabPage("Прокачка")
            {
                BackColor = Color.FromArgb(30, 30, 40),
                ForeColor = Color.White,
            };
            tabUpgrades.Controls.Add(new Label
            {
                Text = "В разработке...",
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 14),
                AutoSize = true,
                Location = new Point(20, 20),
            });
            _tabs.TabPages.Add(tabUpgrades);

            // Вкладка 3: Артефакты (заглушка)
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

            // Вкладка 4: Заклинания (заглушка)
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

            // === Содержимое вкладки Юниты ===

            // Фильтр по фракциям
            var filterLabel = new Label
            {
                Text = "Фракция:",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Location = new Point(10, 12),
                AutoSize = true,
                Parent = tabCreatures,
            };

            _factionFilter = new ComboBox
            {
                Parent = tabCreatures,
                Location = new Point(80, 8),
                Size = new Size(180, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9),
                BackColor = Color.FromArgb(50, 50, 65),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
            };
            _factionFilter.Items.Add("Все фракции");
            foreach (string faction in GameDataParser.FactionOrder)
                _factionFilter.Items.Add(faction);
            _factionFilter.SelectedIndex = 0;
            _factionFilter.SelectedIndexChanged += (s, e) => FilterCreatures();

            // Список юнитов (ListView с иконками)
            _creatureList = new ListView
            {
                Parent = tabCreatures,
                Location = new Point(10, 42),
                Size = new Size(620, tabCreatures.Height > 0 ? 600 : 620),
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

            _creatureList.Columns.Add("", 68);       // Иконка
            _creatureList.Columns.Add("Имя", 150);
            _creatureList.Columns.Add("Атк", 42);
            _creatureList.Columns.Add("Защ", 42);
            _creatureList.Columns.Add("Урон", 60);
            _creatureList.Columns.Add("HP", 45);
            _creatureList.Columns.Add("Скор", 42);
            _creatureList.Columns.Add("Иниц", 42);
            _creatureList.Columns.Add("Золото", 55);
            _creatureList.Columns.Add("Рост", 42);

            _creatureList.DrawColumnHeader += CreatureList_DrawColumnHeader;
            _creatureList.DrawSubItem += CreatureList_DrawSubItem;
            _creatureList.SelectedIndexChanged += CreatureList_SelectedIndexChanged;

            // Панель деталей (справа)
            _detailPanel = new Panel
            {
                Parent = tabCreatures,
                Location = new Point(640, 42),
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

            // Индикатор загрузки
            _loadingLabel = new Label
            {
                Parent = tabCreatures,
                Text = "Загрузка данных...",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.Gray,
                Size = new Size(600, 300),
                Location = new Point(20, 200),
            };

            // Размер ListView подстраиваем под размер таба
            _tabs.SelectedIndexChanged += (s, e) => AdjustSizes();
            this.Shown += GameParserForm_Shown;
            this.Resize += (s, e) => AdjustSizes();
        }

        private void AdjustSizes()
        {
            var tab = _tabs.SelectedTab;
            if (tab == null) return;

            int contentHeight = tab.ClientSize.Height - 50;
            if (contentHeight < 100) contentHeight = 600;

            _creatureList.Size = new Size(620, contentHeight);
            _detailPanel.Size = new Size(470, contentHeight);
        }

        private void GameParserForm_Shown(object? sender, EventArgs e)
        {
            AdjustSizes();
            LoadCreatures();
        }

        private void LoadCreatures()
        {
            _creatureList.Visible = false;
            _detailPanel.Visible = false;
            _loadingLabel.Visible = true;
            _loadingLabel.Text = "Загрузка данных...";
            _loadingLabel.BringToFront();
            _creatureList.Items.Clear();

            System.Threading.Tasks.Task.Run(() =>
            {
                var parser = new GameDataParser(_gameRoot);
                parser.BuildVfs();
                var creatures = parser.ParseCreatures();
                return (creatures, parser.DiagInfo);
            }).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    _loadingLabel.Text = "Ошибка загрузки:\n" + task.Exception?.InnerException?.Message;
                    return;
                }

                var (creatures, diagInfo) = task.Result;
                _allCreatures = creatures;

                if (_allCreatures.Count == 0)
                {
                    _loadingLabel.Text = "Юниты не найдены.\n\nДиагностика:\n" + diagInfo;
                    return;
                }

                _loadingLabel.Visible = false;
                _creatureList.Visible = true;
                _detailPanel.Visible = true;
                FilterCreatures();
            }, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void FilterCreatures()
        {
            _creatureList.Items.Clear();
            _creatureList.SmallImageList?.Dispose();

            string? selectedFaction = _factionFilter.SelectedIndex == 0
                ? null
                : _factionFilter.SelectedItem?.ToString();

            var filtered = selectedFaction == null
                ? _allCreatures
                : _allCreatures.Where(c => c.Faction == selectedFaction).ToList();

            // Сортируем: по порядку фракций, потом по Gold
            var sorted = filtered
                .OrderBy(c => Array.IndexOf(GameDataParser.FactionOrder, c.Faction))
                .ThenBy(c => c.Gold)
                .ToList();

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

        private void CreatureList_DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(40, 40, 55)), e.Bounds);
            TextRenderer.DrawText(e.Graphics, e.Header?.Text ?? "",
                new Font("Segoe UI", 9, FontStyle.Bold), e.Bounds, Color.White,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
        }

        private void CreatureList_DrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
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
                // Рисуем иконку
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

        private void CreatureList_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_creatureList.SelectedItems.Count == 0)
                return;

            var creature = _creatureList.SelectedItems[0].Tag as CreatureInfo;
            if (creature == null)
                return;

            _selectedCreature = creature;

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
                ? "Способности:\n" + string.Join("\n", creature.Abilities.Select(a => "• " + a))
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
