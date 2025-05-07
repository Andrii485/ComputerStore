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
            "Вінницька область", "Волинська область", "Дніпропетровська область", "Донецька область",
            "Житомирська область", "Закарпатська область", "Запорізька область", "Івано-Франківська область",
            "Київська область", "Кіровоградська область", "Луганська область", "Львівська область",
            "Миколаївська область", "Одеська область", "Полтавська область", "Рівненська область",
            "Сумська область", "Тернопільська область", "Харківська область", "Херсонська область",
            "Хмельницька область", "Черкаська область", "Чернігівська область", "Чернівецька область",
            "Автономна Республіка Крим"
        };

        public OrderWindow(List<DbProduct> cartItems, UserProfile userProfile)
        {
            InitializeComponent();
            this.cartItems = cartItems;
            this.userProfile = userProfile;
            connectionString = ConfigurationManager.ConnectionStrings["ElitePCConnection"]?.ConnectionString;

            // Заповнюємо контактні дані з профілю користувача
            ContactFirstName.Text = userProfile.FirstName;
            ContactMiddleName.Text = userProfile.MiddleName;
            ContactPhone.Text = userProfile.Phone;
            ContactEmail.Text = userProfile.Email;

            // Завантажуємо області
            ShippingRegion.ItemsSource = regions;

            // Завантажуємо пункти самовивозу і способи оплати
            LoadPickupPoints();
            LoadPaymentMethods();

            // Встановлюємо обробник для фільтрації пунктів самовивозу за вибраною областю
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
                MessageBox.Show($"Помилка при завантаженні пунктів самовивозу: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show($"Помилка при завантаженні способів оплати: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
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

            // Перевірка заповненості обов'язкових полів
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrEmpty(shippingRegion) || pickupPointId == null || paymentMethodId == null)
            {
                MessageBox.Show("Заповніть усі обов'язкові поля (ім'я, телефон, email, область, пункт самовивозу, спосіб оплати).", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    // Отримуємо buyerid користувача
                    int buyerId;
                    using (var command = new NpgsqlCommand("SELECT user_id FROM user_credentials WHERE email = @email", connection))
                    {
                        command.Parameters.AddWithValue("email", userProfile.Email);
                        buyerId = (int)command.ExecuteScalar();
                    }

                    // Зберігаємо замовлення для кожного товару в кошику
                    foreach (var item in cartItems)
                    {
                        // Отримуємо sellerid для товару
                        int sellerId;
                        using (var command = new NpgsqlCommand("SELECT seller_id FROM products WHERE product_id = @product_id", connection))
                        {
                            command.Parameters.AddWithValue("product_id", item.ProductId);
                            sellerId = (int)command.ExecuteScalar();
                        }

                        // Зберігаємо замовлення в таблиці orders
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

                    MessageBox.Show("Замовлення успішно оформлено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при оформленні замовлення: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class UserProfile1
    {
        internal decimal Balance;

        public string Email { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string Phone { get; set; }
        public object UserId { get; internal set; }
        public string LastName { get; internal set; }
        public object Username { get; internal set; }
        public bool IsSeller { get; internal set; }
    }
}