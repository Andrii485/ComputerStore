using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Npgsql;
using System.Configuration;
using ElmirClone.Models;
using System.Text.RegularExpressions;

namespace ElmirClone
{
    public partial class CartWindow : Window
    {
        private List<ProductDetails> _cartItems;
        private UserProfile _userProfile;
        private string _connectionString;
        private decimal totalPrice;
        private bool isInitializedSuccessfully;
        private readonly List<string> regions = new List<string>
        {
            "Вінницька область", "Волинська область", "Дніпропетровська область", "Донецька область",
            "Житомирська область", "Закарпатська область", "Запорізька область", "Івано-Франківська область",
            "Київська область", "Кіровоградська область", "Луганська область", "Львівська область",
            "Миколаївська область", "Одеська область", "Полтавська область", "Рівненська область",
            "Сумська область", "Тернопільська область", "Харківська область", "Херсонська область",
            "Хмельницька область", "Черкаська область", "Чернігівська область", "Чернівецька область",
            "Автономна Республіка Крим"
        };

        public CartWindow(List<ProductDetails> cartItems, UserProfile userProfile, string connectionString, MainWindow mainWindow)
        {
            InitializeComponent();
            _cartItems = cartItems ?? new List<ProductDetails>(); // Ініціалізація порожнього списку, якщо null
            _userProfile = userProfile ?? throw new ArgumentNullException(nameof(userProfile));
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

            if (string.IsNullOrEmpty(_connectionString))
            {
                MessageBox.Show("Рядок підключення до бази даних не знайдено.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                isInitializedSuccessfully = false;
                return;
            }
            if (_userProfile == null || !(_userProfile.UserId is int id) || id <= 0)
            {
                MessageBox.Show("Користувач не авторизований або ідентифікатор користувача некоректний. Перенаправляємо на сторінку входу.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                isInitializedSuccessfully = false;
                return;
            }

            // Завантажуємо актуальні дані з бази даних для кошика
            LoadCartFromDatabase();
            foreach (var item in _cartItems)
            {
                if (item.StockQuantity == 0)
                {
                    item.StockQuantity = GetAvailableStock(item.ProductId);
                }
                if (item.Quantity == 0)
                {
                    item.Quantity = 1; // Встановлюємо мінімальну кількість 1, якщо 0
                }
            }

            CartItemsList.ItemsSource = _cartItems;
            CalculateTotalPrice();
            LoadContactDetails();
            LoadShippingRegions();
            LoadPaymentMethods();
            DataContext = _userProfile;
            isInitializedSuccessfully = true;
        }

        public CartWindow(UserProfile userProfile)
        {
            _userProfile = userProfile;
        }

        private void LoadCartFromDatabase()
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand(
                        "SELECT p.productid, p.name, p.price, p.image_url, p.stock_quantity, c.quantity " +
                        "FROM cart c " +
                        "JOIN products p ON c.productid = p.productid " +
                        "WHERE c.buyerid = @buyerId AND p.ishidden = false", connection))
                    {
                        command.Parameters.AddWithValue("buyerId", _userProfile.UserId);
                        using (var reader = command.ExecuteReader())
                        {
                            _cartItems.Clear();
                            while (reader.Read())
                            {
                                _cartItems.Add(new ProductDetails
                                {
                                    ProductId = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    Price = reader.GetDecimal(2),
                                    ImageUrl = reader.IsDBNull(3) ? "https://via.placeholder.com/150" : reader.GetString(3),
                                    StockQuantity = reader.GetInt32(4),
                                    Quantity = reader.GetInt32(5)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при завантаженні кошика з бази даних: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CalculateTotalPrice()
        {
            totalPrice = (_cartItems != null) ? _cartItems.Sum(item => item.Price * item.Quantity) : 0;
            TotalPriceText.Text = $"Загальна сума: {totalPrice:F2} грн";
            UserBalanceText.Text = $"Ваш баланс: {_userProfile.Balance:F2} грн";
        }

        private void LoadContactDetails()
        {
            if (_userProfile == null)
            {
                MessageBox.Show("Профіль користувача не завантажено. Перенаправляємо на сторінку входу.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ContactLastName.Text = _userProfile.LastName ?? "";
            ContactFirstName.Text = _userProfile.FirstName ?? "";
            ContactMiddleName.Text = _userProfile.MiddleName ?? "";
            ContactPhone.Text = _userProfile.Phone ?? "";
            UserBalanceText.Text = $"Ваш баланс: {_userProfile.Balance:F2} грн";
        }

        private void LoadShippingRegions()
        {
            if (ShippingRegion == null)
            {
                MessageBox.Show("Елемент ShippingRegion не знайдено у розмітці.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ShippingRegion.ItemsSource = regions;
            if (regions.Any())
            {
                ShippingRegion.SelectedIndex = 0;
            }
        }

        private void ShippingRegion_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadPickupPoints();
        }

        private void LoadPickupPoints()
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    string selectedRegion = ShippingRegion?.SelectedItem?.ToString();
                    string query = "SELECT pickup_point_id, address, region FROM pickup_points";
                    if (!string.IsNullOrEmpty(selectedRegion))
                    {
                        query += " WHERE region = @region";
                    }

                    using (var command = new NpgsqlCommand(query, connection))
                    {
                        if (!string.IsNullOrEmpty(selectedRegion))
                        {
                            command.Parameters.AddWithValue("region", selectedRegion);
                        }

                        using (var reader = command.ExecuteReader())
                        {
                            var pickupPoints = new List<PickupPoint>();
                            while (reader.Read())
                            {
                                pickupPoints.Add(new PickupPoint
                                {
                                    PickupPointId = reader.GetInt32(0),
                                    Address = reader.GetString(1),
                                    Region = reader.GetString(2)
                                });
                            }
                            if (PickupPoint == null)
                            {
                                MessageBox.Show("Елемент PickupPoint не знайдено у розмітці.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                            PickupPoint.ItemsSource = pickupPoints;
                            if (pickupPoints.Any())
                            {
                                PickupPoint.SelectedIndex = 0;
                            }
                            else
                            {
                                MessageBox.Show("Пункти самовивозу не знайдено для обраного регіону.", "Попередження", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час завантаження пунктів самовивозу: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadPaymentMethods()
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    var paymentMethods = new List<PaymentMethod>();
                    using (var command = new NpgsqlCommand("SELECT methodid, name FROM payment_methods WHERE is_active = TRUE", connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                paymentMethods.Add(new PaymentMethod
                                {
                                    PaymentMethodId = reader.GetInt32(0),
                                    Name = reader.GetString(1)
                                });
                            }
                        }
                    }

                    if (PaymentMethodsComboBox == null)
                    {
                        MessageBox.Show("Елемент PaymentMethodsComboBox не знайдено у розмітці.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    PaymentMethodsComboBox.ItemsSource = paymentMethods;
                    PaymentMethodsComboBox.DisplayMemberPath = "Name";
                    PaymentMethodsComboBox.SelectedValuePath = "PaymentMethodId";
                    if (paymentMethods.Any())
                    {
                        PaymentMethodsComboBox.SelectedIndex = 0;
                    }
                    else
                    {
                        MessageBox.Show("Немає доступних способів оплати. Будь ласка, зверніться до адміністратора.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час завантаження способів оплати: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PaymentMethodsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedPaymentMethod = (PaymentMethodsComboBox?.SelectedItem as PaymentMethod)?.Name?.ToLower();
            if (selectedPaymentMethod != null &&
                (selectedPaymentMethod.Contains("карт") ||
                 selectedPaymentMethod.Contains("оплатити зараз") ||
                 selectedPaymentMethod.Contains("онлайн")))
            {
                if (CardDetailsPanel != null)
                {
                    CardDetailsPanel.Visibility = Visibility.Visible;
                }
            }
            else
            {
                if (CardDetailsPanel != null)
                {
                    CardDetailsPanel.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void IncreaseQuantity_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int productId)
            {
                var item = _cartItems.FirstOrDefault(i => i.ProductId == productId);
                if (item != null)
                {
                    int availableQuantity = GetAvailableStock(productId);
                    if (item.Quantity + 1 > availableQuantity)
                    {
                        MessageBox.Show($"Неможливо збільшити кількість. Доступно лише {availableQuantity} одиниць товару {item.Name}.", "Попередження", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    item.Quantity++;
                    UpdateCartInDatabase(productId, item.Quantity);
                    CartItemsList.ItemsSource = null;
                    CartItemsList.ItemsSource = _cartItems;
                    CalculateTotalPrice();
                }
            }
        }

        private void DecreaseQuantity_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int productId)
            {
                var item = _cartItems.FirstOrDefault(i => i.ProductId == productId);
                if (item != null && item.Quantity > 1)
                {
                    item.Quantity--;
                    UpdateCartInDatabase(productId, item.Quantity);
                    CartItemsList.ItemsSource = null;
                    CartItemsList.ItemsSource = _cartItems;
                    CalculateTotalPrice();
                }
            }
        }

        private void QuantityTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Tag is int productId)
            {
                var item = _cartItems.FirstOrDefault(i => i.ProductId == productId);
                if (item == null) return;

                if (!int.TryParse(textBox.Text, out int newQuantity) || newQuantity < 1)
                {
                    MessageBox.Show("Будь ласка, введіть коректне число (не менше 1).", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                    textBox.Text = item.Quantity.ToString();
                    return;
                }

                int availableQuantity = GetAvailableStock(productId);
                if (newQuantity > availableQuantity)
                {
                    MessageBox.Show($"Неможливо встановити кількість. Доступно лише {availableQuantity} одиниць товару {item.Name}.", "Попередження", MessageBoxButton.OK, MessageBoxImage.Warning);
                    textBox.Text = availableQuantity.ToString();
                    item.Quantity = availableQuantity;
                }
                else
                {
                    item.Quantity = newQuantity;
                }

                UpdateCartInDatabase(productId, item.Quantity);
                CartItemsList.ItemsSource = null;
                CartItemsList.ItemsSource = _cartItems;
                CalculateTotalPrice();
            }
        }

        private void UpdateCartInDatabase(int productId, int quantity)
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand(
                        "UPDATE cart SET quantity = @quantity WHERE buyerid = @buyerid AND productid = @productid", connection))
                    {
                        command.Parameters.AddWithValue("quantity", quantity);
                        command.Parameters.AddWithValue("buyerid", _userProfile.UserId);
                        command.Parameters.AddWithValue("productid", productId);
                        int rowsAffected = command.ExecuteNonQuery();
                        if (rowsAffected == 0)
                        {
                            // Якщо запису немає, додаємо новий
                            using (var insertCommand = new NpgsqlCommand(
                                "INSERT INTO cart (buyerid, productid, quantity) VALUES (@buyerid, @productid, @quantity)", connection))
                            {
                                insertCommand.Parameters.AddWithValue("buyerid", _userProfile.UserId);
                                insertCommand.Parameters.AddWithValue("productid", productId);
                                insertCommand.Parameters.AddWithValue("quantity", quantity);
                                insertCommand.ExecuteNonQuery();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при оновленні кошика в базі даних: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveFromCart_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int productId)
            {
                var itemToRemove = _cartItems.FirstOrDefault(i => i.ProductId == productId);
                if (itemToRemove != null)
                {
                    _cartItems.Remove(itemToRemove);
                    CartItemsList.ItemsSource = null;
                    CartItemsList.ItemsSource = _cartItems;
                    CalculateTotalPrice();
                    MessageBox.Show($"{itemToRemove.Name} видалено з кошика.", "Інформація", MessageBoxButton.OK, MessageBoxImage.Information);

                    try
                    {
                        using (var connection = new NpgsqlConnection(_connectionString))
                        {
                            connection.Open();
                            using (var command = new NpgsqlCommand("DELETE FROM cart WHERE buyerid = @buyerid AND productid = @productid", connection))
                            {
                                command.Parameters.AddWithValue("buyerid", _userProfile.UserId);
                                command.Parameters.AddWithValue("productid", productId);
                                command.ExecuteNonQuery();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Помилка при видаленні товару з кошика в базі даних: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private int GetAvailableStock(int productId)
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("SELECT stock_quantity FROM products WHERE productid = @productId", connection))
                    {
                        command.Parameters.AddWithValue("productId", productId);
                        var result = command.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            return Convert.ToInt32(result);
                        }
                        return 0;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при перевірці доступної кількості: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return 0;
            }
        }

        private bool CheckProductAvailability(int productId, int quantity)
        {
            int availableQuantity = GetAvailableStock(productId);
            return availableQuantity >= quantity;
        }

        private void ClearCartInDatabase()
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("DELETE FROM cart WHERE buyerid = @buyerid", connection))
                    {
                        command.Parameters.AddWithValue("buyerid", _userProfile.UserId);
                        command.ExecuteNonQuery();
                    }
                }
                _cartItems.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при очищенні кошика в базі даних: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Checkout_Click(object sender, RoutedEventArgs e)
        {
            if (_cartItems == null || !_cartItems.Any())
            {
                MessageBox.Show("Кошик порожній.", "Попередження", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_userProfile == null)
            {
                MessageBox.Show("Профіль користувача не завантажено. Перенаправляємо на сторінку входу.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!(_userProfile.UserId is int buyerIdValue) || buyerIdValue <= 0)
            {
                MessageBox.Show($"Ідентифікатор користувача некоректний (UserId = {_userProfile.UserId}). Перенаправляємо на сторінку входу.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (ContactLastName == null || ContactFirstName == null || ContactMiddleName == null || ContactPhone == null ||
                ShippingRegion == null || PickupPoint == null || PaymentMethodsComboBox == null)
            {
                MessageBox.Show("Одне або кілька полів форми не знайдено у розмітці. Перевірте XAML.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string lastName = ContactLastName.Text?.Trim() ?? string.Empty;
            string firstName = ContactFirstName.Text?.Trim() ?? string.Empty;
            string middleName = ContactMiddleName.Text?.Trim() ?? string.Empty;
            string phone = ContactPhone.Text?.Trim() ?? string.Empty;
            string shippingRegion = ShippingRegion.SelectedItem?.ToString();
            int? pickupPointId = PickupPoint.SelectedValue as int?;
            int? paymentMethodId = PaymentMethodsComboBox.SelectedValue as int?;
            string paymentMethodName = (PaymentMethodsComboBox.SelectedItem as PaymentMethod)?.Name;

            if (string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(phone) ||
                string.IsNullOrEmpty(shippingRegion) || pickupPointId == null || paymentMethodId == null || string.IsNullOrEmpty(paymentMethodName))
            {
                MessageBox.Show("Заповніть усі обов'язкові поля (прізвище, ім'я, телефон, область, пункт самовивозу, спосіб оплати).", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            bool requiresCardDetails = paymentMethodName.ToLower().Contains("карт") ||
                                       paymentMethodName.ToLower().Contains("оплатити зараз") ||
                                       paymentMethodName.ToLower().Contains("онлайн");
            if (requiresCardDetails)
            {
                if (CardNumberTextBox == null || CardExpiryTextBox == null || CardCvvTextBox == null)
                {
                    MessageBox.Show("Поля для даних картки не знайдено у розмітці. Перевірте XAML.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string cardNumber = CardNumberTextBox.Text?.Trim() ?? string.Empty;
                string cardExpiry = CardExpiryTextBox.Text?.Trim() ?? string.Empty;
                string cardCvv = CardCvvTextBox.Text?.Trim() ?? string.Empty;

                if (!Regex.IsMatch(cardNumber, @"^\d{16}$"))
                {
                    MessageBox.Show("Введіть коректний номер картки (16 цифр).", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!Regex.IsMatch(cardExpiry, @"^(0[1-9]|1[0-2])\/\d{2}$"))
                {
                    MessageBox.Show("Введіть коректний термін дії картки у форматі MM/YY.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!Regex.IsMatch(cardCvv, @"^\d{3}$"))
                {
                    MessageBox.Show("Введіть коректний CVV (3 цифри).", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if ((decimal)_userProfile.Balance < totalPrice)
                {
                    MessageBox.Show($"Недостатньо коштів для здійснення покупки. Ваш баланс: {_userProfile.Balance:F2} грн, потрібно: {totalPrice:F2} грн.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            foreach (var item in _cartItems)
            {
                if (!CheckProductAvailability(item.ProductId, item.Quantity))
                {
                    MessageBox.Show($"Товар {item.Name} недоступний у достатній кількості ({item.Quantity} од.).", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            foreach (var item in _cartItems)
                            {
                                if (item == null || item.ProductId <= 0 || item.Price < 0)
                                {
                                    MessageBox.Show($"Некоректні дані товару в кошику: {item?.Name ?? "Невідомий товар"}.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                                    transaction.Rollback();
                                    return;
                                }

                                int? sellerId = null;
                                try
                                {
                                    using (var command = new NpgsqlCommand("SELECT sellerid FROM products WHERE productid = @productId", connection))
                                    {
                                        command.Parameters.AddWithValue("productId", item.ProductId);
                                        var result = command.ExecuteScalar();
                                        if (result != null && result != DBNull.Value)
                                        {
                                            sellerId = (int)result;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"Помилка під час отримання sellerId для товару {item.Name}: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                                    transaction.Rollback();
                                    return;
                                }

                                if (!sellerId.HasValue)
                                {
                                    MessageBox.Show($"Не вдалося визначити продавця для товару {item.Name}.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                                    transaction.Rollback();
                                    return;
                                }

                                try
                                {
                                    using (var command = new NpgsqlCommand(
                                        "INSERT INTO orders (buyerid, sellerid, productid, quantity, totalprice, orderdate, status, pickup_point_id, payment_method_id, shipping_region, contact_first_name, contact_middle_name, contact_phone, contact_last_name) " +
                                        "VALUES (@buyerid, @sellerid, @productid, @quantity, @totalprice, @orderdate, @status, @pickup_point_id, @payment_method_id, @shipping_region, @contact_first_name, @contact_middle_name, @contact_phone, @contact_last_name)", connection))
                                    {
                                        command.Parameters.AddWithValue("buyerid", buyerIdValue);
                                        command.Parameters.AddWithValue("sellerid", sellerId.Value);
                                        command.Parameters.AddWithValue("productid", item.ProductId);
                                        command.Parameters.AddWithValue("quantity", item.Quantity); // Використовуємо актуальну кількість
                                        command.Parameters.AddWithValue("totalprice", (double)(item.Price * item.Quantity));
                                        command.Parameters.AddWithValue("orderdate", DateTime.Now);
                                        command.Parameters.AddWithValue("status", "Ожидает отправки");
                                        command.Parameters.AddWithValue("pickup_point_id", pickupPointId.Value);
                                        command.Parameters.AddWithValue("payment_method_id", paymentMethodId.Value);
                                        command.Parameters.AddWithValue("shipping_region", shippingRegion);
                                        command.Parameters.AddWithValue("contact_first_name", firstName);
                                        command.Parameters.AddWithValue("contact_middle_name", string.IsNullOrWhiteSpace(middleName) ? (object)DBNull.Value : middleName);
                                        command.Parameters.AddWithValue("contact_phone", phone);
                                        command.Parameters.AddWithValue("contact_last_name", lastName);
                                        command.Transaction = transaction;
                                        command.ExecuteNonQuery();
                                    }

                                    using (var updateCommand = new NpgsqlCommand("UPDATE products SET stock_quantity = stock_quantity - @quantity WHERE productid = @productId", connection))
                                    {
                                        updateCommand.Parameters.AddWithValue("quantity", item.Quantity);
                                        updateCommand.Parameters.AddWithValue("productId", item.ProductId);
                                        updateCommand.Transaction = transaction;
                                        updateCommand.ExecuteNonQuery();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"Помилка під час вставки замовлення для товару {item.Name}: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                                    transaction.Rollback();
                                    return;
                                }
                            }

                            if (requiresCardDetails)
                            {
                                try
                                {
                                    using (var command = new NpgsqlCommand("UPDATE userdetails SET balance = balance - @amount WHERE userid = @userId", connection))
                                    {
                                        command.Parameters.AddWithValue("amount", totalPrice);
                                        command.Parameters.AddWithValue("userId", buyerIdValue);
                                        command.Transaction = transaction;
                                        int rowsAffected = command.ExecuteNonQuery();
                                        if (rowsAffected == 0)
                                        {
                                            throw new Exception("Не вдалося оновити баланс користувача.");
                                        }
                                    }
                                    _userProfile.Balance = (decimal)_userProfile.Balance - totalPrice;
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"Помилка під час оновлення балансу: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                                    transaction.Rollback();
                                    return;
                                }
                            }

                            // Очищаємо кошик у базі даних після успішного оформлення замовлення
                            using (var clearCartCommand = new NpgsqlCommand("DELETE FROM cart WHERE buyerid = @buyerid", connection))
                            {
                                clearCartCommand.Parameters.AddWithValue("buyerid", buyerIdValue);
                                clearCartCommand.Transaction = transaction;
                                clearCartCommand.ExecuteNonQuery();
                            }

                            transaction.Commit();
                            MessageBox.Show($"Замовлення успішно оформлено!\nСпосіб оплати: {paymentMethodName}\nСума: {totalPrice:F2} грн\n" +
                                            (requiresCardDetails ? $"Залишок на балансі: {_userProfile.Balance:F2} грн" : ""),
                                            "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);

                            // Очищаємо локальний список товарів у кошику
                            _cartItems.Clear();
                            Close();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            MessageBox.Show($"Помилка під час оформлення замовлення: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Сталася помилка: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class PickupPoint
    {
        public int PickupPointId { get; set; }
        public string Address { get; set; }
        public string Region { get; set; }
        public override string ToString() => Address;
    }

    public class PaymentMethod
    {
        public int PaymentMethodId { get; set; }
        public string Name { get; set; }
        public int MethodId { get; internal set; }
        public bool IsActive { get; internal set; }
    }
}