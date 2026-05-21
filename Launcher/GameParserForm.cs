using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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

        // Основной контент
        private TabControl _tabs;

        private string _faction1 = "";
        private string _faction2 = "";

        // Общее золото для обоих игроков
        private int _totalGold = 120000;

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

            new Label
            {
                Parent = _selectionPanel,
                Text = "Выберите фракции",
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 220, 100),
                AutoSize = true,
                Location = new Point(400, 120),
            };

            new Label
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

            new Label
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
                var c1 = parser.ParseCreatures(new List<string> { _faction1 });
                var c2 = parser.ParseCreatures(new List<string> { _faction2 });
                var artifacts = parser.ParseArtifacts();
                return (c1, c2, artifacts, parser.DiagInfo);
            }).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    _selectionStatus.Text = "Ошибка: " + task.Exception?.InnerException?.Message;
                    _selectionStatus.ForeColor = Color.Red;
                    _btnStart.Enabled = true;
                    return;
                }

                var (c1, c2, artifacts, diag) = task.Result;
                if (c1.Count == 0 && c2.Count == 0)
                {
                    _selectionStatus.Text = "Юниты не найдены.\n\nДиагностика:\n" + diag;
                    _selectionStatus.ForeColor = Color.OrangeRed;
                    _btnStart.Enabled = true;
                    return;
                }

                ShowMainContent(c1, c2, artifacts);
            }, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void ShowMainContent(List<CreatureInfo> creatures1, List<CreatureInfo> creatures2, List<ArtifactInfo> artifacts)
        {
            _selectionPanel.Visible = false;

            // Панель переключения игроков
            var playerPanel = new Panel
            {
                Parent = this,
                Location = new Point(10, 10),
                Size = new Size(Width - 36, 40),
                BackColor = Color.FromArgb(35, 35, 50),
            };

            var btnPlayer1 = new Button
            {
                Parent = playerPanel,
                Text = $"Игрок 1: {_faction1}",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Size = new Size(250, 34),
                Location = new Point(3, 3),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(100, 180, 255),
                ForeColor = Color.Black,
            };
            btnPlayer1.FlatAppearance.BorderSize = 0;

            var btnPlayer2 = new Button
            {
                Parent = playerPanel,
                Text = $"Игрок 2: {_faction2}",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Size = new Size(250, 34),
                Location = new Point(260, 3),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 80),
                ForeColor = Color.White,
            };
            btnPlayer2.FlatAppearance.BorderSize = 0;

            _tabs.Location = new Point(10, 55);
            _tabs.Size = new Size(Width - 36, Height - 105);
            _tabs.Visible = true;
            _tabs.TabPages.Clear();

            var goldState1 = new GoldState(_totalGold);
            var goldState2 = new GoldState(_totalGold);

            // Вкладки Игрока 1
            var tabArmy1 = new TabPage("Армия") { BackColor = Color.FromArgb(30, 30, 40), ForeColor = Color.White };
            new ArmyPurchaseTab(tabArmy1, creatures1, goldState1);

            var tabArt1 = new TabPage("Артефакты") { BackColor = Color.FromArgb(30, 30, 40), ForeColor = Color.White };
            new ArtifactTab(tabArt1, artifacts, goldState1);

            var tabSpells1 = new TabPage("Заклинания") { BackColor = Color.FromArgb(30, 30, 40), ForeColor = Color.White };
            tabSpells1.Controls.Add(new Label { Text = "В разработке...", ForeColor = Color.Gray, Font = new Font("Segoe UI", 14), AutoSize = true, Location = new Point(20, 20) });

            // Вкладки Игрока 2
            var tabArmy2 = new TabPage("Армия") { BackColor = Color.FromArgb(30, 30, 40), ForeColor = Color.White };
            new ArmyPurchaseTab(tabArmy2, creatures2, goldState2);

            var tabArt2 = new TabPage("Артефакты") { BackColor = Color.FromArgb(30, 30, 40), ForeColor = Color.White };
            new ArtifactTab(tabArt2, artifacts, goldState2);

            var tabSpells2 = new TabPage("Заклинания") { BackColor = Color.FromArgb(30, 30, 40), ForeColor = Color.White };
            tabSpells2.Controls.Add(new Label { Text = "В разработке...", ForeColor = Color.Gray, Font = new Font("Segoe UI", 14), AutoSize = true, Location = new Point(20, 20) });

            // По умолчанию — Игрок 1
            _tabs.TabPages.Add(tabArmy1);
            _tabs.TabPages.Add(tabArt1);
            _tabs.TabPages.Add(tabSpells1);

            btnPlayer1.Click += (s, ev) =>
            {
                btnPlayer1.BackColor = Color.FromArgb(100, 180, 255);
                btnPlayer1.ForeColor = Color.Black;
                btnPlayer2.BackColor = Color.FromArgb(60, 60, 80);
                btnPlayer2.ForeColor = Color.White;
                int selectedIdx = _tabs.SelectedIndex;
                _tabs.TabPages.Clear();
                _tabs.TabPages.Add(tabArmy1);
                _tabs.TabPages.Add(tabArt1);
                _tabs.TabPages.Add(tabSpells1);
                if (selectedIdx >= 0 && selectedIdx < _tabs.TabCount)
                    _tabs.SelectedIndex = selectedIdx;
            };

            btnPlayer2.Click += (s, ev) =>
            {
                btnPlayer2.BackColor = Color.FromArgb(255, 130, 130);
                btnPlayer2.ForeColor = Color.Black;
                btnPlayer1.BackColor = Color.FromArgb(60, 60, 80);
                btnPlayer1.ForeColor = Color.White;
                int selectedIdx = _tabs.SelectedIndex;
                _tabs.TabPages.Clear();
                _tabs.TabPages.Add(tabArmy2);
                _tabs.TabPages.Add(tabArt2);
                _tabs.TabPages.Add(tabSpells2);
                if (selectedIdx >= 0 && selectedIdx < _tabs.TabCount)
                    _tabs.SelectedIndex = selectedIdx;
            };
        }
    }

    /// <summary>
    /// Общее золото между вкладками.
    /// </summary>
    internal class GoldState
    {
        public int Total { get; private set; }
        public int Spent { get; set; }
        public int Remaining => Total - Spent;

        public event Action? Changed;

        public GoldState(int total)
        {
            Total = total;
        }

        public bool TrySpend(int amount)
        {
            if (amount > Remaining) return false;
            Spent += amount;
            Changed?.Invoke();
            return true;
        }

        public void Refund(int amount)
        {
            Spent -= amount;
            if (Spent < 0) Spent = 0;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Слот армии: юнит + количество.
    /// </summary>
    internal class ArmySlot
    {
        public CreatureInfo? Creature { get; set; }
        public int Count { get; set; }
    }

    /// <summary>
    /// Множитель прироста по тиру (таблица).
    /// </summary>
    internal static class TierMultipliers
    {
        private static readonly Dictionary<int, int> Multipliers = new()
        {
            { 1, 12 }, { 2, 11 }, { 3, 10 }, { 4, 9 },
            { 5, 8 }, { 6, 7 }, { 7, 6 },
        };

        public static int Get(int tier)
        {
            return Multipliers.TryGetValue(tier, out int m) ? m : 6;
        }
    }

    /// <summary>
    /// Пул юнитов одного тира.
    /// </summary>
    internal class TierPool
    {
        public int Tier { get; }
        public int MaxPool { get; private set; }
        public int Available { get; set; }
        public List<CreatureInfo> Creatures { get; } = new();

        public TierPool(int tier)
        {
            Tier = tier;
        }

        public void Initialize()
        {
            int baseGrowth = Creatures.Where(c => c.IsBase).Sum(c => c.WeeklyGrowth);
            MaxPool = baseGrowth * TierMultipliers.Get(Tier);
            Available = MaxPool;
        }
    }

    internal class ArmyPurchaseTab
    {
        private readonly TabPage _tab;
        private readonly GoldState _gold;
        private readonly List<CreatureInfo> _creatures;
        private readonly Dictionary<int, TierPool> _pools = new();
        private readonly ArmySlot[] _slots = new ArmySlot[7];
        private readonly Panel[] _slotPanels = new Panel[7];
        private Label _goldLabel;
        private Panel _shopPanel;
        private Panel _armyPanel;

        public ArmyPurchaseTab(TabPage tab, List<CreatureInfo> creatures, GoldState gold)
        {
            _tab = tab;
            _gold = gold;
            _creatures = creatures;

            for (int i = 0; i < 7; i++)
                _slots[i] = new ArmySlot();

            BuildPools();
            BuildUI();
            _gold.Changed += RefreshGold;
        }

        private void BuildPools()
        {
            foreach (var c in _creatures)
            {
                int tier = c.CreatureTier;
                if (tier < 1 || tier > 7) continue;

                if (!_pools.ContainsKey(tier))
                    _pools[tier] = new TierPool(tier);
                _pools[tier].Creatures.Add(c);
            }

            foreach (var pool in _pools.Values)
                pool.Initialize();
        }

        private void BuildUI()
        {
            // Золото
            _goldLabel = new Label
            {
                Parent = _tab,
                Location = new Point(10, 8),
                Size = new Size(300, 25),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 220, 100),
            };
            RefreshGold();

            // Магазин юнитов (левая часть)
            _shopPanel = new DoubleBufferedPanel
            {
                Parent = _tab,
                Location = new Point(10, 40),
                Size = new Size(620, 610),
                AutoScroll = true,
                BackColor = Color.FromArgb(30, 30, 40),
            };

            // Армия (правая часть)
            _armyPanel = new DoubleBufferedPanel
            {
                Parent = _tab,
                Location = new Point(640, 40),
                Size = new Size(470, 610),
                BackColor = Color.FromArgb(35, 35, 50),
                BorderStyle = BorderStyle.FixedSingle,
            };

            var armyTitle = new Label
            {
                Parent = _armyPanel,
                Text = "Армия (7 слотов)",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(10, 5),
                AutoSize = true,
            };

            for (int i = 0; i < 7; i++)
                BuildSlotPanel(i);

            RebuildShop();
        }

        private void BuildSlotPanel(int index)
        {
            int y = 30 + index * 80;
            var panel = new Panel
            {
                Parent = _armyPanel,
                Location = new Point(5, y),
                Size = new Size(455, 74),
                BackColor = Color.FromArgb(45, 45, 65),
                BorderStyle = BorderStyle.FixedSingle,
            };

            var lblIndex = new Label
            {
                Parent = panel,
                Text = $"{index + 1}.",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.Gray,
                Location = new Point(5, 26),
                AutoSize = true,
            };

            _slotPanels[index] = panel;
            RefreshSlot(index);
        }

        private void RefreshSlot(int index)
        {
            var panel = _slotPanels[index];
            panel.SuspendLayout();
            // Удаляем всё кроме номера
            var toRemove = new List<Control>();
            foreach (Control c in panel.Controls)
            {
                if (c is Label lbl && lbl.Text == $"{index + 1}.")
                    continue;
                toRemove.Add(c);
            }
            foreach (var c in toRemove)
            {
                panel.Controls.Remove(c);
                c.Dispose();
            }

            var slot = _slots[index];
            if (slot.Creature == null)
            {
                var emptyLbl = new Label
                {
                    Parent = panel,
                    Text = "Пустой слот",
                    Font = new Font("Segoe UI", 9),
                    ForeColor = Color.DarkGray,
                    Location = new Point(30, 26),
                    AutoSize = true,
                };
                panel.BackColor = Color.FromArgb(45, 45, 65);
                panel.ResumeLayout();
                return;
            }

            panel.BackColor = Color.FromArgb(50, 50, 70);
            panel.Cursor = Cursors.Hand;
            var creature = slot.Creature;
            panel.DoubleClick += (s, ev) => ShowCreatureDetail(creature);

            // Иконка
            var icon = new PictureBox
            {
                Parent = panel,
                Location = new Point(25, 3),
                Size = new Size(64, 64),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = creature.Icon != null ? new Bitmap(creature.Icon) : null,
                BackColor = Color.FromArgb(35, 35, 50),
                Cursor = Cursors.Hand,
            };
            icon.DoubleClick += (s, ev) => ShowCreatureDetail(creature);

            // Имя + количество
            var nameLbl = new Label
            {
                Parent = panel,
                Text = $"{creature.Name} x{slot.Count}",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(95, 5),
                AutoSize = true,
                Cursor = Cursors.Hand,
            };
            nameLbl.DoubleClick += (s, ev) => ShowCreatureDetail(creature);

            // Статы краткие
            var statsLbl = new Label
            {
                Parent = panel,
                Text = $"Т{creature.CreatureTier} | А:{creature.AttackSkill} З:{creature.DefenceSkill} У:{creature.MinDamage}-{creature.MaxDamage} HP:{creature.Health}",
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.LightGray,
                Location = new Point(95, 25),
                AutoSize = true,
                Cursor = Cursors.Hand,
            };
            statsLbl.DoubleClick += (s, ev) => ShowCreatureDetail(creature);

            var gradeLbl = new Label
            {
                Parent = panel,
                Text = creature.IsBase ? "Базовый" : "Грейд",
                Font = new Font("Segoe UI", 8),
                ForeColor = creature.IsBase ? Color.Gray : Color.FromArgb(100, 255, 100),
                Location = new Point(95, 43),
                AutoSize = true,
                Cursor = Cursors.Hand,
            };
            gradeLbl.DoubleClick += (s, ev) => ShowCreatureDetail(creature);

            // Кнопки грейда (2 кнопки для 2 грейдов)
            if (creature.IsBase && creature.Upgrades.Count > 0)
            {
                int btnX = 200;
                foreach (var upId in creature.Upgrades)
                {
                    var upgraded = _creatures.FirstOrDefault(c => c.Id == upId);
                    if (upgraded != null)
                    {
                        int costDiff = (upgraded.Gold - creature.Gold) * slot.Count;
                        var btnUp = new Button
                        {
                            Parent = panel,
                            Text = $"▲ {upgraded.Name} ({costDiff}g)",
                            Font = new Font("Segoe UI", 7),
                            Size = new Size(120, 22),
                            Location = new Point(btnX, 43),
                            FlatStyle = FlatStyle.Flat,
                            BackColor = Color.FromArgb(50, 100, 50),
                            ForeColor = Color.White,
                            Tag = new object[] { index, upgraded, costDiff },
                        };
                        btnUp.FlatAppearance.BorderSize = 0;
                        btnUp.Click += BtnUpgrade_Click;
                        btnX += 125;
                    }
                }
            }
            else if (!creature.IsBase)
            {
                // Найти базового юнита (у кого в Upgrades есть этот ID)
                var baseCreature = _creatures.FirstOrDefault(c =>
                    c.IsBase && c.Upgrades.Contains(creature.Id) && c.CreatureTier == creature.CreatureTier);
                if (baseCreature != null)
                {
                    // Кнопка дегрейда (бесплатно)
                    var btnDown = new Button
                    {
                        Parent = panel,
                        Text = $"▼ {baseCreature.Name} (0g)",
                        Font = new Font("Segoe UI", 7),
                        Size = new Size(120, 22),
                        Location = new Point(200, 43),
                        FlatStyle = FlatStyle.Flat,
                        BackColor = Color.FromArgb(80, 80, 50),
                        ForeColor = Color.White,
                        Tag = new object[] { index, baseCreature },
                    };
                    btnDown.FlatAppearance.BorderSize = 0;
                    btnDown.Click += BtnDowngrade_Click;

                    // Кнопка регрейда в другой апгрейд
                    if (baseCreature.Upgrades.Count > 1)
                    {
                        var otherId = baseCreature.Upgrades.FirstOrDefault(u => u != creature.Id);
                        var other = _creatures.FirstOrDefault(c => c.Id == otherId);
                        if (other != null)
                        {
                            int costDiff = (other.Gold - creature.Gold) * slot.Count;
                            int displayCost = Math.Max(0, costDiff);
                            var btnRe = new Button
                            {
                                Parent = panel,
                                Text = $"⇄ {other.Name} ({displayCost}g)",
                                Font = new Font("Segoe UI", 7),
                                Size = new Size(120, 22),
                                Location = new Point(325, 43),
                                FlatStyle = FlatStyle.Flat,
                                BackColor = Color.FromArgb(60, 60, 100),
                                ForeColor = Color.White,
                                Tag = new object[] { index, other, costDiff },
                            };
                            btnRe.FlatAppearance.BorderSize = 0;
                            btnRe.Click += BtnRegrade_Click;
                        }
                    }
                }
            }

            // Кнопка продажи
            int sellValue = creature.Gold * slot.Count;
            var btnSell = new Button
            {
                Parent = panel,
                Text = $"Продать (+{sellValue}g)",
                Font = new Font("Segoe UI", 7),
                Size = new Size(90, 22),
                Location = new Point(360, 5),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(120, 50, 50),
                ForeColor = Color.White,
                Tag = index,
            };
            btnSell.FlatAppearance.BorderSize = 0;
            btnSell.Click += BtnSell_Click;
            panel.ResumeLayout();
        }

        private void BtnUpgrade_Click(object? sender, EventArgs e)
        {
            var btn = sender as Button;
            if (btn?.Tag is not object[] args) return;
            int slotIdx = (int)args[0];
            var upgraded = (CreatureInfo)args[1];
            int costDiff = (int)args[2];

            if (!_gold.TrySpend(costDiff))
            {
                MessageBox.Show("Недостаточно золота!", "Грейд", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _slots[slotIdx].Creature = upgraded;
            RefreshSlot(slotIdx);
            RebuildShop();
        }

        private void BtnDowngrade_Click(object? sender, EventArgs e)
        {
            var btn = sender as Button;
            if (btn?.Tag is not object[] args) return;
            int slotIdx = (int)args[0];
            var baseCreature = (CreatureInfo)args[1];

            var current = _slots[slotIdx].Creature;
            if (current != null)
            {
                int refund = (current.Gold - baseCreature.Gold) * _slots[slotIdx].Count;
                if (refund > 0) _gold.Refund(refund);
            }

            _slots[slotIdx].Creature = baseCreature;
            RefreshSlot(slotIdx);
            RebuildShop();
        }

        private void BtnRegrade_Click(object? sender, EventArgs e)
        {
            var btn = sender as Button;
            if (btn?.Tag is not object[] args) return;
            int slotIdx = (int)args[0];
            var other = (CreatureInfo)args[1];
            int costDiff = (int)args[2];

            if (costDiff > 0 && !_gold.TrySpend(costDiff))
            {
                MessageBox.Show("Недостаточно золота!", "Регрейд", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            else if (costDiff < 0)
            {
                _gold.Refund(-costDiff);
            }

            _slots[slotIdx].Creature = other;
            RefreshSlot(slotIdx);
            RebuildShop();
        }

        private void BtnSell_Click(object? sender, EventArgs e)
        {
            var btn = sender as Button;
            if (btn?.Tag is not int slotIdx) return;

            var slot = _slots[slotIdx];
            if (slot.Creature == null) return;

            int refund = slot.Creature.Gold * slot.Count;
            int tier = slot.Creature.CreatureTier;

            // Вернуть юнитов в пул
            if (_pools.TryGetValue(tier, out var pool))
                pool.Available += slot.Count;

            _gold.Refund(refund);

            slot.Creature = null;
            slot.Count = 0;

            RefreshSlot(slotIdx);
            RebuildShop();
        }

        private void RebuildShop()
        {
            _shopPanel.SuspendLayout();
            foreach (Control c in _shopPanel.Controls)
                c.Dispose();
            _shopPanel.Controls.Clear();
            int y = 0;

            foreach (int tier in _pools.Keys.OrderBy(t => t))
            {
                var pool = _pools[tier];

                // Заголовок тира
                var tierHeader = new Label
                {
                    Parent = _shopPanel,
                    Text = $"Тир {tier}  |  Доступно: {pool.Available}/{pool.MaxPool}",
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    ForeColor = Color.FromArgb(200, 200, 100),
                    Location = new Point(5, y + 2),
                    AutoSize = true,
                };
                y += 22;

                // Сортируем: базовые сначала, потом грейды
                var sorted = pool.Creatures.OrderBy(c => c.IsBase ? 0 : 1).ThenBy(c => c.Gold).ToList();

                foreach (var creature in sorted)
                {
                    var card = BuildCreatureCard(creature, pool, y);
                    card.Parent = _shopPanel;
                    y += card.Height + 4;
                }

                y += 8;
            }
            _shopPanel.ResumeLayout();
        }

        private Panel BuildCreatureCard(CreatureInfo creature, TierPool pool, int yPos)
        {
            var card = new Panel
            {
                Location = new Point(0, yPos),
                Size = new Size(595, 44),
                BackColor = Color.FromArgb(42, 42, 60),
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand,
            };
            card.DoubleClick += (s, e) => ShowCreatureDetail(creature);

            // Иконка
            var icon = new PictureBox
            {
                Parent = card,
                Location = new Point(2, 2),
                Size = new Size(38, 38),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = creature.Icon != null ? new Bitmap(creature.Icon) : null,
                BackColor = Color.FromArgb(35, 35, 50),
                Cursor = Cursors.Hand,
            };
            icon.DoubleClick += (s, e) => ShowCreatureDetail(creature);

            // Имя + цена
            string gradeTag = creature.IsBase ? "" : " ★";
            var nameLbl = new Label
            {
                Parent = card,
                Text = $"{creature.Name}{gradeTag}  —  {creature.Gold}g",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = creature.IsBase ? Color.White : Color.FromArgb(100, 255, 100),
                Location = new Point(46, 11),
                AutoSize = true,
                Cursor = Cursors.Hand,
            };
            nameLbl.DoubleClick += (s, e) => ShowCreatureDetail(creature);

            // Кнопка купить (с NumericUpDown для количества)
            bool canBuy = pool.Available > 0;
            int maxBuy = pool.Available;

            // Проверяем есть ли свободные слоты или слот с таким же юнитом
            bool hasSlot = false;
            for (int i = 0; i < 7; i++)
            {
                if (_slots[i].Creature == null || _slots[i].Creature.Id == creature.Id)
                {
                    hasSlot = true;
                    break;
                }
            }

            if (canBuy && hasSlot)
            {
                int maxByGold = creature.Gold > 0 ? _gold.Remaining / creature.Gold : maxBuy;
                int realMax = Math.Max(1, Math.Min(maxBuy, maxByGold));
                var nud = new NumericUpDown
                {
                    Parent = card,
                    Location = new Point(420, 8),
                    Size = new Size(60, 25),
                    Minimum = 1,
                    Maximum = realMax,
                    Value = realMax,
                    Font = new Font("Segoe UI", 9),
                    BackColor = Color.FromArgb(50, 50, 65),
                    ForeColor = Color.White,
                };

                var btnBuy = new Button
                {
                    Parent = card,
                    Text = "Купить",
                    Font = new Font("Segoe UI", 8, FontStyle.Bold),
                    Size = new Size(95, 28),
                    Location = new Point(490, 7),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(50, 110, 50),
                    ForeColor = Color.White,
                    Tag = new object[] { creature, nud },
                };
                btnBuy.FlatAppearance.BorderSize = 0;
                btnBuy.Click += BtnBuy_Click;
            }
            else
            {
                var noLbl = new Label
                {
                    Parent = card,
                    Text = !hasSlot ? "Нет слотов" : "Нет в пуле",
                    Font = new Font("Segoe UI", 8),
                    ForeColor = Color.DarkGray,
                    Location = new Point(490, 12),
                    AutoSize = true,
                };
            }

            return card;
        }

        private void BtnBuy_Click(object? sender, EventArgs e)
        {
            var btn = sender as Button;
            if (btn?.Tag is not object[] args) return;
            var creature = (CreatureInfo)args[0];
            var nud = (NumericUpDown)args[1];
            int count = (int)nud.Value;

            int totalCost = creature.Gold * count;
            if (!_gold.TrySpend(totalCost))
            {
                MessageBox.Show("Недостаточно золота!", "Покупка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int tier = creature.CreatureTier;
            if (_pools.TryGetValue(tier, out var pool))
                pool.Available -= count;

            // Найти слот: существующий с таким юнитом или пустой
            int targetSlot = -1;
            for (int i = 0; i < 7; i++)
            {
                if (_slots[i].Creature?.Id == creature.Id)
                {
                    targetSlot = i;
                    break;
                }
            }
            if (targetSlot == -1)
            {
                for (int i = 0; i < 7; i++)
                {
                    if (_slots[i].Creature == null)
                    {
                        targetSlot = i;
                        break;
                    }
                }
            }
            if (targetSlot == -1)
            {
                _gold.Refund(totalCost);
                if (_pools.TryGetValue(tier, out var p)) p.Available += count;
                MessageBox.Show("Нет свободных слотов!", "Покупка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _slots[targetSlot].Creature = creature;
            _slots[targetSlot].Count += count;

            RefreshSlot(targetSlot);
            RebuildShop();
        }

        private void RefreshGold()
        {
            if (_goldLabel != null)
                _goldLabel.Text = $"Золото: {_gold.Remaining} / {_gold.Total}";
        }

        private void ShowCreatureDetail(CreatureInfo creature)
        {
            var form = new CreatureDetailForm(creature);
            form.ShowDialog();
        }
    }

    /// <summary>
    /// Вкладка артефактов: магазин (3 минора, 2 мажора, 1 реликвия) + слоты экипировки.
    /// </summary>
    internal class ArtifactTab
    {
        private readonly TabPage _tab;
        private readonly GoldState _gold;
        private readonly List<ArtifactInfo> _allArtifacts;
        private readonly Random _rng = new();

        private const int RerollCost = 5000;

        private static readonly string[] SlotNames =
        {
            "PRIMARY", "SECONDARY", "HEAD", "CHEST",
            "NECK", "SHOULDERS", "FINGER 1", "FINGER 2",
            "FEET", "MISCSLOT1",
        };

        private const int HeroPanelW = 490;
        private const int HeroPanelH = 362;
        private const int SlotIconSize = 64;

        // Позиции центров слотов на фоне (масштабированные из 664×490 → 490×362)
        private static readonly Dictionary<string, Point> SlotPositions = new()
        {
            { "HEAD",     new Point(66, 63) },
            { "NECK",     new Point(203, 63) },
            { "SHOULDERS",new Point(300, 63) },
            { "MISCSLOT1",new Point(419, 63) },
            { "PRIMARY",  new Point(66, 179) },
            { "SECONDARY",new Point(419, 179) },
            { "FEET",     new Point(66, 300) },
            { "FINGER 1", new Point(203, 300) },
            { "FINGER 2", new Point(300, 300) },
            { "CHEST",    new Point(419, 300) },
        };

        private List<ArtifactInfo> _shopItems = new();
        private readonly Dictionary<string, ArtifactInfo?> _equipped = new();
        private readonly Dictionary<string, PictureBox> _slotIcons = new();
        private Panel _shopPanel;
        private Panel _slotsPanel;
        private Panel _heroPanel;
        private Label _goldLabel;
        private ToolTip _slotTip = new();

        public ArtifactTab(TabPage tab, List<ArtifactInfo> allArtifacts, GoldState gold)
        {
            _tab = tab;
            _gold = gold;
            _allArtifacts = allArtifacts;

            foreach (var slot in SlotNames)
                _equipped[slot] = null;

            BuildUI();
            RollShop();
            _gold.Changed += RefreshGold;
        }

        private void BuildUI()
        {
            _goldLabel = new Label
            {
                Parent = _tab,
                Location = new Point(10, 8),
                Size = new Size(300, 25),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 220, 100),
            };
            RefreshGold();

            // Магазин (левая часть)
            _shopPanel = new DoubleBufferedPanel
            {
                Parent = _tab,
                Location = new Point(10, 40),
                Size = new Size(600, 610),
                AutoScroll = true,
                BackColor = Color.FromArgb(30, 30, 40),
            };

            // Слоты экипировки (правая часть)
            _slotsPanel = new DoubleBufferedPanel
            {
                Parent = _tab,
                Location = new Point(620, 40),
                Size = new Size(HeroPanelW, 610),
                BackColor = Color.FromArgb(35, 35, 50),
            };

            // Фон героя с ячейками слотов
            _heroPanel = new DoubleBufferedPanel
            {
                Parent = _slotsPanel,
                Location = new Point(0, 0),
                Size = new Size(HeroPanelW, HeroPanelH),
            };
            var bgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hero_slots.png");
            if (File.Exists(bgPath))
            {
                _heroPanel.BackgroundImage = Image.FromFile(bgPath);
                _heroPanel.BackgroundImageLayout = ImageLayout.Stretch;
            }

            // Создаём PictureBox для каждого слота
            int half = SlotIconSize / 2;
            foreach (var slot in SlotNames)
            {
                if (!SlotPositions.TryGetValue(slot, out var center))
                    continue;

                var pb = new PictureBox
                {
                    Parent = _heroPanel,
                    Location = new Point(center.X - half, center.Y - half),
                    Size = new Size(SlotIconSize, SlotIconSize),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand,
                    Tag = slot,
                };

                var ctx = new ContextMenuStrip();
                ctx.Items.Add("Снять", null, (s, ev) => RemoveArtifact(slot));
                ctx.Items.Add("Подробнее", null, (s, ev) =>
                {
                    if (_equipped[slot] is ArtifactInfo art)
                        ShowArtifactDetail(art);
                });
                pb.ContextMenuStrip = ctx;
                pb.DoubleClick += (s, ev) =>
                {
                    if (_equipped[slot] is ArtifactInfo art)
                        ShowArtifactDetail(art);
                };

                _slotIcons[slot] = pb;
            }
        }

        private void RemoveArtifact(string slot)
        {
            var old = _equipped[slot];
            if (old == null) return;
            _equipped[slot] = null;
            _gold.Refund(old.CostOfGold);
            RebuildSlots();
        }

        private void RollShop()
        {
            var minors = _allArtifacts.Where(a => a.Type == "ARTF_CLASS_MINOR").ToList();
            var majors = _allArtifacts.Where(a => a.Type == "ARTF_CLASS_MAJOR").ToList();
            var relics = _allArtifacts.Where(a => a.Type == "ARTF_CLASS_RELIC").ToList();

            _shopItems.Clear();
            _shopItems.AddRange(PickRandom(minors, 3));
            _shopItems.AddRange(PickRandom(majors, 2));
            _shopItems.AddRange(PickRandom(relics, 1));

            RebuildShop();
        }

        private List<ArtifactInfo> PickRandom(List<ArtifactInfo> source, int count)
        {
            var shuffled = source.OrderBy(_ => _rng.Next()).ToList();
            return shuffled.Take(Math.Min(count, shuffled.Count)).ToList();
        }

        private void RebuildShop()
        {
            _shopPanel.SuspendLayout();
            foreach (Control c in _shopPanel.Controls)
                c.Dispose();
            _shopPanel.Controls.Clear();

            var title = new Label
            {
                Parent = _shopPanel,
                Text = "Магазин артефактов",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(5, 5),
                AutoSize = true,
            };

            int y = 30;
            string currentType = "";

            foreach (var art in _shopItems.OrderBy(a => a.Type))
            {
                if (art.Type != currentType)
                {
                    currentType = art.Type;
                    string typeLabel = currentType switch
                    {
                        "ARTF_CLASS_MINOR" => "Минорные",
                        "ARTF_CLASS_MAJOR" => "Мажорные",
                        "ARTF_CLASS_RELIC" => "Реликвии",
                        _ => currentType,
                    };
                    var header = new Label
                    {
                        Parent = _shopPanel,
                        Text = typeLabel,
                        Font = new Font("Segoe UI", 9, FontStyle.Bold),
                        ForeColor = Color.FromArgb(200, 200, 100),
                        Location = new Point(5, y),
                        AutoSize = true,
                    };
                    y += 22;
                }

                var card = BuildArtifactCard(art, y);
                card.Parent = _shopPanel;
                y += card.Height + 4;
            }

            y += 10;

            // Кнопка рерол
            var btnReroll = new Button
            {
                Parent = _shopPanel,
                Text = $"Рерол ({RerollCost}g)",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Size = new Size(180, 35),
                Location = new Point(5, y),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(80, 60, 120),
                ForeColor = Color.White,
            };
            btnReroll.FlatAppearance.BorderSize = 0;
            btnReroll.Click += (s, ev) =>
            {
                if (!_gold.TrySpend(RerollCost))
                {
                    MessageBox.Show("Недостаточно золота!", "Рерол", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                RollShop();
            };

            _shopPanel.ResumeLayout();
            RebuildSlots();
        }

        private Panel BuildArtifactCard(ArtifactInfo art, int yPos)
        {
            var card = new Panel
            {
                Location = new Point(0, yPos),
                Size = new Size(575, 64),
                BackColor = Color.FromArgb(42, 42, 60),
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand,
            };
            card.DoubleClick += (s, e) => ShowArtifactDetail(art);

            var icon = new PictureBox
            {
                Parent = card,
                Location = new Point(2, 2),
                Size = new Size(58, 58),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = art.Icon != null ? new Bitmap(art.Icon) : null,
                BackColor = Color.FromArgb(35, 35, 50),
                Cursor = Cursors.Hand,
            };
            icon.DoubleClick += (s, e) => ShowArtifactDetail(art);

            var nameLbl = new Label
            {
                Parent = card,
                Text = $"{art.Name}  —  {art.CostOfGold}g",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = art.Type == "ARTF_CLASS_RELIC" ? Color.FromArgb(255, 180, 50)
                          : art.Type == "ARTF_CLASS_MAJOR" ? Color.FromArgb(180, 130, 255)
                          : Color.White,
                Location = new Point(66, 5),
                AutoSize = true,
                Cursor = Cursors.Hand,
            };
            nameLbl.DoubleClick += (s, e) => ShowArtifactDetail(art);

            var typeLbl = new Label
            {
                Parent = card,
                Text = $"{art.TypeDisplay}  |  Слот: {art.SlotDisplay}",
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.LightGray,
                Location = new Point(66, 25),
                AutoSize = true,
            };

            var btnBuy = new Button
            {
                Parent = card,
                Text = "Купить",
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                Size = new Size(100, 28),
                Location = new Point(460, 16),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 110, 50),
                ForeColor = Color.White,
                Tag = art,
            };
            btnBuy.FlatAppearance.BorderSize = 0;
            btnBuy.Click += BtnBuyArt_Click;

            return card;
        }

        private void BtnBuyArt_Click(object? sender, EventArgs e)
        {
            var btn = sender as Button;
            if (btn?.Tag is not ArtifactInfo art) return;

            string slotName = ResolveSlot(art);
            if (string.IsNullOrEmpty(slotName))
            {
                MessageBox.Show("Нет подходящего слота!", "Покупка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!_gold.TrySpend(art.CostOfGold))
            {
                MessageBox.Show("Недостаточно золота!", "Покупка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var old = _equipped[slotName];
            if (old != null)
                _gold.Refund(old.CostOfGold);

            _equipped[slotName] = art;
            RebuildSlots();
        }

        private string ResolveSlot(ArtifactInfo art)
        {
            string display = art.SlotDisplay;
            if (display == "FINGER")
            {
                // Два слота кольца: первый свободный или первый
                if (_equipped["FINGER 1"] == null) return "FINGER 1";
                if (_equipped["FINGER 2"] == null) return "FINGER 2";
                return "FINGER 1";
            }
            if (_equipped.ContainsKey(display))
                return display;
            return "";
        }

        private void RebuildSlots()
        {
            foreach (var slot in SlotNames)
            {
                if (!_slotIcons.TryGetValue(slot, out var pb))
                    continue;

                var equipped = _equipped[slot];
                pb.Image = equipped?.Icon != null ? new Bitmap(equipped.Icon) : null;
                _slotTip.SetToolTip(pb, equipped != null
                    ? $"{slot}: {equipped.Name}\n{equipped.TypeDisplay}  |  {equipped.CostOfGold}g"
                    : slot);
            }
        }

        private void ShowArtifactDetail(ArtifactInfo art)
        {
            var form = new ArtifactDetailForm(art);
            form.ShowDialog();
        }

        private void RefreshGold()
        {
            if (_goldLabel != null)
                _goldLabel.Text = $"Золото: {_gold.Remaining} / {_gold.Total}";
        }
    }

    /// <summary>
    /// Окно деталей артефакта.
    /// </summary>
    internal class ArtifactDetailForm : Form
    {
        public ArtifactDetailForm(ArtifactInfo art)
        {
            Text = art.Name;
            Size = new Size(450, 500);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(30, 30, 40);
            ForeColor = Color.White;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScroll = true;

            // Иконка
            new PictureBox
            {
                Parent = this,
                Location = new Point(15, 15),
                Size = new Size(96, 96),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = art.Icon != null ? new Bitmap(art.Icon) : null,
                BackColor = Color.FromArgb(35, 35, 50),
                BorderStyle = BorderStyle.FixedSingle,
            };

            // Название
            new Label
            {
                Parent = this,
                Text = art.Name,
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 220, 100),
                Location = new Point(125, 15),
                AutoSize = true,
            };

            // Тип
            new Label
            {
                Parent = this,
                Text = art.TypeDisplay,
                Font = new Font("Segoe UI", 10),
                ForeColor = art.Type == "ARTF_CLASS_RELIC" ? Color.FromArgb(255, 180, 50)
                          : art.Type == "ARTF_CLASS_MAJOR" ? Color.FromArgb(180, 130, 255)
                          : Color.LightGray,
                Location = new Point(125, 50),
                AutoSize = true,
            };

            // Цена
            new Label
            {
                Parent = this,
                Text = $"Цена: {art.CostOfGold} золота",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(255, 220, 100),
                Location = new Point(125, 75),
                AutoSize = true,
            };

            // ID
            new Label
            {
                Parent = this,
                Text = $"ID: {art.Id}",
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.Gray,
                Location = new Point(15, 120),
                AutoSize = true,
            };

            // Описание
            if (!string.IsNullOrEmpty(art.Description))
            {
                new Label
                {
                    Parent = this,
                    Text = "Описание:",
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    ForeColor = Color.White,
                    Location = new Point(15, 145),
                    AutoSize = true,
                };

                new Label
                {
                    Parent = this,
                    Text = art.Description,
                    Font = new Font("Segoe UI", 9),
                    ForeColor = Color.White,
                    Location = new Point(25, 170),
                    AutoSize = true,
                    MaximumSize = new Size(390, 0),
                };
            }
        }
    }

    internal class CreatureDetailForm : Form
    {
        public CreatureDetailForm(CreatureInfo creature)
        {
            Text = creature.Name;
            Size = new Size(450, 700);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(30, 30, 40);
            ForeColor = Color.White;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScroll = true;

            // Иконка
            new PictureBox
            {
                Parent = this,
                Location = new Point(15, 15),
                Size = new Size(96, 96),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = creature.Icon != null ? new Bitmap(creature.Icon) : null,
                BackColor = Color.FromArgb(35, 35, 50),
                BorderStyle = BorderStyle.FixedSingle,
            };

            // Имя
            new Label
            {
                Parent = this,
                Text = creature.Name,
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 220, 100),
                Location = new Point(125, 15),
                AutoSize = true,
            };

            // Грейд/Базовый
            new Label
            {
                Parent = this,
                Text = creature.IsBase ? "Базовый юнит" : "Грейд ★",
                Font = new Font("Segoe UI", 9),
                ForeColor = creature.IsBase ? Color.Gray : Color.FromArgb(100, 255, 100),
                Location = new Point(125, 50),
                AutoSize = true,
            };

            // Фракция + Тир
            new Label
            {
                Parent = this,
                Text = $"Фракция: {creature.Faction}  |  Тир: {creature.CreatureTier}",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.LightGray,
                Location = new Point(125, 70),
                AutoSize = true,
            };

            // Характеристики
            int y = 125;

            var headerFont = new Font("Segoe UI", 10, FontStyle.Bold);
            var statFont = new Font("Segoe UI", 10);

            var headerLabel = new Label
            {
                Parent = this,
                Text = "Характеристики:",
                Font = headerFont,
                ForeColor = Color.White,
                Location = new Point(15, y),
                AutoSize = true,
            };
            y += headerLabel.PreferredHeight + 4;

            var statsLines = new List<string>
            {
                $"Атака: {creature.AttackSkill}",
                $"Защита: {creature.DefenceSkill}",
                $"Урон: {creature.MinDamage}-{creature.MaxDamage}",
                $"HP: {creature.Health}",
                $"Скорость: {creature.Speed}",
                $"Инициатива: {creature.Initiative}",
            };
            if (creature.Shots > 0)
                statsLines.Add($"Выстрелы: {creature.Shots}");
            if (creature.Flying)
                statsLines.Add("Летает");
            statsLines.Add($"Золото: {creature.Gold}");
            statsLines.Add($"Рост в неделю: {creature.WeeklyGrowth}");

            var statsLabel = new Label
            {
                Parent = this,
                Text = string.Join("\n", statsLines),
                Font = statFont,
                ForeColor = Color.White,
                Location = new Point(25, y),
                AutoSize = true,
                MaximumSize = new Size(390, 0),
            };
            y += statsLabel.PreferredHeight + 10;

            // Разделитель
            new Panel
            {
                Parent = this,
                Location = new Point(15, y),
                Size = new Size(400, 1),
                BackColor = Color.FromArgb(80, 80, 100),
            };
            y += 10;

            // Способности
            if (creature.Abilities.Count > 0)
            {
                var abilitiesHeader = new Label
                {
                    Parent = this,
                    Text = "Способности:",
                    Font = headerFont,
                    ForeColor = Color.FromArgb(180, 255, 180),
                    Location = new Point(15, y),
                    AutoSize = true,
                };
                y += abilitiesHeader.PreferredHeight + 4;

                new Label
                {
                    Parent = this,
                    Text = string.Join("\n", creature.Abilities.Select(a => "  " + a)),
                    Font = statFont,
                    ForeColor = Color.FromArgb(180, 255, 180),
                    Location = new Point(25, y),
                    AutoSize = true,
                    MaximumSize = new Size(390, 0),
                };
            }
        }
    }
}
