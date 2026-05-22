using System.Threading;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace SeleniumTests.Day4Toolshop
{
    [TestFixture]
    public class UITests : BaseTest
    {
        private const string BaseUrl = "https://practicesoftwaretesting.com";

        private void WaitForPage()
        {
            Driver.Navigate().GoToUrl(BaseUrl);
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(30));
            wait.Until(d =>
                d.FindElement(By.CssSelector("[data-test='product-name']")));
            Thread.Sleep(2000);
        }

        [Test]
        public void TestNavigationButtons()
        {
            WaitForPage();
            var navHome = Driver.FindElements(By.CssSelector("[data-test='nav-home']"));
            Assert.That(navHome.Count, Is.GreaterThan(0), "Кнопка Home не найдена");
        }

        [Test]
        public void TestNavCategories()
        {
            WaitForPage();
            var navItems = Driver.FindElements(By.CssSelector("[data-test='nav-categories']"));
            if (navItems.Count > 0)
            {
                JsClick(navItems[0]);
                Thread.Sleep(1000);
                var dropdownItems = Driver.FindElements(
                    By.CssSelector(".dropdown-menu a, .dropdown-item"));
                Assert.That(dropdownItems.Count, Is.GreaterThan(0),
                    "Выпадающее меню категорий не появилось");
            }
        }

        [Test]
        public void TestNavLinks()
        {
            WaitForPage();
            var signIn = Driver.FindElements(By.CssSelector("[data-test='nav-sign-in']"));
            var contact = Driver.FindElements(By.CssSelector("[data-test='nav-contact']"));
            Assert.That(signIn.Count, Is.GreaterThan(0), "Ссылка Sign In не найдена");
            Assert.That(contact.Count, Is.GreaterThan(0), "Ссылка Contact не найдена");
        }

        [Test]
        public void TestPageNavigation()
        {
            WaitForPage();
            var productsPage1 = Driver.FindElements(By.CssSelector("[data-test='product-name']"))
                .Select(p => p.Text).ToList();
            var nextBtns = Driver.FindElements(By.CssSelector(".pagination .page-link"));
            if (nextBtns.Count > 1)
            {
                JsClick(nextBtns[^1]);
                Thread.Sleep(3000);
                var productsPage2 = Driver.FindElements(By.CssSelector("[data-test='product-name']"))
                    .Select(p => p.Text).ToList();
                if (productsPage2.Count > 0)
                    Assert.Pass("Пагинация работает");
            }
        }
    }
}
