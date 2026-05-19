using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace SeleniumTests.Day3Banking
{
    [TestFixture]
    public class AddCustomerTests : BaseTest
    {
        private const string BaseUrl =
            "https://www.globalsqa.com/angularJs-protractor/BankingProject/#/login";

        private void GoToAddCustomer()
        {
            Driver.Navigate().GoToUrl(BaseUrl);
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
            wait.Until(d =>
                d.FindElement(By.XPath("//button[contains(text(),'Bank Manager Login')]"))).Click();
            wait.Until(d =>
                d.FindElement(By.XPath("//button[@ng-click='addCust()']"))).Click();
            wait.Until(d =>
                d.FindElement(By.XPath("//input[@ng-model='fName']")));
        }

        [Test]
        public void TestFillCustomerForm()
        {
            GoToAddCustomer();
            var fname = Driver.FindElement(By.XPath("//input[@ng-model='fName']"));
            var lname = Driver.FindElement(By.XPath("//input[@ng-model='lName']"));
            var pcode = Driver.FindElement(By.XPath("//input[@ng-model='postCd']"));
            fname.SendKeys("Ivan");
            lname.SendKeys("Petrov");
            pcode.SendKeys("E12345");
            Assert.That(fname.GetAttribute("value"), Is.EqualTo("Ivan"));
            Assert.That(lname.GetAttribute("value"), Is.EqualTo("Petrov"));
            Assert.That(pcode.GetAttribute("value"), Is.EqualTo("E12345"));
        }

        [Test]
        public void TestCreateCustomer()
        {
            GoToAddCustomer();
            Driver.FindElement(By.XPath("//input[@ng-model='fName']")).SendKeys("Ivan");
            Driver.FindElement(By.XPath("//input[@ng-model='lName']")).SendKeys("Petrov");
            Driver.FindElement(By.XPath("//input[@ng-model='postCd']")).SendKeys("E12345");
            Driver.FindElement(By.XPath("//button[@type='submit']")).Click();

            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
            wait.Until(d =>
            {
                try { d.SwitchTo().Alert(); return true; }
                catch { return false; }
            });
            var alert = Driver.SwitchTo().Alert();
            Assert.That(alert.Text, Does.Contain("Customer added successfully"));
            alert.Accept();
        }

        [Test]
        public void TestDuplicateCustomer()
        {
            GoToAddCustomer();
            // First customer
            Driver.FindElement(By.XPath("//input[@ng-model='fName']")).SendKeys("Ivan");
            Driver.FindElement(By.XPath("//input[@ng-model='lName']")).SendKeys("Petrov");
            Driver.FindElement(By.XPath("//input[@ng-model='postCd']")).SendKeys("E12345");
            Driver.FindElement(By.XPath("//button[@type='submit']")).Click();
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
            wait.Until(d =>
            {
                try { d.SwitchTo().Alert(); return true; }
                catch { return false; }
            });
            Driver.SwitchTo().Alert().Accept();

            // Duplicate
            Driver.FindElement(By.XPath("//input[@ng-model='fName']")).SendKeys("Ivan");
            Driver.FindElement(By.XPath("//input[@ng-model='lName']")).SendKeys("Petrov");
            Driver.FindElement(By.XPath("//input[@ng-model='postCd']")).SendKeys("E12345");
            Driver.FindElement(By.XPath("//button[@type='submit']")).Click();
            wait.Until(d =>
            {
                try { d.SwitchTo().Alert(); return true; }
                catch { return false; }
            });
            var alert = Driver.SwitchTo().Alert();
            Assert.That(alert.Text, Does.Contain("please check the details")
                .Or.Contains("duplicate").IgnoreCase);
            alert.Accept();
        }
    }
}
