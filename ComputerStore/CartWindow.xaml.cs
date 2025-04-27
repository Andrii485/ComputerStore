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
    public partial class CartWindow : Window
    {
        private List<DbProduct> cartItems;
        private UserProfile userProfile;
        private string connectionString;
        private decimal totalPrice;

        // Изменяем модификатор доступа конструктора на internal для диагностики
        internal CartWindow(List<DbProduct> cartItems, UserProfile userProfile)
        {
            InitializeComponent();
            this.cartItems = cartItems;
            this.userProfile = userProfile;
            connectionString = ConfigurationManager.ConnectionStrings["ElitePCConnection"]?.ConnectionString;

            if (string.IsNullOrEmpty(connectionString))
            {
                MessageBox.Show("Строка подключения к базе данных не найдена.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            CartItemsList.ItemsSource = cartItems;
            CalculateTotalPrice();
            LoadPaymentMethods();
        }

        private void CalculateTotalPrice()
        {
            totalPrice = cartItems.Sum(item => item.Price);
            TotalPriceText.Text = $"Итого: {totalPrice:F2} грн";
        }

        private void LoadPaymentMethods()
        {
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("SELECT id, name FROM payment_methods WHERE is_active = TRUE", connection))
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
                            PaymentMethodsComboBox.ItemsSource = paymentMethods;
                            PaymentMethodsComboBox.DisplayMemberPath = "Name";
                            PaymentMethodsComboBox.SelectedValuePath = "PaymentMethodId";
                            if (paymentMethods.Any())
                            {
                                PaymentMethodsComboBox.SelectedIndex = 0;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке способов оплаты: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveFromCart_Click(object sender, RoutedEventArgs e)
        {
            int productId = (int)((Button)sender).Tag;
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

        private void Checkout_Click(object sender, RoutedEventArgs e)
        {
            if (!cartItems.Any())
            {
                MessageBox.Show("Корзина пуста.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (PaymentMethodsComboBox.SelectedItem == null)
            {
                MessageBox.Show("Выберите способ оплаты.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int paymentMethodId = (int)PaymentMethodsComboBox.SelectedValue;
            string paymentMethodName = ((PaymentMethod)PaymentMethodsComboBox.SelectedItem).Name;

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // Создаем заказ
                            int orderId;
                            using (var command = new NpgsqlCommand("INSERT INTO Orders (buyerid, orderdate, totalprice, payment_method_id) VALUES (@userId, @orderDate, @totalAmount, @paymentMethodId) RETURNING orderid", connection))
                            {
                                command.Parameters.AddWithValue("userId", userProfile.UserId);
                                command.Parameters.AddWithValue("orderDate", DateTime.Now);
                                command.Parameters.AddWithValue("totalAmount", (double)totalPrice);
                                command.Parameters.AddWithValue("paymentMethodId", paymentMethodId);
                                command.Transaction = transaction;
                                orderId = (int)command.ExecuteScalar();
                            }

                            // Добавляем товары в заказ
                            foreach (var item in cartItems)
                            {
                                // Получаем sellerid для товара
                                int? sellerId = null;
                                using (var command = new NpgsqlCommand("SELECT seller_id FROM products WHERE product_id = @product_id", connection))
                                {
                                    command.Parameters.AddWithValue("product_id", item.ProductId);
                                    var result = command.ExecuteScalar();
                                    if (result != null && result != DBNull.Value)
                                    {
                                        sellerId = (int)result;
                                    }
                                }

                                using (var command = new NpgsqlCommand("INSERT INTO orders (buyerid, sellerid, productid, quantity, totalprice, orderdate, status, payment_method_id) VALUES (@buyerid, @sellerid, @productId, @quantity, @price, @orderdate, @status, @paymentMethodId)", connection))
                                {
                                    command.Parameters.AddWithValue("buyerid", userProfile.UserId);
                                    command.Parameters.AddWithValue("sellerid", sellerId == null ? (object)DBNull.Value : sellerId);
                                    command.Parameters.AddWithValue("productId", item.ProductId);
                                    command.Parameters.AddWithValue("quantity", 1);
                                    command.Parameters.AddWithValue("price", (double)item.Price);
                                    command.Parameters.AddWithValue("orderdate", DateTime.Now);
                                    command.Parameters.AddWithValue("status", "Pending");
                                    command.Parameters.AddWithValue("paymentMethodId", paymentMethodId);
                                    command.Transaction = transaction;
                                    command.ExecuteNonQuery();
                                }
                            }

                            transaction.Commit();
                            MessageBox.Show($"Заказ успешно оформлен!\nСпособ оплаты: {paymentMethodName}\nСумма: {totalPrice:F2} грн", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                            cartItems.Clear();
                            CartItemsList.ItemsSource = null;
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