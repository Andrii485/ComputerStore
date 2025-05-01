using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Npgsql;
using System.Configuration;
using ElmirClone.Models;

namespace ElmirClone
{
    public partial class OrderWindow : Window
    {
        private List<DbProduct> cartItems;
        private UserProfile userProfile;
        private string connectionString;
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

        public OrderWindow(List<DbProduct> cartItems, UserProfile userProfile)
        {
            InitializeComponent();
            this.cartItems = cartItems;
            this.userProfile = userProfile;
            connectionString = ConfigurationManager.ConnectionStrings["ElitePCConnection"]?.ConnectionString;

            // Заполняем контактные данные из профиля пользователя
            ContactFirstName.Text = userProfile.FirstName;
            ContactMiddleName.Text = userProfile.MiddleName;
            ContactPhone.Text = userProfile.Phone;
            ContactEmail.Text = userProfile.Email;

            // Загружаем области
            ShippingRegion.ItemsSource = regions;

            // Загружаем пункты самовывоза и способы оплаты
            LoadPickupPoints();
            LoadPaymentMethods();

            // Устанавливаем обработчик для фильтрации пунктов самовывоза по выбранной области
            ShippingRegion.SelectionChanged += ShippingRegion_SelectionChanged;
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
                    using (var command = new NpgsqlCommand("SELECT payment_method_id, name FROM payment_methods", connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            var paymentMethods = new List<PaymentMethod>();
                            while (reader.Read())
                            {
                                paymentMethods.Add(new PaymentMethod
                                {
                                    PaymentMethodId = reader.GetInt32(0),
                                    Name = reader.GetString(1)
                                });
                            }
                            PaymentMethod.ItemsSource = paymentMethods;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке способов оплаты: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ConfirmOrder_Click(object sender, RoutedEventArgs e)
        {
            string firstName = ContactFirstName.Text.Trim();
            string middleName = ContactMiddleName.Text.Trim();
            string phone = ContactPhone.Text.Trim();
            string email = ContactEmail.Text.Trim();
            string shippingRegion = ShippingRegion.SelectedItem?.ToString();
            int? pickupPointId = PickupPoint.SelectedValue as int?;
            int? paymentMethodId = PaymentMethod.SelectedValue as int?;

            // Проверка заполненности обязательных полей
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrEmpty(shippingRegion) || pickupPointId == null || paymentMethodId == null)
            {
                MessageBox.Show("Заполните все обязательные поля (имя, телефон, email, область, пункт самовывоза, способ оплаты).", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    // Получаем buyerid пользователя
                    int buyerId;
                    using (var command = new NpgsqlCommand("SELECT user_id FROM user_credentials WHERE email = @email", connection))
                    {
                        command.Parameters.AddWithValue("email", userProfile.Email);
                        buyerId = (int)command.ExecuteScalar();
                    }

                    // Сохраняем заказ для каждого товара в корзине
                    foreach (var item in cartItems)
                    {
                        // Получаем sellerid для товара
                        int sellerId;
                        using (var command = new NpgsqlCommand("SELECT seller_id FROM products WHERE product_id = @product_id", connection))
                        {
                            command.Parameters.AddWithValue("product_id", item.ProductId);
                            sellerId = (int)command.ExecuteScalar();
                        }

                        // Сохраняем заказ в таблице orders
                        using (var command = new NpgsqlCommand(
                            "INSERT INTO orders (buyerid, sellerid, productid, quantity, totalprice, orderdate, status, pickup_point_id, payment_method_id, shipping_region, contact_first_name, contact_middle_name, contact_phone, contact_email) " +
                            "VALUES (@buyerid, @sellerid, @productid, @quantity, @totalprice, @orderdate, @status, @pickup_point_id, @payment_method_id, @shipping_region, @contact_first_name, @contact_middle_name, @contact_phone, @contact_email)", connection))
                        {
                            command.Parameters.AddWithValue("buyerid", buyerId);
                            command.Parameters.AddWithValue("sellerid", sellerId);
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
                            command.Parameters.AddWithValue("contact_email", email);
                            command.ExecuteNonQuery();
                        }
                    }

                    MessageBox.Show("Заказ успешно оформлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при оформлении заказа: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class UserProfile
    {
        internal decimal Balance;

        public string Email { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string Phone { get; set; }
        public object UserId { get; internal set; }
        public string LastName { get; internal set; }
    }
}