using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Npgsql;
using System.Configuration;
using System.Windows.Threading;
using System.Threading.Tasks;
using ElmirClone.Models;

namespace ElmirClone
{
    public partial class OrdersWindow : Window
    {
        private List<OrderDisplay> orders;
        private DispatcherTimer orderStatusTimer;
        private HashSet<int> notifiedOrders;
        private int? selectedOrderId;
        private string connectionString;
        private UserProfile userProfile;
        private int buyerId;

        public OrdersWindow(UserProfile userProfile)
        {
            InitializeComponent();
            this.userProfile = userProfile;
            connectionString = ConfigurationManager.ConnectionStrings["ElitePCConnection"]?.ConnectionString;
            if (string.IsNullOrEmpty(connectionString))
            {
                MessageBox.Show("Рядок підключення до бази даних не знайдено.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            orders = new List<OrderDisplay>();
            notifiedOrders = new HashSet<int>();
            selectedOrderId = null;

            orderStatusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            orderStatusTimer.Tick += (s, e) => CheckOrderStatus();
            orderStatusTimer.Start();

            if (userProfile?.UserId is int id && id > 0)
            {
                buyerId = id;
                LoadOrders(buyerId);
                InitializeFilters();
            }
            else
            {
                MessageBox.Show("Необхідно авторизуватися.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void InitializeFilters()
        {
            StatusFilterComboBox.Items.Add("Всі");
            StatusFilterComboBox.Items.Add("Новий");
            StatusFilterComboBox.Items.Add("Обробляється");
            StatusFilterComboBox.Items.Add("Відправлено");
            StatusFilterComboBox.Items.Add("Доставлено");
            StatusFilterComboBox.SelectedIndex = 0;

            SortOrderComboBox.Items.Add("Дата: новіші зверху");
            SortOrderComboBox.Items.Add("Дата: старіші зверху");
            SortOrderComboBox.SelectedIndex = 0;

            StatusFilterComboBox.SelectionChanged += FilterOrders;
            SortOrderComboBox.SelectionChanged += FilterOrders;
        }

        private void LoadOrders(int buyerId)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => LoadOrders(buyerId));
                return;
            }

            try
            {
                orders.Clear();
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand(
                        "SELECT o.orderid, o.orderdate, o.totalprice, o.status, pp.address, p.name, o.quantity, o.sellerid " +
                        "FROM orders o " +
                        "JOIN products p ON o.productid = p.productid " +
                        "LEFT JOIN pickup_points pp ON o.pickup_point_id = pp.pickup_point_id " +
                        "WHERE o.buyerid = @buyerId", connection))
                    {
                        command.Parameters.AddWithValue("buyerId", buyerId);
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                orders.Add(new OrderDisplay
                                {
                                    OrderId = reader.GetInt32(0),
                                    OrderDate = reader.GetDateTime(1),
                                    TotalPrice = reader.GetDecimal(2),
                                    Status = reader.GetString(3),
                                    DeliveryAddress = reader.IsDBNull(4) ? "Немає адреси" : reader.GetString(4),
                                    ProductName = reader.GetString(5),
                                    Quantity = reader.GetInt32(6),
                                    SellerId = reader.GetInt32(7)
                                });
                            }
                        }
                    }
                }
                FilterOrders(null, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при завантаженні замовлень: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                OrdersList.ItemsSource = new List<OrderDisplay> { new OrderDisplay { Status = "Помилка завантаження" } };
            }
        }

        private void FilterOrders(object sender, SelectionChangedEventArgs e)
        {
            string selectedStatus = StatusFilterComboBox.SelectedItem?.ToString();
            string selectedSort = SortOrderComboBox.SelectedItem?.ToString();

            var filteredOrders = orders.AsEnumerable();
            if (selectedStatus != "Всі")
            {
                filteredOrders = filteredOrders.Where(o => o.Status == selectedStatus);
            }

            if (selectedSort == "Дата: новіші зверху")
            {
                filteredOrders = filteredOrders.OrderByDescending(o => o.OrderDate);
            }
            else
            {
                filteredOrders = filteredOrders.OrderBy(o => o.OrderDate);
            }

            OrdersList.ItemsSource = null;
            OrdersList.ItemsSource = filteredOrders.ToList();
        }

