using System.Threading;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace SeleniumTests.Day5SauceDemo
{
    [TestFixture]
    public class CartTests : BaseTest
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

        [Test]
        public void TestAddProductToCart()
        {
            Login();
            var btn = Driver.FindElement(
                By.CssSelector("[data-test='add-to-cart-sauce-labs-backpack']"));
            JsClick(btn);
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
            var badge = wait.Until(d =>
                d.FindElement(By.CssSelector("[data-test='shopping-cart-badge']")));
            Assert.That(badge.Text, Is.EqualTo("1"));

            JsClick(Driver.FindElement(By.CssSelector("[data-test='shopping-cart-link']")));
            wait.Until(d => d.Url.Contains("cart"));
            var items = Driver.FindElements(By.CssSelector(".cart_item"));
            Assert.That(items.Count, Is.EqualTo(1));
        }

        [Test]
        public void TestRemoveProductFromCart()
        {
            Login();
            JsClick(Driver.FindElement(
                By.CssSelector("[data-test='add-to-cart-sauce-labs-backpack']")));
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
            wait.Until(d =>
                d.FindElement(By.CssSelector("[data-test='shopping-cart-badge']")));
            JsClick(Driver.FindElement(By.CssSelector("[data-test='shopping-cart-link']")));
            wait.Until(d => d.Url.Contains("cart"));
            JsClick(Driver.FindElement(
                By.CssSelector("[data-test='remove-sauce-labs-backpack']")));
            Thread.Sleep(1000);
            var items = Driver.FindElements(By.CssSelector(".cart_item"));
            Assert.That(items.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestCartBadgeCount()
        {
            Login();
            JsClick(Driver.FindElement(
                By.CssSelector("[data-test='add-to-cart-sauce-labs-backpack']")));
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
            var badge = wait.Until(d =>
                d.FindElement(By.CssSelector("[data-test='shopping-cart-badge']")));
            Assert.That(badge.Text, Is.EqualTo("1"));

            JsClick(Driver.FindElement(
                By.CssSelector("[data-test='add-to-cart-sauce-labs-bike-light']")));
            Thread.Sleep(1000);
            badge = Driver.FindElement(By.CssSelector("[data-test='shopping-cart-badge']"));
            Assert.That(badge.Text, Is.EqualTo("2"));

            JsClick(Driver.FindElement(
                By.CssSelector("[data-test='remove-sauce-labs-backpack']")));
            Thread.Sleep(1000);
            badge = Driver.FindElement(By.CssSelector("[data-test='shopping-cart-badge']"));
            Assert.That(badge.Text, Is.EqualTo("1"));
        }
    }
}
