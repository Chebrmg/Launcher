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
    public class ArmySlot
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

        private UserAccount? _player1Account;
        private UserAccount? _player2Account;
        private Button _btnLogin1 = null!;
        private Button _btnLogin2 = null!;

        public GameParserForm(string gameRoot)
        {
            _gameRoot = gameRoot;
            this.DoubleBuffered = true;
            DatabaseManager.Init(System.IO.Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location) ?? gameRoot);
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

            _btnLogin1 = new Button
            {
                Parent = _selectionPanel,
                Text = "Войти (Игрок 1)",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Size = new Size(180, 32),
                Location = new Point(720, 218),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 120, 200),
                ForeColor = Color.White,
            };
            _btnLogin1.FlatAppearance.BorderSize = 0;
            _btnLogin1.Click += (s, ev) =>
            {
                using var form = new LoginForm();
                if (form.ShowDialog(this) == DialogResult.OK && form.LoggedInUser != null)
                {
                    _player1Account = form.LoggedInUser;
                    _btnLogin1.Text = _player1Account.Username + (_player1Account.IsAdmin ? " [A]" : "");
                    _btnLogin1.BackColor = Color.FromArgb(50, 160, 50);
                }
            };

            _btnLogin2 = new Button
            {
                Parent = _selectionPanel,
                Text = "Войти (Игрок 2)",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Size = new Size(180, 32),
                Location = new Point(720, 278),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 120, 200),
                ForeColor = Color.White,
            };
            _btnLogin2.FlatAppearance.BorderSize = 0;
            _btnLogin2.Click += (s, ev) =>
            {
                using var form = new LoginForm();
                if (form.ShowDialog(this) == DialogResult.OK && form.LoggedInUser != null)
                {
                    _player2Account = form.LoggedInUser;
                    _btnLogin2.Text = _player2Account.Username + (_player2Account.IsAdmin ? " [A]" : "");
                    _btnLogin2.BackColor = Color.FromArgb(50, 160, 50);
                }
            };

            var btnProfile = new Button
            {
                Parent = _selectionPanel,
                Text = "Профиль / Дуэли",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Size = new Size(180, 32),
                Location = new Point(720, 340),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(100, 80, 160),
                ForeColor = Color.White,
            };
            btnProfile.FlatAppearance.BorderSize = 0;
            btnProfile.Click += (s, ev) =>
            {
                var account = _player1Account ?? _player2Account;
                if (account == null)
                {
                    MessageBox.Show("Сначала войдите в аккаунт!", "Профиль",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                using var form = new PlayerProfileForm(account);
                form.ShowDialog(this);
            };

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

        private List<SkillInfo> _allSkills = new();
        private List<HeroClassInfo> _heroClasses = new();
        private List<HeroInfo> _allHeroes = new();
        private List<ArtifactInfo> _allArtifacts = new();
        private List<SpellInfo> _allSpells = new();
        private List<CreatureInfo> _creatures1 = new();
        private List<CreatureInfo> _creatures2 = new();

        private void BtnStart_Click(object? sender, EventArgs e)
        {
            if (_player1Account == null || _player2Account == null)
            {
                MessageBox.Show("Оба игрока должны войти в аккаунт!", "Парсер",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_player1Account.Id == _player2Account.Id)
            {
                MessageBox.Show("Игроки должны войти в разные аккаунты!", "Парсер",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

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
                var skills = parser.ParseSkills();
                var heroClasses = parser.ParseHeroClasses();
                var heroes = parser.ParseHeroes();
                var spells = parser.ParseSpells();
                parser.MapSpellGameIds(spells);
                return (c1, c2, artifacts, skills, heroClasses, heroes, spells, parser.DiagInfo);
            }).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    _selectionStatus.Text = "Ошибка: " + task.Exception?.InnerException?.Message;
                    _selectionStatus.ForeColor = Color.Red;
                    _btnStart.Enabled = true;
                    return;
                }

                var (c1, c2, artifacts, skills, heroClasses, heroes, spells, diag) = task.Result;
                if (c1.Count == 0 && c2.Count == 0)
                {
                    _selectionStatus.Text = "Юниты не найдены.\n\nДиагностика:\n" + diag;
                    _selectionStatus.ForeColor = Color.OrangeRed;
                    _btnStart.Enabled = true;
                    return;
                }

                _creatures1 = c1;
                _creatures2 = c2;
                _allArtifacts = artifacts;
                _allSkills = skills;
                _heroClasses = heroClasses;
                _allHeroes = heroes;
                _allSpells = spells;

                ShowHeroSelection();
            }, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void ShowHeroSelection()
        {
            _selectionPanel.Controls.Clear();

            var rng = new Random();

            string class1 = GameDataParser.FactionToHeroClass.TryGetValue(_faction1, out var hc1) ? hc1 : "";
            string class2 = GameDataParser.FactionToHeroClass.TryGetValue(_faction2, out var hc2) ? hc2 : "";

            var heroes1 = _allHeroes.Where(h => h.HeroClass == class1).OrderBy(_ => rng.Next()).Take(2).ToList();
            var heroes2 = _allHeroes.Where(h => h.HeroClass == class2).OrderBy(_ => rng.Next()).Take(2).ToList();

            new Label
            {
                Parent = _selectionPanel,
                Text = "Выберите героев",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 220, 100),
                AutoSize = true,
                Location = new Point(420, 30),
            };

            HeroInfo? selectedHero1 = null;
            HeroInfo? selectedHero2 = null;

            void UpdateSelection(Panel panel, Panel? otherPanel)
            {
                panel.BackColor = Color.FromArgb(60, 120, 60);
                if (otherPanel != null) otherPanel.BackColor = Color.FromArgb(50, 50, 70);
            }

            new Label
            {
                Parent = _selectionPanel,
                Text = $"Игрок 1: {_faction1}",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 180, 255),
                AutoSize = true,
                Location = new Point(50, 80),
            };

            int yOff = 110;
            Panel? p1a = null, p1b = null;
            for (int i = 0; i < heroes1.Count; i++)
            {
                var hero = heroes1[i];
                var panel = BuildHeroCard(hero, new Point(50, yOff + i * 140));
                panel.Parent = _selectionPanel;
                int idx = i;
                panel.Click += (s, ev) =>
                {
                    selectedHero1 = hero;
                    UpdateSelection(panel, idx == 0 ? p1b : p1a);
                };
                foreach (Control c in panel.Controls)
                    c.Click += (s, ev) =>
                    {
                        selectedHero1 = hero;
                        UpdateSelection(panel, idx == 0 ? p1b : p1a);
                    };
                if (i == 0) p1a = panel; else p1b = panel;
            }

            new Label
            {
                Parent = _selectionPanel,
                Text = $"Игрок 2: {_faction2}",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 130, 130),
                AutoSize = true,
                Location = new Point(580, 80),
            };

            Panel? p2a = null, p2b = null;
            for (int i = 0; i < heroes2.Count; i++)
            {
                var hero = heroes2[i];
                var panel = BuildHeroCard(hero, new Point(580, yOff + i * 140));
                panel.Parent = _selectionPanel;
                int idx = i;
                panel.Click += (s, ev) =>
                {
                    selectedHero2 = hero;
                    UpdateSelection(panel, idx == 0 ? p2b : p2a);
                };
                foreach (Control c in panel.Controls)
                    c.Click += (s, ev) =>
                    {
                        selectedHero2 = hero;
                        UpdateSelection(panel, idx == 0 ? p2b : p2a);
                    };
                if (i == 0) p2a = panel; else p2b = panel;
            }

            var btnConfirm = new Button
            {
                Parent = _selectionPanel,
                Text = "Подтвердить",
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                Size = new Size(200, 45),
                Location = new Point(450, 420),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 120, 50),
                ForeColor = Color.White,
            };
            btnConfirm.FlatAppearance.BorderSize = 0;
            btnConfirm.Click += (s, ev) =>
            {
                if (selectedHero1 == null || selectedHero2 == null)
                {
                    MessageBox.Show("Выберите героя для каждого игрока!", "Парсер", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                ShowMainContent(_creatures1, _creatures2, _allArtifacts, selectedHero1, selectedHero2);
            };

            if (heroes1.Count > 0) { selectedHero1 = heroes1[0]; if (p1a != null) p1a.BackColor = Color.FromArgb(60, 120, 60); }
            if (heroes2.Count > 0) { selectedHero2 = heroes2[0]; if (p2a != null) p2a.BackColor = Color.FromArgb(60, 120, 60); }
        }

        private Panel BuildHeroCard(HeroInfo hero, Point location)
        {
            var panel = new Panel
            {
                Location = location,
                Size = new Size(480, 125),
                BackColor = Color.FromArgb(50, 50, 70),
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand,
            };

            var icon = new PictureBox
            {
                Parent = panel,
                Location = new Point(5, 5),
                Size = new Size(80, 80),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(35, 35, 50),
                Cursor = Cursors.Hand,
            };
            if (hero.FaceIcon != null) icon.Image = new Bitmap(hero.FaceIcon);

            new Label
            {
                Parent = panel,
                Text = hero.Name,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(95, 5),
                AutoSize = true,
                Cursor = Cursors.Hand,
            };

            new Label
            {
                Parent = panel,
                Text = hero.SpecializationName,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 220, 100),
                Location = new Point(95, 30),
                AutoSize = true,
                Cursor = Cursors.Hand,
            };

            new Label
            {
                Parent = panel,
                Text = $"А:{hero.Offence} З:{hero.Defence} С:{hero.Spellpower} М:{hero.Knowledge}",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.LightGray,
                Location = new Point(95, 52),
                AutoSize = true,
                Cursor = Cursors.Hand,
            };

            string startSkills = "";
            if (!string.IsNullOrEmpty(hero.PrimarySkillId))
            {
                var sk = _allSkills.FirstOrDefault(s => s.Id == hero.PrimarySkillId);
                startSkills += $"Расовый: {sk?.GetName() ?? hero.PrimarySkillId}";
            }
            foreach (var (sid, mastery) in hero.Skills)
            {
                var sk = _allSkills.FirstOrDefault(s => s.Id == sid);
                if (startSkills.Length > 0) startSkills += ", ";
                startSkills += sk?.GetName() ?? sid;
            }

            new Label
            {
                Parent = panel,
                Text = startSkills,
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.Gray,
                Location = new Point(95, 72),
                AutoSize = true,
                MaximumSize = new Size(370, 0),
                Cursor = Cursors.Hand,
            };

            icon.DoubleClick += (s, ev) => ShowHeroDetail(hero);
            panel.DoubleClick += (s, ev) => ShowHeroDetail(hero);

            return panel;
        }

        private void ShowHeroDetail(HeroInfo hero)
        {
            using var form = new HeroDetailForm(hero);
            form.ShowDialog();
        }

        private void ShowMainContent(List<CreatureInfo> creatures1, List<CreatureInfo> creatures2,
            List<ArtifactInfo> artifacts, HeroInfo hero1, HeroInfo hero2)
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
                Text = $"Игрок 1: {_faction1} — {hero1.Name}",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Size = new Size(300, 34),
                Location = new Point(3, 3),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(100, 180, 255),
                ForeColor = Color.Black,
            };
            btnPlayer1.FlatAppearance.BorderSize = 0;

            var btnPlayer2 = new Button
            {
                Parent = playerPanel,
                Text = $"Игрок 2: {_faction2} — {hero2.Name}",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Size = new Size(300, 34),
                Location = new Point(310, 3),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 80),
                ForeColor = Color.White,
            };
            btnPlayer2.FlatAppearance.BorderSize = 0;

            var btnReady = new Button
            {
                Parent = playerPanel,
                Text = "Готов!",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Size = new Size(160, 34),
                Location = new Point(playerPanel.Width - 170, 3),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 160, 50),
                ForeColor = Color.White,
            };
            btnReady.FlatAppearance.BorderSize = 0;

            _tabs.Location = new Point(10, 55);
            _tabs.Size = new Size(Width - 36, Height - 105);
            _tabs.Visible = true;
            _tabs.TabPages.Clear();

            var goldState1 = new GoldState(_totalGold);
            var goldState2 = new GoldState(_totalGold);

            var tabArmy1 = new TabPage("Армия") { BackColor = Color.FromArgb(30, 30, 40), ForeColor = Color.White };
            var tabArt1 = new TabPage("Артефакты") { BackColor = Color.FromArgb(30, 30, 40), ForeColor = Color.White };
            var tabLvl1 = new TabPage("Прокачка") { BackColor = Color.FromArgb(30, 30, 40), ForeColor = Color.White };
            var tabSpells1 = new TabPage("Заклинания") { BackColor = Color.FromArgb(30, 30, 40), ForeColor = Color.White };

            var tabArmy2 = new TabPage("Армия") { BackColor = Color.FromArgb(30, 30, 40), ForeColor = Color.White };
            var tabArt2 = new TabPage("Артефакты") { BackColor = Color.FromArgb(30, 30, 40), ForeColor = Color.White };
            var tabLvl2 = new TabPage("Прокачка") { BackColor = Color.FromArgb(30, 30, 40), ForeColor = Color.White };
            var tabSpells2 = new TabPage("Заклинания") { BackColor = Color.FromArgb(30, 30, 40), ForeColor = Color.White };

            var hci1 = _heroClasses.FirstOrDefault(c => c.Id == hero1.HeroClass);
            var hci2 = _heroClasses.FirstOrDefault(c => c.Id == hero2.HeroClass);

            var armyTab1 = new ArmyPurchaseTab(tabArmy1, creatures1, goldState1);
            var artTab1  = new ArtifactTab(tabArt1, artifacts, goldState1);
            var lvlTab1  = new LevelingTab(tabLvl1, hero1, _allSkills, hci1, goldState1);
            var spellTab1 = new SpellTab(tabSpells1, _allSpells, _faction1, hero1.HeroClass, goldState1);
            var armyTab2 = new ArmyPurchaseTab(tabArmy2, creatures2, goldState2);
            var artTab2  = new ArtifactTab(tabArt2, artifacts, goldState2);
            var lvlTab2  = new LevelingTab(tabLvl2, hero2, _allSkills, hci2, goldState2);
            var spellTab2 = new SpellTab(tabSpells2, _allSpells, _faction2, hero2.HeroClass, goldState2);

            _tabs.TabPages.Add(tabArmy1);
            _tabs.TabPages.Add(tabArt1);
            _tabs.TabPages.Add(tabLvl1);
            _tabs.TabPages.Add(tabSpells1);

            btnPlayer1.Click += (s, ev) =>
            {
                btnPlayer1.BackColor = Color.FromArgb(100, 180, 255); btnPlayer1.ForeColor = Color.Black;
                btnPlayer2.BackColor = Color.FromArgb(60, 60, 80); btnPlayer2.ForeColor = Color.White;
                int idx = _tabs.SelectedIndex;
                _tabs.TabPages.Clear();
                _tabs.TabPages.AddRange(new[] { tabArmy1, tabArt1, tabLvl1, tabSpells1 });
                if (idx >= 0 && idx < _tabs.TabCount) _tabs.SelectedIndex = idx;
            };

            btnPlayer2.Click += (s, ev) =>
            {
                btnPlayer2.BackColor = Color.FromArgb(255, 130, 130); btnPlayer2.ForeColor = Color.Black;
                btnPlayer1.BackColor = Color.FromArgb(60, 60, 80); btnPlayer1.ForeColor = Color.White;
                int idx = _tabs.SelectedIndex;
                _tabs.TabPages.Clear();
                _tabs.TabPages.AddRange(new[] { tabArmy2, tabArt2, tabLvl2, tabSpells2 });
                if (idx >= 0 && idx < _tabs.TabCount) _tabs.SelectedIndex = idx;
            };

            btnReady.Click += (s, ev) =>
            {
                var preset1 = BuildPreset(hero1, armyTab1, artTab1, lvlTab1, spellTab1, goldState1);
                var preset2 = BuildPreset(hero2, armyTab2, artTab2, lvlTab2, spellTab2, goldState2);

                string userModsDir = System.IO.Path.Combine(_gameRoot, "UserMods");
                try
                {
                    string path = PresetGenerator.Generate(userModsDir, preset1, preset2,
                        _faction1, _faction2);

                    if (_player1Account != null && _player2Account != null)
                    {
                        int duelId = DatabaseManager.SaveDuel(_player1Account.Id, _player2Account.Id);
                        DatabaseManager.SaveDuelHero(BuildHeroSnapshot(duelId, _player1Account.Id,
                            _faction1, hero1, preset1));
                        DatabaseManager.SaveDuelHero(BuildHeroSnapshot(duelId, _player2Account.Id,
                            _faction2, hero2, preset2));
                    }

                    MessageBox.Show($"Пресет сохранён:\n{path}", "Готов!",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка создания пресета:\n{ex.Message}", "Ошибка",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
        }

        private static PlayerPreset BuildPreset(HeroInfo hero,
            ArmyPurchaseTab armyTab, ArtifactTab artTab,
            LevelingTab lvlTab, SpellTab spellTab, GoldState gold)
        {
            return new PlayerPreset
            {
                Hero = hero,
                ArmySlots = armyTab.Slots,
                EquippedArtifacts = artTab.Equipped.ToDictionary(kv => kv.Key, kv => kv.Value),
                HeroLevel = lvlTab.HeroLevel,
                TotalOffence = lvlTab.TotalOffence,
                TotalDefence = lvlTab.TotalDefence,
                TotalSpellpower = lvlTab.TotalSpellpower,
                TotalKnowledge = lvlTab.TotalKnowledge,
                Skills = lvlTab.TakenSkills.ToList(),
                Perks = lvlTab.TakenPerks.ToList(),
                RacialMastery = lvlTab.RacialMastery,
                Spells = spellTab.ChosenSpells.ToList(),
                Runes = spellTab.ChosenRunes.ToList(),
                GoldSpent = gold.Spent,
            };
        }

        private static DuelHeroSnapshot BuildHeroSnapshot(int duelId, int playerId,
            string faction, HeroInfo hero, PlayerPreset preset)
        {
            var armyList = new List<object>();
            foreach (var slot in preset.ArmySlots)
            {
                if (slot?.Creature != null && slot.Count > 0)
                    armyList.Add(new { Creature = slot.Creature.Id, slot.Count });
            }

            var skillsList = new List<object>();
            foreach (var (skillId, mastery) in preset.Skills)
                skillsList.Add(new { SkillId = skillId, Mastery = mastery });

            var spellIds = new List<string>();
            foreach (var sp in preset.Spells)
                spellIds.Add(!string.IsNullOrEmpty(sp.GameId) ? sp.GameId : sp.Id);
            foreach (var rn in preset.Runes)
                spellIds.Add(!string.IsNullOrEmpty(rn.GameId) ? rn.GameId : rn.Id);

            var artifactIds = new List<string>();
            foreach (var kv in preset.EquippedArtifacts)
            {
                if (kv.Value != null) artifactIds.Add(kv.Value.Id);
            }

            return new DuelHeroSnapshot
            {
                DuelId = duelId,
                PlayerId = playerId,
                Faction = faction,
                HeroName = hero.Name,
                HeroLevel = preset.HeroLevel,
                Offence = preset.TotalOffence,
                Defence = preset.TotalDefence,
                Spellpower = preset.TotalSpellpower,
                Knowledge = preset.TotalKnowledge,
                SkillsJson = System.Text.Json.JsonSerializer.Serialize(skillsList),
                PerksJson = System.Text.Json.JsonSerializer.Serialize(preset.Perks),
                SpellsJson = System.Text.Json.JsonSerializer.Serialize(spellIds),
                ArmyJson = System.Text.Json.JsonSerializer.Serialize(armyList),
                ArtifactsJson = System.Text.Json.JsonSerializer.Serialize(artifactIds),
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

        public ArmySlot[] Slots => _slots;

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

        public IReadOnlyDictionary<string, ArtifactInfo?> Equipped => _equipped;

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

    // ═════════════════════════════════════════════════════════════════════════════
    //  КАРТОЧКА ГЕРОЯ
    // ═════════════════════════════════════════════════════════════════════════════
    internal class HeroDetailForm : Form
    {
        public HeroDetailForm(HeroInfo hero)
        {
            Text = hero.Name;
            Size = new Size(420, 350);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(30, 30, 40);

            var icon = new PictureBox
            {
                Parent = this,
                Location = new Point(15, 15),
                Size = new Size(100, 100),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(45, 45, 60),
            };
            if (hero.FaceIcon != null) icon.Image = new Bitmap(hero.FaceIcon);

            var nameFont = new Font("Segoe UI", 16, FontStyle.Bold);
            new Label { Parent = this, Text = hero.Name, Font = nameFont, ForeColor = Color.White, Location = new Point(125, 15), AutoSize = true };

            var specFont = new Font("Segoe UI", 11, FontStyle.Bold);
            new Label { Parent = this, Text = hero.SpecializationName, Font = specFont, ForeColor = Color.FromArgb(255, 220, 100), Location = new Point(125, 50), AutoSize = true };

            var statFont = new Font("Segoe UI", 10);
            new Label
            {
                Parent = this,
                Text = $"Атака: {hero.Offence}  Защита: {hero.Defence}  Сила магии: {hero.Spellpower}  Знания: {hero.Knowledge}",
                Font = statFont,
                ForeColor = Color.LightGray,
                Location = new Point(125, 80),
                AutoSize = true,
            };

            new Panel { Parent = this, Location = new Point(15, 125), Size = new Size(375, 1), BackColor = Color.FromArgb(80, 80, 100) };

            var descFont = new Font("Segoe UI", 9);
            new Label
            {
                Parent = this,
                Text = hero.SpecializationDesc,
                Font = descFont,
                ForeColor = Color.LightGray,
                Location = new Point(15, 135),
                AutoSize = true,
                MaximumSize = new Size(375, 0),
            };
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    //  ВКЛАДКА ПРОКАЧКИ
    // ═════════════════════════════════════════════════════════════════════════════
    internal class LevelingTab
    {
        private readonly TabPage _tab;
        private readonly HeroInfo _hero;
        private readonly List<SkillInfo> _allSkills;
        private readonly HeroClassInfo? _heroClassInfo;
        private readonly GoldState _gold;
        private readonly Random _rng = new();

        private int _heroLevel = 1;
        private int _levelUpCount = 0;
        private int _bonusOffence, _bonusDefence, _bonusSpellpower, _bonusKnowledge;

        private readonly List<(string SkillId, int Mastery)> _takenSkills = new();
        private readonly List<string> _takenPerks = new();
        private string _racialSkillId = "";
        private int _racialMastery;

        public int HeroLevel => _heroLevel;
        public int TotalOffence => _hero.Offence + _bonusOffence;
        public int TotalDefence => _hero.Defence + _bonusDefence;
        public int TotalSpellpower => _hero.Spellpower + _bonusSpellpower;
        public int TotalKnowledge => _hero.Knowledge + _bonusKnowledge;
        public IReadOnlyList<(string SkillId, int Mastery)> TakenSkills => _takenSkills;
        public IReadOnlyList<string> TakenPerks => _takenPerks;
        public int RacialMastery => _racialMastery;

        private Label _levelLabel = null!;
        private Label _statsLabel = null!;
        private Panel _skillsGrid = null!;
        private Button _levelUpBtn = null!;
        private Label _goldCostLabel = null!;

        private const int MaxNonRacialSkills = 5;
        private const int MaxLevel = 20;

        public LevelingTab(TabPage tab, HeroInfo hero, List<SkillInfo> allSkills, HeroClassInfo? heroClassInfo, GoldState gold)
        {
            _tab = tab;
            _hero = hero;
            _allSkills = allSkills;
            _heroClassInfo = heroClassInfo;
            _gold = gold;

            InitializeHeroState();
            BuildUI();
        }

        private void InitializeHeroState()
        {
            _racialSkillId = _hero.PrimarySkillId;
            _racialMastery = MasteryToInt(_hero.PrimarySkillMastery);

            foreach (var (sid, mastery) in _hero.Skills)
                _takenSkills.Add((sid, MasteryToInt(mastery)));

            foreach (var pid in _hero.PerkIds)
                _takenPerks.Add(pid);

            _bonusOffence = 0;
            _bonusDefence = 0;
            _bonusSpellpower = 0;
            _bonusKnowledge = 0;
        }

        private static int MasteryToInt(string m) => m switch
        {
            "MASTERY_BASIC" => 0,
            "MASTERY_ADVANCED" => 1,
            "MASTERY_EXPERT" => 2,
            _ => 0,
        };

        internal static string MasteryName(int m) => m switch
        {
            0 => "Основа",
            1 => "Умелый",
            2 => "Искусный",
            _ => "Основа",
        };

        private void BuildUI()
        {
            var mainPanel = new DoubleBufferedPanel
            {
                Parent = _tab,
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(30, 30, 40),
            };

            // Hero info area
            var heroPanel = new Panel
            {
                Parent = mainPanel,
                Location = new Point(10, 10),
                Size = new Size(350, 100),
                BackColor = Color.FromArgb(40, 40, 55),
                BorderStyle = BorderStyle.FixedSingle,
            };

            var faceBox = new PictureBox
            {
                Parent = heroPanel,
                Location = new Point(5, 5),
                Size = new Size(70, 70),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(35, 35, 50),
                Cursor = Cursors.Hand,
            };
            if (_hero.FaceIcon != null) faceBox.Image = new Bitmap(_hero.FaceIcon);
            faceBox.DoubleClick += (s, ev) =>
            {
                using var form = new HeroDetailForm(_hero);
                form.ShowDialog();
            };

            new Label
            {
                Parent = heroPanel,
                Text = _hero.Name,
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(82, 5),
                AutoSize = true,
                Cursor = Cursors.Hand,
            };

            _levelLabel = new Label
            {
                Parent = heroPanel,
                Text = $"Уровень: {_heroLevel}",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(255, 220, 100),
                Location = new Point(82, 30),
                AutoSize = true,
            };

            _statsLabel = new Label
            {
                Parent = heroPanel,
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.LightGray,
                Location = new Point(82, 50),
                AutoSize = true,
            };
            UpdateStatsLabel();

            new Label
            {
                Parent = heroPanel,
                Text = _hero.SpecializationName,
                Font = new Font("Segoe UI", 8, FontStyle.Italic),
                ForeColor = Color.FromArgb(180, 180, 200),
                Location = new Point(82, 72),
                AutoSize = true,
            };

            // Level up area
            int lvlY = 10;
            _levelUpBtn = new Button
            {
                Parent = mainPanel,
                Text = "Повысить уровень",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Size = new Size(180, 35),
                Location = new Point(380, lvlY),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 120, 50),
                ForeColor = Color.White,
            };
            _levelUpBtn.FlatAppearance.BorderSize = 0;
            _levelUpBtn.Click += (s, ev) => DoLevelUp();

            _goldCostLabel = new Label
            {
                Parent = mainPanel,
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(255, 220, 100),
                Location = new Point(380, lvlY + 40),
                AutoSize = true,
            };
            UpdateGoldCostLabel();

            _gold.Changed += () => UpdateGoldCostLabel();

            // Skills grid
            _skillsGrid = new DoubleBufferedPanel
            {
                Parent = mainPanel,
                Location = new Point(10, 120),
                Size = new Size(750, 450),
                AutoScroll = true,
                BackColor = Color.FromArgb(30, 30, 40),
            };

            RebuildSkillsGrid();
        }

        private void UpdateStatsLabel()
        {
            int a = _hero.Offence + _bonusOffence;
            int d = _hero.Defence + _bonusDefence;
            int s = _hero.Spellpower + _bonusSpellpower;
            int k = _hero.Knowledge + _bonusKnowledge;
            _statsLabel.Text = $"А:{a} З:{d} С:{s} М:{k}";
        }

        private int GetLevelUpCost()
        {
            if (_heroLevel < MaxLevel) return 0;
            int extra = _heroLevel - MaxLevel;
            return 5000 + 1000 * extra;
        }

        private void UpdateGoldCostLabel()
        {
            int cost = GetLevelUpCost();
            _goldCostLabel.Text = cost == 0 ? "Бесплатно" : $"Стоимость: {cost}g (Осталось: {_gold.Remaining}g)";
            _levelUpBtn.Enabled = cost == 0 || _gold.Remaining >= cost;
        }

        private void DoLevelUp()
        {
            int cost = GetLevelUpCost();
            if (cost > 0 && !_gold.TrySpend(cost)) return;

            _heroLevel++;
            _levelUpCount++;
            _levelLabel.Text = $"Уровень: {_heroLevel}";

            RollStat();
            UpdateStatsLabel();
            UpdateGoldCostLabel();

            ShowLevelUpDialog();
        }

        private void RollStat()
        {
            if (_heroClassInfo == null) { _bonusOffence++; return; }

            int total = _heroClassInfo.OffenceProb + _heroClassInfo.DefenceProb
                      + _heroClassInfo.SpellpowerProb + _heroClassInfo.KnowledgeProb;
            if (total <= 0) { _bonusOffence++; return; }

            int roll = _rng.Next(total);
            if (roll < _heroClassInfo.OffenceProb) _bonusOffence++;
            else if (roll < _heroClassInfo.OffenceProb + _heroClassInfo.DefenceProb) _bonusDefence++;
            else if (roll < _heroClassInfo.OffenceProb + _heroClassInfo.DefenceProb + _heroClassInfo.SpellpowerProb) _bonusSpellpower++;
            else _bonusKnowledge++;
        }

        private void ShowLevelUpDialog()
        {
            var options = GenerateLevelUpOptions();
            if (options.Count == 0) { RebuildSkillsGrid(); return; }

            using var dlg = new LevelUpForm(options, _allSkills);
            if (dlg.ShowDialog() != DialogResult.OK || dlg.SelectedOption == null) return;

            ApplyLevelUpOption(dlg.SelectedOption);
            RebuildSkillsGrid();
        }

        private List<LevelUpOption> GenerateLevelUpOptions()
        {
            var options = new List<LevelUpOption>();
            var heroClass = _hero.HeroClass;

            var allTakenSkillIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(_racialSkillId)) allTakenSkillIds.Add(_racialSkillId);
            foreach (var (sid, _) in _takenSkills) allTakenSkillIds.Add(sid);

            // Available regular skills (HeroClass = NONE, not yet taken)
            var availableNewSkills = _allSkills
                .Where(s => s.IsSkill && !s.IsRacial && !allTakenSkillIds.Contains(s.Id))
                .Where(s => _heroClassInfo == null || (_heroClassInfo.SkillProbs.TryGetValue(s.Id, out int p) && p > 0))
                .ToList();

            bool hasOpenSlots = _takenSkills.Count < MaxNonRacialSkills;

            // Skills that can be advanced
            var advanceable = new List<(string SkillId, int CurrentMastery, bool IsRacial)>();
            if (!string.IsNullOrEmpty(_racialSkillId) && _racialMastery < 2)
                advanceable.Add((_racialSkillId, _racialMastery, true));
            foreach (var (sid, m) in _takenSkills)
            {
                var sk = _allSkills.FirstOrDefault(s => s.Id == sid);
                int maxM = (sk != null && sk.MasteryLevels >= 4) ? 3 : 2;
                if (m < maxM) advanceable.Add((sid, m, false));
            }

            // LEFT-UPPER: new skill or advance
            if (hasOpenSlots && availableNewSkills.Count > 0)
            {
                var pick = PickByProb(availableNewSkills);
                if (pick != null) options.Add(new LevelUpOption { Type = LevelUpType.NewSkill, SkillId = pick.Id });
            }
            else if (advanceable.Count > 0)
            {
                var adv = advanceable[_rng.Next(advanceable.Count)];
                options.Add(new LevelUpOption { Type = LevelUpType.AdvanceSkill, SkillId = adv.SkillId, NewMastery = adv.CurrentMastery + 1 });
                advanceable.Remove(adv);
            }

            // LEFT-LOWER: advance skill or different new skill
            if (advanceable.Count > 0)
            {
                var adv = advanceable[_rng.Next(advanceable.Count)];
                options.Add(new LevelUpOption { Type = LevelUpType.AdvanceSkill, SkillId = adv.SkillId, NewMastery = adv.CurrentMastery + 1 });
            }
            else if (hasOpenSlots && availableNewSkills.Count > 1)
            {
                var usedIds = options.Where(o => o.Type == LevelUpType.NewSkill).Select(o => o.SkillId).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var remaining = availableNewSkills.Where(s => !usedIds.Contains(s.Id)).ToList();
                if (remaining.Count > 0)
                {
                    var pick = PickByProb(remaining);
                    if (pick != null) options.Add(new LevelUpOption { Type = LevelUpType.NewSkill, SkillId = pick.Id });
                }
            }

            // Get available perks, classified relative to this hero's class
            var takenPerkSet = new HashSet<string>(_takenPerks, StringComparer.OrdinalIgnoreCase);
            var availPerks = GetAvailablePerks(heroClass, allTakenSkillIds, takenPerkSet);

            var primaryPerks = availPerks.Where(p => p.Prerequisites.Count == 0 || !HasPrereqsForClass(p, heroClass)).ToList();
            var secondaryPerks = availPerks.Where(p => p.Prerequisites.Count > 0 && HasPrereqsForClass(p, heroClass)).ToList();

            // RIGHT-UPPER: primary perk, or secondary if no primaries
            if (primaryPerks.Count > 0)
            {
                var pick = primaryPerks[_rng.Next(primaryPerks.Count)];
                options.Add(new LevelUpOption { Type = LevelUpType.Perk, PerkId = pick.Id });
                primaryPerks.Remove(pick);
            }
            else if (secondaryPerks.Count > 0)
            {
                var pick = secondaryPerks[_rng.Next(secondaryPerks.Count)];
                options.Add(new LevelUpOption { Type = LevelUpType.Perk, PerkId = pick.Id });
                secondaryPerks.Remove(pick);
            }

            // RIGHT-LOWER: secondary perk, or different primary
            if (secondaryPerks.Count > 0)
            {
                var pick = secondaryPerks[_rng.Next(secondaryPerks.Count)];
                options.Add(new LevelUpOption { Type = LevelUpType.Perk, PerkId = pick.Id });
            }
            else if (primaryPerks.Count > 0)
            {
                var pick = primaryPerks[_rng.Next(primaryPerks.Count)];
                options.Add(new LevelUpOption { Type = LevelUpType.Perk, PerkId = pick.Id });
            }

            return options;
        }

        private List<SkillInfo> GetAvailablePerks(string heroClass, HashSet<string> takenSkillIds, HashSet<string> takenPerkIds)
        {
            var result = new List<SkillInfo>();

            foreach (var perk in _allSkills.Where(s => s.IsPerk))
            {
                if (takenPerkIds.Contains(perk.Id)) continue;

                // Must belong to a taken skill
                if (string.IsNullOrEmpty(perk.BasicSkillId) || perk.BasicSkillId == "HERO_SKILL_NONE") continue;
                if (!takenSkillIds.Contains(perk.BasicSkillId)) continue;

                // Check perk slot availability
                if (!HasPerkSlot(perk.BasicSkillId)) continue;

                // Class filter + prerequisites
                if (perk.Prerequisites.Count > 0)
                {
                    // Perks with SkillPrerequisites: class determined by prereq entries
                    var classPrereqs = perk.Prerequisites.Where(p => p.HeroClass == heroClass).ToList();
                    if (classPrereqs.Count == 0)
                        continue; // no prereq entry for this class — unavailable
                    bool met = classPrereqs.Any(cp => cp.DependencyIds.All(d => takenPerkIds.Contains(d)));
                    if (!met) continue;
                }
                else
                {
                    // Perks without SkillPrerequisites: use HeroClass field
                    if (perk.IsRacial && perk.HeroClass != heroClass) continue;
                }

                result.Add(perk);
            }

            return result;
        }

        private bool HasPerkSlot(string skillId)
        {
            int mastery;
            bool isRacial;
            if (skillId.Equals(_racialSkillId, StringComparison.OrdinalIgnoreCase))
            {
                mastery = _racialMastery;
                isRacial = true;
            }
            else
            {
                var entry = _takenSkills.FirstOrDefault(t => t.SkillId.Equals(skillId, StringComparison.OrdinalIgnoreCase));
                if (entry.SkillId == null) return false;
                mastery = entry.Mastery;
                isRacial = false;
            }

            int maxSlots = mastery + 1;
            int usedSlots = _takenPerks.Count(pid =>
            {
                var p = _allSkills.FirstOrDefault(s => s.Id == pid);
                return p != null && p.BasicSkillId.Equals(skillId, StringComparison.OrdinalIgnoreCase);
            });

            // Center perks: if racial skill is expert (3 slots), center perk doesn't use slot
            if (isRacial && mastery >= 2)
            {
                int centerPerkCount = _takenPerks.Count(pid =>
                {
                    var p = _allSkills.FirstOrDefault(s => s.Id == pid);
                    return p != null && p.BasicSkillId.Equals(skillId, StringComparison.OrdinalIgnoreCase) && p.IsSpecialPerk;
                });
                usedSlots -= centerPerkCount;
            }

            return usedSlots < maxSlots;
        }

        private static bool HasPrereqsForClass(SkillInfo perk, string heroClass) =>
            perk.Prerequisites.Any(p => p.HeroClass == heroClass);

        private SkillInfo? PickByProb(List<SkillInfo> skills)
        {
            if (skills.Count == 0) return null;
            if (_heroClassInfo == null) return skills[_rng.Next(skills.Count)];

            var weighted = new List<(SkillInfo s, int w)>();
            foreach (var s in skills)
            {
                int prob = _heroClassInfo.SkillProbs.TryGetValue(s.Id, out int p) ? p : 1;
                if (prob > 0) weighted.Add((s, prob));
            }
            if (weighted.Count == 0) return skills[_rng.Next(skills.Count)];

            int total = weighted.Sum(w => w.w);
            int roll = _rng.Next(total);
            int acc = 0;
            foreach (var (s, w) in weighted)
            {
                acc += w;
                if (roll < acc) return s;
            }
            return weighted[^1].s;
        }

        private void ApplyLevelUpOption(LevelUpOption option)
        {
            switch (option.Type)
            {
                case LevelUpType.NewSkill:
                    _takenSkills.Add((option.SkillId, 0));
                    break;
                case LevelUpType.AdvanceSkill:
                    if (option.SkillId.Equals(_racialSkillId, StringComparison.OrdinalIgnoreCase))
                    {
                        _racialMastery = option.NewMastery;
                    }
                    else
                    {
                        int idx = _takenSkills.FindIndex(t => t.SkillId.Equals(option.SkillId, StringComparison.OrdinalIgnoreCase));
                        if (idx >= 0) _takenSkills[idx] = (_takenSkills[idx].SkillId, option.NewMastery);
                    }
                    break;
                case LevelUpType.Perk:
                    _takenPerks.Add(option.PerkId);
                    break;
            }
        }

        private void RebuildSkillsGrid()
        {
            _skillsGrid.Controls.Clear();
            int y = 5;
            int iconSize = 52;
            int gap = 6;

            void AddSkillRow(string skillId, int mastery, bool isRacial)
            {
                var skill = _allSkills.FirstOrDefault(s => s.Id == skillId);
                if (skill == null) return;

                var skillIcon = new PictureBox
                {
                    Parent = _skillsGrid,
                    Location = new Point(5, y),
                    Size = new Size(iconSize, iconSize),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.FromArgb(45, 45, 60),
                    BorderStyle = BorderStyle.FixedSingle,
                    Cursor = Cursors.Hand,
                };
                var sImg = skill.GetIcon(mastery);
                if (sImg != null) skillIcon.Image = new Bitmap(sImg);

                var tip = new ToolTip();
                string skillName = skill.GetName(mastery);
                string label = isRacial ? $"[Расовый] {skillName} ({MasteryName(mastery)})" : $"{skillName} ({MasteryName(mastery)})";
                tip.SetToolTip(skillIcon, label);

                skillIcon.DoubleClick += (s, ev) => ShowSkillDetail(skill, mastery);

                // Arrow
                new Label
                {
                    Parent = _skillsGrid,
                    Text = "→",
                    Font = new Font("Segoe UI", 14, FontStyle.Bold),
                    ForeColor = Color.FromArgb(120, 120, 140),
                    Location = new Point(iconSize + 10, y + 12),
                    AutoSize = true,
                };

                // Perk slots
                int perkX = iconSize + 35;
                var perksForSkill = _takenPerks
                    .Select(pid => _allSkills.FirstOrDefault(s => s.Id == pid))
                    .Where(p => p != null && p.BasicSkillId.Equals(skillId, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                int totalSlots = mastery + 1;
                for (int slot = 0; slot < totalSlots; slot++)
                {
                    var perkBox = new PictureBox
                    {
                        Parent = _skillsGrid,
                        Location = new Point(perkX, y),
                        Size = new Size(iconSize, iconSize),
                        SizeMode = PictureBoxSizeMode.Zoom,
                        BackColor = Color.FromArgb(40, 40, 55),
                        BorderStyle = BorderStyle.FixedSingle,
                    };

                    if (slot < perksForSkill.Count && perksForSkill[slot] != null)
                    {
                        var perk = perksForSkill[slot]!;
                        var pImg = perk.GetIcon();
                        if (pImg != null) perkBox.Image = new Bitmap(pImg);
                        perkBox.Cursor = Cursors.Hand;

                        var pTip = new ToolTip();
                        pTip.SetToolTip(perkBox, perk.GetName());
                        perkBox.DoubleClick += (s, ev) => ShowPerkDetail(perk);
                    }
                    else
                    {
                        new Label
                        {
                            Parent = _skillsGrid,
                            Text = "?",
                            Font = new Font("Segoe UI", 18, FontStyle.Bold),
                            ForeColor = Color.FromArgb(80, 80, 100),
                            Location = new Point(perkX + 14, y + 10),
                            AutoSize = true,
                        };
                        perkBox.SendToBack();
                    }

                    perkX += iconSize + gap;
                }

                // Extra center perk for racial at expert
                if (isRacial && mastery >= 2)
                {
                    var centerPerks = perksForSkill.Where(p => p != null && p.IsSpecialPerk).ToList();
                    foreach (var cp in centerPerks.Skip(totalSlots))
                    {
                        var cpBox = new PictureBox
                        {
                            Parent = _skillsGrid,
                            Location = new Point(perkX, y),
                            Size = new Size(iconSize, iconSize),
                            SizeMode = PictureBoxSizeMode.Zoom,
                            BackColor = Color.FromArgb(55, 45, 45),
                            BorderStyle = BorderStyle.FixedSingle,
                            Cursor = Cursors.Hand,
                        };
                        var cpImg = cp!.GetIcon();
                        if (cpImg != null) cpBox.Image = new Bitmap(cpImg);
                        var cpTip = new ToolTip();
                        cpTip.SetToolTip(cpBox, $"[Центр] {cp.GetName()}");
                        cpBox.DoubleClick += (s, ev) => ShowPerkDetail(cp);
                        perkX += iconSize + gap;
                    }
                }

                y += iconSize + gap;
            }

            // Racial skill first
            if (!string.IsNullOrEmpty(_racialSkillId))
                AddSkillRow(_racialSkillId, _racialMastery, true);

            // Regular skills
            foreach (var (sid, m) in _takenSkills)
                AddSkillRow(sid, m, false);

            // Empty skill slots
            int emptySlots = MaxNonRacialSkills - _takenSkills.Count;
            for (int i = 0; i < emptySlots; i++)
            {
                var emptyBox = new PictureBox
                {
                    Parent = _skillsGrid,
                    Location = new Point(5, y),
                    Size = new Size(iconSize, iconSize),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.FromArgb(35, 35, 48),
                    BorderStyle = BorderStyle.FixedSingle,
                };

                new Label
                {
                    Parent = _skillsGrid,
                    Text = "?",
                    Font = new Font("Segoe UI", 18, FontStyle.Bold),
                    ForeColor = Color.FromArgb(60, 60, 80),
                    Location = new Point(19, y + 10),
                    AutoSize = true,
                };
                emptyBox.SendToBack();

                y += iconSize + gap;
            }
        }

        private void ShowSkillDetail(SkillInfo skill, int mastery)
        {
            using var form = new SkillDetailForm(skill, mastery);
            form.ShowDialog();
        }

        private void ShowPerkDetail(SkillInfo perk)
        {
            using var form = new SkillDetailForm(perk, 0);
            form.ShowDialog();
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    //  ОКНО ВЫБОРА ПРИ ПОВЫШЕНИИ УРОВНЯ
    // ═════════════════════════════════════════════════════════════════════════════
    internal enum LevelUpType { NewSkill, AdvanceSkill, Perk }

    internal class LevelUpOption
    {
        public LevelUpType Type { get; set; }
        public string SkillId { get; set; } = "";
        public string PerkId { get; set; } = "";
        public int NewMastery { get; set; }
    }

    internal class LevelUpForm : Form
    {
        public LevelUpOption? SelectedOption { get; private set; }

        public LevelUpForm(List<LevelUpOption> options, List<SkillInfo> allSkills)
        {
            Text = "Повышение уровня";
            Size = new Size(580, 350);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(30, 30, 40);

            new Label
            {
                Parent = this,
                Text = "Выберите 1 из доступных:",
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 220, 100),
                Location = new Point(15, 10),
                AutoSize = true,
            };

            string[] sectionLabels = { "Навык (лево-верх)", "Навык (лево-низ)", "Перк (право-верх)", "Перк (право-низ)" };

            int y = 45;
            for (int i = 0; i < options.Count; i++)
            {
                var opt = options[i];
                string title = i < sectionLabels.Length ? sectionLabels[i] : "Вариант";
                string desc = "";
                Image? icon = null;

                switch (opt.Type)
                {
                    case LevelUpType.NewSkill:
                    {
                        var skill = allSkills.FirstOrDefault(s => s.Id == opt.SkillId);
                        desc = $"Новый навык: {skill?.GetName() ?? opt.SkillId}";
                        icon = skill?.GetIcon(0);
                        break;
                    }
                    case LevelUpType.AdvanceSkill:
                    {
                        var skill = allSkills.FirstOrDefault(s => s.Id == opt.SkillId);
                        string mName = LevelingTab.MasteryName(opt.NewMastery);
                        desc = $"Продвинуть: {skill?.GetName(opt.NewMastery) ?? opt.SkillId} → {mName}";
                        icon = skill?.GetIcon(opt.NewMastery);
                        break;
                    }
                    case LevelUpType.Perk:
                    {
                        var perk = allSkills.FirstOrDefault(s => s.Id == opt.PerkId);
                        desc = $"Перк: {perk?.GetName() ?? opt.PerkId}";
                        icon = perk?.GetIcon();
                        break;
                    }
                }

                var panel = new Panel
                {
                    Parent = this,
                    Location = new Point(15, y),
                    Size = new Size(535, 60),
                    BackColor = Color.FromArgb(45, 45, 60),
                    BorderStyle = BorderStyle.FixedSingle,
                    Cursor = Cursors.Hand,
                };

                var iconBox = new PictureBox
                {
                    Parent = panel,
                    Location = new Point(3, 3),
                    Size = new Size(52, 52),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.FromArgb(35, 35, 50),
                    Cursor = Cursors.Hand,
                };
                if (icon != null) iconBox.Image = new Bitmap(icon);

                new Label
                {
                    Parent = panel,
                    Text = title,
                    Font = new Font("Segoe UI", 8),
                    ForeColor = Color.Gray,
                    Location = new Point(62, 3),
                    AutoSize = true,
                    Cursor = Cursors.Hand,
                };

                new Label
                {
                    Parent = panel,
                    Text = desc,
                    Font = new Font("Segoe UI", 11, FontStyle.Bold),
                    ForeColor = Color.White,
                    Location = new Point(62, 22),
                    AutoSize = true,
                    Cursor = Cursors.Hand,
                };

                int capturedIdx = i;
                void SelectThis(object? s, EventArgs ev)
                {
                    SelectedOption = options[capturedIdx];
                    DialogResult = DialogResult.OK;
                    Close();
                }

                panel.Click += SelectThis;
                iconBox.Click += SelectThis;
                foreach (Control c in panel.Controls) c.Click += SelectThis;

                y += 65;
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    //  КАРТОЧКА НАВЫКА / ПЕРКА
    // ═════════════════════════════════════════════════════════════════════════════
    internal class SkillDetailForm : Form
    {
        public SkillDetailForm(SkillInfo skill, int mastery)
        {
            Text = skill.GetName(mastery);
            Size = new Size(420, 320);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(30, 30, 40);

            var icon = new PictureBox
            {
                Parent = this,
                Location = new Point(15, 15),
                Size = new Size(80, 80),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(45, 45, 60),
            };
            var img = skill.GetIcon(mastery);
            if (img != null) icon.Image = new Bitmap(img);

            new Label
            {
                Parent = this,
                Text = skill.GetName(mastery),
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(105, 15),
                AutoSize = true,
            };

            if (skill.IsSkill)
            {
                new Label
                {
                    Parent = this,
                    Text = LevelingTab.MasteryName(mastery),
                    Font = new Font("Segoe UI", 10, FontStyle.Italic),
                    ForeColor = Color.FromArgb(255, 220, 100),
                    Location = new Point(105, 45),
                    AutoSize = true,
                };
            }

            new Panel { Parent = this, Location = new Point(15, 105), Size = new Size(375, 1), BackColor = Color.FromArgb(80, 80, 100) };

            new Label
            {
                Parent = this,
                Text = skill.GetDescription(mastery),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.LightGray,
                Location = new Point(15, 115),
                AutoSize = true,
                MaximumSize = new Size(375, 0),
            };
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    //  ВКЛАДКА ЗАКЛИНАНИЙ
    // ═════════════════════════════════════════════════════════════════════════════
    internal class SpellTab
    {
        private readonly TabPage _tab;
        private readonly GoldState _gold;
        private readonly List<SpellInfo> _allSpells;
        private readonly string _faction;
        private readonly string _heroClass;
        private readonly Random _rng = new();
        private const int SwapCost = 5000;
        private const int RerollCost = 5000;

        public IReadOnlyList<SpellInfo> ChosenSpells => _chosenSpells;
        public IReadOnlyList<SpellInfo> ChosenRunes => _chosenRunes;

        private static readonly Dictionary<string, string[]> ProfiledSchools = new()
        {
            { "Орден Света",    new[] { "MAGIC_SCHOOL_LIGHT", "MAGIC_SCHOOL_DARK" } },
            { "Инферно",        new[] { "MAGIC_SCHOOL_DARK", "MAGIC_SCHOOL_DESTRUCTIVE" } },
            { "Лесной Союз",    new[] { "MAGIC_SCHOOL_LIGHT", "MAGIC_SCHOOL_DESTRUCTIVE" } },
            { "Некрополис",     new[] { "MAGIC_SCHOOL_DARK", "MAGIC_SCHOOL_SUMMONING" } },
            { "Академия",       new[] { "MAGIC_SCHOOL_LIGHT", "MAGIC_SCHOOL_SUMMONING" } },
            { "Лига Теней",     new[] { "MAGIC_SCHOOL_DESTRUCTIVE", "MAGIC_SCHOOL_SUMMONING" } },
            { "Северные Кланы", new[] { "MAGIC_SCHOOL_LIGHT", "MAGIC_SCHOOL_DESTRUCTIVE" } },
        };

        private static readonly string[] AllCombatSchools =
        {
            "MAGIC_SCHOOL_DARK", "MAGIC_SCHOOL_DESTRUCTIVE",
            "MAGIC_SCHOOL_LIGHT", "MAGIC_SCHOOL_SUMMONING"
        };

        private readonly List<SpellInfo> _chosenSpells = new();
        private readonly List<SpellInfo> _chosenRunes = new();
        private Panel _spellsContainer = null!;
        private Label _goldLabel = null!;

        public SpellTab(TabPage tab, List<SpellInfo> allSpells, string faction, string heroClass, GoldState gold)
        {
            _tab = tab;
            _gold = gold;
            _allSpells = allSpells;
            _faction = faction;
            _heroClass = heroClass;

            BuildUI();
            RollSpells();
            _gold.Changed += RefreshGold;
        }

        private bool IsBarbarian => _heroClass == "HERO_CLASS_BARBARIAN";
        private bool IsDwarf => _heroClass == "HERO_CLASS_RUNEMAGE";
        private bool IsAcademy => _heroClass == "HERO_CLASS_WIZARD";

        private string[] GetProfiled() =>
            ProfiledSchools.TryGetValue(_faction, out var s) ? s : Array.Empty<string>();

        private string[] GetNonProfiled()
        {
            var prof = GetProfiled();
            return AllCombatSchools.Where(s => !prof.Contains(s)).ToArray();
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

            var btnReroll = new Button
            {
                Parent = _tab,
                Text = $"Перебрать ({RerollCost} зол.)",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Size = new Size(180, 28),
                Location = new Point(320, 6),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 80),
                ForeColor = Color.White,
            };
            btnReroll.FlatAppearance.BorderSize = 0;
            btnReroll.Click += (s, e) =>
            {
                if (!_gold.TrySpend(RerollCost))
                {
                    MessageBox.Show("Недостаточно золота!", "Заклинания", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                RollSpells();
            };

            _spellsContainer = new DoubleBufferedPanel
            {
                Parent = _tab,
                Location = new Point(10, 40),
                Size = new Size(700, 600),
                AutoScroll = true,
                BackColor = Color.FromArgb(30, 30, 40),
            };
        }

        private void RollSpells()
        {
            _chosenSpells.Clear();
            _chosenRunes.Clear();

            if (IsBarbarian)
                RollWarcries();
            else
                RollCombatSpells();

            if (IsDwarf)
                RollRunes();

            RebuildUI();
        }

        private void RollWarcries()
        {
            var warcries = _allSpells.Where(s => s.IsWarcry).ToList();
            var byLevel = warcries.GroupBy(s => s.Level).ToDictionary(g => g.Key, g => g.ToList());

            // Floor 1: 2 warcries, Floors 2-3: 1 each
            for (int floor = 1; floor <= 3; floor++)
            {
                int count = floor == 1 ? 2 : 1;
                if (byLevel.TryGetValue(floor, out var pool))
                {
                    var shuffled = pool.OrderBy(_ => _rng.Next()).ToList();
                    _chosenSpells.AddRange(shuffled.Take(count));
                }
            }
        }

        private void RollCombatSpells()
        {
            var profiled = GetProfiled();
            var nonProfiled = GetNonProfiled();

            var combatSpells = _allSpells
                .Where(s => !s.IsRunic && !s.IsWarcry && s.Level >= 1 && s.Level <= 5)
                .ToList();

            var bySchoolLevel = combatSpells
                .GroupBy(s => (s.MagicSchool, s.Level))
                .ToDictionary(g => g.Key, g => g.ToList());

            for (int floor = 1; floor <= 5; floor++)
            {
                // Profiled: 1 spell from each profiled school (2 total)
                var usedIds = new HashSet<string>();
                foreach (var school in profiled)
                {
                    var key = (school, floor);
                    if (bySchoolLevel.TryGetValue(key, out var pool))
                    {
                        var pick = pool.Where(s => !usedIds.Contains(s.Id)).OrderBy(_ => _rng.Next()).FirstOrDefault();
                        if (pick != null) { _chosenSpells.Add(pick); usedIds.Add(pick.Id); }
                    }
                }

                // Non-profiled: floors 1-3 get 1; Academy gets +1 on all floors
                int nonProfCount = floor <= 3 ? 1 : 0;
                if (IsAcademy) nonProfCount++;

                if (nonProfCount > 0)
                {
                    var nonProfPool = nonProfiled
                        .SelectMany(school => bySchoolLevel.TryGetValue((school, floor), out var p) ? p : Enumerable.Empty<SpellInfo>())
                        .Where(s => !usedIds.Contains(s.Id))
                        .OrderBy(_ => _rng.Next())
                        .ToList();

                    foreach (var pick in nonProfPool.Take(nonProfCount))
                    {
                        _chosenSpells.Add(pick);
                        usedIds.Add(pick.Id);
                    }
                }
            }
        }

        private void RollRunes()
        {
            var runes = _allSpells.Where(s => s.IsRunic).ToList();
            var byLevel = runes.GroupBy(s => s.Level).ToDictionary(g => g.Key, g => g.ToList());

            for (int floor = 1; floor <= 5; floor++)
            {
                if (byLevel.TryGetValue(floor, out var pool))
                {
                    var pick = pool.OrderBy(_ => _rng.Next()).FirstOrDefault();
                    if (pick != null) _chosenRunes.Add(pick);
                }
            }
        }

        private void RebuildUI()
        {
            RenderHelper.Freeze(_spellsContainer);
            _spellsContainer.Controls.Clear();

            int y = 5;

            if (IsBarbarian)
            {
                y = BuildFloorHeader(y, "Боевые кличи");
                for (int floor = 1; floor <= 3; floor++)
                {
                    var floorSpells = _chosenSpells.Where(s => s.Level == floor).ToList();
                    y = BuildFloorRow(y, $"Уровень {floor}", floorSpells);
                }
            }
            else
            {
                y = BuildFloorHeader(y, "Заклинания");
                for (int floor = 1; floor <= 5; floor++)
                {
                    var floorSpells = _chosenSpells.Where(s => s.Level == floor).ToList();
                    y = BuildFloorRow(y, $"Уровень {floor}", floorSpells);
                }
            }

            if (IsDwarf && _chosenRunes.Count > 0)
            {
                y += 10;
                y = BuildFloorHeader(y, "Рунная магия");
                for (int floor = 1; floor <= 5; floor++)
                {
                    var floorRunes = _chosenRunes.Where(s => s.Level == floor).ToList();
                    if (floorRunes.Count > 0)
                        y = BuildFloorRow(y, $"Уровень {floor}", floorRunes);
                }
            }

            RenderHelper.Unfreeze(_spellsContainer);
        }

        private int BuildFloorHeader(int y, string text)
        {
            new Label
            {
                Parent = _spellsContainer,
                Text = text,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 220, 100),
                Location = new Point(5, y),
                AutoSize = true,
            };
            return y + 28;
        }

        private int BuildFloorRow(int y, string label, List<SpellInfo> spells)
        {
            new Label
            {
                Parent = _spellsContainer,
                Text = label,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(180, 200, 255),
                Location = new Point(10, y),
                AutoSize = true,
            };
            y += 22;

            int x = 15;
            foreach (var spell in spells)
            {
                var card = BuildSpellCard(spell, x, y);
                _spellsContainer.Controls.Add(card);
                x += card.Width + 8;
            }

            return y + 78;
        }

        private Panel BuildSpellCard(SpellInfo spell, int x, int y)
        {
            var schoolColor = spell.MagicSchool switch
            {
                "MAGIC_SCHOOL_DARK" => Color.FromArgb(130, 80, 180),
                "MAGIC_SCHOOL_DESTRUCTIVE" => Color.FromArgb(200, 80, 60),
                "MAGIC_SCHOOL_LIGHT" => Color.FromArgb(220, 200, 100),
                "MAGIC_SCHOOL_SUMMONING" => Color.FromArgb(80, 180, 120),
                "MAGIC_SCHOOL_RUNIC" => Color.FromArgb(100, 160, 220),
                "MAGIC_SCHOOL_WARCRIES" => Color.FromArgb(200, 140, 60),
                _ => Color.Gray,
            };

            var panel = new Panel
            {
                Size = new Size(160, 68),
                Location = new Point(x, y),
                BackColor = Color.FromArgb(45, 45, 60),
                BorderStyle = BorderStyle.FixedSingle,
            };

            var icon = new PictureBox
            {
                Parent = panel,
                Size = new Size(48, 48),
                Location = new Point(4, 4),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = spell.Icon,
                BackColor = Color.FromArgb(30, 30, 40),
            };

            var nameLabel = new Label
            {
                Parent = panel,
                Text = spell.Name,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = schoolColor,
                Location = new Point(56, 2),
                Size = new Size(100, 30),
            };

            string costText = spell.IsRunic && spell.ResourceCost != null
                ? spell.ResourceCost.ToString()
                : $"Мана: {spell.ManaCost}";

            var costLabel = new Label
            {
                Parent = panel,
                Text = costText,
                Font = new Font("Segoe UI", 7),
                ForeColor = Color.FromArgb(180, 200, 220),
                Location = new Point(56, 34),
                Size = new Size(100, 14),
            };

            var schoolLabel = new Label
            {
                Parent = panel,
                Text = spell.SchoolDisplayName,
                Font = new Font("Segoe UI", 7),
                ForeColor = schoolColor,
                Location = new Point(56, 50),
                Size = new Size(100, 14),
            };

            var tip = new ToolTip();
            tip.SetToolTip(panel, spell.Name);
            tip.SetToolTip(icon, spell.Name);
            tip.SetToolTip(nameLabel, spell.Name);

            // Double click — spell detail card
            void ShowCard(object? s, EventArgs ev)
            {
                using var form = new SpellDetailForm(spell);
                form.ShowDialog();
            }
            panel.DoubleClick += ShowCard;
            icon.DoubleClick += ShowCard;
            nameLabel.DoubleClick += ShowCard;

            // Swap button
            var btnSwap = new Button
            {
                Parent = panel,
                Text = "↻",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Size = new Size(22, 22),
                Location = new Point(134, 44),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(70, 70, 90),
                ForeColor = Color.White,
            };
            btnSwap.FlatAppearance.BorderSize = 0;
            tip.SetToolTip(btnSwap, $"Заменить ({SwapCost} зол.)");
            btnSwap.Click += (s, e) => SwapSpell(spell);

            return panel;
        }

        private void SwapSpell(SpellInfo current)
        {
            if (!_gold.TrySpend(SwapCost))
            {
                MessageBox.Show("Недостаточно золота!", "Заклинания", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var usedIds = new HashSet<string>(_chosenSpells.Select(s => s.Id));
            usedIds.UnionWith(_chosenRunes.Select(s => s.Id));

            List<SpellInfo> pool;
            if (current.IsRunic)
            {
                pool = _allSpells
                    .Where(s => s.IsRunic && s.Level == current.Level && !usedIds.Contains(s.Id))
                    .ToList();
            }
            else if (current.IsWarcry)
            {
                pool = _allSpells
                    .Where(s => s.IsWarcry && s.Level == current.Level && !usedIds.Contains(s.Id))
                    .ToList();
            }
            else
            {
                pool = _allSpells
                    .Where(s => !s.IsRunic && !s.IsWarcry
                        && s.Level == current.Level
                        && s.MagicSchool == current.MagicSchool
                        && !usedIds.Contains(s.Id))
                    .ToList();
            }

            if (pool.Count == 0)
            {
                _gold.Refund(SwapCost);
                MessageBox.Show("Нет доступных заклинаний для замены.", "Заклинания", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var replacement = pool[_rng.Next(pool.Count)];

            if (current.IsRunic)
            {
                int idx = _chosenRunes.IndexOf(current);
                if (idx >= 0) _chosenRunes[idx] = replacement;
            }
            else
            {
                int idx = _chosenSpells.IndexOf(current);
                if (idx >= 0) _chosenSpells[idx] = replacement;
            }

            RebuildUI();
        }

        private void RefreshGold() => _goldLabel.Text = GoldText();
        private string GoldText() => $"Золото: {_gold.Remaining}";
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  КАРТОЧКА ЗАКЛИНАНИЯ
    // ─────────────────────────────────────────────────────────────────────────────
    internal class SpellDetailForm : Form
    {
        public SpellDetailForm(SpellInfo spell)
        {
            Text = spell.Name;
            Size = new Size(420, 350);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(30, 30, 40);
            ForeColor = Color.White;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var schoolColor = spell.MagicSchool switch
            {
                "MAGIC_SCHOOL_DARK" => Color.FromArgb(130, 80, 180),
                "MAGIC_SCHOOL_DESTRUCTIVE" => Color.FromArgb(200, 80, 60),
                "MAGIC_SCHOOL_LIGHT" => Color.FromArgb(220, 200, 100),
                "MAGIC_SCHOOL_SUMMONING" => Color.FromArgb(80, 180, 120),
                "MAGIC_SCHOOL_RUNIC" => Color.FromArgb(100, 160, 220),
                "MAGIC_SCHOOL_WARCRIES" => Color.FromArgb(200, 140, 60),
                _ => Color.Gray,
            };

            if (spell.Icon != null)
            {
                new PictureBox
                {
                    Parent = this,
                    Size = new Size(64, 64),
                    Location = new Point(15, 15),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Image = spell.Icon,
                    BackColor = Color.FromArgb(40, 40, 55),
                };
            }

            new Label
            {
                Parent = this,
                Text = spell.Name,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = schoolColor,
                Location = new Point(90, 15),
                AutoSize = true,
            };

            new Label
            {
                Parent = this,
                Text = $"{spell.SchoolDisplayName}  •  Уровень {spell.Level}",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(180, 200, 220),
                Location = new Point(90, 45),
                AutoSize = true,
            };

            string costText = spell.IsRunic && spell.ResourceCost != null
                ? $"Стоимость: {spell.ResourceCost}"
                : $"Мана: {spell.ManaCost}";

            new Label
            {
                Parent = this,
                Text = costText,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 200, 255),
                Location = new Point(15, 90),
                AutoSize = true,
            };

            new Label
            {
                Parent = this,
                Text = spell.Description,
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.LightGray,
                Location = new Point(15, 120),
                AutoSize = true,
                MaximumSize = new Size(375, 0),
            };
        }
    }
}