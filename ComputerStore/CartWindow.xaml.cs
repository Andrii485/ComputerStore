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
            InitializeComponent();
            this.cartItems = cartItems ?? throw new ArgumentNullException(nameof(cartItems));
            this.userProfile = userProfile ?? throw new ArgumentNullException(nameof(userProfile));
            connectionString = ConfigurationManager.ConnectionStrings["ElitePCConnection"]?.ConnectionString;

            if (string.IsNullOrEmpty(connectionString))
            {
                MessageBox.Show("Строка подключения к базе данных не найдена.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            // Инициализация данных
            CartItemsList.ItemsSource = cartItems;
            CalculateTotalPrice();
            LoadContactDetails();
            LoadRegions();
            LoadPaymentMethods();
        }

        private void CalculateTotalPrice()
        {
            totalPrice = cartItems.Sum(item => item.Price);
            TotalPriceText.Text = $"Общая сумма: {totalPrice:F2} грн";
        }

        private void LoadContactDetails()
        {
            ContactLastName.Text = userProfile.LastName ?? "";
            ContactFirstName.Text = userProfile.FirstName ?? "";
            ContactMiddleName.Text = userProfile.MiddleName ?? "";
            ContactPhone.Text = userProfile.Phone ?? "";
        }

        private void LoadRegions()
        {
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
                    string selectedRegion = ShippingRegion.SelectedItem?.ToString();
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
                            PickupPoint.ItemsSource = pickupPoints;
                            if (pickupPoints.Any())
                            {
                                PickupPoint.SelectedIndex = 0;
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

                    // Проверяем, есть ли уже способы оплаты
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

                    // Если способы оплаты не найдены, добавляем их
                    if (!paymentMethods.Any())
                    {
                        using (var command = new NpgsqlCommand(
                            "INSERT INTO payment_methods (name, is_active) VALUES (@name, @is_active) ON CONFLICT (name) DO NOTHING", connection))
                        {
                            command.Parameters.AddWithValue("is_active", true);

                            // Добавляем "Оплата під час отримання товару"
                            command.Parameters.AddWithValue("name", "Оплата під час отримання товару");
                            command.ExecuteNonQuery();

                            // Добавляем "Оплатити зараз"
                            command.Parameters[0].Value = "Оплатити зараз";
                            command.ExecuteNonQuery();
                        }

                        // Повторно загружаем способы оплаты после добавления
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

                    // Устанавливаем способы оплаты в ComboBox
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
            var selectedPaymentMethod = (PaymentMethodsComboBox.SelectedItem as PaymentMethod)?.Name;
            if (selectedPaymentMethod == "Оплатити зараз")
            {
                CardDetailsPanel.Visibility = Visibility.Visible;
            }
            else
            {
                CardDetailsPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void RemoveFromCart_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int productId)
            {
                var productToRemove = cartItems.FirstOrDefault(p => p.ProductId == productId);
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
            if (!cartItems.Any())
            {
                MessageBox.Show("Корзина пуста.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Проверка userProfile.UserId
            if (userProfile.UserId == null || Convert.ToInt32(userProfile.UserId) <= 0)
            {
                MessageBox.Show("Не удалось определить идентификатор пользователя. Пожалуйста, войдите в систему.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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

            // Проверка заполненности обязательных полей
            if (string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(phone) ||
                string.IsNullOrEmpty(shippingRegion) || pickupPointId == null || paymentMethodId == null)
            {
                MessageBox.Show("Заполните все обязательные поля (фамилия, имя, телефон, область, пункт самовывоза, способ оплаты).", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Проверка данных карты, если выбран способ "Оплатити зараз"
            if (paymentMethodName == "Оплатити зараз")
            {
                string cardNumber = CardNumberTextBox.Text?.Trim() ?? string.Empty;
                string cardExpiry = CardExpiryTextBox.Text?.Trim() ?? string.Empty;
                string cardCvv = CardCvvTextBox.Text?.Trim() ?? string.Empty;

                // Простая валидация номера карты (16 цифр)
                if (!Regex.IsMatch(cardNumber, @"^\d{16}$"))
                {
                    MessageBox.Show("Введите корректный номер карты (16 цифр).", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Проверка срока действия (MM/YY)
                if (!Regex.IsMatch(cardExpiry, @"^(0[1-9]|1[0-2])\/\d{2}$"))
                {
                    MessageBox.Show("Введите корректный срок действия карты в формате MM/YY.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Проверка CVV (3 цифры)
                if (!Regex.IsMatch(cardCvv, @"^\d{3}$"))
                {
                    MessageBox.Show("Введите корректный CVV (3 цифры).", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                            // Сохраняем заказ для каждого товара в корзине
                            foreach (var item in cartItems)
                            {
                                // Получаем sellerid для товара
                                int? sellerId = null;
                                using (var command = new NpgsqlCommand("SELECT sellerid FROM products WHERE productid = @productId", connection))
                                {
                                    command.Parameters.AddWithValue("productId", item.ProductId);
                                    var result = command.ExecuteScalar();
                                    if (result != null && result != DBNull.Value)
                                    {
                                        sellerId = (int)result;
                                    }
                                }

                                // Сохраняем заказ в таблице orders
                                using (var command = new NpgsqlCommand(
                                    "INSERT INTO orders (buyerid, sellerid, productid, quantity, totalprice, orderdate, status, pickup_point_id, payment_method_id, shipping_region, contact_first_name, contact_middle_name, contact_phone, contact_last_name) " +
                                    "VALUES (@buyerid, @sellerid, @productid, @quantity, @totalprice, @orderdate, @status, @pickup_point_id, @payment_method_id, @shipping_region, @contact_first_name, @contact_middle_name, @contact_phone, @contact_last_name)", connection))
                                {
                                    command.Parameters.AddWithValue("buyerid", userProfile.UserId);
                                    command.Parameters.AddWithValue("sellerid", sellerId == null ? (object)DBNull.Value : sellerId);
                                    command.Parameters.AddWithValue("productid", item.ProductId);
                                    command.Parameters.AddWithValue("quantity", 1);
                                    command.Parameters.AddWithValue("totalprice", (double)item.Price);
                                    command.Parameters.AddWithValue("orderdate", DateTime.Now);
                                    command.Parameters.AddWithValue("status", "Pending");
                                    command.Parameters.AddWithValue("pickup_point_id", pickupPointId);
                                    command.Parameters.AddWithValue("payment_method_id", paymentMethodId);
                                    command.Parameters.AddWithValue("shipping_region", shippingRegion);
                                    command.Parameters.AddWithValue("contact_first_name", firstName);
                                    command.Parameters.AddWithValue("contact_middle_name", string.IsNullOrWhiteSpace(middleName) ? (object)DBNull.Value : middleName);
                                    command.Parameters.AddWithValue("contact_phone", phone);
                                    command.Parameters.AddWithValue("contact_last_name", lastName);
                                    command.Transaction = transaction;
                                    command.ExecuteNonQuery();
                                }
                            }

                            transaction.Commit();
                            MessageBox.Show($"Заказ успешно оформлен!\nСпособ оплаты: {paymentMethodName}\nСумма: {totalPrice:F2} грн", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                            this.DialogResult = true; // Устанавливаем результат диалога
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
    }
}