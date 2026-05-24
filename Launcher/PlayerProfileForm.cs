using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;

namespace Launcher
{
    public class PlayerProfileForm : Form
    {
        private readonly UserAccount _viewer;
        private readonly bool _isAdmin;
        private ListView _duelList = null!;
        private RichTextBox _detailBox = null!;
        private List<DuelRecord> _duels = new();

        public PlayerProfileForm(UserAccount viewer)
        {
            _viewer = viewer;
            _isAdmin = viewer.IsAdmin;
            Text = _isAdmin ? "Админ-панель — Дуэли" : $"Профиль — {viewer.Username}";
            Size = new Size(950, 620);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(30, 30, 40);
            ForeColor = Color.White;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            BuildUI();
            LoadDuels();
        }

        private void BuildUI()
        {
            new Label
            {
                Parent = this,
                Text = _isAdmin ? "Все дуэли" : $"Дуэли игрока: {_viewer.Username}",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 220, 100),
                AutoSize = true,
                Location = new Point(15, 10),
            };

            _duelList = new ListView
            {
                Parent = this,
                Location = new Point(15, 50),
                Size = new Size(900, 250),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(40, 40, 55),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
            };
            _duelList.Columns.Add("ID", 40);
            _duelList.Columns.Add("Дата", 140);
            _duelList.Columns.Add("Игрок 1", 130);
            _duelList.Columns.Add("Фракция 1", 110);
            _duelList.Columns.Add("Герой 1", 100);
            _duelList.Columns.Add("Ур.", 40);
            _duelList.Columns.Add("Игрок 2", 130);
            _duelList.Columns.Add("Фракция 2", 110);
            _duelList.Columns.Add("Герой 2", 100);
            _duelList.Columns.Add("Ур.", 40);
            _duelList.SelectedIndexChanged += DuelList_Selected;

            _detailBox = new RichTextBox
            {
                Parent = this,
                Location = new Point(15, 310),
                Size = new Size(900, 250),
                ReadOnly = true,
                BackColor = Color.FromArgb(35, 35, 50),
                ForeColor = Color.White,
                Font = new Font("Consolas", 10),
                BorderStyle = BorderStyle.None,
            };

            if (_isAdmin)
            {
                var btnManageUsers = new Button
                {
                    Parent = this,
                    Text = "Управление пользователями",
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    Size = new Size(240, 32),
                    Location = new Point(680, 12),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(180, 60, 60),
                    ForeColor = Color.White,
                };
                btnManageUsers.FlatAppearance.BorderSize = 0;
                btnManageUsers.Click += (s, e) =>
                {
                    using var form = new AdminUsersForm();
                    form.ShowDialog(this);
                };
            }
        }

        private void LoadDuels()
        {
            _duels = _isAdmin
                ? DatabaseManager.GetDuels()
                : DatabaseManager.GetDuels(_viewer.Id);

            _duelList.Items.Clear();
            foreach (var d in _duels)
            {
                var item = new ListViewItem(d.Id.ToString());
                item.SubItems.Add(d.PlayedAt.ToString("yyyy-MM-dd HH:mm"));
                item.SubItems.Add(d.Player1Name);
                item.SubItems.Add(d.Player1Faction);
                item.SubItems.Add(d.Player1Hero);
                item.SubItems.Add(d.Player1Level.ToString());
                item.SubItems.Add(d.Player2Name);
                item.SubItems.Add(d.Player2Faction);
                item.SubItems.Add(d.Player2Hero);
                item.SubItems.Add(d.Player2Level.ToString());
                item.Tag = d;
                _duelList.Items.Add(item);
            }
        }

        private void DuelList_Selected(object? sender, EventArgs e)
        {
            if (_duelList.SelectedItems.Count == 0) return;
            var duel = (DuelRecord)_duelList.SelectedItems[0].Tag;

            var snap1 = DatabaseManager.GetDuelHero(duel.Id, duel.Player1Id);
            var snap2 = DatabaseManager.GetDuelHero(duel.Id, duel.Player2Id);

            _detailBox.Clear();
            if (snap1 != null) AppendSnapshot("Игрок 1", snap1);
            if (snap2 != null) AppendSnapshot("Игрок 2", snap2);
        }

