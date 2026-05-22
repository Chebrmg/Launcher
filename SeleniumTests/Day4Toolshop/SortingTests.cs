using System.Globalization;
using System.Threading;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace SeleniumTests.Day4Toolshop
{
    [TestFixture]
    public class SortingTests : BaseTest
    {
        private const string BaseUrl = "https://practicesoftwaretesting.com";

        private void WaitForProducts()
        {
            Driver.Navigate().GoToUrl(BaseUrl);
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(30));
            wait.Until(d =>
                d.FindElement(By.CssSelector("[data-test='product-name']")));
            Thread.Sleep(2000);
        }

        private List<double> GetPrices()
        {
            var priceElements = Driver.FindElements(By.CssSelector("[data-test='product-price']"));
            var prices = new List<double>();
            foreach (var p in priceElements)
            {
                var text = p.Text.Replace("$", "").Replace(",", "").Trim();
                if (!string.IsNullOrEmpty(text) &&
                    double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
                    prices.Add(val);
            }
            return prices;
        }

        [Test]
        public void TestSortByPriceLowToHigh()
        {
            WaitForProducts();
            var sortSelect = new SelectElement(
                Driver.FindElement(By.CssSelector("[data-test='sort']")));
            sortSelect.SelectByText("Price (Low - High)");
            Thread.Sleep(3000);
            var prices = GetPrices();
            if (prices.Count > 1)
            {
                var sorted = prices.OrderBy(p => p).ToList();
                Assert.That(prices, Is.EqualTo(sorted), "Цены не отсортированы по возрастанию");
            }
        }

        [Test]
        public void TestSortByPriceHighToLow()
        {
            WaitForProducts();
            var sortSelect = new SelectElement(
                Driver.FindElement(By.CssSelector("[data-test='sort']")));
            sortSelect.SelectByText("Price (High - Low)");
            Thread.Sleep(3000);
            var prices = GetPrices();
            if (prices.Count > 1)
            {
                var sorted = prices.OrderByDescending(p => p).ToList();
                Assert.That(prices, Is.EqualTo(sorted), "Цены не отсортированы по убыванию");
            }
        }

        [Test]
        public void TestPricesDisplayed()
        {
            WaitForProducts();
            var products = Driver.FindElements(By.CssSelector("[data-test='product-name']"));
            var prices = Driver.FindElements(By.CssSelector("[data-test='product-price']"));
            Assert.That(prices.Count, Is.EqualTo(products.Count),
                "Не у всех товаров отображается цена");
            foreach (var p in prices)
                Assert.That(p.Text.Trim(), Is.Not.Empty, "Цена пустая");
        }

        [Test]
        public void TestSortByNameAZ()
        {
            WaitForProducts();
            var sortSelect = new SelectElement(
                Driver.FindElement(By.CssSelector("[data-test='sort']")));
            sortSelect.SelectByText("Name (A - Z)");
            Thread.Sleep(3000);
            var names = Driver.FindElements(By.CssSelector("[data-test='product-name']"))
                .Where(n => !string.IsNullOrWhiteSpace(n.Text))
                .Select(n => n.Text.ToLower())
                .ToList();
            if (names.Count > 1)
            {
                var sorted = names.OrderBy(n => n).ToList();
                Assert.That(names, Is.EqualTo(sorted), "Товары не отсортированы по имени A-Z");
            }
        }
    }
}
