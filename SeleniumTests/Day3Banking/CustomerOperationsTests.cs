using System.Threading;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace SeleniumTests.Day3Banking
{
    [TestFixture]
    public class CustomerOperationsTests : BaseTest
    {
        private const string BaseUrl =
            "https://www.globalsqa.com/angularJs-protractor/BankingProject/#/login";

        private void LoginAsCustomer(string name = "Harry Potter")
        {
            Driver.Navigate().GoToUrl(BaseUrl);
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
            wait.Until(d =>
                d.FindElement(By.XPath("//button[contains(text(),'Customer Login')]"))).Click();
            wait.Until(d => d.FindElement(By.Id("userSelect")));
            var custSelect = new SelectElement(Driver.FindElement(By.Id("userSelect")));
            custSelect.SelectByText(name);
            Driver.FindElement(By.XPath("//button[text()='Login']")).Click();
            wait.Until(d => d.Url.Contains("#/account"));
        }

        [Test]
        public void TestCustomerLogin()
        {
            LoginAsCustomer();
            Assert.That(Driver.Url, Does.Contain("#/account"));
            var welcome = Driver.FindElement(
                By.XPath("//span[@class='fontBig ng-binding']"));
            Assert.That(welcome.Text, Does.Contain("Harry"));
        }

        [Test]
        public void TestViewBalance()
        {
            LoginAsCustomer();
            var balanceElements = Driver.FindElements(
                By.XPath("//div[@ng-hide='noAccount']//strong"));
            Assert.That(balanceElements.Count, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void TestDeposit()
        {
            LoginAsCustomer();
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
            Driver.FindElement(By.XPath("//button[@ng-click=\"deposit()\"]")).Click();
            wait.Until(d => d.FindElement(By.XPath("//input[@type='number']")));
            Driver.FindElement(By.XPath("//input[@type='number']")).SendKeys("500");
            Driver.FindElement(By.XPath("//button[@type='submit']")).Click();
            wait.Until(d =>
            {
                var msg = d.FindElement(By.XPath("//span[@ng-show='message']"));
                return msg.Text.Contains("Deposit Successful");
            });
            var message = Driver.FindElement(By.XPath("//span[@ng-show='message']"));
            Assert.That(message.Text, Does.Contain("Deposit Successful"));
        }

        [Test]
        public void TestWithdrawal()
        {
            LoginAsCustomer();
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));

            // Deposit first
            Driver.FindElement(By.XPath("//button[@ng-click=\"deposit()\"]")).Click();
            wait.Until(d => d.FindElement(By.XPath("//input[@type='number']")));
            Driver.FindElement(By.XPath("//input[@type='number']")).SendKeys("1000");
            Driver.FindElement(By.XPath("//button[@type='submit']")).Click();
            wait.Until(d =>
            {
                var msg = d.FindElement(By.XPath("//span[@ng-show='message']"));
                return msg.Text.Contains("Deposit Successful");
            });

            // Withdraw
            Driver.FindElement(By.XPath("//button[@ng-click=\"withdrawl()\"]")).Click();
            Thread.Sleep(1000);
            var withdrawInput = wait.Until(d =>
            {
                var el = d.FindElement(By.XPath("//input[@type='number']"));
                return el.Displayed && el.Enabled ? el : null;
            })!;
            withdrawInput.SendKeys("200");
            Driver.FindElement(By.XPath("//button[@type='submit']")).Click();
            wait.Until(d =>
            {
                var msg = d.FindElement(By.XPath("//span[@ng-show='message']"));
                return msg.Text.Contains("Transaction successful");
            });
            var message = Driver.FindElement(By.XPath("//span[@ng-show='message']"));
            Assert.That(message.Text, Does.Contain("Transaction successful"));
        }

        [Test]
        public void TestTransactionsHistory()
        {
            LoginAsCustomer();
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));

            // Deposit to have transactions
            Driver.FindElement(By.XPath("//button[@ng-click=\"deposit()\"]")).Click();
            wait.Until(d => d.FindElement(By.XPath("//input[@type='number']")));
            Driver.FindElement(By.XPath("//input[@type='number']")).SendKeys("100");
            Driver.FindElement(By.XPath("//button[@type='submit']")).Click();
            wait.Until(d =>
            {
                var msg = d.FindElement(By.XPath("//span[@ng-show='message']"));
                return msg.Text.Contains("Deposit Successful");
            });

            // View transactions
            Driver.FindElement(By.XPath("//button[@ng-click=\"transactions()\"]")).Click();
            wait.Until(d => d.Url.Contains("listTx"));
            Thread.Sleep(1000);
            var rows = Driver.FindElements(By.XPath("//table//tbody/tr"));
            Assert.That(rows.Count, Is.GreaterThanOrEqualTo(1));
        }
    }
}
