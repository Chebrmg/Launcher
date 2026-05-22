using System.Threading;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace SeleniumTests.Day5SauceDemo
{
    [TestFixture]
    public class LogoutTests : BaseTest
    {
        private const string BaseUrl = "https://www.saucedemo.com";
        private const string Password = "secret_sauce";

        private void Login()
        {
            Driver.Navigate().GoToUrl(BaseUrl);
            Driver.FindElement(By.Id("user-name")).SendKeys("standard_user");
            Driver.FindElement(By.Id("password")).SendKeys(Password);
            Driver.FindElement(By.Id("login-button")).Click();
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
            wait.Until(d => d.Url.Contains("inventory"));
        }

        private void Logout()
        {
            var burger = Driver.FindElement(By.Id("react-burger-menu-btn"));
            JsClick(burger);
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
            var logout = wait.Until(d =>
            {
                var el = d.FindElement(By.Id("logout_sidebar_link"));
                return el.Displayed && el.Enabled ? el : null;
            })!;
            JsClick(logout);
            wait.Until(d => d.FindElement(By.Id("login-button")));
        }

        [Test]
        public void TestLogoutButton()
        {
            Login();
            Logout();
            Assert.That(Driver.Url, Does.Contain("saucedemo.com"));
            Assert.That(Driver.FindElement(By.Id("login-button")).Displayed, Is.True);
        }

        [Test]
        public void TestLogoutReturnsToLoginForm()
        {
            Login();
            Logout();
            Assert.That(Driver.FindElement(By.Id("login-button")).Displayed, Is.True);
            Assert.That(Driver.FindElement(By.Id("user-name")).Displayed, Is.True);
        }

        [Test]
        public void TestSessionTerminatedAfterLogout()
        {
            Login();
            Logout();
            Driver.Navigate().GoToUrl(BaseUrl + "/inventory.html");
            Thread.Sleep(1000);
            var errors = Driver.FindElements(By.CssSelector("[data-test='error']"));
            var loginBtns = Driver.FindElements(By.Id("login-button"));
            Assert.That(errors.Count > 0 || loginBtns.Count > 0, Is.True,
                "Сессия не завершена после logout");
        }
    }
}
