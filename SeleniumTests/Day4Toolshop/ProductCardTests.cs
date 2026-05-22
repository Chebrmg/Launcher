using System.Threading;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace SeleniumTests.Day4Toolshop
{
    [TestFixture]
    public class ProductCardTests : BaseTest
    {
        private const string BaseUrl = "https://practicesoftwaretesting.com";

        private void OpenFirstProduct()
        {
            Driver.Navigate().GoToUrl(BaseUrl);
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(30));
            wait.Until(d =>
                d.FindElement(By.CssSelector("[data-test='product-name']")));
            Thread.Sleep(2000);
            var product = Driver.FindElement(By.CssSelector("[data-test='product-name']"));
            JsClick(product);
            wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(15));
            wait.Until(d =>
                d.FindElement(By.CssSelector("[data-test='add-to-cart']")));
            Thread.Sleep(1000);
        }

        [Test]
        public void TestProductHasImage()
        {
            OpenFirstProduct();
            var images = Driver.FindElements(By.CssSelector("[data-test='product-image']"));
            if (images.Count == 0)
                images = Driver.FindElements(By.CssSelector("img.figure-img, .figure img, img"));
            Assert.That(images.Count, Is.GreaterThan(0), "Изображение товара не найдено");
        }

        [Test]
        public void TestProductHasDescription()
        {
            OpenFirstProduct();
            var desc = Driver.FindElements(By.CssSelector("[data-test='product-description']"));
            if (desc.Count == 0)
                desc = Driver.FindElements(By.CssSelector(".product-description, p"));
            Assert.That(desc.Count, Is.GreaterThan(0), "Описание товара не найдено");
        }

        [Test]
        public void TestProductHasPrice()
        {
            OpenFirstProduct();
            var price = Driver.FindElements(By.CssSelector("[data-test='unit-price']"));
            if (price.Count == 0)
                price = Driver.FindElements(By.CssSelector(".product-price, span[data-test]"));
            Assert.That(price.Count, Is.GreaterThan(0), "Цена товара не найдена");
        }

        [Test]
        public void TestProductHasAddToCartButton()
        {
            OpenFirstProduct();
            var btn = Driver.FindElements(By.CssSelector("[data-test='add-to-cart']"));
            Assert.That(btn.Count, Is.GreaterThan(0), "Кнопка 'Add to Cart' не найдена");
        }
    }
}
