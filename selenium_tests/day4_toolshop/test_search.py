"""
День 4 — Тест 1: Проверка поиска товаров
Сайт: Practice Software Testing (Toolshop)
"""
import time
import pytest
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC

BASE_URL = "https://practicesoftwaretesting.com"


class TestSearch:
    """Проверка поиска товаров."""

    def _wait_for_page(self, driver):
        driver.get(BASE_URL)
        WebDriverWait(driver, 30).until(
            EC.presence_of_element_located((By.CSS_SELECTOR, "[data-test='search-query']"))
        )
        time.sleep(2)

    def _search(self, driver, query):
        self._wait_for_page(driver)
        search_input = driver.find_element(By.CSS_SELECTOR, "[data-test='search-query']")
        search_input.clear()
        search_input.send_keys(query)
        driver.find_element(By.CSS_SELECTOR, "[data-test='search-submit']").click()
        time.sleep(3)

    def test_search_existing_product(self, driver):
        """Поиск существующего товара."""
        self._search(driver, "Pliers")
        products = driver.find_elements(By.CSS_SELECTOR, "[data-test='product-name']")
        assert len(products) > 0, "Товары не найдены при поиске 'Pliers'"
        found = any("pliers" in p.text.lower() for p in products)
        assert found, "Ни один товар не содержит 'Pliers' в названии"

    def test_search_nonexistent_product(self, driver):
        """Поиск отсутствующего товара."""
        self._search(driver, "xyznonexistent12345")
        time.sleep(2)
        products = driver.find_elements(By.CSS_SELECTOR, "[data-test='product-name']")
        assert len(products) == 0, "Найдены товары для несуществующего запроса"

    def test_partial_search(self, driver):
        """Частичный поиск по подстроке."""
        self._search(driver, "Ham")
        products = driver.find_elements(By.CSS_SELECTOR, "[data-test='product-name']")
        assert len(products) > 0, "Частичный поиск не вернул результатов"
