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
        private List<DbProduct> cartItems;
        private UserProfile userProfile;
        private string connectionString;
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

        internal CartWindow(List<DbProduct> cartItems, UserProfile userProfile)
        {
            isInitializedSuccessfully = false;
            InitializeComponent();
            this.cartItems = cartItems ?? throw new ArgumentNullException(nameof(cartItems));
            this.userProfile = userProfile;
            connectionString = ConfigurationManager.ConnectionStrings["ElitePCConnection"]?.ConnectionString;

            if (string.IsNullOrEmpty(connectionString))
            {
                MessageBox.Show("Рядок підключення до бази даних не знайдено.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (this.userProfile == null || !(this.userProfile.UserId is int id) || id <= 0)
            {
                MessageBox.Show("Користувач не авторизований або ідентифікатор користувача некоректний. Перенаправляємо на сторінку входу.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                CartItemsList.ItemsSource = cartItems;
                CalculateTotalPrice();
                LoadContactDetails();
                LoadRegions();
                LoadPaymentMethods();
                isInitializedSuccessfully = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час ініціалізації кошика: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RedirectToLogin()
        {
            try
            {
                LoginWindow loginWindow = new LoginWindow();
                loginWindow.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час перенаправлення на сторінку входу: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CalculateTotalPrice()
        {
            totalPrice = (cartItems != null) ? cartItems.Sum(item => item.Price) : 0;
            TotalPriceText.Text = $"Загальна сума: {totalPrice:F2} грн";
        }

        private void LoadContactDetails()
        {
            if (userProfile == null)
            {
                MessageBox.Show("Профіль користувача не завантажено. Перенаправляємо на сторінку входу.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ContactLastName.Text = userProfile.LastName ?? "";
            ContactFirstName.Text = userProfile.FirstName ?? "";
            ContactMiddleName.Text = userProfile.MiddleName ?? "";
            ContactPhone.Text = userProfile.Phone ?? "";
        }

        private void LoadRegions()
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
                using (var connection = new NpgsqlConnection(connectionString))
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
                using (var connection = new NpgsqlConnection(connectionString))
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
            // Припускаємо, що способи оплати, які містять "карт", "оплатити зараз" або "онлайн", потребують введення даних картки
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

        private void RemoveFromCart_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int productId)
            {
                var productToRemove = cartItems?.FirstOrDefault(p => p.ProductId == productId);
                if (productToRemove != null)
                {
                    cartItems.Remove(productToRemove);
                    CartItemsList.ItemsSource = null;
                    CartItemsList.ItemsSource = cartItems;
                    CalculateTotalPrice();
                    MessageBox.Show($"{productToRemove.Name} видалено з кошика.", "Інформація", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private UserProfile GetUserProfile()
        {
            return userProfile;
        }

        private void Checkout_Click(object sender, RoutedEventArgs e)
        {
            if (cartItems == null || !cartItems.Any())
            {
                MessageBox.Show("Кошик порожній.", "Попередження", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (userProfile == null)
            {
                MessageBox.Show("Профіль користувача не завантажено. Перенаправляємо на сторінку входу.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                RedirectToLogin();
                return;
            }

            if (!(userProfile.UserId is int buyerIdValue) || buyerIdValue <= 0)
            {
                MessageBox.Show($"Ідентифікатор користувача некоректний (UserId = {userProfile.UserId}). Перенаправляємо на сторінку входу.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                RedirectToLogin();
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

            // Перевірка даних картки, якщо потрібно
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

                // Перевірка балансу користувача
                if (userProfile.Balance < totalPrice)
                {
                    MessageBox.Show($"Недостатньо коштів для здійснення покупки. Ваш баланс: {userProfile.Balance:F2} грн, потрібно: {totalPrice:F2} грн.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            foreach (var item in cartItems)
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

                                try
                                {
                                    using (var command = new NpgsqlCommand(
                                        "INSERT INTO orders (buyerid, sellerid, productid, quantity, totalprice, orderdate, status, pickup_point_id, payment_method_id, shipping_region, contact_first_name, contact_middle_name, contact_phone, contact_last_name) " +
                                        "VALUES (@buyerid, @sellerid, @productid, @quantity, @totalprice, @orderdate, @status, @pickup_point_id, @payment_method_id, @shipping_region, @contact_first_name, @contact_middle_name, @contact_phone, @contact_last_name)", connection))
                                    {
                                        command.Parameters.AddWithValue("buyerid", buyerIdValue);
                                        command.Parameters.AddWithValue("sellerid", sellerId.HasValue ? (object)sellerId.Value : DBNull.Value);
                                        command.Parameters.AddWithValue("productid", item.ProductId);
                                        command.Parameters.AddWithValue("quantity", 1);
                                        command.Parameters.AddWithValue("totalprice", (double)item.Price);
                                        command.Parameters.AddWithValue("orderdate", DateTime.Now);
                                        command.Parameters.AddWithValue("status", "Pending");
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
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"Помилка під час вставки замовлення для товару {item.Name}: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                                    transaction.Rollback();
                                    return;
                                }
                            }

                            // Якщо потрібна оплата з картки, оновлюємо баланс користувача
                            if (requiresCardDetails)
                            {
                                try
                                {
                                    using (var command = new NpgsqlCommand("UPDATE UserDetails SET Balance = Balance - @amount WHERE UserId = @userId", connection))
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

                                    // Оновлюємо баланс в об'єкті userProfile
                                    userProfile.Balance -= totalPrice;
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"Помилка під час оновлення балансу: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                                    transaction.Rollback();
                                    return;
                                }
                            }

                            transaction.Commit();
                            MessageBox.Show($"Замовлення успішно оформлено!\nСпосіб оплати: {paymentMethodName}\nСума: {totalPrice:F2} грн\n" +
                                            (requiresCardDetails ? $"Залишок на балансі: {userProfile.Balance:F2} грн" : ""),
                                            "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                            this.DialogResult = true;
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

        internal bool CanShowDialog()
        {
            return isInitializedSuccessfully;
        }
    }
}