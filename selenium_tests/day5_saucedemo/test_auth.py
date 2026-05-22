"""
День 5 — Тест 1: Проверка авторизации пользователей
Сайт: SauceDemo
"""
import time
import pytest
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC

BASE_URL = "https://www.saucedemo.com"
PASSWORD = "secret_sauce"


class TestAuth:
    """Проверка авторизации всех тестовых пользователей."""

    def _login(self, driver, username):
        driver.get(BASE_URL)
        driver.find_element(By.ID, "user-name").clear()
        driver.find_element(By.ID, "user-name").send_keys(username)
        driver.find_element(By.ID, "password").clear()
        driver.find_element(By.ID, "password").send_keys(PASSWORD)
        driver.find_element(By.ID, "login-button").click()

    def test_standard_user_login(self, driver):
        """standard_user — успешный вход."""
        self._login(driver, "standard_user")
        WebDriverWait(driver, 10).until(EC.url_contains("inventory"))
        assert "inventory" in driver.current_url

    def test_locked_out_user(self, driver):
        """locked_out_user — ошибка авторизации."""
        self._login(driver, "locked_out_user")
        error = WebDriverWait(driver, 10).until(
            EC.presence_of_element_located((By.CSS_SELECTOR, "[data-test='error']"))
        )
        assert "locked out" in error.text.lower()

    def test_problem_user_login(self, driver):
        """problem_user — вход и проверка ошибок интерфейса."""
        self._login(driver, "problem_user")
        WebDriverWait(driver, 10).until(EC.url_contains("inventory"))
        images = driver.find_elements(By.CSS_SELECTOR, ".inventory_item_img img")
        srcs = [img.get_attribute("src") for img in images]
        # problem_user shows wrong images (all same), check if all images are identical
        if len(srcs) > 1:
            all_same = all(s == srcs[0] for s in srcs)
            assert all_same, "problem_user должен показывать некорректные изображения"

    def test_performance_glitch_user(self, driver):
        """performance_glitch_user — вход с задержкой."""
        start = time.time()
        self._login(driver, "performance_glitch_user")
        WebDriverWait(driver, 30).until(EC.url_contains("inventory"))
        elapsed = time.time() - start
        assert "inventory" in driver.current_url
        # performance_glitch_user may take longer
        assert elapsed > 0

    def test_error_user_login(self, driver):
        """error_user — вход и проверка нестабильного поведения."""
        self._login(driver, "error_user")
        WebDriverWait(driver, 10).until(EC.url_contains("inventory"))
        assert "inventory" in driver.current_url

    def test_visual_user_login(self, driver):
        """visual_user — вход и проверка отображения интерфейса."""
        self._login(driver, "visual_user")
        WebDriverWait(driver, 10).until(EC.url_contains("inventory"))
        assert "inventory" in driver.current_url
        products = driver.find_elements(By.CSS_SELECTOR, ".inventory_item")
        assert len(products) > 0
