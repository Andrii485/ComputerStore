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
            "Винницкая область", "Волынская область", "Днепропетровская область", "Донецкая область",
            "Житомирская область", "Закарпатская область", "Запорожская область", "Ивано-Франковская область",
            "Киевская область", "Кировоградская область", "Луганская область", "Львовская область",
            "Николаевская область", "Одесская область", "Полтавская область", "Ровенская область",
            "Сумская область", "Тернопольская область", "Харьковская область", "Херсонская область",
            "Хмельницкая область", "Черкасская область", "Черниговская область", "Черновицкая область",
            "Автономная Республика Крым"
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
                MessageBox.Show("Строка подключения к базе данных не найдена.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (this.userProfile == null || !(this.userProfile.UserId is int id) || id <= 0)
            {
                MessageBox.Show("Пользователь не авторизован или идентификатор пользователя некорректен. Перенаправляем на страницу входа.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show($"Ошибка при инициализации корзины: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show($"Ошибка при перенаправлении на страницу входа: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CalculateTotalPrice()
        {
            totalPrice = (cartItems != null) ? cartItems.Sum(item => item.Price) : 0;
            TotalPriceText.Text = $"Общая сумма: {totalPrice:F2} грн";
        }

        private void LoadContactDetails()
        {
            if (userProfile == null)
            {
                MessageBox.Show("Профиль пользователя не загружен. Перенаправляем на страницу входа.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show("Элемент ShippingRegion не найден в разметке.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                                MessageBox.Show("Элемент PickupPoint не найден в разметке.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                            PickupPoint.ItemsSource = pickupPoints;
                            if (pickupPoints.Any())
                            {
                                PickupPoint.SelectedIndex = 0;
                            }
                            else
                            {
                                MessageBox.Show("Пункты самовывоза не найдены для выбранного региона.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке пунктов самовывоза: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    using (var command = new NpgsqlCommand("SELECT methodid, name FROM payment_methods WHERE name IN ('Оплата під час отримання товару', 'Оплатити зараз') AND is_active = TRUE", connection))
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

                    if (!paymentMethods.Any())
                    {
                        using (var command = new NpgsqlCommand(
                            "INSERT INTO payment_methods (name, is_active) VALUES (@name, @is_active) ON CONFLICT (name) DO NOTHING", connection))
                        {
                            command.Parameters.AddWithValue("is_active", true);

                            command.Parameters.AddWithValue("name", "Оплата під час отримання товару");
                            command.ExecuteNonQuery();

                            command.Parameters[0].Value = "Оплатити зараз";
                            command.ExecuteNonQuery();
                        }

                        using (var command = new NpgsqlCommand("SELECT methodid, name FROM payment_methods WHERE name IN ('Оплата під час отримання товару', 'Оплатити зараз') AND is_active = TRUE", connection))
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
                    }

                    if (PaymentMethodsComboBox == null)
                    {
                        MessageBox.Show("Элемент PaymentMethodsComboBox не найден в разметке.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        MessageBox.Show("Не удалось добавить способы оплаты в базу данных.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке способов оплаты: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PaymentMethodsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedPaymentMethod = (PaymentMethodsComboBox?.SelectedItem as PaymentMethod)?.Name;
            if (selectedPaymentMethod == "Оплатити зараз")
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
                    MessageBox.Show($"{productToRemove.Name} удален из корзины.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
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
                MessageBox.Show("Корзина пуста.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (userProfile == null)
            {
                MessageBox.Show("Профиль пользователя не загружен. Перенаправляем на страницу входа.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                RedirectToLogin();
                return;
            }

            if (!(userProfile.UserId is int buyerIdValue) || buyerIdValue <= 0)
            {
                MessageBox.Show($"Идентификатор пользователя некорректен (UserId = {userProfile.UserId}). Перенаправляем на страницу входа.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                RedirectToLogin();
                return;
            }

            if (ContactLastName == null || ContactFirstName == null || ContactMiddleName == null || ContactPhone == null ||
                ShippingRegion == null || PickupPoint == null || PaymentMethodsComboBox == null)
            {
                MessageBox.Show("Одно или несколько полей формы не найдены в разметке. Проверьте XAML.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show("Заполните все обязательные поля (фамилия, имя, телефон, область, пункт самовывоза, способ оплаты).", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Проверка данных карты, если выбран способ оплаты "Оплатити зараз"
            if (paymentMethodName == "Оплатити зараз")
            {
                if (CardNumberTextBox == null || CardExpiryTextBox == null || CardCvvTextBox == null)
                {
                    MessageBox.Show("Поля для данных карты не найдены в разметке. Проверьте XAML.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string cardNumber = CardNumberTextBox.Text?.Trim() ?? string.Empty;
                string cardExpiry = CardExpiryTextBox.Text?.Trim() ?? string.Empty;
                string cardCvv = CardCvvTextBox.Text?.Trim() ?? string.Empty;

                if (!Regex.IsMatch(cardNumber, @"^\d{16}$"))
                {
                    MessageBox.Show("Введите корректный номер карты (16 цифр).", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!Regex.IsMatch(cardExpiry, @"^(0[1-9]|1[0-2])\/\d{2}$"))
                {
                    MessageBox.Show("Введите корректный срок действия карты в формате MM/YY.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!Regex.IsMatch(cardCvv, @"^\d{3}$"))
                {
                    MessageBox.Show("Введите корректный CVV (3 цифры).", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Проверка баланса пользователя
                if (userProfile.Balance < totalPrice)
                {
                    MessageBox.Show($"Недостаточно денег для совершения покупки. Ваш баланс: {userProfile.Balance:F2} грн, требуется: {totalPrice:F2} грн.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                                    MessageBox.Show($"Некорректные данные товара в корзине: {item?.Name ?? "Неизвестный товар"}.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                                    MessageBox.Show($"Ошибка при получении sellerId для товара {item.Name}: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                                    MessageBox.Show($"Ошибка при вставке заказа для товара {item.Name}: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                                    transaction.Rollback();
                                    return;
                                }
                            }

                            // Если выбран способ оплаты "Оплатити зараз", обновляем баланс пользователя
                            if (paymentMethodName == "Оплатити зараз")
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
                                            throw new Exception("Не удалось обновить баланс пользователя.");
                                        }
                                    }

                                    // Обновляем баланс в объекте userProfile
                                    userProfile.Balance -= totalPrice;
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"Ошибка при обновлении баланса: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                                    transaction.Rollback();
                                    return;
                                }
                            }

                            transaction.Commit();
                            MessageBox.Show($"Заказ успешно оформлен!\nСпособ оплаты: {paymentMethodName}\nСумма: {totalPrice:F2} грн\n" +
                                            (paymentMethodName == "Оплатити зараз" ? $"Остаток на балансе: {userProfile.Balance:F2} грн" : ""),
                                            "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                            this.DialogResult = true;
                            Close();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            MessageBox.Show($"Ошибка при оформлении заказа: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        internal bool CanShowDialog()
        {
            return isInitializedSuccessfully;
        }
    }
}