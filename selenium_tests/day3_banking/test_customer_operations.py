"""
День 3 — Тест 4: Проверка работы клиента банка
Сайт: GlobalSQA Banking Project
"""
import time
import pytest
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait, Select
from selenium.webdriver.support import expected_conditions as EC

BASE_URL = "https://www.globalsqa.com/angularJs-protractor/BankingProject/#/login"


class TestCustomerOperations:
    """Проверка операций клиента банка."""

    def _login_as_customer(self, driver, name="Harry Potter"):
        driver.get(BASE_URL)
        driver.find_element(By.XPATH, "//button[contains(text(),'Customer Login')]").click()
        WebDriverWait(driver, 10).until(
            EC.presence_of_element_located((By.ID, "userSelect"))
        )
        select = Select(driver.find_element(By.ID, "userSelect"))
        select.select_by_visible_text(name)
        driver.find_element(By.XPATH, "//button[text()='Login']").click()
        WebDriverWait(driver, 10).until(EC.url_contains("#/account"))

    def test_customer_login(self, driver):
        """Вход клиента в систему."""
        self._login_as_customer(driver)
        assert "#/account" in driver.current_url
        welcome = driver.find_element(By.XPATH, "//span[@class='fontBig ng-binding']")
        assert "Harry" in welcome.text

    def test_view_balance(self, driver):
        """Просмотр баланса клиента."""
        self._login_as_customer(driver)
        balance_elements = driver.find_elements(By.XPATH, "//div[@ng-hide='noAccount']//strong")
        assert len(balance_elements) >= 1

    def test_deposit(self, driver):
        """Пополнение счета клиента."""
        self._login_as_customer(driver)
        driver.find_element(By.XPATH, "//button[@ng-click=\"deposit()\"]").click()
        WebDriverWait(driver, 10).until(
            EC.presence_of_element_located((By.XPATH, "//input[@type='number']"))
        )
        amount_input = driver.find_element(By.XPATH, "//input[@type='number']")
        amount_input.send_keys("500")
        driver.find_element(By.XPATH, "//button[@type='submit']").click()
        WebDriverWait(driver, 10).until(
            EC.text_to_be_present_in_element(
                (By.XPATH, "//span[@ng-show='message']"), "Deposit Successful"
            )
        )
        message = driver.find_element(By.XPATH, "//span[@ng-show='message']")
        assert "Deposit Successful" in message.text

    def test_withdrawal(self, driver):
        """Снятие средств со счета."""
        self._login_as_customer(driver)
        # deposit first
        driver.find_element(By.XPATH, "//button[@ng-click=\"deposit()\"]").click()
        WebDriverWait(driver, 10).until(
            EC.presence_of_element_located((By.XPATH, "//input[@type='number']"))
        )
        driver.find_element(By.XPATH, "//input[@type='number']").send_keys("1000")
        driver.find_element(By.XPATH, "//button[@type='submit']").click()
        WebDriverWait(driver, 10).until(
            EC.text_to_be_present_in_element(
                (By.XPATH, "//span[@ng-show='message']"), "Deposit Successful"
            )
        )

        # withdraw
        driver.find_element(By.XPATH, "//button[@ng-click=\"withdrawl()\"]").click()
        time.sleep(1)
        withdraw_input = WebDriverWait(driver, 10).until(
            EC.element_to_be_clickable((By.XPATH, "//input[@type='number']"))
        )
        withdraw_input.send_keys("200")
        driver.find_element(By.XPATH, "//button[@type='submit']").click()
        WebDriverWait(driver, 10).until(
            EC.text_to_be_present_in_element(
                (By.XPATH, "//span[@ng-show='message']"), "Transaction successful"
            )
        )
        message = driver.find_element(By.XPATH, "//span[@ng-show='message']")
        assert "Transaction successful" in message.text

    def test_transactions_history(self, driver):
        """Просмотр истории операций."""
        self._login_as_customer(driver)
        # deposit to have transactions
        driver.find_element(By.XPATH, "//button[@ng-click=\"deposit()\"]").click()
        WebDriverWait(driver, 10).until(
            EC.presence_of_element_located((By.XPATH, "//input[@type='number']"))
        )
        driver.find_element(By.XPATH, "//input[@type='number']").send_keys("100")
        driver.find_element(By.XPATH, "//button[@type='submit']").click()
        WebDriverWait(driver, 10).until(
            EC.text_to_be_present_in_element(
                (By.XPATH, "//span[@ng-show='message']"), "Deposit Successful"
            )
        )

        driver.find_element(By.XPATH, "//button[@ng-click=\"transactions()\"]").click()
        WebDriverWait(driver, 10).until(EC.url_contains("listTx"))
        time.sleep(1)
        rows = driver.find_elements(By.XPATH, "//table//tbody/tr")
        assert len(rows) >= 1, "Должна быть хотя бы одна транзакция в истории"
