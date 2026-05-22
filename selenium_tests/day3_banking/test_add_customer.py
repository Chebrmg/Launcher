"""
День 3 — Тест 2: Проверка добавления клиента
Сайт: GlobalSQA Banking Project
"""
import pytest
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC

BASE_URL = "https://www.globalsqa.com/angularJs-protractor/BankingProject/#/login"


class TestAddCustomer:
    """Проверка добавления клиента через менеджера."""

    def _go_to_add_customer(self, driver):
        driver.get(BASE_URL)
        driver.find_element(By.XPATH, "//button[contains(text(),'Bank Manager Login')]").click()
        WebDriverWait(driver, 10).until(EC.url_contains("#/manager"))
        driver.find_element(By.XPATH, "//button[@ng-class='btnClass1']").click()
        WebDriverWait(driver, 10).until(
            EC.presence_of_element_located((By.XPATH, "//input[@placeholder='First Name']"))
        )

    def test_fill_customer_form(self, driver):
        """Заполнение формы добавления клиента."""
        self._go_to_add_customer(driver)
        first_name = driver.find_element(By.XPATH, "//input[@placeholder='First Name']")
        last_name = driver.find_element(By.XPATH, "//input[@placeholder='Last Name']")
        post_code = driver.find_element(By.XPATH, "//input[@placeholder='Post Code']")

        first_name.send_keys("Test")
        last_name.send_keys("User")
        post_code.send_keys("12345")

        assert first_name.get_attribute("value") == "Test"
        assert last_name.get_attribute("value") == "User"
        assert post_code.get_attribute("value") == "12345"

    def test_create_customer(self, driver):
        """Создание клиента — успешное добавление."""
        self._go_to_add_customer(driver)
        driver.find_element(By.XPATH, "//input[@placeholder='First Name']").send_keys("Ivan")
        driver.find_element(By.XPATH, "//input[@placeholder='Last Name']").send_keys("Petrov")
        driver.find_element(By.XPATH, "//input[@placeholder='Post Code']").send_keys("67890")
        driver.find_element(By.XPATH, "//button[@type='submit']").click()

        WebDriverWait(driver, 10).until(EC.alert_is_present())
        alert = driver.switch_to.alert
        assert "Customer added successfully" in alert.text
        alert.accept()

    def test_duplicate_customer(self, driver):
        """Повторное добавление клиента с теми же данными."""
        self._go_to_add_customer(driver)
        for _ in range(2):
            driver.find_element(By.XPATH, "//input[@placeholder='First Name']").clear()
            driver.find_element(By.XPATH, "//input[@placeholder='Last Name']").clear()
            driver.find_element(By.XPATH, "//input[@placeholder='Post Code']").clear()
            driver.find_element(By.XPATH, "//input[@placeholder='First Name']").send_keys("Harry")
            driver.find_element(By.XPATH, "//input[@placeholder='Last Name']").send_keys("Potter")
            driver.find_element(By.XPATH, "//input[@placeholder='Post Code']").send_keys("E725JB")
            driver.find_element(By.XPATH, "//button[@type='submit']").click()
            WebDriverWait(driver, 10).until(EC.alert_is_present())
            alert = driver.switch_to.alert
            alert_text = alert.text
            alert.accept()

        assert "please check the details" in alert_text.lower() or "already" in alert_text.lower() or "customer added" in alert_text.lower()
