"""
День 5 — Тест 5: Проверка выхода из системы
Сайт: SauceDemo
"""
import time
import pytest
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC

BASE_URL = "https://www.saucedemo.com"
PASSWORD = "secret_sauce"


class TestLogout:
    """Проверка выхода из системы."""

    def _login(self, driver):
        driver.get(BASE_URL)
        driver.find_element(By.ID, "user-name").send_keys("standard_user")
        driver.find_element(By.ID, "password").send_keys(PASSWORD)
        driver.find_element(By.ID, "login-button").click()
        WebDriverWait(driver, 10).until(EC.url_contains("inventory"))

    def _logout(self, driver):
        burger = driver.find_element(By.ID, "react-burger-menu-btn")
        driver.execute_script("arguments[0].click();", burger)
        logout = WebDriverWait(driver, 10).until(
            EC.element_to_be_clickable((By.ID, "logout_sidebar_link"))
        )
        driver.execute_script("arguments[0].click();", logout)
        WebDriverWait(driver, 10).until(
            EC.presence_of_element_located((By.ID, "login-button"))
        )

    def test_logout_button(self, driver):
        """Работа кнопки Logout."""
        self._login(driver)
        self._logout(driver)
        assert "saucedemo.com" in driver.current_url
        login_btn = driver.find_element(By.ID, "login-button")
        assert login_btn.is_displayed()

    def test_logout_returns_to_login_form(self, driver):
        """Возврат к форме авторизации после выхода."""
        self._login(driver)
        self._logout(driver)
        login_btn = driver.find_element(By.ID, "login-button")
        assert login_btn.is_displayed()
        username_field = driver.find_element(By.ID, "user-name")
        assert username_field.is_displayed()

    def test_session_terminated_after_logout(self, driver):
        """Завершение пользовательской сессии — нельзя вернуться назад."""
        self._login(driver)
        self._logout(driver)
        driver.get(BASE_URL + "/inventory.html")
        time.sleep(1)
        error = driver.find_elements(By.CSS_SELECTOR, "[data-test='error']")
        login_btn = driver.find_elements(By.ID, "login-button")
        assert len(error) > 0 or len(login_btn) > 0, "Сессия не завершена после logout"
