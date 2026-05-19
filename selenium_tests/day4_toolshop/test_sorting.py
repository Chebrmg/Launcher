"""
День 4 — Тест 3: Проверка сортировки и цен
Сайт: Practice Software Testing (Toolshop)
"""
import time
import pytest
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait, Select
from selenium.webdriver.support import expected_conditions as EC

BASE_URL = "https://practicesoftwaretesting.com"


class TestSorting:
    """Проверка сортировки товаров и отображения цен."""

    def _wait_for_products(self, driver):
        driver.get(BASE_URL)
        WebDriverWait(driver, 30).until(
            EC.presence_of_element_located((By.CSS_SELECTOR, "[data-test='product-name']"))
        )
        time.sleep(2)

    def test_sort_by_price_low_to_high(self, driver):
        """Сортировка по цене (по возрастанию)."""
        self._wait_for_products(driver)
        sort_select = Select(driver.find_element(By.CSS_SELECTOR, "[data-test='sort']"))
        sort_select.select_by_visible_text("Price (Low - High)")
        time.sleep(3)
        prices = driver.find_elements(By.CSS_SELECTOR, "[data-test='product-price']")
        price_values = []
        for p in prices:
            text = p.text.replace("$", "").replace(",", "").strip()
            if text:
                price_values.append(float(text))
        if len(price_values) > 1:
            assert price_values == sorted(price_values), "Цены не отсортированы по возрастанию"

    def test_sort_by_price_high_to_low(self, driver):
        """Сортировка по цене (по убыванию)."""
        self._wait_for_products(driver)
        sort_select = Select(driver.find_element(By.CSS_SELECTOR, "[data-test='sort']"))
        sort_select.select_by_visible_text("Price (High - Low)")
        time.sleep(3)
        prices = driver.find_elements(By.CSS_SELECTOR, "[data-test='product-price']")
        price_values = []
        for p in prices:
            text = p.text.replace("$", "").replace(",", "").strip()
            if text:
                price_values.append(float(text))
        if len(price_values) > 1:
            assert price_values == sorted(price_values, reverse=True), "Цены не отсортированы по убыванию"

    def test_prices_displayed(self, driver):
        """Отображение цены у каждого товара."""
        self._wait_for_products(driver)
        products = driver.find_elements(By.CSS_SELECTOR, "[data-test='product-name']")
        prices = driver.find_elements(By.CSS_SELECTOR, "[data-test='product-price']")
        assert len(prices) == len(products), "Не у всех товаров отображается цена"
        for p in prices:
            assert p.text.strip() != "", "Цена пустая"

    def test_sort_by_name_az(self, driver):
        """Сортировка по имени (A-Z)."""
        self._wait_for_products(driver)
        sort_select = Select(driver.find_element(By.CSS_SELECTOR, "[data-test='sort']"))
        sort_select.select_by_visible_text("Name (A - Z)")
        time.sleep(3)
        names = driver.find_elements(By.CSS_SELECTOR, "[data-test='product-name']")
        name_texts = [n.text.lower() for n in names if n.text.strip()]
        if len(name_texts) > 1:
            assert name_texts == sorted(name_texts), "Товары не отсортированы по имени A-Z"
