using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Launcher
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Вспомогательный класс: заморозка перерисовки панели через WinAPI
    // ─────────────────────────────────────────────────────────────────────────────
    internal static class RenderHelper
    {
        [DllImport("user32.dll")]
        private static extern bool SendMessage(IntPtr hWnd, int msg, bool wParam, int lParam);
        private const int WM_SETREDRAW = 0x000B;

        public static void Freeze(Control c) =>
            SendMessage(c.Handle, WM_SETREDRAW, false, 0);

        public static void Unfreeze(Control c)
        {
            SendMessage(c.Handle, WM_SETREDRAW, true, 0);
            c.Refresh();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Общее золото между вкладками
    // ─────────────────────────────────────────────────────────────────────────────
    internal class GoldState
    {
        public int Total { get; private set; }
        public int Spent { get; set; }
        public int Remaining => Total - Spent;
        public event Action? Changed;

        public GoldState(int total) { Total = total; }

        public bool TrySpend(int amount)
        {
            if (amount > Remaining) return false;
            Spent += amount;
            Changed?.Invoke();
            return true;
        }

        public void Refund(int amount)
        {
            Spent = Math.Max(0, Spent - amount);
            Changed?.Invoke();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Слот армии
    // ─────────────────────────────────────────────────────────────────────────────
    internal class ArmySlot
    {
        public CreatureInfo? Creature { get; set; }
        public int Count { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Множители прироста по тиру
    // ─────────────────────────────────────────────────────────────────────────────
    internal static class TierMultipliers
    {
        private static readonly Dictionary<int, int> Multipliers = new()
        {
            { 1, 12 }, { 2, 11 }, { 3, 10 }, { 4, 9 },
            { 5, 8  }, { 6, 7  }, { 7, 6  },
        };
        public static int Get(int tier) => Multipliers.TryGetValue(tier, out int m) ? m : 6;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Пул юнитов одного тира
    // ─────────────────────────────────────────────────────────────────────────────
    internal class TierPool
    {
        public int Tier { get; }
        public int MaxPool { get; private set; }
        public int Available { get; set; }
        public List<CreatureInfo> Creatures { get; } = new();

        public TierPool(int tier) { Tier = tier; }

        public void Initialize()
        {
            int baseGrowth = Creatures.Where(c => c.IsBase).Sum(c => c.WeeklyGrowth);
            MaxPool = baseGrowth * TierMultipliers.Get(Tier);
            Available = MaxPool;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Главная форма
    // ─────────────────────────────────────────────────────────────────────────────
    public class GameParserForm : Form
    {
        private readonly string _gameRoot;
        private Panel _selectionPanel = null!;
        private ComboBox _cmbFaction1 = null!;
        private ComboBox _cmbFaction2 = null!;
        private Button _btnStart = null!;
        private Label _selectionStatus = null!;
        private TabControl _tabs = null!;
        private string _faction1 = "";
        private string _faction2 = "";
        private int _totalGold = 120000;

        public GameParserForm(string gameRoot)
        {
            _gameRoot = gameRoot;
            this.DoubleBuffered = true;
            InitUI();
        }

        private void InitUI()
        {
            Text = "Парсер игровых данных";
            Size = new Size(1150, 820);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(30, 30, 40);
            ForeColor = Color.White;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            _selectionPanel = new Panel
            {
                Parent = this,
                Location = Point.Empty,
                Size = ClientSize,
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
            foreach (string f in GameDataParser.SelectableFactions) _cmbFaction1.Items.Add(f);
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
            foreach (string f in GameDataParser.SelectableFactions) _cmbFaction2.Items.Add(f);
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
                MessageBox.Show("Выберите разные фракции!", "Парсер", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

            var tabArmy1 = new TabPage("Армия") { BackColor = Color.FromArgb(30, 30, 40), ForeColor = Color.White };
            var tabArt1 = new TabPage("Артефакты") { BackColor = Color.FromArgb(30, 30, 40), ForeColor = Color.White };
            var tabSpells1 = new TabPage("Заклинания") { BackColor = Color.FromArgb(30, 30, 40), ForeColor = Color.White };
            tabSpells1.Controls.Add(new Label { Text = "В разработке...", ForeColor = Color.Gray, Font = new Font("Segoe UI", 14), AutoSize = true, Location = new Point(20, 20) });

            var tabArmy2 = new TabPage("Армия") { BackColor = Color.FromArgb(30, 30, 40), ForeColor = Color.White };
            var tabArt2 = new TabPage("Артефакты") { BackColor = Color.FromArgb(30, 30, 40), ForeColor = Color.White };
            var tabSpells2 = new TabPage("Заклинания") { BackColor = Color.FromArgb(30, 30, 40), ForeColor = Color.White };
            tabSpells2.Controls.Add(new Label { Text = "В разработке...", ForeColor = Color.Gray, Font = new Font("Segoe UI", 14), AutoSize = true, Location = new Point(20, 20) });

            // Создаём вкладки — UI строится один раз здесь
            new ArmyPurchaseTab(tabArmy1, creatures1, goldState1);
            new ArtifactTab(tabArt1, artifacts, goldState1);
            new ArmyPurchaseTab(tabArmy2, creatures2, goldState2);
            new ArtifactTab(tabArt2, artifacts, goldState2);

            _tabs.TabPages.Add(tabArmy1);
            _tabs.TabPages.Add(tabArt1);
            _tabs.TabPages.Add(tabSpells1);

            btnPlayer1.Click += (s, ev) =>
            {
                btnPlayer1.BackColor = Color.FromArgb(100, 180, 255); btnPlayer1.ForeColor = Color.Black;
                btnPlayer2.BackColor = Color.FromArgb(60, 60, 80); btnPlayer2.ForeColor = Color.White;
                int idx = _tabs.SelectedIndex;
                _tabs.TabPages.Clear();
                _tabs.TabPages.AddRange(new[] { tabArmy1, tabArt1, tabSpells1 });
                if (idx >= 0 && idx < _tabs.TabCount) _tabs.SelectedIndex = idx;
            };

            btnPlayer2.Click += (s, ev) =>
            {
                btnPlayer2.BackColor = Color.FromArgb(255, 130, 130); btnPlayer2.ForeColor = Color.Black;
                btnPlayer1.BackColor = Color.FromArgb(60, 60, 80); btnPlayer1.ForeColor = Color.White;
                int idx = _tabs.SelectedIndex;
                _tabs.TabPages.Clear();
                _tabs.TabPages.AddRange(new[] { tabArmy2, tabArt2, tabSpells2 });
                if (idx >= 0 && idx < _tabs.TabCount) _tabs.SelectedIndex = idx;
            };
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    //  ВКЛАДКА АРМИИ — Build once + UpdateShopState + UpdateSlotDisplay
    // ═════════════════════════════════════════════════════════════════════════════
    internal class ArmyPurchaseTab
    {
        private readonly TabPage _tab;
        private readonly GoldState _gold;
        private readonly List<CreatureInfo> _creatures;
        private readonly Dictionary<int, TierPool> _pools = new();
        private readonly ArmySlot[] _slots = new ArmySlot[7];

        // ── кеши элементов магазина ──────────────────────────────────────────────
        // ключ = CreatureInfo.Id
        private readonly Dictionary<string, Label> _tierAvailLabels = new(); // ключ = "tier_{n}"
        private readonly Dictionary<string, Button> _buyButtons = new();
        private readonly Dictionary<string, NumericUpDown> _buyNuds = new();
        private readonly Dictionary<string, Label> _noAvailLabels = new(); // "Нет в пуле" / "Нет слотов"

        // ── кеши элементов слотов армии ──────────────────────────────────────────
        private readonly PictureBox[] _slotIconBoxes = new PictureBox[7];
        private readonly Label[] _slotNameLabels = new Label[7];
        private readonly Label[] _slotStatLabels = new Label[7];
        private readonly Label[] _slotGradeLabels = new Label[7];
        private readonly Panel[] _slotPanels = new Panel[7];

        // кнопки действий в слоте: Up1, Up2, Down, Regrade, Sell — 5 кнопок на слот
        private readonly Button?[,] _slotActionBtns = new Button?[7, 5]; // [slot, 0..4]

        private Panel _shopPanel = null!;
        private Label _goldLabel = null!;

        public ArmyPurchaseTab(TabPage tab, List<CreatureInfo> creatures, GoldState gold)
        {
            _tab = tab;
            _gold = gold;
            _creatures = creatures;

            for (int i = 0; i < 7; i++) _slots[i] = new ArmySlot();

            BuildPools();
            BuildUI();          // создаём всё один раз
            UpdateSlotDisplayAll();
            UpdateShopState();
            _gold.Changed += OnGoldChanged;
        }

        // ── построение пулов ─────────────────────────────────────────────────────
        private void BuildPools()
        {
            // Первый проход: добавляем всех у кого тир 1-7
            foreach (var c in _creatures)
            {
                int tier = c.CreatureTier;
                if (tier < 1 || tier > 7) continue;
                if (!_pools.ContainsKey(tier)) _pools[tier] = new TierPool(tier);
                _pools[tier].Creatures.Add(c);
            }

            // Второй проход: добавляем грейды у которых тир не задан (0),
            // определяем тир через базового юнита
            foreach (var c in _creatures)
            {
                int tier = c.CreatureTier;
                if (tier >= 1 && tier <= 7) continue; // уже добавлен
                if (c.IsBase) continue;                // базовый без тира — пропускаем

                // Ищем базового, у которого этот юнит listed в Upgrades
                var baseCreature = _creatures.FirstOrDefault(b =>
                    b.IsBase && b.Upgrades.Contains(c.Id));

                if (baseCreature == null) continue;

                int baseTier = baseCreature.CreatureTier;
                if (baseTier < 1 || baseTier > 7) continue;

                if (!_pools.ContainsKey(baseTier)) _pools[baseTier] = new TierPool(baseTier);

                // Не добавляем дубль
                if (!_pools[baseTier].Creatures.Any(x => x.Id == c.Id))
                    _pools[baseTier].Creatures.Add(c);
            }

            foreach (var pool in _pools.Values) pool.Initialize();
        }

        // ── построение UI (один раз) ─────────────────────────────────────────────
        private void BuildUI()
        {
            _goldLabel = new Label
            {
                Parent = _tab,
                Location = new Point(10, 8),
                Size = new Size(300, 25),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 220, 100),
                Text = GoldText(),
            };

            _shopPanel = new DoubleBufferedPanel
            {
                Parent = _tab,
                Location = new Point(10, 40),
                Size = new Size(620, 680),
                AutoScroll = true,
                BackColor = Color.FromArgb(30, 30, 40),
            };
            var shopPanel = _shopPanel;

            var armyPanel = new DoubleBufferedPanel
            {
                Parent = _tab,
                Location = new Point(640, 40),
                Size = new Size(470, 610),
                BackColor = Color.FromArgb(35, 35, 50),
                BorderStyle = BorderStyle.FixedSingle,
            };

            new Label
            {
                Parent = armyPanel,
                Text = "Армия (7 слотов)",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(10, 5),
                AutoSize = true,
            };

            // ── слоты армии ──────────────────────────────────────────────────────
            for (int i = 0; i < 7; i++)
                BuildSlotPanelOnce(armyPanel, i);

            // ── магазин ──────────────────────────────────────────────────────────
            BuildShopOnce(shopPanel);
        }

        private void BuildSlotPanelOnce(Panel armyPanel, int index)
        {
            int y = 30 + index * 80;
            var panel = new Panel
            {
                Parent = armyPanel,
                Location = new Point(5, y),
                Size = new Size(455, 74),
                BackColor = Color.FromArgb(45, 45, 65),
                BorderStyle = BorderStyle.FixedSingle,
            };
            _slotPanels[index] = panel;

            new Label
            {
                Parent = panel,
                Text = $"{index + 1}.",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.Gray,
                Location = new Point(5, 26),
                AutoSize = true,
            };

            var icon = new PictureBox
            {
                Parent = panel,
                Location = new Point(25, 3),
                Size = new Size(64, 64),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(35, 35, 50),
                Cursor = Cursors.Hand,
                Visible = false,
            };
            icon.DoubleClick += (s, ev) => { if (_slots[index].Creature != null) ShowCreatureDetail(_slots[index].Creature!); };
            _slotIconBoxes[index] = icon;

            var nameLbl = new Label
            {
                Parent = panel,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(95, 5),
                AutoSize = true,
                Cursor = Cursors.Hand,
                Visible = false,
            };
            nameLbl.DoubleClick += (s, ev) => { if (_slots[index].Creature != null) ShowCreatureDetail(_slots[index].Creature!); };
            _slotNameLabels[index] = nameLbl;

            var statsLbl = new Label
            {
                Parent = panel,
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.LightGray,
                Location = new Point(95, 25),
                AutoSize = true,
                Visible = false,
            };
            _slotStatLabels[index] = statsLbl;

            var gradeLbl = new Label
            {
                Parent = panel,
                Font = new Font("Segoe UI", 8),
                Location = new Point(95, 43),
                AutoSize = true,
                Visible = false,
            };
            _slotGradeLabels[index] = gradeLbl;

            // 5 кнопок действий: [0]=Up1, [1]=Up2, [2]=Down, [3]=Regrade, [4]=Sell
            for (int b = 0; b < 5; b++)
            {
                var btn = new Button
                {
                    Parent = panel,
                    Font = new Font("Segoe UI", 7),
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = Color.White,
                    Visible = false,
                };
                btn.FlatAppearance.BorderSize = 0;
                _slotActionBtns[index, b] = btn;
            }

            // Кнопка продажи — фиксированная позиция
            _slotActionBtns[index, 4]!.Size = new Size(90, 22);
            _slotActionBtns[index, 4]!.Location = new Point(360, 5);
            _slotActionBtns[index, 4]!.BackColor = Color.FromArgb(120, 50, 50);
            int capturedIndex = index;
            _slotActionBtns[index, 4]!.Click += (s, ev) => BtnSell_Click(capturedIndex);
        }

        private void BuildShopOnce(Panel shopPanel)
        {
            int y = 0;
            foreach (int tier in _pools.Keys.OrderBy(t => t))
            {
                var pool = _pools[tier];
                var tierLbl = new Label
                {
                    Parent = shopPanel,
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    ForeColor = Color.FromArgb(200, 200, 100),
                    Location = new Point(5, y + 2),
                    AutoSize = true,
                };
                _tierAvailLabels[$"tier_{tier}"] = tierLbl;
                y += 22;

                var sorted = pool.Creatures.OrderBy(c => c.IsBase ? 0 : 1).ThenBy(c => c.Gold).ToList();
                foreach (var creature in sorted)
                {
                    BuildCreatureCardOnce(creature, pool, shopPanel, y);
                    y += 48; // высота карточки 44 + отступ 4
                }
                y += 8;
            }
            // Явно задаём высоту прокрутки — иначе Panel не знает о реальном контенте
            shopPanel.AutoScrollMinSize = new Size(0, y + 60);
        }

        private void BuildCreatureCardOnce(CreatureInfo creature, TierPool pool, Panel shopPanel, int yPos)
        {
            // Пропускаем дубли по ID (не должно быть, но на всякий случай)
            if (_buyButtons.ContainsKey(creature.Id)) return;

            var card = new Panel
            {
                Parent = shopPanel,
                Location = new Point(0, yPos),
                Size = new Size(595, 44),
                BackColor = Color.FromArgb(42, 42, 60),
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand,
            };
            card.DoubleClick += (s, e) => ShowCreatureDetail(creature);

            // Иконка — создаём один раз, не пересоздаём Bitmap при каждом обновлении
            var icon = new PictureBox
            {
                Parent = card,
                Location = new Point(2, 2),
                Size = new Size(38, 38),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(35, 35, 50),
                Cursor = Cursors.Hand,
            };
            // Один Bitmap на всё время жизни карточки
            if (creature.Icon != null) icon.Image = new Bitmap(creature.Icon);
            card.Disposed += (s, e) => { icon.Image?.Dispose(); icon.Image = null; };
            icon.DoubleClick += (s, e) => ShowCreatureDetail(creature);

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

            // NumericUpDown
            var nud = new NumericUpDown
            {
                Parent = card,
                Location = new Point(420, 8),
                Size = new Size(60, 25),
                Minimum = 1,
                Maximum = 1,
                Value = 1,
                Font = new Font("Segoe UI", 9),
                BackColor = Color.FromArgb(50, 50, 65),
                ForeColor = Color.White,
            };
            _buyNuds[creature.Id] = nud;

            // Кнопка "Купить"
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
                Tag = creature,
            };
            btnBuy.FlatAppearance.BorderSize = 0;
            btnBuy.Click += (s, e) => BtnBuy_Click(creature, nud);
            _buyButtons[creature.Id] = btnBuy;

            // Метка "Нет в пуле / Нет слотов"
            var noLbl = new Label
            {
                Parent = card,
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.DarkGray,
                Location = new Point(490, 12),
                AutoSize = true,
                Visible = false,
            };
            _noAvailLabels[creature.Id] = noLbl;
        }

        // ── обновление состояния магазина (без пересоздания контролов) ────────────
        private void UpdateShopState()
        {
            RenderHelper.Freeze(_shopPanel);
            try
            {
                // Обновить заголовки тиров
                foreach (int tier in _pools.Keys)
                {
                    var pool = _pools[tier];
                    if (_tierAvailLabels.TryGetValue($"tier_{tier}", out var lbl))
                        lbl.Text = $"Тир {tier}  |  Доступно: {pool.Available}/{pool.MaxPool}";
                }

                // Обновить карточки
                foreach (var creature in _creatures)
                {
                    if (!_buyButtons.TryGetValue(creature.Id, out var btn)) continue;
                    if (!_buyNuds.TryGetValue(creature.Id, out var nud)) continue;
                    if (!_noAvailLabels.TryGetValue(creature.Id, out var noLbl)) continue;

                    int tier = creature.CreatureTier;
                    bool hasPool = _pools.TryGetValue(tier, out var pool) && pool.Available > 0;
                    bool hasSlot = HasSlotFor(creature);
                    bool canBuy = hasPool && hasSlot;

                    if (canBuy)
                    {
                        int maxByPool = pool!.Available;
                        int maxByGold = creature.Gold > 0 ? _gold.Remaining / creature.Gold : maxByPool;
                        int realMax = Math.Max(1, Math.Min(maxByPool, maxByGold));

                        nud.Maximum = realMax;
                        nud.Value = realMax; // всегда выставляем макс по умолчанию

                        btn.Enabled = _gold.Remaining >= creature.Gold;
                        btn.Visible = true;
                        nud.Visible = true;
                        noLbl.Visible = false;
                    }
                    else
                    {
                        btn.Visible = false;
                        nud.Visible = false;
                        noLbl.Text = !hasSlot ? "Нет слотов" : "Нет в пуле";
                        noLbl.Visible = true;
                    }
                }
            }
            finally
            {
                RenderHelper.Unfreeze(_shopPanel);
            }
        }

        // ── обновление одного слота армии (без пересоздания контролов) ────────────
        private void UpdateSlotDisplay(int index)
        {
            var slot = _slots[index];
            var panel = _slotPanels[index];
            var icon = _slotIconBoxes[index];
            var nameLbl = _slotNameLabels[index];
            var statLbl = _slotStatLabels[index];
            var gradeLbl = _slotGradeLabels[index];

            // Кнопки действий
            var btnUp1 = _slotActionBtns[index, 0]!;
            var btnUp2 = _slotActionBtns[index, 1]!;
            var btnDown = _slotActionBtns[index, 2]!;
            var btnRegrade = _slotActionBtns[index, 3]!;
            var btnSell = _slotActionBtns[index, 4]!;

            if (slot.Creature == null)
            {
                panel.BackColor = Color.FromArgb(45, 45, 65);
                icon.Visible = nameLbl.Visible = statLbl.Visible = gradeLbl.Visible = false;
                btnUp1.Visible = btnUp2.Visible = btnDown.Visible = btnRegrade.Visible = btnSell.Visible = false;

                // Показать метку "Пустой слот" (можно хранить отдельно, но проще через Tag)
                if (panel.Tag is not Label emptyLbl)
                {
                    emptyLbl = new Label
                    {
                        Text = "Пустой слот",
                        Font = new Font("Segoe UI", 9),
                        ForeColor = Color.DarkGray,
                        Location = new Point(30, 26),
                        AutoSize = true,
                    };
                    panel.Controls.Add(emptyLbl);
                    panel.Tag = emptyLbl;
                }
                ((Label)panel.Tag).Visible = true;
                return;
            }

            // Скрываем метку "Пустой слот"
            if (panel.Tag is Label el) el.Visible = false;

            var creature = slot.Creature;
            panel.BackColor = Color.FromArgb(50, 50, 70);

            // Иконка — меняем Image, не пересоздаём PictureBox
            var oldImg = icon.Image;
            icon.Image = creature.Icon != null ? new Bitmap(creature.Icon) : null;
            oldImg?.Dispose();
            icon.Visible = true;

            nameLbl.Text = $"{creature.Name} x{slot.Count}";
            nameLbl.Visible = true;

            statLbl.Text = $"Т{creature.CreatureTier} | А:{creature.AttackSkill} З:{creature.DefenceSkill} У:{creature.MinDamage}-{creature.MaxDamage} HP:{creature.Health}";
            statLbl.Visible = true;

            gradeLbl.Text = creature.IsBase ? "Базовый" : "Грейд";
            gradeLbl.ForeColor = creature.IsBase ? Color.Gray : Color.FromArgb(100, 255, 100);
            gradeLbl.Visible = true;

            // Кнопка продажи
            btnSell.Text = $"Продать (+{creature.Gold * slot.Count}g)";
            btnSell.Visible = true;

            // Скрываем все кнопки грейдов перед переназначением
            btnUp1.Visible = btnUp2.Visible = btnDown.Visible = btnRegrade.Visible = false;

            // Удаляем старые обработчики через пересоздание кнопки — используем Tag для хранения Action
            SetButtonAction(btnUp1, null);
            SetButtonAction(btnUp2, null);
            SetButtonAction(btnDown, null);
            SetButtonAction(btnRegrade, null);

            if (creature.IsBase && creature.Upgrades.Count > 0)
            {
                int btnX = 200;
                int upIdx = 0;
                foreach (var upId in creature.Upgrades)
                {
                    var upgraded = _creatures.FirstOrDefault(c => c.Id == upId);
                    if (upgraded == null) continue;
                    int costDiff = (upgraded.Gold - creature.Gold) * slot.Count;

                    if (upIdx == 0)
                    {
                        btnUp1.Text = $"▲ {upgraded.Name} ({costDiff}g)";
                        btnUp1.Size = new Size(120, 22);
                        btnUp1.Location = new Point(btnX, 43);
                        btnUp1.BackColor = Color.FromArgb(50, 100, 50);
                        SetButtonAction(btnUp1, () => DoUpgrade(index, upgraded, costDiff));
                        btnUp1.Visible = true;
                    }
                    else if (upIdx == 1)
                    {
                        btnUp2.Text = $"▲ {upgraded.Name} ({costDiff}g)";
                        btnUp2.Size = new Size(120, 22);
                        btnUp2.Location = new Point(btnX + 125, 43);
                        btnUp2.BackColor = Color.FromArgb(50, 100, 50);
                        SetButtonAction(btnUp2, () => DoUpgrade(index, upgraded, costDiff));
                        btnUp2.Visible = true;
                    }
                    upIdx++;
                }
            }
            else if (!creature.IsBase)
            {
                var baseCreature = _creatures.FirstOrDefault(c =>
                    c.IsBase && c.Upgrades.Contains(creature.Id) && c.CreatureTier == creature.CreatureTier);

                if (baseCreature != null)
                {
                    btnDown.Text = $"▼ {baseCreature.Name} (0g)";
                    btnDown.Size = new Size(120, 22);
                    btnDown.Location = new Point(200, 43);
                    btnDown.BackColor = Color.FromArgb(80, 80, 50);
                    SetButtonAction(btnDown, () => DoDowngrade(index, baseCreature));
                    btnDown.Visible = true;

                    if (baseCreature.Upgrades.Count > 1)
                    {
                        var otherId = baseCreature.Upgrades.FirstOrDefault(u => u != creature.Id);
                        var other = _creatures.FirstOrDefault(c => c.Id == otherId);
                        if (other != null)
                        {
                            int costDiff = (other.Gold - creature.Gold) * slot.Count;
                            int displayCost = Math.Max(0, costDiff);
                            btnRegrade.Text = $"⇄ {other.Name} ({displayCost}g)";
                            btnRegrade.Size = new Size(120, 22);
                            btnRegrade.Location = new Point(325, 43);
                            btnRegrade.BackColor = Color.FromArgb(60, 60, 100);
                            SetButtonAction(btnRegrade, () => DoRegrade(index, other, costDiff));
                            btnRegrade.Visible = true;
                        }
                    }
                }
            }
        }

        private void UpdateSlotDisplayAll()
        {
            for (int i = 0; i < 7; i++) UpdateSlotDisplay(i);
        }

        // ── управление обработчиками кнопок без утечки через Tag ─────────────────
        private static void SetButtonAction(Button btn, Action? action)
        {
            // Удаляем предыдущий обработчик, сохранённый в Tag
            if (btn.Tag is EventHandler old)
                btn.Click -= old;

            if (action == null) { btn.Tag = null; return; }

            EventHandler handler = (s, e) => action();
            btn.Click += handler;
            btn.Tag = handler;
        }

        // ── вспомогательные методы ────────────────────────────────────────────────
        private bool HasSlotFor(CreatureInfo creature)
        {
            for (int i = 0; i < 7; i++)
                if (_slots[i].Creature == null || _slots[i].Creature!.Id == creature.Id)
                    return true;
            return false;
        }

        private string GoldText() => $"Золото: {_gold.Remaining} / {_gold.Total}";

        private void OnGoldChanged()
        {
            _goldLabel.Text = GoldText();
            UpdateShopState();
        }

        // ── действия ─────────────────────────────────────────────────────────────
        private void DoUpgrade(int slotIdx, CreatureInfo upgraded, int costDiff)
        {
            if (!_gold.TrySpend(costDiff))
            {
                MessageBox.Show("Недостаточно золота!", "Грейд", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _slots[slotIdx].Creature = upgraded;
            UpdateSlotDisplay(slotIdx);
            UpdateShopState();
        }

        private void DoDowngrade(int slotIdx, CreatureInfo baseCreature)
        {
            var current = _slots[slotIdx].Creature;
            if (current != null)
            {
                int refund = (current.Gold - baseCreature.Gold) * _slots[slotIdx].Count;
                if (refund > 0) _gold.Refund(refund);
            }
            _slots[slotIdx].Creature = baseCreature;
            UpdateSlotDisplay(slotIdx);
            UpdateShopState();
        }

        private void DoRegrade(int slotIdx, CreatureInfo other, int costDiff)
        {
            if (costDiff > 0 && !_gold.TrySpend(costDiff))
            {
                MessageBox.Show("Недостаточно золота!", "Регрейд", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (costDiff < 0) _gold.Refund(-costDiff);
            _slots[slotIdx].Creature = other;
            UpdateSlotDisplay(slotIdx);
            UpdateShopState();
        }

        private void BtnBuy_Click(CreatureInfo creature, NumericUpDown nud)
        {
            int count = (int)nud.Value;
            int totalCost = creature.Gold * count;

            if (!_gold.TrySpend(totalCost))
            {
                MessageBox.Show("Недостаточно золота!", "Покупка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int tier = creature.CreatureTier;
            if (_pools.TryGetValue(tier, out var pool)) pool.Available -= count;

            // Найти слот
            int targetSlot = -1;
            for (int i = 0; i < 7; i++)
                if (_slots[i].Creature?.Id == creature.Id) { targetSlot = i; break; }
            if (targetSlot == -1)
                for (int i = 0; i < 7; i++)
                    if (_slots[i].Creature == null) { targetSlot = i; break; }

            if (targetSlot == -1)
            {
                _gold.Refund(totalCost);
                if (_pools.TryGetValue(tier, out var p)) p.Available += count;
                MessageBox.Show("Нет свободных слотов!", "Покупка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _slots[targetSlot].Creature = creature;
            _slots[targetSlot].Count += count;

            UpdateSlotDisplay(targetSlot);
            UpdateShopState(); // золото уже обновилось через Changed, но пул изменился — нужен явный вызов
        }

        private void BtnSell_Click(int slotIdx)
        {
            var slot = _slots[slotIdx];
            if (slot.Creature == null) return;

            int refund = slot.Creature.Gold * slot.Count;
            int tier = slot.Creature.CreatureTier;

            if (_pools.TryGetValue(tier, out var pool)) pool.Available += slot.Count;

            _gold.Refund(refund);
            slot.Creature = null;
            slot.Count = 0;

            UpdateSlotDisplay(slotIdx);
            UpdateShopState();
        }

        private void ShowCreatureDetail(CreatureInfo creature)
        {
            using var form = new CreatureDetailForm(creature);
            form.ShowDialog();
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    //  ВКЛАДКА АРТЕФАКТОВ — Build once + UpdateShopState
    // ═════════════════════════════════════════════════════════════════════════════
    internal class ArtifactTab
    {
        private readonly TabPage _tab;
        private readonly GoldState _gold;
        private readonly List<ArtifactInfo> _allArtifacts;
        private readonly Random _rng = new();
        private const int RerollCost = 5000;

        private static readonly Dictionary<string, string> SlotRuNames = new()
        {
            { "PRIMARY",   "Меч"      }, { "SECONDARY", "Щит"      },
            { "HEAD",      "Корона"   }, { "CHEST",     "Кираса"   },
            { "NECK",      "Ожерелье" }, { "SHOULDERS", "Плащ"     },
            { "FINGER 1",  "Кольцо 1" }, { "FINGER 2",  "Кольцо 2" },
            { "FEET",      "Сапоги"   }, { "MISCSLOT1", "Карман"   },
        };

        private static readonly string[] SlotNames =
        {
            "PRIMARY","SECONDARY","HEAD","CHEST",
            "NECK","SHOULDERS","FINGER 1","FINGER 2","FEET","MISCSLOT1",
        };

        private const int HeroPanelW = 490;
        private const int HeroPanelH = 362;
        private const int SlotIconSize = 72;

        private static readonly Dictionary<string, Point> SlotPositions = new()
        {
            { "HEAD",      new Point(203, 61)  }, { "NECK",      new Point(306, 61)  },
            { "SHOULDERS", new Point(306, 293) }, { "MISCSLOT1", new Point(406, 166) },
            { "PRIMARY",   new Point(81,  293) }, { "SECONDARY", new Point(306, 166) },
            { "FEET",      new Point(203, 293) }, { "FINGER 1",  new Point(81,  61)  },
            { "FINGER 2",  new Point(81,  166) }, { "CHEST",     new Point(203, 166) },
        };

        private List<ArtifactInfo> _shopItems = new();
        private readonly Dictionary<string, ArtifactInfo?> _equipped = new();
        private readonly Dictionary<string, PictureBox> _slotIcons = new();
        private readonly ToolTip _slotTip = new();

        // ── кеши элементов магазина ──────────────────────────────────────────────
        // Карточки артефактов — пересоздаём только при рероле (6 штук)
        // Остальное обновляем in-place
        private Panel _shopItemsContainer = null!;  // контейнер только для 6 карточек
        private Label _goldLabel = null!;

        public ArtifactTab(TabPage tab, List<ArtifactInfo> allArtifacts, GoldState gold)
        {
            _tab = tab;
            _gold = gold;
            _allArtifacts = allArtifacts;
            foreach (var slot in SlotNames) _equipped[slot] = null;

            BuildUI();
            RollAndRebuildCards();
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
                Text = GoldText(),
            };

            var shopPanel = new DoubleBufferedPanel
            {
                Parent = _tab,
                Location = new Point(10, 40),
                Size = new Size(600, 680),
                AutoScroll = true,
                BackColor = Color.FromArgb(30, 30, 40),
            };

            new Label
            {
                Parent = shopPanel,
                Text = "Магазин артефактов",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(5, 5),
                AutoSize = true,
            };

            // Контейнер для 6 карточек — только его содержимое меняется при рероле
            _shopItemsContainer = new DoubleBufferedPanel
            {
                Parent = shopPanel,
                Location = new Point(0, 30),
                Size = new Size(590, 6 * 68),
                BackColor = Color.FromArgb(30, 30, 40),
            };

            var btnReroll = new Button
            {
                Parent = shopPanel,
                Text = $"Рерол ({RerollCost}g)",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Size = new Size(180, 35),
                Location = new Point(5, 30 + 6 * 68 + 10),
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
                RollAndRebuildCards();
            };

            // ── панель экипировки ─────────────────────────────────────────────────
            var slotsPanel = new DoubleBufferedPanel
            {
                Parent = _tab,
                Location = new Point(620, 40),
                Size = new Size(HeroPanelW, 610),
                BackColor = Color.FromArgb(35, 35, 50),
            };

            var heroPanel = new DoubleBufferedPanel
            {
                Parent = slotsPanel,
                Location = Point.Empty,
                Size = new Size(HeroPanelW, HeroPanelH),
            };

            string bgPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hero_slots.png");
            if (System.IO.File.Exists(bgPath))
            {
                heroPanel.BackgroundImage = Image.FromFile(bgPath);
                heroPanel.BackgroundImageLayout = ImageLayout.Stretch;
            }

            int half = SlotIconSize / 2;
            foreach (var slot in SlotNames)
            {
                if (!SlotPositions.TryGetValue(slot, out var center)) continue;

                var pb = new PictureBox
                {
                    Parent = heroPanel,
                    Location = new Point(center.X - half, center.Y - half),
                    Size = new Size(SlotIconSize, SlotIconSize),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand,
                    Tag = slot,
                };

                var ctx = new ContextMenuStrip();
                string capturedSlot = slot;
                ctx.Items.Add("Снять", null, (s, ev) => RemoveArtifact(capturedSlot));
                ctx.Items.Add("Подробнее", null, (s, ev) =>
                {
                    if (_equipped[capturedSlot] is ArtifactInfo art) ShowArtifactDetail(art);
                });
                pb.ContextMenuStrip = ctx;
                pb.DoubleClick += (s, ev) =>
                {
                    if (_equipped[capturedSlot] is ArtifactInfo art) ShowArtifactDetail(art);
                };

                _slotIcons[slot] = pb;
            }
        }

        // Рерол: пересоздаём только 6 карточек внутри _shopItemsContainer
        private void RollAndRebuildCards()
        {
            var minors = _allArtifacts.Where(a => a.Type == "ARTF_CLASS_MINOR").ToList();
            var majors = _allArtifacts.Where(a => a.Type == "ARTF_CLASS_MAJOR").ToList();
            var relics = _allArtifacts.Where(a => a.Type == "ARTF_CLASS_RELIC").ToList();

            _shopItems.Clear();
            _shopItems.AddRange(PickRandom(minors, 3));
            _shopItems.AddRange(PickRandom(majors, 2));
            _shopItems.AddRange(PickRandom(relics, 1));

            RenderHelper.Freeze(_shopItemsContainer);
            try
            {
                // Диспозим старые Bitmap иконок
                foreach (Control c in _shopItemsContainer.Controls)
                {
                    if (c is Panel p)
                        foreach (Control pc in p.Controls)
                            if (pc is PictureBox pb) { pb.Image?.Dispose(); pb.Image = null; }
                    c.Dispose();
                }
                _shopItemsContainer.Controls.Clear();

                var ordered = _shopItems
                    .OrderBy(a => a.Type == "ARTF_CLASS_MINOR" ? 0 : a.Type == "ARTF_CLASS_MAJOR" ? 1 : 2)
                    .ToList();

                int y = 0;
                foreach (var art in ordered)
                {
                    var card = BuildArtifactCard(art, y);
                    card.Parent = _shopItemsContainer;
                    y += card.Height + 4;
                }
            }
            finally { RenderHelper.Unfreeze(_shopItemsContainer); }
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
                BackColor = Color.FromArgb(35, 35, 50),
                Cursor = Cursors.Hand,
            };
            if (art.Icon != null) icon.Image = new Bitmap(art.Icon);
            card.Disposed += (s, e) => { icon.Image?.Dispose(); icon.Image = null; };
            icon.DoubleClick += (s, e) => ShowArtifactDetail(art);

            new Label
            {
                Parent = card,
                Text = $"{art.Name}  —  {art.CostOfGold}g",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = art.Type == "ARTF_CLASS_RELIC" ? Color.FromArgb(198, 100, 99)
                          : art.Type == "ARTF_CLASS_MAJOR" ? Color.FromArgb(200, 152, 106)
                          : Color.White,
                Location = new Point(66, 5),
                AutoSize = true,
                Cursor = Cursors.Hand,
            };

            new Label
            {
                Parent = card,
                Text = $"Слот: {art.SlotDisplayRu}",
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
            };
            btnBuy.FlatAppearance.BorderSize = 0;
            btnBuy.Click += (s, e) => BuyArtifact(art);

            return card;
        }

        private void BuyArtifact(ArtifactInfo art)
        {
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
            if (old != null) _gold.Refund(old.CostOfGold);

            _equipped[slotName] = art;
            UpdateSlotIcons();
        }

        private void RemoveArtifact(string slot)
        {
            var old = _equipped[slot];
            if (old == null) return;
            _equipped[slot] = null;
            _gold.Refund(old.CostOfGold);
            UpdateSlotIcons();
        }

        private void UpdateSlotIcons()
        {
            foreach (var slot in SlotNames)
            {
                if (!_slotIcons.TryGetValue(slot, out var pb)) continue;

                var equipped = _equipped[slot];

                // Диспозим старый Bitmap
                var old = pb.Image;
                pb.Image = equipped?.Icon != null ? new Bitmap(equipped.Icon) : null;
                old?.Dispose();

                string ruName = SlotRuNames.TryGetValue(slot, out var rn) ? rn : slot;
                _slotTip.SetToolTip(pb, equipped != null
                    ? $"{ruName}: {equipped.Name}\n{equipped.CostOfGold}g"
                    : ruName);
            }
        }

        private string ResolveSlot(ArtifactInfo art)
        {
            string display = art.SlotDisplay;
            if (display == "FINGER")
            {
                if (_equipped["FINGER 1"] == null) return "FINGER 1";
                if (_equipped["FINGER 2"] == null) return "FINGER 2";
                return "FINGER 1";
            }
            return _equipped.ContainsKey(display) ? display : "";
        }

        private List<ArtifactInfo> PickRandom(List<ArtifactInfo> source, int count)
            => source.OrderBy(_ => _rng.Next()).Take(Math.Min(count, source.Count)).ToList();

        private void RefreshGold() => _goldLabel.Text = GoldText();
        private string GoldText() => $"Золото: {_gold.Remaining} / {_gold.Total}";

        private void ShowArtifactDetail(ArtifactInfo art)
        {
            using var form = new ArtifactDetailForm(art);
            form.ShowDialog();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Окно деталей артефакта
    // ─────────────────────────────────────────────────────────────────────────────
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

            new Label { Parent = this, Text = art.Name, Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Color.FromArgb(255, 220, 100), Location = new Point(125, 15), AutoSize = true };
            new Label
            {
                Parent = this,
                Text = art.TypeDisplay,
                Font = new Font("Segoe UI", 10),
                ForeColor = art.Type == "ARTF_CLASS_RELIC" ? Color.FromArgb(198, 100, 99) : art.Type == "ARTF_CLASS_MAJOR" ? Color.FromArgb(200, 152, 106) : Color.White,
                Location = new Point(125, 50),
                AutoSize = true
            };
            new Label { Parent = this, Text = $"Цена: {art.CostOfGold} золота", Font = new Font("Segoe UI", 10), ForeColor = Color.FromArgb(255, 220, 100), Location = new Point(125, 75), AutoSize = true };
            new Label { Parent = this, Text = $"ID: {art.Id}", Font = new Font("Segoe UI", 8), ForeColor = Color.Gray, Location = new Point(15, 120), AutoSize = true };

            if (!string.IsNullOrEmpty(art.Description))
            {
                new Label { Parent = this, Text = "Описание:", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.White, Location = new Point(15, 145), AutoSize = true };
                new Label { Parent = this, Text = art.Description, Font = new Font("Segoe UI", 9), ForeColor = Color.White, Location = new Point(25, 170), AutoSize = true, MaximumSize = new Size(390, 0) };
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Окно деталей юнита
    // ─────────────────────────────────────────────────────────────────────────────
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

            new Label { Parent = this, Text = creature.Name, Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Color.FromArgb(255, 220, 100), Location = new Point(125, 15), AutoSize = true };
            new Label
            {
                Parent = this,
                Text = creature.IsBase ? "Базовый юнит" : "Грейд ★",
                Font = new Font("Segoe UI", 9),
                ForeColor = creature.IsBase ? Color.Gray : Color.FromArgb(100, 255, 100),
                Location = new Point(125, 50),
                AutoSize = true
            };
            new Label
            {
                Parent = this,
                Text = $"Фракция: {creature.Faction}  |  Тир: {creature.CreatureTier}",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.LightGray,
                Location = new Point(125, 70),
                AutoSize = true
            };

            int y = 125;
            var hFont = new Font("Segoe UI", 10, FontStyle.Bold);
            var sFont = new Font("Segoe UI", 10);

            var hLbl = new Label { Parent = this, Text = "Характеристики:", Font = hFont, ForeColor = Color.White, Location = new Point(15, y), AutoSize = true };
            y += hLbl.PreferredHeight + 4;

            var lines = new List<string>
            {
                $"Атака: {creature.AttackSkill}",
                $"Защита: {creature.DefenceSkill}",
                $"Урон: {creature.MinDamage}-{creature.MaxDamage}",
                $"HP: {creature.Health}",
                $"Скорость: {creature.Speed}",
                $"Инициатива: {creature.Initiative}",
            };
            if (creature.Shots > 0) lines.Add($"Выстрелы: {creature.Shots}");
            if (creature.Flying) lines.Add("Летает");
            lines.Add($"Золото: {creature.Gold}");
            lines.Add($"Рост в неделю: {creature.WeeklyGrowth}");

            var sLbl = new Label { Parent = this, Text = string.Join("\n", lines), Font = sFont, ForeColor = Color.White, Location = new Point(25, y), AutoSize = true, MaximumSize = new Size(390, 0) };
            y += sLbl.PreferredHeight + 10;

            new Panel { Parent = this, Location = new Point(15, y), Size = new Size(400, 1), BackColor = Color.FromArgb(80, 80, 100) };
            y += 10;

            if (creature.Abilities.Count > 0)
            {
                var aHdr = new Label { Parent = this, Text = "Способности:", Font = hFont, ForeColor = Color.FromArgb(180, 255, 180), Location = new Point(15, y), AutoSize = true };
                y += aHdr.PreferredHeight + 4;
                new Label
                {
                    Parent = this,
                    Text = string.Join("\n", creature.Abilities.Select(a => "  " + a)),
                    Font = sFont,
                    ForeColor = Color.FromArgb(180, 255, 180),
                    Location = new Point(25, y),
                    AutoSize = true,
                    MaximumSize = new Size(390, 0)
                };
            }
        }
    }
}