"""
День 5 — Тест 4: Проверка оформления заказа
Сайт: SauceDemo
"""
import time
import pytest
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC

BASE_URL = "https://www.saucedemo.com"
PASSWORD = "secret_sauce"


class TestCheckout:
    """Проверка полного цикла оформления заказа."""

    def _login(self, driver):
        driver.get(BASE_URL)
        driver.find_element(By.ID, "user-name").send_keys("standard_user")
        driver.find_element(By.ID, "password").send_keys(PASSWORD)
        driver.find_element(By.ID, "login-button").click()
        WebDriverWait(driver, 10).until(EC.url_contains("inventory"))

    def _click(self, driver, element):
        driver.execute_script("arguments[0].click();", element)

    def _set_react_input(self, driver, selector, value):
        driver.execute_script("""
            var el = document.querySelector(arguments[0]);
            var setter = Object.getOwnPropertyDescriptor(
                window.HTMLInputElement.prototype, 'value'
            ).set;
            setter.call(el, arguments[1]);
            el.dispatchEvent(new Event('input', { bubbles: true }));
            el.dispatchEvent(new Event('change', { bubbles: true }));
        """, selector, value)

    def _add_and_go_to_cart(self, driver, items=None):
        if items is None:
            items = ["sauce-labs-backpack"]
        for item in items:
            btn = driver.find_element(By.CSS_SELECTOR, f"[data-test='add-to-cart-{item}']")
            self._click(driver, btn)
            time.sleep(0.5)
        WebDriverWait(driver, 10).until(
            EC.presence_of_element_located((By.CSS_SELECTOR, "[data-test='shopping-cart-badge']"))
        )
        cart = driver.find_element(By.CSS_SELECTOR, "[data-test='shopping-cart-link']")
        self._click(driver, cart)
        WebDriverWait(driver, 10).until(EC.url_contains("cart"))

    def _fill_checkout_form(self, driver, first="Ivan", last="Petrov", postal="123456"):
        self._set_react_input(driver, "[data-test='firstName']", first)
        self._set_react_input(driver, "[data-test='lastName']", last)
        self._set_react_input(driver, "[data-test='postalCode']", postal)
        time.sleep(0.5)

    def test_full_checkout_flow(self, driver):
        """Полный цикл: добавление -> корзина -> форма -> проверка -> завершение."""
        self._login(driver)

        # 1. Добавление товара
        self._add_and_go_to_cart(driver)
        items = driver.find_elements(By.CSS_SELECTOR, ".cart_item")
        assert len(items) == 1

        # 2. Переход к оформлению
        checkout_btn = driver.find_element(By.CSS_SELECTOR, "[data-test='checkout']")
        self._click(driver, checkout_btn)
        WebDriverWait(driver, 10).until(EC.url_contains("checkout-step-one"))

        # 3. Заполнение формы заказа
        self._fill_checkout_form(driver)
        cont_btn = driver.find_element(By.CSS_SELECTOR, "[data-test='continue']")
        self._click(driver, cont_btn)

        # 4. Проверка итоговой информации
        WebDriverWait(driver, 10).until(EC.url_contains("checkout-step-two"))
        summary_items = driver.find_elements(By.CSS_SELECTOR, ".cart_item")
        assert len(summary_items) == 1
        total = driver.find_element(By.CSS_SELECTOR, "[data-test='total-label']")
        assert "$" in total.text

        # 5. Завершение покупки
        finish_btn = driver.find_element(By.CSS_SELECTOR, "[data-test='finish']")
        self._click(driver, finish_btn)
        WebDriverWait(driver, 10).until(EC.url_contains("checkout-complete"))
        complete_header = driver.find_element(By.CSS_SELECTOR, "[data-test='complete-header']")
        assert "Thank you for your order" in complete_header.text

    def test_checkout_requires_info(self, driver):
        """Проверка что форма заказа требует заполнения."""
        self._login(driver)
        self._add_and_go_to_cart(driver)
        checkout_btn = driver.find_element(By.CSS_SELECTOR, "[data-test='checkout']")
        self._click(driver, checkout_btn)
        WebDriverWait(driver, 10).until(EC.url_contains("checkout-step-one"))
        cont_btn = driver.find_element(By.CSS_SELECTOR, "[data-test='continue']")
        self._click(driver, cont_btn)
        time.sleep(1)
        error = driver.find_element(By.CSS_SELECTOR, "[data-test='error']")
        assert error.is_displayed()

    def test_checkout_with_multiple_items(self, driver):
        """Оформление заказа с несколькими товарами."""
        self._login(driver)
        self._add_and_go_to_cart(driver, ["sauce-labs-backpack", "sauce-labs-bike-light"])
        items = driver.find_elements(By.CSS_SELECTOR, ".cart_item")
        assert len(items) == 2

        checkout_btn = driver.find_element(By.CSS_SELECTOR, "[data-test='checkout']")
        self._click(driver, checkout_btn)
        WebDriverWait(driver, 10).until(EC.url_contains("checkout-step-one"))
        self._fill_checkout_form(driver, "Test", "User", "00000")
        cont_btn = driver.find_element(By.CSS_SELECTOR, "[data-test='continue']")
        self._click(driver, cont_btn)

        WebDriverWait(driver, 10).until(EC.url_contains("checkout-step-two"))
        summary_items = driver.find_elements(By.CSS_SELECTOR, ".cart_item")
        assert len(summary_items) == 2

        finish_btn = driver.find_element(By.CSS_SELECTOR, "[data-test='finish']")
        self._click(driver, finish_btn)
        WebDriverWait(driver, 10).until(EC.url_contains("checkout-complete"))
        complete_header = driver.find_element(By.CSS_SELECTOR, "[data-test='complete-header']")
        assert "Thank you for your order" in complete_header.text
