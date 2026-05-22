"""
День 4 — Тест 5: Проверка корзины
Сайт: Practice Software Testing (Toolshop)
"""
import time
import pytest
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC

BASE_URL = "https://practicesoftwaretesting.com"


class TestCart:
    """Проверка функционала корзины."""

    def _add_first_product_to_cart(self, driver):
        driver.get(BASE_URL)
        WebDriverWait(driver, 30).until(
            EC.presence_of_element_located((By.CSS_SELECTOR, "[data-test='product-name']"))
        )
        time.sleep(2)
        product = driver.find_element(By.CSS_SELECTOR, "[data-test='product-name']")
        driver.execute_script("arguments[0].click();", product)
        WebDriverWait(driver, 30).until(
            EC.presence_of_element_located((By.CSS_SELECTOR, "[data-test='add-to-cart']"))
        )
        time.sleep(1)
        add_btn = driver.find_element(By.CSS_SELECTOR, "[data-test='add-to-cart']")
        driver.execute_script("arguments[0].click();", add_btn)
        time.sleep(2)

    def test_add_product_to_cart(self, driver):
        """Добавление товара в корзину."""
        self._add_first_product_to_cart(driver)
        cart_qty = driver.find_elements(By.CSS_SELECTOR, "[data-test='cart-quantity']")
        if cart_qty:
            text = cart_qty[0].text.strip()
            assert text != "" and text != "0", "Корзина пуста после добавления"

    def test_change_quantity(self, driver):
        """Изменение количества товара в корзине."""
        self._add_first_product_to_cart(driver)
        nav_cart = driver.find_element(By.CSS_SELECTOR, "[data-test='nav-cart']")
        driver.execute_script("arguments[0].click();", nav_cart)
        WebDriverWait(driver, 30).until(
            EC.presence_of_element_located((By.CSS_SELECTOR, "[data-test='product-quantity']"))
        )
        qty_input = driver.find_element(By.CSS_SELECTOR, "[data-test='product-quantity']")
        qty_input.clear()
        qty_input.send_keys("3")
        qty_input.send_keys("\t")
        time.sleep(2)
        updated_qty = driver.find_element(By.CSS_SELECTOR, "[data-test='product-quantity']")
        assert updated_qty.get_attribute("value") == "3"

    def test_remove_product_from_cart(self, driver):
        """Удаление товара из корзины."""
        self._add_first_product_to_cart(driver)
        nav_cart = driver.find_element(By.CSS_SELECTOR, "[data-test='nav-cart']")
        driver.execute_script("arguments[0].click();", nav_cart)
        WebDriverWait(driver, 30).until(
            EC.presence_of_element_located((By.CSS_SELECTOR, "[data-test='product-quantity']"))
        )
        remove_buttons = driver.find_elements(By.CSS_SELECTOR, ".btn-danger")
        if remove_buttons:
            driver.execute_script("arguments[0].click();", remove_buttons[0])
            time.sleep(2)
        remaining = driver.find_elements(By.CSS_SELECTOR, "[data-test='product-quantity']")
        assert len(remaining) == 0 or "empty" in driver.page_source.lower()
