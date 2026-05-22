"""
День 5 — Тест 3: Проверка корзины
Сайт: SauceDemo
"""
import time
import pytest
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC

BASE_URL = "https://www.saucedemo.com"
PASSWORD = "secret_sauce"


class TestCart:
    """Проверка работы корзины."""

    def _login(self, driver):
        driver.get(BASE_URL)
        driver.find_element(By.ID, "user-name").send_keys("standard_user")
        driver.find_element(By.ID, "password").send_keys(PASSWORD)
        driver.find_element(By.ID, "login-button").click()
        WebDriverWait(driver, 10).until(EC.url_contains("inventory"))

    def _click(self, driver, element):
        driver.execute_script("arguments[0].click();", element)

    def test_add_product_to_cart(self, driver):
        """Добавление товара — товар отображается в корзине."""
        self._login(driver)
        btn = driver.find_element(By.CSS_SELECTOR, "[data-test='add-to-cart-sauce-labs-backpack']")
        self._click(driver, btn)
        badge = WebDriverWait(driver, 10).until(
            EC.presence_of_element_located((By.CSS_SELECTOR, "[data-test='shopping-cart-badge']"))
        )
        assert badge.text == "1"

        cart_link = driver.find_element(By.CSS_SELECTOR, "[data-test='shopping-cart-link']")
        self._click(driver, cart_link)
        WebDriverWait(driver, 10).until(EC.url_contains("cart"))
        items = driver.find_elements(By.CSS_SELECTOR, ".cart_item")
        assert len(items) == 1

    def test_remove_product_from_cart(self, driver):
        """Удаление товара — корзина обновляется."""
        self._login(driver)
        btn = driver.find_element(By.CSS_SELECTOR, "[data-test='add-to-cart-sauce-labs-backpack']")
        self._click(driver, btn)
        WebDriverWait(driver, 10).until(
            EC.presence_of_element_located((By.CSS_SELECTOR, "[data-test='shopping-cart-badge']"))
        )
        cart_link = driver.find_element(By.CSS_SELECTOR, "[data-test='shopping-cart-link']")
        self._click(driver, cart_link)
        WebDriverWait(driver, 10).until(EC.url_contains("cart"))
        remove_btn = driver.find_element(By.CSS_SELECTOR, "[data-test='remove-sauce-labs-backpack']")
        self._click(driver, remove_btn)
        time.sleep(1)
        items = driver.find_elements(By.CSS_SELECTOR, ".cart_item")
        assert len(items) == 0

    def test_cart_badge_count(self, driver):
        """Счётчик товаров изменяется при добавлении."""
        self._login(driver)
        btn1 = driver.find_element(By.CSS_SELECTOR, "[data-test='add-to-cart-sauce-labs-backpack']")
        self._click(driver, btn1)
        badge = WebDriverWait(driver, 10).until(
            EC.presence_of_element_located((By.CSS_SELECTOR, "[data-test='shopping-cart-badge']"))
        )
        assert badge.text == "1"

        btn2 = driver.find_element(By.CSS_SELECTOR, "[data-test='add-to-cart-sauce-labs-bike-light']")
        self._click(driver, btn2)
        time.sleep(1)
        badge = driver.find_element(By.CSS_SELECTOR, "[data-test='shopping-cart-badge']")
        assert badge.text == "2"

        remove_btn = driver.find_element(By.CSS_SELECTOR, "[data-test='remove-sauce-labs-backpack']")
        self._click(driver, remove_btn)
        time.sleep(1)
        badge = driver.find_element(By.CSS_SELECTOR, "[data-test='shopping-cart-badge']")
        assert badge.text == "1"
