using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Npgsql;
using System.Configuration;

namespace ElmirClone
{
    public partial class SellerWindow : Window
    {
        private string connectionString;
        private int sellerId;

        public SellerWindow(int sellerId)
        {
            InitializeComponent();
            this.sellerId = sellerId;
            connectionString = ConfigurationManager.ConnectionStrings["ElitePCConnection"]?.ConnectionString;
            if (string.IsNullOrEmpty(connectionString))
            {
                MessageBox.Show("Строка подключения к базе данных не найдена.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            LoadCategories();
            LoadProducts();
            LoadOrders();
            LoadFinancials();
            LoadStoreProfile();
        }

        // Переключение панелей
        private void ShowProductsPanel_Click(object sender, RoutedEventArgs e)
        {
            ProductsPanel.Visibility = Visibility.Visible;
            OrdersPanel.Visibility = Visibility.Collapsed;
            FinancePanel.Visibility = Visibility.Collapsed;
            ProfilePanel.Visibility = Visibility.Collapsed;
        }

        private void ShowOrdersPanel_Click(object sender, RoutedEventArgs e)
        {
            ProductsPanel.Visibility = Visibility.Collapsed;
            OrdersPanel.Visibility = Visibility.Visible;
            FinancePanel.Visibility = Visibility.Collapsed;
            ProfilePanel.Visibility = Visibility.Collapsed;
        }

        private void ShowFinancePanel_Click(object sender, RoutedEventArgs e)
        {
            ProductsPanel.Visibility = Visibility.Collapsed;
            OrdersPanel.Visibility = Visibility.Collapsed;
            FinancePanel.Visibility = Visibility.Visible;
            ProfilePanel.Visibility = Visibility.Collapsed;
        }

        private void ShowProfilePanel_Click(object sender, RoutedEventArgs e)
        {
            ProductsPanel.Visibility = Visibility.Collapsed;
            OrdersPanel.Visibility = Visibility.Collapsed;
            FinancePanel.Visibility = Visibility.Collapsed;
            ProfilePanel.Visibility = Visibility.Visible;
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            LoginWindow loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }

        // Управление товарами
        private void LoadCategories()
        {
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("SELECT CategoryId, Name FROM Categories", connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            var categories = new List<Category>();
                            while (reader.Read())
                            {
                                categories.Add(new Category
                                {
                                    CategoryId = reader.GetInt32(0),
                                    Name = reader.GetString(1)
                                });
                            }
                            ProductCategory.ItemsSource = categories;
                            ProductCategory.DisplayMemberPath = "Name";
                            ProductCategory.SelectedValuePath = "CategoryId";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке категорий: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadProducts()
        {
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("SELECT p.ProductId, p.Name, p.Description, p.Price, p.Brand, p.Discount, c.Name AS CategoryName FROM Products p JOIN Categories c ON p.CategoryId = c.CategoryId WHERE p.SellerId = @sellerId", connection))
                    {
                        command.Parameters.AddWithValue("sellerId", sellerId);
                        using (var reader = command.ExecuteReader())
                        {
                            var products = new List<DbProduct>();
                            while (reader.Read())
                            {
                                products.Add(new DbProduct
                                {
                                    ProductId = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    Price = reader.GetDecimal(3),
                                    Brand = reader.GetString(4),
                                    Discount = reader.GetDecimal(5),
                                    CategoryName = reader.GetString(6)
                                });
                            }
                            ProductsList.ItemsSource = products;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке товаров: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddProduct_Click(object sender, RoutedEventArgs e)
        {
            string name = NewProductName.Text.Trim();
            string description = NewProductDescription.Text.Trim();
            if (!decimal.TryParse(NewProductPrice.Text, out decimal price))
            {
                MessageBox.Show("Введите корректную цену.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            string brand = NewProductBrand.Text.Trim();
            if (!decimal.TryParse(NewProductDiscount.Text, out decimal discount))
            {
                MessageBox.Show("Введите корректную скидку (в процентах).", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            int? categoryId = ProductCategory.SelectedValue as int?;

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(brand) || categoryId == null)
            {
                MessageBox.Show("Заполните все обязательные поля (название, бренд, категория).", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("INSERT INTO Products (Name, Description, Price, Brand, Discount, CategoryId, SellerId) VALUES (@name, @description, @price, @brand, @discount, @categoryId, @sellerId)", connection))
                    {
                        command.Parameters.AddWithValue("name", name);
                        command.Parameters.AddWithValue("description", description);
                        command.Parameters.AddWithValue("price", price);
                        command.Parameters.AddWithValue("brand", brand);
                        command.Parameters.AddWithValue("discount", discount);
                        command.Parameters.AddWithValue("categoryId", categoryId);
                        command.Parameters.AddWithValue("sellerId", sellerId);
                        command.ExecuteNonQuery();
                    }
                }
                LoadProducts();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении товара: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditProduct_Click(object sender, RoutedEventArgs e)
        {
            int productId = (int)((Button)sender).Tag;
            var product = (DbProduct)ProductsList.Items.Cast<DbProduct>().First(p => p.ProductId == productId);

            string name = product.Name;
            string description = product.Description;
            decimal price = product.Price;
            string brand = product.Brand;
            decimal discount = product.Discount;
            int categoryId = ProductCategory.Items.Cast<Category>().First(c => c.Name == product.CategoryName).CategoryId;

            NewProductName.Text = name;
            NewProductDescription.Text = description;
            NewProductPrice.Text = price.ToString();
            NewProductBrand.Text = brand;
            NewProductDiscount.Text = discount.ToString();
            ProductCategory.SelectedValue = categoryId;

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("UPDATE Products SET Name = @name, Description = @description, Price = @price, Brand = @brand, Discount = @discount, CategoryId = @categoryId WHERE ProductId = @productId AND SellerId = @sellerId", connection))
                    {
                        command.Parameters.AddWithValue("name", name);
                        command.Parameters.AddWithValue("description", description);
                        command.Parameters.AddWithValue("price", price);
                        command.Parameters.AddWithValue("brand", brand);
                        command.Parameters.AddWithValue("discount", discount);
                        command.Parameters.AddWithValue("categoryId", categoryId);
                        command.Parameters.AddWithValue("productId", productId);
                        command.Parameters.AddWithValue("sellerId", sellerId);
                        command.ExecuteNonQuery();
                    }
                }
                LoadProducts();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при редактировании товара: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteProduct_Click(object sender, RoutedEventArgs e)
        {
            int productId = (int)((Button)sender).Tag;
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("DELETE FROM Products WHERE ProductId = @productId AND SellerId = @sellerId", connection))
                    {
                        command.Parameters.AddWithValue("productId", productId);
                        command.Parameters.AddWithValue("sellerId", sellerId);
                        command.ExecuteNonQuery();
                    }
                }
                LoadProducts();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении товара: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Управление заказами
        private void LoadOrders()
        {
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("SELECT o.OrderId, p.Name AS ProductName, o.Quantity, o.TotalPrice, o.OrderDate, o.Status FROM Orders o JOIN Products p ON o.ProductId = p.ProductId WHERE o.SellerId = @sellerId", connection))
                    {
                        command.Parameters.AddWithValue("sellerId", sellerId);
                        using (var reader = command.ExecuteReader())
                        {
                            var orders = new List<Order>();
                            while (reader.Read())
                            {
                                orders.Add(new Order
                                {
                                    OrderId = reader.GetInt32(0),
                                    ProductName = reader.GetString(1),
                                    Quantity = reader.GetInt32(2),
                                    TotalPrice = reader.GetDecimal(3),
                                    OrderDate = reader.GetDateTime(4),
                                    Status = reader.GetString(5)
                                });
                            }
                            OrdersList.ItemsSource = orders;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке заказов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateOrderStatus_Click(object sender, RoutedEventArgs e)
        {
            int orderId = (int)((Button)sender).Tag;
            var stackPanel = ((Button)sender).Parent as StackPanel;
            var statusCombo = stackPanel.Children[6] as ComboBox;

            string status = (statusCombo.SelectedItem as ComboBoxItem)?.Content.ToString();

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("UPDATE Orders SET Status = @status WHERE OrderId = @orderId AND SellerId = @sellerId", connection))
                    {
                        command.Parameters.AddWithValue("status", status);
                        command.Parameters.AddWithValue("orderId", orderId);
                        command.Parameters.AddWithValue("sellerId", sellerId);
                        command.ExecuteNonQuery();
                    }
                }
                LoadOrders();
                LoadFinancials(); // Обновляем финансовую информацию
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении статуса заказа: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Финансовые функции
        private void LoadFinancials()
        {
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    decimal totalRevenue = 0;
                    decimal feePercentage = 0;
                    var sales = new List<Sale>();

                    // Получаем процент комиссии продавца
                    using (var command = new NpgsqlCommand("SELECT FeeValue FROM SellerFees WHERE SellerId = @sellerId AND FeeType = 'Percentage'", connection))
                    {
                        command.Parameters.AddWithValue("sellerId", sellerId);
                        var result = command.ExecuteScalar();
                        if (result != null)
                        {
                            feePercentage = (decimal)result / 100;
                        }
                    }

                    // Получаем историю продаж
                    using (var command = new NpgsqlCommand("SELECT o.OrderId, p.Name AS ProductName, o.TotalPrice, o.OrderDate FROM Orders o JOIN Products p ON o.ProductId = p.ProductId WHERE o.SellerId = @sellerId AND o.Status = 'Delivered'", connection))
                    {
                        command.Parameters.AddWithValue("sellerId", sellerId);
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                decimal totalPrice = reader.GetDecimal(2);
                                decimal sellerRevenue = totalPrice * (1 - feePercentage);
                                totalRevenue += sellerRevenue;

                                sales.Add(new Sale
                                {
                                    OrderId = reader.GetInt32(0),
                                    ProductName = reader.GetString(1),
                                    TotalPrice = totalPrice,
                                    SellerRevenue = sellerRevenue,
                                    OrderDate = reader.GetDateTime(3)
                                });
                            }
                        }
                    }

                    TotalRevenueText.Text = $"{totalRevenue:F2} грн";
                    SalesHistoryList.ItemsSource = sales;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке финансовой информации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Профиль магазина
        private void LoadStoreProfile()
        {
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("SELECT StoreName, Description, ContactInfo FROM SellerProfiles WHERE SellerId = @sellerId", connection))
                    {
                        command.Parameters.AddWithValue("sellerId", sellerId);
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                StoreNameTextBox.Text = reader.IsDBNull(0) ? "" : reader.GetString(0);
                                StoreDescriptionTextBox.Text = reader.IsDBNull(1) ? "" : reader.GetString(1);
                                StoreContactInfoTextBox.Text = reader.IsDBNull(2) ? "" : reader.GetString(2);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке профиля магазина: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveProfile_Click(object sender, RoutedEventArgs e)
        {
            string storeName = StoreNameTextBox.Text.Trim();
            string description = StoreDescriptionTextBox.Text.Trim();
            string contactInfo = StoreContactInfoTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(storeName))
            {
                MessageBox.Show("Название магазина не может быть пустым.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("INSERT INTO SellerProfiles (SellerId, StoreName, Description, ContactInfo) VALUES (@sellerId, @storeName, @description, @contactInfo) ON CONFLICT (SellerId) DO UPDATE SET StoreName = @storeName, Description = @description, ContactInfo = @contactInfo", connection))
                    {
                        command.Parameters.AddWithValue("sellerId", sellerId);
                        command.Parameters.AddWithValue("storeName", storeName);
                        command.Parameters.AddWithValue("description", description);
                        command.Parameters.AddWithValue("contactInfo", contactInfo);
                        command.ExecuteNonQuery();
                    }
                }
                MessageBox.Show("Профиль магазина успешно сохранен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении профиля магазина: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // Модели данных
    public class Order
    {
        public int OrderId { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice { get; set; }
        public DateTime OrderDate { get; set; }
        public string Status { get; set; }
    }

    public class Sale
    {
        public int OrderId { get; set; }
        public string ProductName { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal SellerRevenue { get; set; }
        public DateTime OrderDate { get; set; }
    }
}