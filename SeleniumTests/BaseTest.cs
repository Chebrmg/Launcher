using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace SeleniumTests
{
    public class BaseTest
    {
        private const string ChromeVersion = "133.0.6943.126";
        private static readonly string ChromeBinary =
            $"/opt/.devin/chrome/chrome/linux-{ChromeVersion}/chrome-linux64/chrome";
        private const string ChromeDriverDir = "/tmp/chromedriver_133";
        private static readonly string ChromeDriverPath =
            Path.Combine(ChromeDriverDir, "chromedriver-linux64", "chromedriver");

        protected IWebDriver Driver = null!;

        private static void EnsureChromeDriver()
        {
            if (File.Exists(ChromeDriverPath))
                return;

            var url =
                $"https://storage.googleapis.com/chrome-for-testing-public/{ChromeVersion}/linux64/chromedriver-linux64.zip";
            var zipPath = "/tmp/chromedriver.zip";

            using var httpClient = new HttpClient();
            var bytes = httpClient.GetByteArrayAsync(url).GetAwaiter().GetResult();
            File.WriteAllBytes(zipPath, bytes);

            Directory.CreateDirectory(ChromeDriverDir);
            ZipFile.ExtractToDirectory(zipPath, ChromeDriverDir, true);

            // Make executable
            var info = new System.Diagnostics.ProcessStartInfo("chmod", $"+x {ChromeDriverPath}")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            System.Diagnostics.Process.Start(info)?.WaitForExit();
        }

        [SetUp]
        public void SetUp()
        {
            EnsureChromeDriver();

            var options = new ChromeOptions();
            options.BinaryLocation = ChromeBinary;
            options.AddArgument("--headless=new");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--window-size=1920,1080");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--disable-blink-features=AutomationControlled");
            options.AddArgument(
                "--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36");
            options.AddExcludedArgument("enable-automation");
            options.AddAdditionalOption("useAutomationExtension", false);

            var service = ChromeDriverService.CreateDefaultService(
                Path.GetDirectoryName(ChromeDriverPath)!,
                Path.GetFileName(ChromeDriverPath));

            Driver = new ChromeDriver(service, options, TimeSpan.FromSeconds(60));
            Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);

            ((IJavaScriptExecutor)Driver).ExecuteScript(
                "Object.defineProperty(navigator, 'webdriver', {get: () => undefined})");
        }

        [TearDown]
        public void TearDown()
        {
            Driver?.Quit();
        }

        protected void JsClick(IWebElement element)
        {
            ((IJavaScriptExecutor)Driver).ExecuteScript("arguments[0].click();", element);
        }

        protected void SetReactInput(string cssSelector, string value)
        {
            ((IJavaScriptExecutor)Driver).ExecuteScript(@"
                var el = document.querySelector(arguments[0]);
                var setter = Object.getOwnPropertyDescriptor(
                    window.HTMLInputElement.prototype, 'value'
                ).set;
                setter.call(el, arguments[1]);
                el.dispatchEvent(new Event('input', { bubbles: true }));
                el.dispatchEvent(new Event('change', { bubbles: true }));
            ", cssSelector, value);
        }
    }
}