        private void AppendSnapshot(string label, DuelHeroSnapshot snap)
        {
            _detailBox.SelectionColor = Color.FromArgb(255, 220, 100);
            _detailBox.AppendText($"═══ {label}: {snap.PlayerName} ═══\n");
            _detailBox.SelectionColor = Color.White;

            _detailBox.AppendText($"Фракция: {snap.Faction}   Герой: {snap.HeroName}   Уровень: {snap.HeroLevel}\n");
            _detailBox.AppendText($"Атака: {snap.Offence}  Защита: {snap.Defence}  Сила магии: {snap.Spellpower}  Знание: {snap.Knowledge}\n\n");

            _detailBox.SelectionColor = Color.FromArgb(100, 200, 255);
            _detailBox.AppendText("Армия:\n");
            _detailBox.SelectionColor = Color.White;
            try
            {
                var army = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(snap.ArmyJson);
                if (army != null)
                    foreach (var slot in army)
                    {
                        string creature = slot.TryGetValue("Creature", out var c) ? c.ToString()! : "?";
                        string count = slot.TryGetValue("Count", out var n) ? n.ToString()! : "0";
                        _detailBox.AppendText($"  {creature} x{count}\n");
                    }
            }
            catch { _detailBox.AppendText($"  {snap.ArmyJson}\n"); }

            _detailBox.SelectionColor = Color.FromArgb(100, 200, 255);
            _detailBox.AppendText("\nСкиллы:\n");
            _detailBox.SelectionColor = Color.White;
            try
            {
                var skills = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(snap.SkillsJson);
                if (skills != null)
                    foreach (var sk in skills)
                    {
                        string sid = sk.TryGetValue("SkillId", out var s) ? s.ToString()! : "?";
                        string mastery = sk.TryGetValue("Mastery", out var m) ? m.ToString()! : "0";
                        _detailBox.AppendText($"  {sid} [{mastery}]\n");
                    }
            }
            catch { _detailBox.AppendText($"  {snap.SkillsJson}\n"); }

            _detailBox.SelectionColor = Color.FromArgb(100, 200, 255);
            _detailBox.AppendText("\nПерки:\n");
            _detailBox.SelectionColor = Color.White;
            try
            {
                var perks = JsonSerializer.Deserialize<List<string>>(snap.PerksJson);
                if (perks != null)
                    foreach (var p in perks)
                        _detailBox.AppendText($"  {p}\n");
            }
            catch { _detailBox.AppendText($"  {snap.PerksJson}\n"); }

            _detailBox.SelectionColor = Color.FromArgb(100, 200, 255);
            _detailBox.AppendText("\nЗаклинания:\n");
            _detailBox.SelectionColor = Color.White;
            try
            {
                var spells = JsonSerializer.Deserialize<List<string>>(snap.SpellsJson);
                if (spells != null)
                    foreach (var sp in spells)
                        _detailBox.AppendText($"  {sp}\n");
            }
            catch { _detailBox.AppendText($"  {snap.SpellsJson}\n"); }

            _detailBox.SelectionColor = Color.FromArgb(100, 200, 255);
            _detailBox.AppendText("\nАртефакты:\n");
            _detailBox.SelectionColor = Color.White;
            try
            {
                var arts = JsonSerializer.Deserialize<List<string>>(snap.ArtifactsJson);
                if (arts != null)
                    foreach (var a in arts)
                        _detailBox.AppendText($"  {a}\n");
            }
            catch { _detailBox.AppendText($"  {snap.ArtifactsJson}\n"); }

            _detailBox.AppendText("\n");
        }
    }

    public class AdminUsersForm : Form
    {
        private ListView _userList = null!;

        public AdminUsersForm()
        {
            Text = "Управление пользователями";
            Size = new Size(600, 450);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(30, 30, 40);
            ForeColor = Color.White;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            _userList = new ListView
            {
                Parent = this,
                Location = new Point(15, 15),
                Size = new Size(550, 300),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(40, 40, 55),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
            };
            _userList.Columns.Add("ID", 50);
            _userList.Columns.Add("Имя", 200);
            _userList.Columns.Add("Админ", 70);
            _userList.Columns.Add("Создан", 180);

            var btnToggleAdmin = new Button
            {
                Parent = this,
                Text = "Переключить админа",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Size = new Size(200, 35),
                Location = new Point(15, 325),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 120, 200),
                ForeColor = Color.White,
            };
            btnToggleAdmin.FlatAppearance.BorderSize = 0;
            btnToggleAdmin.Click += (s, e) =>
            {
                if (_userList.SelectedItems.Count == 0) return;
                var user = (UserAccount)_userList.SelectedItems[0].Tag;
                DatabaseManager.SetAdmin(user.Id, !user.IsAdmin);
                LoadUsers();
            };

            var btnDelete = new Button
            {
                Parent = this,
                Text = "Удалить",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Size = new Size(120, 35),
                Location = new Point(230, 325),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(180, 50, 50),
                ForeColor = Color.White,
            };
            btnDelete.FlatAppearance.BorderSize = 0;
            btnDelete.Click += (s, e) =>
            {
                if (_userList.SelectedItems.Count == 0) return;
                var user = (UserAccount)_userList.SelectedItems[0].Tag;
                if (user.IsAdmin)
                {
                    MessageBox.Show("Нельзя удалить администратора", "Ошибка",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                var result = MessageBox.Show($"Удалить пользователя {user.Username}?",
                    "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    DatabaseManager.DeleteUser(user.Id);
                    LoadUsers();
                }
            };

            LoadUsers();
        }

        private void LoadUsers()
        {
            _userList.Items.Clear();
            foreach (var u in DatabaseManager.GetAllUsers())
            {
                var item = new ListViewItem(u.Id.ToString());
                item.SubItems.Add(u.Username);
                item.SubItems.Add(u.IsAdmin ? "Да" : "Нет");
                item.SubItems.Add(u.CreatedAt.ToString("yyyy-MM-dd HH:mm"));
                item.Tag = u;
                _userList.Items.Add(item);
            }
        }
    }
}
