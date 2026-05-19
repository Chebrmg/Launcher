using System.Threading;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace SeleniumTests.Day4Toolshop
{
    [TestFixture]
    public class CartTests : BaseTest
    {
        private const string BaseUrl = "https://practicesoftwaretesting.com";

        private void AddFirstProductToCart()
        {
            Driver.Navigate().GoToUrl(BaseUrl);
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(30));
            wait.Until(d =>
                d.FindElement(By.CssSelector("[data-test='product-name']")));
            Thread.Sleep(2000);
            var product = Driver.FindElement(By.CssSelector("[data-test='product-name']"));
            JsClick(product);
            wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(30));
            wait.Until(d =>
                d.FindElement(By.CssSelector("[data-test='add-to-cart']")));
            Thread.Sleep(1000);
            JsClick(Driver.FindElement(By.CssSelector("[data-test='add-to-cart']")));
            Thread.Sleep(2000);
        }

        [Test]
        public void TestAddProductToCart()
        {
            AddFirstProductToCart();
            var cartQty = Driver.FindElements(By.CssSelector("[data-test='cart-quantity']"));
            if (cartQty.Count > 0)
            {
                var text = cartQty[0].Text.Trim();
                Assert.That(text, Is.Not.Empty.And.Not.EqualTo("0"),
                    "Корзина пуста после добавления");
            }
        }

        [Test]
        public void TestChangeQuantity()
        {
            AddFirstProductToCart();
            JsClick(Driver.FindElement(By.CssSelector("[data-test='nav-cart']")));
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(30));
            wait.Until(d =>
                d.FindElement(By.CssSelector("[data-test='product-quantity']")));
            var qtyInput = Driver.FindElement(By.CssSelector("[data-test='product-quantity']"));
            qtyInput.Clear();
            qtyInput.SendKeys("3");
            qtyInput.SendKeys("\t");
            Thread.Sleep(2000);
            var updatedQty = Driver.FindElement(By.CssSelector("[data-test='product-quantity']"));
            Assert.That(updatedQty.GetAttribute("value"), Is.EqualTo("3"));
        }

        [Test]
        public void TestRemoveProductFromCart()
        {
            AddFirstProductToCart();
            JsClick(Driver.FindElement(By.CssSelector("[data-test='nav-cart']")));
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(30));
            wait.Until(d =>
                d.FindElement(By.CssSelector("[data-test='product-quantity']")));
            var removeButtons = Driver.FindElements(By.CssSelector(".btn-danger"));
            if (removeButtons.Count > 0)
            {
                JsClick(removeButtons[0]);
                Thread.Sleep(2000);
            }
            var remaining = Driver.FindElements(By.CssSelector("[data-test='product-quantity']"));
            Assert.That(
                remaining.Count == 0 || Driver.PageSource.ToLower().Contains("empty"),
                Is.True);
        }
    }
}
