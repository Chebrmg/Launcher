"""
День 3 — Тест 3: Проверка открытия банковского счета
Сайт: GlobalSQA Banking Project
"""
import pytest
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait, Select
from selenium.webdriver.support import expected_conditions as EC

BASE_URL = "https://www.globalsqa.com/angularJs-protractor/BankingProject/#/login"


class TestOpenAccount:
    """Проверка открытия банковского счета."""

    def _go_to_open_account(self, driver):
        driver.get(BASE_URL)
        driver.find_element(By.XPATH, "//button[contains(text(),'Bank Manager Login')]").click()
        WebDriverWait(driver, 10).until(EC.url_contains("#/manager"))
        driver.find_element(By.XPATH, "//button[@ng-class='btnClass2']").click()
        WebDriverWait(driver, 10).until(
            EC.presence_of_element_located((By.ID, "userSelect"))
        )

    def test_select_customer_from_list(self, driver):
        """Выбор клиента из выпадающего списка."""
        self._go_to_open_account(driver)
        select = Select(driver.find_element(By.ID, "userSelect"))
        options = [o.text for o in select.options if o.text.strip()]
        assert len(options) > 0, "Список клиентов пуст"
        select.select_by_index(1)
        selected = select.first_selected_option.text
        assert selected.strip() != ""

    def test_select_currency(self, driver):
        """Выбор валюты счета."""
        self._go_to_open_account(driver)
        currency_select = Select(driver.find_element(By.ID, "currency"))
        currencies = [o.text for o in currency_select.options if o.text.strip()]
        assert len(currencies) > 0, "Список валют пуст"
        currency_select.select_by_visible_text("Dollar")
        assert currency_select.first_selected_option.text == "Dollar"

    def test_create_account_success(self, driver):
        """Подтверждение создания счета и проверка результата."""
        self._go_to_open_account(driver)
        customer_select = Select(driver.find_element(By.ID, "userSelect"))
        customer_select.select_by_index(1)

        currency_select = Select(driver.find_element(By.ID, "currency"))
        currency_select.select_by_visible_text("Dollar")

        driver.find_element(By.XPATH, "//button[text()='Process']").click()

        WebDriverWait(driver, 10).until(EC.alert_is_present())
        alert = driver.switch_to.alert
        assert "Account created successfully" in alert.text
        alert.accept()

    def test_create_account_different_currencies(self, driver):
        """Открытие счетов в разных валютах."""
        self._go_to_open_account(driver)
        for currency in ["Dollar", "Pound", "Rupee"]:
            customer_select = Select(driver.find_element(By.ID, "userSelect"))
            customer_select.select_by_index(1)
            currency_select = Select(driver.find_element(By.ID, "currency"))
            currency_select.select_by_visible_text(currency)
            driver.find_element(By.XPATH, "//button[text()='Process']").click()
            WebDriverWait(driver, 10).until(EC.alert_is_present())
            alert = driver.switch_to.alert
            assert "Account created successfully" in alert.text
            alert.accept()