        private void OrdersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (OrdersList.SelectedItem is OrderDisplay selectedOrder)
            {
                selectedOrderId = selectedOrder.OrderId;
                CloseOrderButton.IsEnabled = selectedOrder.Status == "Обробляється";
                ConfirmReceiptButton.IsEnabled = selectedOrder.Status == "Доставлено";
                ReturnButton.IsEnabled = selectedOrder.Status == "Доставлено";
            }
            else
            {
                selectedOrderId = null;
                CloseOrderButton.IsEnabled = false;
                ConfirmReceiptButton.IsEnabled = false;
                ReturnButton.IsEnabled = false;
            }
        }

        private void CloseOrderButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedOrderId.HasValue)
            {
                if (MessageBox.Show("Ви впевнені, що хочете скасувати замовлення?", "Підтвердження", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var connection = new NpgsqlConnection(connectionString))
                        {
                            connection.Open();
                            using (var command = new NpgsqlCommand(
                                "UPDATE orders SET status = 'Скасовано' WHERE orderid = @orderId AND buyerid = @buyerId", connection))
                            {
                                command.Parameters.AddWithValue("orderId", selectedOrderId.Value);
                                command.Parameters.AddWithValue("buyerId", buyerId);
                                int rowsAffected = command.ExecuteNonQuery();
                                if (rowsAffected > 0)
                                {
                                    LoadOrders(buyerId);
                                    MessageBox.Show("Замовлення скасовано.", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                                }
                                else
                                {
                                    MessageBox.Show("Замовлення не знайдено.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Помилка при скасуванні замовлення: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void ConfirmReceiptButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedOrderId.HasValue)
            {
                try
                {
                    using (var connection = new NpgsqlConnection(connectionString))
                    {
                        connection.Open();
                        using (var transaction = connection.BeginTransaction())
                        {
                            // Получаем информацию о заказе
                            int sellerId = 0;
                            decimal totalPrice = 0;
                            using (var command = new NpgsqlCommand(
                                "SELECT sellerid, totalprice FROM orders WHERE orderid = @orderId AND buyerid = @buyerId", connection))
                            {
                                command.Parameters.AddWithValue("orderId", selectedOrderId.Value);
                                command.Parameters.AddWithValue("buyerId", buyerId);
                                using (var reader = command.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        sellerId = reader.GetInt32(0);
                                        totalPrice = reader.GetDecimal(1);
                                    }
                                    else
                                    {
                                        transaction.Rollback();
                                        MessageBox.Show("Замовлення не знайдено.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                                        return;
                                    }
                                }
                            }

                            // Обновляем баланс продавца
                            using (var command = new NpgsqlCommand(
                                "UPDATE usercredentials SET balance = balance + @totalPrice WHERE userid = @sellerId", connection))
                            {
                                command.Parameters.AddWithValue("totalPrice", totalPrice);
                                command.Parameters.AddWithValue("sellerId", sellerId);
                                command.ExecuteNonQuery();
                            }

                            // Подтверждаем заказ
                            using (var command = new NpgsqlCommand(
                                "UPDATE orders SET status = 'Завершено' WHERE orderid = @orderId AND buyerid = @buyerId", connection))
                            {
                                command.Parameters.AddWithValue("orderId", selectedOrderId.Value);
                                command.Parameters.AddWithValue("buyerId", buyerId);
                                int rowsAffected = command.ExecuteNonQuery();
                                if (rowsAffected > 0)
                                {
                                    transaction.Commit();
                                    LoadOrders(buyerId);
                                    MessageBox.Show("Замовлення підтверджено, кошти переведено продавцю.", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                                }
                                else
                                {
                                    transaction.Rollback();
                                    MessageBox.Show("Замовлення не знайдено.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка при підтвердженні замовлення: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ReturnButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedOrderId.HasValue)
            {
                if (MessageBox.Show("Ви впевнені, що хочете повернути замовлення?", "Підтвердження", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var connection = new NpgsqlConnection(connectionString))
                        {
                            connection.Open();
                            using (var command = new NpgsqlCommand(
                                "UPDATE orders SET status = 'Повернення' WHERE orderid = @orderId AND buyerid = @buyerId", connection))
                            {
                                command.Parameters.AddWithValue("orderId", selectedOrderId.Value);
                                command.Parameters.AddWithValue("buyerId", buyerId);
                                int rowsAffected = command.ExecuteNonQuery();
                                if (rowsAffected > 0)
                                {
                                    LoadOrders(buyerId);
                                    MessageBox.Show("Замовлення відправлено на повернення.", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                                }
                                else
                                {
                                    MessageBox.Show("Замовлення не знайдено.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Помилка при запиті на повернення: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void BackToShoppingButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void RetryOrderLoad_Click(object sender, RoutedEventArgs e)
        {
            OrdersList.ItemsSource = null; // Очищаем текущий список
            LoadOrders(buyerId); // Загружаем заказы заново
        }

        private async void CheckOrderStatus()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(CheckOrderStatus);
                return;
            }

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new NpgsqlCommand(
                        "SELECT orderid, status FROM orders WHERE buyerid = @buyerId AND status IN ('Обробляється', 'Відправлено', 'Доставлено')", connection))
                    {
                        command.Parameters.AddWithValue("buyerId", buyerId);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                int orderId = reader.GetInt32(0);
                                string status = reader.GetString(1);
                                var order = orders.FirstOrDefault(o => o.OrderId == orderId);
                                if (order != null && !notifiedOrders.Contains(orderId))
                                {
                                    switch (status)
                                    {
                                        case "Відправлено":
                                            notifiedOrders.Add(orderId);
                                            MessageBox.Show($"Ваше замовлення #{orderId} відправлено!", "Оновлення статусу", MessageBoxButton.OK, MessageBoxImage.Information);
                                            break;
                                        case "Доставлено":
                                            notifiedOrders.Add(orderId);
                                            MessageBox.Show($"Ваше замовлення #{orderId} доставлено!", "Оновлення статусу", MessageBoxButton.OK, MessageBoxImage.Information);
                                            break;
                                    }
                                    order.Status = status;
                                }
                            }
                        }
                    }
                }
                LoadOrders(buyerId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при перевірці статусу замовлення: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class OrderDisplay
    {
        public int OrderId { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal TotalPrice { get; set; }
        public string Status { get; set; }
        public string DeliveryAddress { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public int SellerId { get; set; }
    }
}