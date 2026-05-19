using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace SeleniumTests.Day5SauceDemo
{
    [TestFixture]
    public class AuthTests : BaseTest
    {
        private const string BaseUrl = "https://www.saucedemo.com";
        private const string Password = "secret_sauce";

        private void Login(string username)
        {
            Driver.Navigate().GoToUrl(BaseUrl);
            Driver.FindElement(By.Id("user-name")).SendKeys(username);
            Driver.FindElement(By.Id("password")).SendKeys(Password);
            Driver.FindElement(By.Id("login-button")).Click();
        }

        [Test]
        public void TestStandardUserLogin()
        {
            Login("standard_user");
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
            wait.Until(d => d.Url.Contains("inventory"));
            var products = Driver.FindElements(By.CssSelector("[data-test='inventory-item']"));
            Assert.That(products.Count, Is.GreaterThan(0));
        }

        [Test]
        public void TestLockedOutUser()
        {
            Login("locked_out_user");
            var error = Driver.FindElement(By.CssSelector("[data-test='error']"));
            Assert.That(error.Displayed, Is.True);
            Assert.That(error.Text, Does.Contain("locked out"));
        }

        [Test]
        public void TestProblemUserLogin()
        {
            Login("problem_user");
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
            wait.Until(d => d.Url.Contains("inventory"));
            Assert.That(Driver.Url, Does.Contain("inventory"));
        }

        [Test]
        public void TestPerformanceGlitchUser()
        {
            Login("performance_glitch_user");
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(30));
            wait.Until(d => d.Url.Contains("inventory"));
            Assert.That(Driver.Url, Does.Contain("inventory"));
        }

        [Test]
        public void TestErrorUserLogin()
        {
            Login("error_user");
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
            wait.Until(d => d.Url.Contains("inventory"));
            Assert.That(Driver.Url, Does.Contain("inventory"));
        }

        [Test]
        public void TestVisualUserLogin()
        {
            Login("visual_user");
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
            wait.Until(d => d.Url.Contains("inventory"));
            Assert.That(Driver.Url, Does.Contain("inventory"));
        }
    }
}
