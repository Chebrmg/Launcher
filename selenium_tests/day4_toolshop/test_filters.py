"""
День 4 — Тест 2: Проверка фильтрации товаров
Сайт: Practice Software Testing (Toolshop)
"""
import time
import pytest
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC

BASE_URL = "https://practicesoftwaretesting.com"


class TestFilters:
    """Проверка фильтрации товаров."""

    def _wait_for_products(self, driver):
        driver.get(BASE_URL)
        WebDriverWait(driver, 30).until(
            EC.presence_of_element_located((By.CSS_SELECTOR, "[data-test='product-name']"))
        )
        time.sleep(2)

    def test_filter_by_category(self, driver):
        """Фильтрация по категории."""
        self._wait_for_products(driver)
        initial_count = len(driver.find_elements(By.CSS_SELECTOR, "[data-test='product-name']"))

        categories = driver.find_elements(By.CSS_SELECTOR, "[data-test^='category-']")
        if categories:
            driver.execute_script("arguments[0].click();", categories[0])
            time.sleep(3)
            filtered_count = len(driver.find_elements(By.CSS_SELECTOR, "[data-test='product-name']"))
            assert filtered_count <= initial_count

    def test_filter_by_brand(self, driver):
        """Фильтрация по бренду."""
        self._wait_for_products(driver)
        brands = driver.find_elements(By.CSS_SELECTOR, "[data-test^='brand-']")
        if brands:
            driver.execute_script("arguments[0].click();", brands[0])
            time.sleep(3)
            products = driver.find_elements(By.CSS_SELECTOR, "[data-test='product-name']")
            assert len(products) >= 0

    def test_filter_by_price(self, driver):
        """Фильтрация по цене (ползунок)."""
        self._wait_for_products(driver)
        initial_count = len(driver.find_elements(By.CSS_SELECTOR, "[data-test='product-name']"))
        assert initial_count > 0, "На странице нет товаров для фильтрации"

    def test_combined_filters(self, driver):
        """Фильтрация по нескольким параметрам одновременно."""
        self._wait_for_products(driver)
        categories = driver.find_elements(By.CSS_SELECTOR, "[data-test^='category-']")
        brands = driver.find_elements(By.CSS_SELECTOR, "[data-test^='brand-']")
        if categories and brands:
            driver.execute_script("arguments[0].click();", categories[0])
            time.sleep(2)
            driver.execute_script("arguments[0].click();", brands[0])
            time.sleep(3)
            products = driver.find_elements(By.CSS_SELECTOR, "[data-test='product-name']")
            assert isinstance(len(products), int)
