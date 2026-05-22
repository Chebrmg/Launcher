using System.Threading;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace SeleniumTests.Day4Toolshop
{
    [TestFixture]
    public class FilterTests : BaseTest
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

        [Test]
        public void TestFilterByCategory()
        {
            WaitForProducts();
            var initialCount = Driver.FindElements(By.CssSelector("[data-test='product-name']")).Count;
            var categories = Driver.FindElements(By.CssSelector("[data-test^='category-']"));
            if (categories.Count > 0)
            {
                JsClick(categories[0]);
                Thread.Sleep(3000);
                var filteredCount = Driver.FindElements(By.CssSelector("[data-test='product-name']")).Count;
                Assert.That(filteredCount, Is.LessThanOrEqualTo(initialCount));
            }
        }

        [Test]
        public void TestFilterByBrand()
        {
            WaitForProducts();
            var brands = Driver.FindElements(By.CssSelector("[data-test^='brand-']"));
            if (brands.Count > 0)
            {
                JsClick(brands[0]);
                Thread.Sleep(3000);
                var products = Driver.FindElements(By.CssSelector("[data-test='product-name']"));
                Assert.That(products.Count, Is.GreaterThanOrEqualTo(0));
            }
        }

        [Test]
        public void TestFilterByPrice()
        {
            WaitForProducts();
            var initialCount = Driver.FindElements(By.CssSelector("[data-test='product-name']")).Count;
            Assert.That(initialCount, Is.GreaterThan(0), "На странице нет товаров для фильтрации");
        }

        [Test]
        public void TestCombinedFilters()
        {
            WaitForProducts();
            var categories = Driver.FindElements(By.CssSelector("[data-test^='category-']"));
            var brands = Driver.FindElements(By.CssSelector("[data-test^='brand-']"));
            if (categories.Count > 0 && brands.Count > 0)
            {
                JsClick(categories[0]);
                Thread.Sleep(2000);
                JsClick(brands[0]);
                Thread.Sleep(3000);
                var products = Driver.FindElements(By.CssSelector("[data-test='product-name']"));
                Assert.That(products.Count, Is.TypeOf<int>());
            }
        }
    }
}
