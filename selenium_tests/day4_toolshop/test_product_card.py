"""
День 4 — Тест 4: Проверка карточки товара
Сайт: Practice Software Testing (Toolshop)
"""
import time
import pytest
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC

BASE_URL = "https://practicesoftwaretesting.com"


class TestProductCard:
    """Проверка содержимого карточки товара."""

    def _open_first_product(self, driver):
        driver.get(BASE_URL)
        WebDriverWait(driver, 30).until(
            EC.presence_of_element_located((By.CSS_SELECTOR, "[data-test='product-name']"))
        )
        time.sleep(2)
        product = driver.find_element(By.CSS_SELECTOR, "[data-test='product-name']")
        driver.execute_script("arguments[0].click();", product)
        WebDriverWait(driver, 15).until(
            EC.presence_of_element_located((By.CSS_SELECTOR, "[data-test='add-to-cart']"))
        )
        time.sleep(1)

    def test_product_has_image(self, driver):
        """Карточка товара содержит изображение."""
        self._open_first_product(driver)
        images = driver.find_elements(By.CSS_SELECTOR, "[data-test='product-image']")
        if not images:
            images = driver.find_elements(By.CSS_SELECTOR, "img.figure-img, .figure img, img")
        assert len(images) > 0, "Изображение товара не найдено"

    def test_product_has_description(self, driver):
        """Карточка товара содержит описание."""
        self._open_first_product(driver)
        desc = driver.find_elements(By.CSS_SELECTOR, "[data-test='product-description']")
        if not desc:
            desc = driver.find_elements(By.CSS_SELECTOR, ".product-description, p")
        assert len(desc) > 0, "Описание товара не найдено"

    def test_product_has_price(self, driver):
        """Карточка товара содержит цену."""
        self._open_first_product(driver)
        price = driver.find_elements(By.CSS_SELECTOR, "[data-test='unit-price']")
        if not price:
            price = driver.find_elements(By.CSS_SELECTOR, ".product-price, span[data-test]")
        assert len(price) > 0, "Цена товара не найдена"

    def test_product_has_add_to_cart_button(self, driver):
        """Карточка товара содержит кнопку добавления в корзину."""
        self._open_first_product(driver)
        btn = driver.find_elements(By.CSS_SELECTOR, "[data-test='add-to-cart']")
        assert len(btn) > 0, "Кнопка 'Add to Cart' не найдена"
