using System;
using System.Drawing;
using System.Windows.Forms;

namespace Launcher
{
    public class LoginForm : Form
    {
        public UserAccount? LoggedInUser { get; private set; }

        private TextBox _txtUser = null!;
        private TextBox _txtPass = null!;
        private Label _lblError = null!;

        public LoginForm()
        {
            Text = "Авторизация";
            Size = new Size(400, 340);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(30, 30, 40);
            ForeColor = Color.White;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            new Label
            {
                Parent = this,
                Text = "Вход в аккаунт",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 220, 100),
                AutoSize = true,
                Location = new Point(110, 20),
            };

            new Label
            {
                Parent = this,
                Text = "Имя:",
                Font = new Font("Segoe UI", 11),
                AutoSize = true,
                Location = new Point(40, 80),
            };

            _txtUser = new TextBox
            {
                Parent = this,
                Location = new Point(130, 78),
                Size = new Size(210, 28),
                Font = new Font("Segoe UI", 11),
                BackColor = Color.FromArgb(50, 50, 65),
                ForeColor = Color.White,
            };

            new Label
            {
                Parent = this,
                Text = "Пароль:",
                Font = new Font("Segoe UI", 11),
                AutoSize = true,
                Location = new Point(40, 120),
            };

            _txtPass = new TextBox
            {
                Parent = this,
                Location = new Point(130, 118),
                Size = new Size(210, 28),
                Font = new Font("Segoe UI", 11),
                BackColor = Color.FromArgb(50, 50, 65),
                ForeColor = Color.White,
                UseSystemPasswordChar = true,
            };

            var btnLogin = new Button
            {
                Parent = this,
                Text = "Войти",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Size = new Size(140, 38),
                Location = new Point(40, 175),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 120, 200),
                ForeColor = Color.White,
            };
            btnLogin.FlatAppearance.BorderSize = 0;
            btnLogin.Click += BtnLogin_Click;

            var btnRegister = new Button
            {
                Parent = this,
                Text = "Регистрация",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Size = new Size(140, 38),
                Location = new Point(200, 175),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 160, 50),
                ForeColor = Color.White,
            };
            btnRegister.FlatAppearance.BorderSize = 0;
            btnRegister.Click += BtnRegister_Click;

            _lblError = new Label
            {
                Parent = this,
                Text = "",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(255, 100, 100),
                Size = new Size(320, 40),
                Location = new Point(40, 230),
            };

            AcceptButton = btnLogin;
        }

        private void BtnLogin_Click(object? sender, EventArgs e)
        {
            string user = _txtUser.Text.Trim();
            string pass = _txtPass.Text;

            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
            {
                _lblError.Text = "Заполните все поля";
                return;
            }

            var account = DatabaseManager.Authenticate(user, pass);
            if (account == null)
            {
                _lblError.Text = "Неверное имя или пароль";
                return;
            }

            LoggedInUser = account;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void BtnRegister_Click(object? sender, EventArgs e)
        {
            string user = _txtUser.Text.Trim();
            string pass = _txtPass.Text;

            var (ok, error) = DatabaseManager.Register(user, pass);
            if (!ok)
            {
                _lblError.Text = error;
                return;
            }

            var account = DatabaseManager.Authenticate(user, pass);
            if (account == null)
            {
                _lblError.Text = "Ошибка после регистрации";
                return;
            }

            LoggedInUser = account;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
