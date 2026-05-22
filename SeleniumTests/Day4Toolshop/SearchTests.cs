using System.Threading;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace SeleniumTests.Day4Toolshop
{
    [TestFixture]
    public class SearchTests : BaseTest
    {
        private const string BaseUrl = "https://practicesoftwaretesting.com";

        private void WaitForPage()
        {
            Driver.Navigate().GoToUrl(BaseUrl);
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(30));
            wait.Until(d =>
                d.FindElement(By.CssSelector("[data-test='search-query']")));
            Thread.Sleep(2000);
        }

        private void Search(string query)
        {
            WaitForPage();
            var input = Driver.FindElement(By.CssSelector("[data-test='search-query']"));
            input.Clear();
            input.SendKeys(query);
            Driver.FindElement(By.CssSelector("[data-test='search-submit']")).Click();
            Thread.Sleep(3000);
        }

        [Test]
        public void TestSearchExistingProduct()
        {
            Search("Pliers");
            var products = Driver.FindElements(By.CssSelector("[data-test='product-name']"));
            Assert.That(products.Count, Is.GreaterThan(0), "Товары не найдены при поиске 'Pliers'");
            var found = products.Any(p => p.Text.ToLower().Contains("pliers"));
            Assert.That(found, Is.True, "Ни один товар не содержит 'Pliers' в названии");
        }

        [Test]
        public void TestSearchNonexistentProduct()
        {
            Search("xyznonexistent12345");
            Thread.Sleep(2000);
            var products = Driver.FindElements(By.CssSelector("[data-test='product-name']"));
            Assert.That(products.Count, Is.EqualTo(0), "Найдены товары для несуществующего запроса");
        }

        [Test]
        public void TestPartialSearch()
        {
            Search("Ham");
            var products = Driver.FindElements(By.CssSelector("[data-test='product-name']"));
            Assert.That(products.Count, Is.GreaterThan(0), "Частичный поиск не вернул результатов");
        }
    }
}
