using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Npgsql;
using System.Configuration;
using ElmirClone.Models;
using System.Windows.Threading;

namespace ElmirClone
{
    public partial class MainWindow : Window
    {
        private UserProfile userProfile;
        private string connectionString;
        private List<DbProduct> cartItems;
        private DispatcherTimer orderStatusTimer;
        private List<int> notifiedOrders;

        internal MainWindow(UserProfile userProfile)
        {
            InitializeComponent();
            this.userProfile = userProfile ?? throw new ArgumentNullException(nameof(userProfile));
            cartItems = new List<DbProduct>();
            notifiedOrders = new List<int>();
            connectionString = ConfigurationManager.ConnectionStrings["ElitePCConnection"]?.ConnectionString;
            if (string.IsNullOrEmpty(connectionString))
            {
                MessageBox.Show("Строка подключения к базе данных не найдена.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            DataContext = userProfile;
            LoadProducts();

            // Инициализация таймера для проверки статуса заказов
            orderStatusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10) // Проверять каждые 10 секунд
            };
            orderStatusTimer.Tick += CheckOrderStatus;
            orderStatusTimer.Start();
        }

        private void CheckOrderStatus(object sender, EventArgs e)
        {
            try
            {
                if (!(userProfile.UserId is int buyerId) || buyerId <= 0)
                {
                    return;
                }

                // Сначала собираем информацию о заказах
                var shippedOrders = new List<(int OrderId, int ProductId)>();

                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand(
                        "SELECT orderid, status, productid FROM orders WHERE buyerid = @buyerid AND status = 'Shipped'", connection))
                    {
                        command.Parameters.AddWithValue("buyerid", buyerId);
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int orderId = reader.GetInt32(0);
                                string status = reader.GetString(1);
                                int productId = reader.GetInt32(2);

                                if (status == "Shipped" && !notifiedOrders.Contains(orderId))
                                {
                                    shippedOrders.Add((orderId, productId));
                                }
                            }
                        }
                    }
                }

                // Теперь для каждого заказа получаем название товара
                foreach (var (orderId, productId) in shippedOrders)
                {
                    string productName = "Неизвестный товар";
                    using (var connection = new NpgsqlConnection(connectionString))
                    {
                        connection.Open();
                        using (var productCommand = new NpgsqlCommand("SELECT name FROM products WHERE productid = @productid", connection))
                        {
                            productCommand.Parameters.AddWithValue("productid", productId);
                            var result = productCommand.ExecuteScalar();
                            if (result != null)
                            {
                                productName = result.ToString();
                            }
                        }
                    }

                    // Показываем уведомление
                    MessageBox.Show($"Ваш заказ (ID: {orderId}, товар: {productName}) уже в пути!", "Уведомление", MessageBoxButton.OK, MessageBoxImage.Information);
                    notifiedOrders.Add(orderId);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при проверке статуса заказов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void LoadProducts(string category = null)
        {
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    // Загрузка дополнительных товаров (без фильтра по категории)
                    using (var command = new NpgsqlCommand("SELECT ProductId, Name, Price, ImageUrl FROM Products LIMIT 5", connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            var additionalProducts = new List<DbProduct>();
                            while (reader.Read())
                            {
                                additionalProducts.Add(new DbProduct
                                {
                                    ProductId = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    Price = reader.GetDecimal(2),
                                    ImageUrl = reader.IsDBNull(3) ? "https://via.placeholder.com/150" : reader.GetString(3)
                                });
                            }
                            if (AdditionalProductsGrid != null)
                            {
                                AdditionalProductsGrid.ItemsSource = additionalProducts;
                            }
                            else
                            {
                                MessageBox.Show("Элемент AdditionalProductsGrid не найден в разметке.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }

                    // Загрузка популярных товаров (с фильтром по категории, если указана)
                    string query = "SELECT p.ProductId, p.Name, p.Price, p.ImageUrl, p.Rating, p.Reviews FROM Products p";
                    if (!string.IsNullOrEmpty(category))
                    {
                        query += " JOIN Categories c ON p.CategoryId = c.CategoryId WHERE c.Name = @category";
                    }
                    query += " LIMIT 5";

                    using (var command = new NpgsqlCommand(query, connection))
                    {
                        if (!string.IsNullOrEmpty(category))
                        {
                            command.Parameters.AddWithValue("category", category);
                        }
                        using (var reader = command.ExecuteReader())
                        {
                            var popularProducts = new List<DbProduct>();
                            while (reader.Read())
                            {
                                popularProducts.Add(new DbProduct
                                {
                                    ProductId = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    Price = reader.GetDecimal(2),
                                    ImageUrl = reader.IsDBNull(3) ? "https://via.placeholder.com/150" : reader.GetString(3),
                                    Rating = reader.IsDBNull(4) ? 0 : reader.GetDouble(4),
                                    Reviews = reader.IsDBNull(5) ? 0 : reader.GetInt32(5)
                                });
                            }
                            if (PopularProductsGrid != null)
                            {
                                PopularProductsGrid.ItemsSource = popularProducts;
                            }
                            else
                            {
                                MessageBox.Show("Элемент PopularProductsGrid не найден в разметке.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке товаров: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Logo_Click(object sender, RoutedEventArgs e)
        {
            LoadProducts();
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox searchBox = sender as TextBox;
            if (searchBox?.Text == "Поиск...")
            {
                searchBox.Text = "";
                searchBox.Foreground = System.Windows.Media.Brushes.White;
            }
        }

        private void CategoryButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button != null)
            {
                string category = button.Content?.ToString();
                if (!string.IsNullOrEmpty(category))
                {
                    LoadProducts(category);
                }
            }
        }

        private void ViewProduct_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int productId)
            {
                try
                {
                    using (var connection = new NpgsqlConnection(connectionString))
                    {
                        connection.Open();
                        using (var command = new NpgsqlCommand("SELECT p.ProductId, p.Name, p.Description, p.Price, p.Brand, p.Discount, c.Name AS CategoryName, p.ImageUrl FROM Products p JOIN Categories c ON p.CategoryId = c.CategoryId WHERE p.ProductId = @productId", connection))
                        {
                            command.Parameters.AddWithValue("productId", productId);
                            using (var reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    var product = new DbProduct
                                    {
                                        ProductId = reader.GetInt32(0),
                                        Name = reader.GetString(1),
                                        Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                        Price = reader.GetDecimal(3),
                                        Brand = reader.GetString(4),
                                        Discount = reader.GetDecimal(5),
                                        CategoryName = reader.GetString(6),
                                        ImageUrl = reader.IsDBNull(7) ? "https://via.placeholder.com/150" : reader.GetString(7)
                                    };

                                    Window productWindow = new Window
                                    {
                                        Title = product.Name,
                                        Width = 400,
                                        Height = 600,
                                        WindowStartupLocation = WindowStartupLocation.CenterScreen
                                    };

                                    StackPanel panel = new StackPanel { Margin = new Thickness(10) };
                                    panel.Children.Add(new Image { Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(product.ImageUrl)), Width = 200, Height = 200, Margin = new Thickness(0, 0, 0, 10) });
                                    panel.Children.Add(new TextBlock { Text = $"Название: {product.Name}", FontWeight = System.Windows.FontWeights.Bold, Margin = new Thickness(0, 0, 0, 5) });
                                    panel.Children.Add(new TextBlock { Text = $"Категория: {product.CategoryName}", Margin = new Thickness(0, 0, 0, 5) });
                                    panel.Children.Add(new TextBlock { Text = $"Бренд: {product.Brand}", Margin = new Thickness(0, 0, 0, 5) });
                                    panel.Children.Add(new TextBlock { Text = $"Цена: {product.Price:F2} грн", Margin = new Thickness(0, 0, 0, 5) });
                                    panel.Children.Add(new TextBlock { Text = $"Скидка: {product.Discount:F2}%", Margin = new Thickness(0, 0, 0, 5) });
                                    panel.Children.Add(new TextBlock { Text = $"Описание: {product.Description}", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) });

                                    Button closeButton = new Button { Content = "Закрыть", Width = 100 };
                                    closeButton.Click += (s, ev) => productWindow.Close();
                                    panel.Children.Add(closeButton);

                                    productWindow.Content = panel;
                                    productWindow.ShowDialog();
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при загрузке деталей товара: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void AddToCart_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int productId)
            {
                try
                {
                    using (var connection = new NpgsqlConnection(connectionString))
                    {
                        connection.Open();
                        using (var command = new NpgsqlCommand("SELECT ProductId, Name, Price, ImageUrl FROM Products WHERE ProductId = @productId", connection))
                        {
                            command.Parameters.AddWithValue("productId", productId);
                            using (var reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    var product = new DbProduct
                                    {
                                        ProductId = reader.GetInt32(0),
                                        Name = reader.GetString(1),
                                        Price = reader.GetDecimal(2),
                                        ImageUrl = reader.IsDBNull(3) ? "https://via.placeholder.com/150" : reader.GetString(3)
                                    };

                                    if (cartItems == null)
                                    {
                                        cartItems = new List<DbProduct>();
                                    }

                                    if (!cartItems.Any(p => p.ProductId == product.ProductId))
                                    {
                                        cartItems.Add(product);
                                        MessageBox.Show($"{product.Name} добавлен в корзину!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                                    }
                                    else
                                    {
                                        MessageBox.Show($"{product.Name} уже находится в корзине.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при добавлении товара в корзину: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OrderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (cartItems == null || !cartItems.Any())
                {
                    MessageBox.Show("Корзина пуста. Добавьте товары в корзину перед оформлением заказа.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (userProfile == null)
                {
                    MessageBox.Show("Пожалуйста, войдите в аккаунт, чтобы оформить заказ.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                CartWindow cartWindow = new CartWindow(cartItems, userProfile);
                if (cartWindow.CanShowDialog())
                {
                    bool? dialogResult = cartWindow.ShowDialog();
                    if (dialogResult == true)
                    {
                        cartItems.Clear();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии окна оформления заказа: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProfilePanel != null)
            {
                ProfilePanel.Visibility = ProfilePanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            }
            else
            {
                MessageBox.Show("Элемент ProfilePanel не найден в разметке.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (cartItems == null || !cartItems.Any())
                {
                    MessageBox.Show("Корзина пуста. Добавьте товары в корзину.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (userProfile == null)
                {
                    MessageBox.Show("Для просмотра корзины необходимо авторизоваться.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                CartWindow cartWindow = new CartWindow(cartItems, userProfile);
                if (cartWindow.CanShowDialog())
                {
                    cartWindow.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии корзины: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (userProfile == null)
            {
                MessageBox.Show("Профиль пользователя не загружен.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                userProfile.FirstName = FirstNameTextBox?.Text ?? string.Empty;
                userProfile.MiddleName = MiddleNameTextBox?.Text ?? string.Empty;
                userProfile.Phone = PhoneTextBox?.Text ?? string.Empty;
                userProfile.Email = EmailTextBox?.Text ?? string.Empty;

                if (string.IsNullOrWhiteSpace(userProfile.Email))
                {
                    MessageBox.Show("Email не может быть пустым.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("UPDATE UserDetails SET FirstName = @firstName, MiddleName = @middleName, Phone = @phone, Email = @email WHERE UserId = (SELECT UserId FROM UserCredentials WHERE Email = @email)", connection))
                    {
                        command.Parameters.AddWithValue("firstName", userProfile.FirstName ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("middleName", string.IsNullOrWhiteSpace(userProfile.MiddleName) ? (object)DBNull.Value : userProfile.MiddleName);
                        command.Parameters.AddWithValue("phone", string.IsNullOrWhiteSpace(userProfile.Phone) ? (object)DBNull.Value : userProfile.Phone);
                        command.Parameters.AddWithValue("email", userProfile.Email);
                        int rowsAffected = command.ExecuteNonQuery();
                        if (rowsAffected == 0)
                        {
                            MessageBox.Show("Не удалось обновить профиль. Пользователь с таким email не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        else
                        {
                            MessageBox.Show("Профиль обновлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении профиля: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                orderStatusTimer.Stop();
                LoginWindow loginWindow = new LoginWindow();
                loginWindow.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при выходе из системы: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}