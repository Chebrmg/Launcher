using System.Threading;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace SeleniumTests.Day5SauceDemo
{
    [TestFixture]
    public class CheckoutTests : BaseTest
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

        private void AddAndGoToCart(params string[] items)
        {
            if (items.Length == 0)
                items = new[] { "sauce-labs-backpack" };
            foreach (var item in items)
            {
                JsClick(Driver.FindElement(
                    By.CssSelector($"[data-test='add-to-cart-{item}']")));
                Thread.Sleep(500);
            }
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
            wait.Until(d =>
                d.FindElement(By.CssSelector("[data-test='shopping-cart-badge']")));
            JsClick(Driver.FindElement(By.CssSelector("[data-test='shopping-cart-link']")));
            wait.Until(d => d.Url.Contains("cart"));
        }

        private void FillCheckoutForm(string first = "Ivan", string last = "Petrov",
            string postal = "123456")
        {
            SetReactInput("[data-test='firstName']", first);
            SetReactInput("[data-test='lastName']", last);
            SetReactInput("[data-test='postalCode']", postal);
            Thread.Sleep(500);
        }

        [Test]
        public void TestFullCheckoutFlow()
        {
            Login();
            AddAndGoToCart();
            var items = Driver.FindElements(By.CssSelector(".cart_item"));
            Assert.That(items.Count, Is.EqualTo(1));

            JsClick(Driver.FindElement(By.CssSelector("[data-test='checkout']")));
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
            wait.Until(d => d.Url.Contains("checkout-step-one"));

            FillCheckoutForm();
            JsClick(Driver.FindElement(By.CssSelector("[data-test='continue']")));

            wait.Until(d => d.Url.Contains("checkout-step-two"));
            var summaryItems = Driver.FindElements(By.CssSelector(".cart_item"));
            Assert.That(summaryItems.Count, Is.EqualTo(1));
            var total = Driver.FindElement(By.CssSelector("[data-test='total-label']"));
            Assert.That(total.Text, Does.Contain("$"));

            JsClick(Driver.FindElement(By.CssSelector("[data-test='finish']")));
            wait.Until(d => d.Url.Contains("checkout-complete"));
            var header = Driver.FindElement(By.CssSelector("[data-test='complete-header']"));
            Assert.That(header.Text, Does.Contain("Thank you for your order"));
        }

        [Test]
        public void TestCheckoutRequiresInfo()
        {
            Login();
            AddAndGoToCart();
            JsClick(Driver.FindElement(By.CssSelector("[data-test='checkout']")));
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
            wait.Until(d => d.Url.Contains("checkout-step-one"));
            JsClick(Driver.FindElement(By.CssSelector("[data-test='continue']")));
            Thread.Sleep(1000);
            var error = Driver.FindElement(By.CssSelector("[data-test='error']"));
            Assert.That(error.Displayed, Is.True);
        }

        [Test]
        public void TestCheckoutWithMultipleItems()
        {
            Login();
            AddAndGoToCart("sauce-labs-backpack", "sauce-labs-bike-light");
            var items = Driver.FindElements(By.CssSelector(".cart_item"));
            Assert.That(items.Count, Is.EqualTo(2));

            JsClick(Driver.FindElement(By.CssSelector("[data-test='checkout']")));
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
            wait.Until(d => d.Url.Contains("checkout-step-one"));
            FillCheckoutForm("Test", "User", "00000");
            JsClick(Driver.FindElement(By.CssSelector("[data-test='continue']")));

            wait.Until(d => d.Url.Contains("checkout-step-two"));
            var summaryItems = Driver.FindElements(By.CssSelector(".cart_item"));
            Assert.That(summaryItems.Count, Is.EqualTo(2));

            JsClick(Driver.FindElement(By.CssSelector("[data-test='finish']")));
            wait.Until(d => d.Url.Contains("checkout-complete"));
            var header = Driver.FindElement(By.CssSelector("[data-test='complete-header']"));
            Assert.That(header.Text, Does.Contain("Thank you for your order"));
        }
    }
}
