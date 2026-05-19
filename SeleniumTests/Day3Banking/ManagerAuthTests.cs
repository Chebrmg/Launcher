using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
namespace SeleniumTests.Day3Banking
{
    [TestFixture]
    public class ManagerAuthTests : BaseTest
    {
        private const string BaseUrl =
            "https://www.globalsqa.com/angularJs-protractor/BankingProject/#/login";

        private void LoginAsManager()
        {
            Driver.Navigate().GoToUrl(BaseUrl);
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
            var managerBtn = wait.Until(d =>
                d.FindElement(By.XPath("//button[contains(text(),'Bank Manager Login')]")));
            managerBtn.Click();
            wait.Until(d =>
                d.FindElement(By.XPath("//button[@ng-click='addCust()']")));
        }

        [Test]
        public void TestManagerLogin()
        {
            Driver.Navigate().GoToUrl(BaseUrl);
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
            var managerBtn = wait.Until(d =>
                d.FindElement(By.XPath("//button[contains(text(),'Bank Manager Login')]")));
            Assert.That(managerBtn.Displayed, Is.True);
            managerBtn.Click();
            var addCustBtn = wait.Until(d =>
                d.FindElement(By.XPath("//button[@ng-click='addCust()']")));
            Assert.That(addCustBtn.Displayed, Is.True);
        }

        [Test]
        public void TestManagerInterfaceElements()
        {
            LoginAsManager();
            Assert.That(Driver.FindElement(By.XPath("//button[@ng-click='addCust()']")).Displayed,
                Is.True);
            Assert.That(Driver.FindElement(By.XPath("//button[@ng-click='openAccount()']")).Displayed,
                Is.True);
            Assert.That(Driver.FindElement(By.XPath("//button[@ng-click='showCust()']")).Displayed,
                Is.True);
        }

        [Test]
        public void TestNavigationAddCustomer()
        {
            LoginAsManager();
            Driver.FindElement(By.XPath("//button[@ng-click='addCust()']")).Click();
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
            wait.Until(d => d.FindElement(By.XPath("//input[@ng-model='fName']")));
            Assert.That(
                Driver.FindElement(By.XPath("//input[@ng-model='fName']")).Displayed, Is.True);
        }

        [Test]
        public void TestNavigationOpenAccount()
        {
            LoginAsManager();
            Driver.FindElement(By.XPath("//button[@ng-click='openAccount()']")).Click();
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
            wait.Until(d => d.FindElement(By.Id("userSelect")));
            Assert.That(Driver.FindElement(By.Id("userSelect")).Displayed, Is.True);
        }

        [Test]
        public void TestNavigationCustomers()
        {
            LoginAsManager();
            Driver.FindElement(By.XPath("//button[@ng-click='showCust()']")).Click();
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
            wait.Until(d => d.FindElement(By.XPath("//input[@ng-model='searchCustomer']")));
            Assert.That(
                Driver.FindElement(By.XPath("//input[@ng-model='searchCustomer']")).Displayed,
                Is.True);
        }

        [Test]
        public void TestHomeButtonReturnsToLogin()
        {
            LoginAsManager();
            Driver.FindElement(By.XPath("//button[@ng-click='home()']")).Click();
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
            var loginBtn = wait.Until(d =>
                d.FindElement(By.XPath("//button[contains(text(),'Bank Manager Login')]")));
            Assert.That(loginBtn.Displayed, Is.True);
        }
    }
}
