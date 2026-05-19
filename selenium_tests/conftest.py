import os
import stat
import urllib.request
import zipfile

import pytest
from selenium import webdriver
from selenium.webdriver.chrome.service import Service
from selenium.webdriver.chrome.options import Options

CHROME_VERSION = "133.0.6943.126"
CHROME_BINARY = f"/opt/.devin/chrome/chrome/linux-{CHROME_VERSION}/chrome-linux64/chrome"
CHROMEDRIVER_DIR = "/tmp/chromedriver_133"
CHROMEDRIVER_PATH = os.path.join(CHROMEDRIVER_DIR, "chromedriver-linux64", "chromedriver")


def _ensure_chromedriver():
    if os.path.isfile(CHROMEDRIVER_PATH):
        return CHROMEDRIVER_PATH
    url = (
        f"https://storage.googleapis.com/chrome-for-testing-public/"
        f"{CHROME_VERSION}/linux64/chromedriver-linux64.zip"
    )
    zip_path = "/tmp/chromedriver.zip"
    urllib.request.urlretrieve(url, zip_path)
    os.makedirs(CHROMEDRIVER_DIR, exist_ok=True)
    with zipfile.ZipFile(zip_path, "r") as z:
        z.extractall(CHROMEDRIVER_DIR)
    os.chmod(CHROMEDRIVER_PATH, os.stat(CHROMEDRIVER_PATH).st_mode | stat.S_IEXEC)
    return CHROMEDRIVER_PATH


@pytest.fixture(scope="function")
def driver():
    _ensure_chromedriver()
    options = Options()
    options.binary_location = CHROME_BINARY
    options.add_argument("--headless=new")
    options.add_argument("--no-sandbox")
    options.add_argument("--disable-dev-shm-usage")
    options.add_argument("--window-size=1920,1080")
    options.add_argument("--disable-gpu")
    options.add_argument("--disable-blink-features=AutomationControlled")
    options.add_argument(
        "--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
        "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36"
    )
    options.add_experimental_option("excludeSwitches", ["enable-automation"])
    options.add_experimental_option("useAutomationExtension", False)
    service = Service(CHROMEDRIVER_PATH)
    drv = webdriver.Chrome(service=service, options=options)
    drv.execute_cdp_cmd(
        "Network.setUserAgentOverride",
        {
            "userAgent": (
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
                "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36"
            )
        },
    )
    drv.execute_script(
        "Object.defineProperty(navigator, 'webdriver', {get: () => undefined})"
    )
    drv.implicitly_wait(10)
    yield drv
    drv.quit()
