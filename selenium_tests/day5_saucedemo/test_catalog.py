"""
День 5 — Тест 2: Проверка каталога товаров
Сайт: SauceDemo
"""
import time
import pytest
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait, Select
from selenium.webdriver.support import expected_conditions as EC

BASE_URL = "https://www.saucedemo.com"
PASSWORD = "secret_sauce"


class TestCatalog:
    """Проверка каталога товаров."""

    def _login(self, driver):
        driver.get(BASE_URL)
        driver.find_element(By.ID, "user-name").send_keys("standard_user")
        driver.find_element(By.ID, "password").send_keys(PASSWORD)
        driver.find_element(By.ID, "login-button").click()
        WebDriverWait(driver, 10).until(EC.url_contains("inventory"))

    def test_products_displayed(self, driver):
        """Отображение списка товаров."""
        self._login(driver)
        products = driver.find_elements(By.CSS_SELECTOR, ".inventory_item")
        assert len(products) == 6, f"Ожидалось 6 товаров, получено {len(products)}"

    def test_product_images(self, driver):
        """Изображения товаров отображаются."""
        self._login(driver)
        images = driver.find_elements(By.CSS_SELECTOR, ".inventory_item_img img")
        assert len(images) == 6
        for img in images:
            src = img.get_attribute("src")
            assert src and src.strip() != ""

    def test_product_prices(self, driver):
        """Цены товаров отображаются."""
        self._login(driver)
        prices = driver.find_elements(By.CSS_SELECTOR, ".inventory_item_price")
        assert len(prices) == 6
        for price in prices:
            text = price.text.replace("$", "").strip()
            assert float(text) > 0

    def test_sort_az(self, driver):
        """Сортировка каталога A-Z."""
        self._login(driver)
        sort = Select(driver.find_element(By.CSS_SELECTOR, "[data-test='product-sort-container']"))
        sort.select_by_value("az")
        time.sleep(1)
        names = [n.text for n in driver.find_elements(By.CSS_SELECTOR, ".inventory_item_name")]
        assert names == sorted(names)

    def test_sort_za(self, driver):
        """Сортировка каталога Z-A."""
        self._login(driver)
        sort = Select(driver.find_element(By.CSS_SELECTOR, "[data-test='product-sort-container']"))
        sort.select_by_value("za")
        time.sleep(1)
        names = [n.text for n in driver.find_elements(By.CSS_SELECTOR, ".inventory_item_name")]
        assert names == sorted(names, reverse=True)

    def test_sort_price_low_high(self, driver):
        """Сортировка по цене (по возрастанию)."""
        self._login(driver)
        sort = Select(driver.find_element(By.CSS_SELECTOR, "[data-test='product-sort-container']"))
        sort.select_by_value("lohi")
        time.sleep(1)
        prices = [float(p.text.replace("$", "")) for p in driver.find_elements(By.CSS_SELECTOR, ".inventory_item_price")]
        assert prices == sorted(prices)

    def test_sort_price_high_low(self, driver):
        """Сортировка по цене (по убыванию)."""
        self._login(driver)
        sort = Select(driver.find_element(By.CSS_SELECTOR, "[data-test='product-sort-container']"))
        sort.select_by_value("hilo")
        time.sleep(1)
        prices = [float(p.text.replace("$", "")) for p in driver.find_elements(By.CSS_SELECTOR, ".inventory_item_price")]
        assert prices == sorted(prices, reverse=True)
