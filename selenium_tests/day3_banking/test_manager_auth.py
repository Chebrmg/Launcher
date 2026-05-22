"""
День 3 — Тест 1: Проверка авторизации менеджера
Сайт: GlobalSQA Banking Project
"""
import pytest
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC

BASE_URL = "https://www.globalsqa.com/angularJs-protractor/BankingProject/#/login"


class TestManagerAuth:
    """Проверка авторизации менеджера банка."""

    def test_manager_login(self, driver):
        """Выполнение входа менеджера."""
        driver.get(BASE_URL)
        btn = WebDriverWait(driver, 10).until(
            EC.element_to_be_clickable((By.XPATH, "//button[contains(text(),'Bank Manager Login')]"))
        )
        btn.click()
        WebDriverWait(driver, 10).until(EC.url_contains("#/manager"))
        assert "#/manager" in driver.current_url

    def test_manager_interface_elements(self, driver):
        """Отображение элементов интерфейса менеджера."""
        driver.get(BASE_URL)
        driver.find_element(By.XPATH, "//button[contains(text(),'Bank Manager Login')]").click()
        WebDriverWait(driver, 10).until(EC.url_contains("#/manager"))

        add_customer_btn = driver.find_element(
            By.XPATH, "//button[@ng-class='btnClass1']"
        )
        open_account_btn = driver.find_element(
            By.XPATH, "//button[@ng-class='btnClass2']"
        )
        customers_btn = driver.find_element(
            By.XPATH, "//button[@ng-class='btnClass3']"
        )
        assert add_customer_btn.is_displayed()
        assert open_account_btn.is_displayed()
        assert customers_btn.is_displayed()

    def test_manager_navigation_add_customer(self, driver):
        """Переход в раздел Add Customer."""
        driver.get(BASE_URL)
        driver.find_element(By.XPATH, "//button[contains(text(),'Bank Manager Login')]").click()
        WebDriverWait(driver, 10).until(EC.url_contains("#/manager"))
        driver.find_element(By.XPATH, "//button[@ng-class='btnClass1']").click()
        WebDriverWait(driver, 10).until(EC.url_contains("addCust"))
        assert "addCust" in driver.current_url

    def test_manager_navigation_open_account(self, driver):
        """Переход в раздел Open Account."""
        driver.get(BASE_URL)
        driver.find_element(By.XPATH, "//button[contains(text(),'Bank Manager Login')]").click()
        WebDriverWait(driver, 10).until(EC.url_contains("#/manager"))
        driver.find_element(By.XPATH, "//button[@ng-class='btnClass2']").click()
        WebDriverWait(driver, 10).until(EC.url_contains("openAccount"))
        assert "openAccount" in driver.current_url

    def test_manager_navigation_customers(self, driver):
        """Переход в раздел Customers."""
        driver.get(BASE_URL)
        driver.find_element(By.XPATH, "//button[contains(text(),'Bank Manager Login')]").click()
        WebDriverWait(driver, 10).until(EC.url_contains("#/manager"))
        driver.find_element(By.XPATH, "//button[@ng-class='btnClass3']").click()
        WebDriverWait(driver, 10).until(EC.url_contains("list"))
        assert "list" in driver.current_url

    def test_home_button_returns_to_login(self, driver):
        """Кнопка Home возвращает на главную."""
        driver.get(BASE_URL)
        driver.find_element(By.XPATH, "//button[contains(text(),'Bank Manager Login')]").click()
        WebDriverWait(driver, 10).until(EC.url_contains("#/manager"))
        driver.find_element(By.XPATH, "//button[contains(text(),'Home')]").click()
        WebDriverWait(driver, 10).until(EC.url_contains("#/login"))
        assert "#/login" in driver.current_url
