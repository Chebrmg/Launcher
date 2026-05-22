using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace SeleniumTests.Day3Banking
{
    [TestFixture]
    public class OpenAccountTests : BaseTest
    {
        private const string BaseUrl =
            "https://www.globalsqa.com/angularJs-protractor/BankingProject/#/login";

        private void CreateCustomerAndOpenAccountPage()
        {
            Driver.Navigate().GoToUrl(BaseUrl);
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
            wait.Until(d =>
                d.FindElement(By.XPath("//button[contains(text(),'Bank Manager Login')]"))).Click();
            wait.Until(d =>
                d.FindElement(By.XPath("//button[@ng-click='addCust()']"))).Click();
            wait.Until(d =>
                d.FindElement(By.XPath("//input[@ng-model='fName']")));
            Driver.FindElement(By.XPath("//input[@ng-model='fName']")).SendKeys("Test");
            Driver.FindElement(By.XPath("//input[@ng-model='lName']")).SendKeys("User");
            Driver.FindElement(By.XPath("//input[@ng-model='postCd']")).SendKeys("E99999");
            Driver.FindElement(By.XPath("//button[@type='submit']")).Click();
            wait.Until(d =>
            {
                try { d.SwitchTo().Alert(); return true; }
                catch { return false; }
            });
            Driver.SwitchTo().Alert().Accept();

            Driver.FindElement(By.XPath("//button[@ng-click='openAccount()']")).Click();
            wait.Until(d => d.FindElement(By.Id("userSelect")));
        }

        [Test]
        public void TestSelectCustomerFromList()
        {
            CreateCustomerAndOpenAccountPage();
            var userSelect = new SelectElement(Driver.FindElement(By.Id("userSelect")));
            var options = userSelect.Options;
            Assert.That(options.Count, Is.GreaterThan(1));
        }

        [Test]
        public void TestSelectCurrency()
        {
            CreateCustomerAndOpenAccountPage();
            var currencySelect = new SelectElement(Driver.FindElement(By.Id("currency")));
            var options = currencySelect.Options;
            Assert.That(options.Count, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void TestCreateAccountSuccess()
        {
            CreateCustomerAndOpenAccountPage();
            var userSelect = new SelectElement(Driver.FindElement(By.Id("userSelect")));
            userSelect.SelectByIndex(1);
            var currencySelect = new SelectElement(Driver.FindElement(By.Id("currency")));
            currencySelect.SelectByText("Dollar");
            Driver.FindElement(By.XPath("//button[@type='submit']")).Click();

            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
            wait.Until(d =>
            {
                try { d.SwitchTo().Alert(); return true; }
                catch { return false; }
            });
            var alert = Driver.SwitchTo().Alert();
            Assert.That(alert.Text, Does.Contain("Account created successfully"));
            alert.Accept();
        }

        [Test]
        public void TestCreateAccountDifferentCurrencies()
        {
            CreateCustomerAndOpenAccountPage();
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
            string[] currencies = { "Dollar", "Pound", "Rupee" };

            foreach (var currency in currencies)
            {
                var userSelect = new SelectElement(Driver.FindElement(By.Id("userSelect")));
                userSelect.SelectByIndex(1);
                var currencySelect = new SelectElement(Driver.FindElement(By.Id("currency")));
                currencySelect.SelectByText(currency);
                Driver.FindElement(By.XPath("//button[@type='submit']")).Click();
                wait.Until(d =>
                {
                    try { d.SwitchTo().Alert(); return true; }
                    catch { return false; }
                });
                var alert = Driver.SwitchTo().Alert();
                Assert.That(alert.Text, Does.Contain("Account created successfully"));
                alert.Accept();
            }
        }
    }
}
