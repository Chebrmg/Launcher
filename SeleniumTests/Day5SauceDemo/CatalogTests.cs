using System.Globalization;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace SeleniumTests.Day5SauceDemo
{
    [TestFixture]
    public class CatalogTests : BaseTest
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
        public void TestProductsDisplayed()
        {
            Login();
            var items = Driver.FindElements(By.CssSelector("[data-test='inventory-item']"));
            Assert.That(items.Count, Is.EqualTo(6));
        }

        [Test]
        public void TestProductImages()
        {
            Login();
            var images = Driver.FindElements(By.CssSelector(".inventory_item_img img"));
            Assert.That(images.Count, Is.EqualTo(6));
            foreach (var img in images)
            {
                var src = img.GetAttribute("src");
                Assert.That(src, Is.Not.Empty);
            }
        }

        [Test]
        public void TestProductPrices()
        {
            Login();
            var prices = Driver.FindElements(By.CssSelector("[data-test='inventory-item-price']"));
            Assert.That(prices.Count, Is.EqualTo(6));
            foreach (var p in prices)
                Assert.That(p.Text, Does.StartWith("$"));
        }

        [Test]
        public void TestSortAZ()
        {
            Login();
            var sortSelect = new SelectElement(
                Driver.FindElement(By.CssSelector("[data-test='product-sort-container']")));
            sortSelect.SelectByValue("az");
            var names = Driver.FindElements(By.CssSelector("[data-test='inventory-item-name']"))
                .Select(n => n.Text).ToList();
            var sorted = names.OrderBy(n => n).ToList();
            Assert.That(names, Is.EqualTo(sorted));
        }

        [Test]
        public void TestSortZA()
        {
            Login();
            var sortSelect = new SelectElement(
                Driver.FindElement(By.CssSelector("[data-test='product-sort-container']")));
            sortSelect.SelectByValue("za");
            var names = Driver.FindElements(By.CssSelector("[data-test='inventory-item-name']"))
                .Select(n => n.Text).ToList();
            var sorted = names.OrderByDescending(n => n).ToList();
            Assert.That(names, Is.EqualTo(sorted));
        }

        [Test]
        public void TestSortPriceLowHigh()
        {
            Login();
            var sortSelect = new SelectElement(
                Driver.FindElement(By.CssSelector("[data-test='product-sort-container']")));
            sortSelect.SelectByValue("lohi");
            var prices = Driver.FindElements(By.CssSelector("[data-test='inventory-item-price']"))
                .Select(p => double.Parse(p.Text.Replace("$", ""), CultureInfo.InvariantCulture))
                .ToList();
            var sorted = prices.OrderBy(p => p).ToList();
            Assert.That(prices, Is.EqualTo(sorted));
        }

        [Test]
        public void TestSortPriceHighLow()
        {
            Login();
            var sortSelect = new SelectElement(
                Driver.FindElement(By.CssSelector("[data-test='product-sort-container']")));
            sortSelect.SelectByValue("hilo");
            var prices = Driver.FindElements(By.CssSelector("[data-test='inventory-item-price']"))
                .Select(p => double.Parse(p.Text.Replace("$", ""), CultureInfo.InvariantCulture))
                .ToList();
            var sorted = prices.OrderByDescending(p => p).ToList();
            Assert.That(prices, Is.EqualTo(sorted));
        }
    }
}
