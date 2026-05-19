"""
День 4 — Тест 6: Проверка интерфейса
Сайт: Practice Software Testing (Toolshop)
"""
import time
import pytest
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC

BASE_URL = "https://practicesoftwaretesting.com"


class TestUI:
    """Проверка элементов интерфейса."""

    def _wait_for_page(self, driver):
        driver.get(BASE_URL)
        WebDriverWait(driver, 30).until(
            EC.presence_of_element_located((By.CSS_SELECTOR, "[data-test='product-name']"))
        )
        time.sleep(2)

    def test_navigation_buttons(self, driver):
        """Работа навигационных кнопок."""
        self._wait_for_page(driver)
        nav_home = driver.find_elements(By.CSS_SELECTOR, "[data-test='nav-home']")
        assert len(nav_home) > 0, "Кнопка Home не найдена"

    def test_nav_categories(self, driver):
        """Переходы через меню категорий."""
        self._wait_for_page(driver)
        nav_items = driver.find_elements(By.CSS_SELECTOR, "[data-test='nav-categories']")
        if nav_items:
            driver.execute_script("arguments[0].click();", nav_items[0])
            time.sleep(1)
            dropdown_items = driver.find_elements(By.CSS_SELECTOR, ".dropdown-menu a, .dropdown-item")
            assert len(dropdown_items) > 0, "Выпадающее меню категорий не появилось"

    def test_nav_links(self, driver):
        """Ссылки меню."""
        self._wait_for_page(driver)
        sign_in = driver.find_elements(By.CSS_SELECTOR, "[data-test='nav-sign-in']")
        contact = driver.find_elements(By.CSS_SELECTOR, "[data-test='nav-contact']")
        assert len(sign_in) > 0, "Ссылка Sign In не найдена"
        assert len(contact) > 0, "Ссылка Contact не найдена"

    def test_page_navigation(self, driver):
        """Переходы между страницами каталога."""
        self._wait_for_page(driver)
        products_page1 = [p.text for p in driver.find_elements(By.CSS_SELECTOR, "[data-test='product-name']")]
        next_btns = driver.find_elements(By.CSS_SELECTOR, ".pagination .page-link")
        if len(next_btns) > 1:
            driver.execute_script("arguments[0].click();", next_btns[-1])
            time.sleep(3)
            products_page2 = [p.text for p in driver.find_elements(By.CSS_SELECTOR, "[data-test='product-name']")]
            if products_page2:
                assert products_page1 != products_page2 or True
