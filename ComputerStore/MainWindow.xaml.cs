using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Npgsql;
using System.Configuration;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Input;

namespace ElmirClone
{
    public partial class MainWindow : Window
    {
        private UserProfile userProfile;
        private string connectionString;
        private List<DbProduct> cartItems;
        private DispatcherTimer orderStatusTimer;
        private List<int> notifiedOrders;
        private int? selectedCategoryId;
        private int? selectedSubCategoryId;
        private List<string> selectedBrands;
        private decimal? priceFrom;
        private decimal? priceTo;

        // История навигации
        private List<(string Category, int? CategoryId, int? SubCategoryId)> navigationHistory;
        private int navigationIndex;

        internal MainWindow(UserProfile userProfile)
        {
            InitializeComponent();
            this.userProfile = userProfile ?? throw new ArgumentNullException(nameof(userProfile));
            cartItems = new List<DbProduct>();
            notifiedOrders = new List<int>();
            navigationHistory = new List<(string, int?, int?)>();
            navigationIndex = -1;
            selectedBrands = new List<string>();

            connectionString = ConfigurationManager.ConnectionStrings["ElitePCConnection"]?.ConnectionString;
            if (string.IsNullOrEmpty(connectionString))
            {
                MessageBox.Show("Рядок підключення до бази даних не знайдено.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            DataContext = userProfile;
            LoadCategories();
            LoadProducts(); // Начальная загрузка товаров
            UpdateNavigationButtons();

            orderStatusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            orderStatusTimer.Tick += CheckOrderStatus;
            orderStatusTimer.Start();
        }

        private void CheckOrderStatus(object sender, EventArgs e)
        {
            if (Dispatcher.CheckAccess())
            {
                try
                {
                    if (!(userProfile?.UserId is int buyerId) || buyerId <= 0) return;

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

                    foreach (var (orderId, productId) in shippedOrders)
                    {
                        string productName = "Невідомий товар";
                        using (var connection = new NpgsqlConnection(connectionString))
                        {
                            connection.Open();
                            using (var productCommand = new NpgsqlCommand("SELECT name FROM products WHERE productid = @productid", connection))
                            {
                                productCommand.Parameters.AddWithValue("productid", productId);
                                var result = productCommand.ExecuteScalar();
                                if (result != null) productName = result.ToString();
                            }
                        }
                        MessageBox.Show($"Ваше замовлення (ID: {orderId}, товар: {productName}) вже в дорозі!", "Сповіщення", MessageBoxButton.OK, MessageBoxImage.Information);
                        notifiedOrders.Add(orderId);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка при перевірці статусу замовлень: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadCategories()
        {
            if (Dispatcher.CheckAccess())
            {
                try
                {
                    CategoryPanel.Children.Clear();
                    CategoryPanel.Visibility = Visibility.Collapsed;

                    using (var connection = new NpgsqlConnection(connectionString))
                    {
                        connection.Open();
                        using (var catCommand = new NpgsqlCommand("SELECT categoryid, name FROM categories WHERE parentcategoryid IS NULL", connection))
                        {
                            using (var reader = catCommand.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    int categoryId = reader.GetInt32(0);
                                    string categoryName = reader.GetString(1);

                                    Button catButton = new Button
                                    {
                                        Content = categoryName,
                                        Tag = categoryId,
                                        Margin = new Thickness(5),
                                        Padding = new Thickness(5),
                                        FontSize = 14,
                                        Height = 40,
                                        Width = 157
                                    };
                                    catButton.Click += CategoryButton_Click;
                                    CategoryPanel.Children.Add(catButton);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка при завантаженні категорій: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadSubCategories(int categoryId)
        {
            if (Dispatcher.CheckAccess())
            {
                try
                {
                    ContentPanel.Children.Clear();
                    selectedCategoryId = categoryId;
                    selectedSubCategoryId = null;
                    FilterPanel.Visibility = Visibility.Collapsed;

                    WrapPanel subCategoryPanel = new WrapPanel
                    {
                        Margin = new Thickness(10),
                        Orientation = Orientation.Horizontal
                    };

                    using (var connection = new NpgsqlConnection(connectionString))
                    {
                        connection.Open();
                        using (var command = new NpgsqlCommand("SELECT categoryid, name, image_url FROM categories WHERE parentcategoryid = @parentId", connection))
                        {
                            command.Parameters.AddWithValue("parentId", categoryId);
                            using (var reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    int subCategoryId = reader.GetInt32(0);
                                    string subCategoryName = reader.GetString(1);
                                    string imageUrl = reader.IsDBNull(2) ? "https://via.placeholder.com/200" : reader.GetString(2);

                                    Border subCategoryBorder = new Border
                                    {
                                        BorderBrush = Brushes.LightGray,
                                        BorderThickness = new Thickness(1),
                                        Margin = new Thickness(10),
                                        Width = 300,
                                        Height = 300,
                                        Style = (Style)FindResource("SubCategoryBorderStyle")
                                    };

                                    StackPanel subCategoryContent = new StackPanel
                                    {
                                        Background = Brushes.White,
                                        HorizontalAlignment = HorizontalAlignment.Center,
                                        VerticalAlignment = VerticalAlignment.Center
                                    };

                                    Image subCategoryImage = new Image
                                    {
                                        Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(imageUrl, UriKind.RelativeOrAbsolute)),
                                        Width = 250,
                                        Height = 200,
                                        Stretch = Stretch.UniformToFill,
                                        Margin = new Thickness(5)
                                    };

                                    TextBlock subCategoryText = new TextBlock
                                    {
                                        Text = subCategoryName,
                                        FontSize = 16,
                                        Margin = new Thickness(5),
                                        TextAlignment = TextAlignment.Center,
                                        TextWrapping = TextWrapping.Wrap,
                                        MaxHeight = 80,
                                        TextTrimming = TextTrimming.CharacterEllipsis
                                    };

                                    subCategoryContent.Children.Add(subCategoryImage);
                                    subCategoryContent.Children.Add(subCategoryText);
                                    subCategoryBorder.Child = subCategoryContent;

                                    Button subCategoryButton = new Button { Content = subCategoryBorder, Tag = subCategoryId };
                                    try
                                    {
                                        subCategoryButton.Style = (Style)FindResource("SubCategoryButtonStyle");
                                    }
                                    catch (ResourceReferenceKeyNotFoundException)
                                    {
                                        subCategoryButton.Background = Brushes.Transparent;
                                        subCategoryButton.BorderThickness = new Thickness(0);
                                        subCategoryButton.Padding = new Thickness(0);
                                        subCategoryButton.Margin = new Thickness(5);
                                        subCategoryButton.Cursor = Cursors.Hand;
                                    }

                                    subCategoryButton.Click += SubCategoryButton_Click;
                                    subCategoryPanel.Children.Add(subCategoryButton);
                                }
                            }
                        }
                    }

                    if (subCategoryPanel.Children.Count == 0)
                    {
                        ContentPanel.Children.Add(new TextBlock
                        {
                            Text = "Підкатегорій не знайдено.",
                            FontSize = 16,
                            Margin = new Thickness(10),
                            Foreground = Brushes.Gray
                        });
                    }
                    else
                    {
                        ContentPanel.Children.Add(subCategoryPanel);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка при завантаженні підкатегорій: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadFilters(int subCategoryId)
        {
            if (Dispatcher.CheckAccess())
            {
                try
                {
                    FilterPanel.Visibility = Visibility.Visible;
                    BrandsPanel.Children.Clear();
                    PriceFromTextBox.Text = "";
                    PriceToTextBox.Text = "";
                    selectedBrands.Clear();
                    priceFrom = null;
                    priceTo = null;

                    // Загружаем бренды для выбранной подкатегории
                    using (var connection = new NpgsqlConnection(connectionString))
                    {
                        connection.Open();
                        using (var command = new NpgsqlCommand(
                            "SELECT DISTINCT p.brand " +
                            "FROM products p " +
                            "WHERE p.subcategoryid = @subCategoryId AND p.ishidden = false AND p.brand IS NOT NULL " +
                            "ORDER BY p.brand", connection))
                        {
                            command.Parameters.AddWithValue("subCategoryId", subCategoryId);
                            using (var reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    string brand = reader.GetString(0);
                                    CheckBox brandCheckBox = new CheckBox
                                    {
                                        Content = brand,
                                        Tag = brand,
                                        Margin = new Thickness(0, 0, 0, 5),
                                        IsChecked = false
                                    };
                                    brandCheckBox.Checked += (s, e) =>
                                    {
                                        if (!selectedBrands.Contains(brand)) selectedBrands.Add(brand);
                                    };
                                    brandCheckBox.Unchecked += (s, e) =>
                                    {
                                        if (selectedBrands.Contains(brand)) selectedBrands.Remove(brand);
                                    };
                                    BrandsPanel.Children.Add(brandCheckBox);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка при завантаженні фільтрів: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void LoadProducts(string category = null, int? categoryId = null, int? subCategoryId = null, bool addToHistory = true)
        {
            if (Dispatcher.CheckAccess())
            {
                try
                {
                    ContentPanel.Children.Clear();
                    selectedCategoryId = categoryId;
                    selectedSubCategoryId = subCategoryId;

                    // Добавляем в историю навигации
                    if (addToHistory)
                    {
                        // Удаляем записи после текущей позиции, если они есть
                        if (navigationIndex < navigationHistory.Count - 1)
                        {
                            navigationHistory.RemoveRange(navigationIndex + 1, navigationHistory.Count - navigationIndex - 1);
                        }

                        // Добавляем текущую страницу в историю
                        navigationHistory.Add((category, categoryId, subCategoryId));
                        navigationIndex++;
                        UpdateNavigationButtons();
                    }

                    if (subCategoryId.HasValue)
                    {
                        FilterPanel.Visibility = Visibility.Visible;
                        LoadFilters(subCategoryId.Value);

                        string query = "SELECT p.ProductId, p.Name, p.Price, p.ImageUrl, p.Rating, s.storename, s.description AS store_description " +
                                      "FROM Products p " +
                                      "JOIN categories sc ON p.subcategoryid = sc.categoryid " +
                                      "JOIN sellerprofiles s ON p.sellerid = s.sellerid " +
                                      "WHERE sc.categoryid = @subCategoryId AND p.ishidden = false";
                        var parameters = new List<NpgsqlParameter>
                        {
                            new NpgsqlParameter("subCategoryId", subCategoryId.Value)
                        };

                        // Применяем фильтры, если они есть
                        if (selectedBrands.Any())
                        {
                            query += " AND p.brand = ANY (@brands)";
                            parameters.Add(new NpgsqlParameter("brands", selectedBrands.ToArray()));
                        }

                        if (priceFrom.HasValue)
                        {
                            query += " AND p.price >= @priceFrom";
                            parameters.Add(new NpgsqlParameter("priceFrom", priceFrom.Value));
                        }

                        if (priceTo.HasValue)
                        {
                            query += " AND p.price <= @priceTo";
                            parameters.Add(new NpgsqlParameter("priceTo", priceTo.Value));
                        }

                        query += " LIMIT 5";
                        LoadProductsWithQuery(query, parameters);
                    }
                    else if (categoryId.HasValue)
                    {
                        FilterPanel.Visibility = Visibility.Collapsed;
                        LoadSubCategories(categoryId.Value);
                    }
                    else
                    {
                        FilterPanel.Visibility = Visibility.Collapsed;
                        using (var connection = new NpgsqlConnection(connectionString))
                        {
                            connection.Open();
                            var categories = new Dictionary<int, (string Name, List<(int SubCategoryId, string SubCategoryName)> SubCategories)>();
                            using (var catCommand = new NpgsqlCommand("SELECT categoryid, name FROM categories WHERE parentcategoryid IS NULL", connection))
                            {
                                using (var reader = catCommand.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        int catId = reader.GetInt32(0);
                                        string catName = reader.GetString(1);
                                        categories[catId] = (catName, new List<(int, string)>());
                                    }
                                }
                            }

                            foreach (var catId in categories.Keys.ToList())
                            {
                                using (var subCatCommand = new NpgsqlCommand("SELECT categoryid, name FROM categories WHERE parentcategoryid = @parentId", connection))
                                {
                                    subCatCommand.Parameters.AddWithValue("parentId", catId);
                                    using (var reader = subCatCommand.ExecuteReader())
                                    {
                                        while (reader.Read())
                                        {
                                            int subCatId = reader.GetInt32(0);
                                            string subCatName = reader.GetString(1);
                                            categories[catId].SubCategories.Add((subCatId, subCatName));
                                        }
                                    }
                                }
                            }

                            foreach (var catEntry in categories)
                            {
                                int catId = catEntry.Key;
                                string catName = catEntry.Value.Name;
                                var subCategories = catEntry.Value.SubCategories;

                                TextBlock categoryHeader = new TextBlock
                                {
                                    Text = catName,
                                    FontSize = 20,
                                    FontWeight = FontWeights.Bold,
                                    Margin = new Thickness(10, 20, 10, 10)
                                };
                                ContentPanel.Children.Add(categoryHeader);

                                string catQuery = "SELECT p.ProductId, p.Name, p.Price, p.ImageUrl, p.Rating, s.storename, s.description AS store_description " +
                                                 "FROM Products p " +
                                                 "JOIN sellerprofiles s ON p.sellerid = s.sellerid " +
                                                 "WHERE p.categoryid = @categoryId AND p.subcategoryid IS NULL AND p.ishidden = false LIMIT 5";
                                var catParams = new List<NpgsqlParameter>
                                {
                                    new NpgsqlParameter("categoryId", catId)
                                };
                                LoadProductsWithQuery(catQuery, catParams);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка при завантаженні товарів: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadProductsWithQuery(string query, List<NpgsqlParameter> parameters)
        {
            if (Dispatcher.CheckAccess())
            {
                try
                {
                    using (var connection = new NpgsqlConnection(connectionString))
                    {
                        connection.Open();
                        using (var command = new NpgsqlCommand(query, connection))
                        {
                            foreach (var param in parameters)
                            {
                                command.Parameters.Add(param);
                            }
                            using (var reader = command.ExecuteReader())
                            {
                                var products = new List<DbProduct>();
                                while (reader.Read())
                                {
                                    string imageUrl = reader.IsDBNull(3) ? "https://via.placeholder.com/150" : reader.GetString(3);
                                    products.Add(new DbProduct
                                    {
                                        ProductId = reader.GetInt32(0),
                                        Name = reader.GetString(1),
                                        Price = reader.GetDecimal(2),
                                        ImageUrl = imageUrl,
                                        Rating = reader.IsDBNull(4) ? 0 : reader.GetDouble(4),
                                        StoreName = reader.IsDBNull(5) ? "Невідомий магазин" : reader.GetString(5),
                                        StoreDescription = reader.IsDBNull(6) ? "Немає опису" : reader.GetString(6)
                                    });
                                }

                                WrapPanel productsPanel = new WrapPanel
                                {
                                    Margin = new Thickness(10),
                                    Orientation = Orientation.Horizontal
                                };

                                foreach (var product in products)
                                {
                                    Border productBorder = new Border
                                    {
                                        BorderBrush = Brushes.LightGray,
                                        BorderThickness = new Thickness(1),
                                        Margin = new Thickness(10),
                                        Width = 200,
                                        Height = 425,
                                        Style = (Style)FindResource("SubCategoryBorderStyle")
                                    };
                                    StackPanel productPanel = new StackPanel
                                    {
                                        Background = Brushes.White,
                                        HorizontalAlignment = HorizontalAlignment.Stretch
                                    };
                                    Image productImage = new Image
                                    {
                                        Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(product.ImageUrl, UriKind.RelativeOrAbsolute)),
                                        Width = 200,
                                        Height = 205,
                                        Stretch = Stretch.Uniform,
                                        Margin = new Thickness(5),
                                        HorizontalAlignment = HorizontalAlignment.Center
                                    };
                                    productPanel.Children.Add(productImage);
                                    TextBlock productName = new TextBlock
                                    {
                                        Text = product.Name,
                                        FontSize = 16,
                                        Margin = new Thickness(5),
                                        TextAlignment = TextAlignment.Center,
                                        FontWeight = FontWeights.Medium,
                                        TextWrapping = TextWrapping.Wrap,
                                        Height = 50
                                    };
                                    productPanel.Children.Add(productName);
                                    StackPanel ratingPanel = new StackPanel
                                    {
                                        Orientation = Orientation.Horizontal,
                                        Margin = new Thickness(5),
                                        HorizontalAlignment = HorizontalAlignment.Center
                                    };
                                    ratingPanel.Children.Add(new TextBlock { Text = "★", Foreground = Brushes.Orange, FontSize = 14 });
                                    ratingPanel.Children.Add(new TextBlock { Text = product.Rating.ToString(), FontSize = 14, Margin = new Thickness(2, 0, 0, 0) });
                                    ratingPanel.Children.Add(new TextBlock { Text = "(", Foreground = Brushes.Gray });
                                    ratingPanel.Children.Add(new TextBlock { Text = "0", Foreground = Brushes.Gray });
                                    ratingPanel.Children.Add(new TextBlock { Text = " відгуків)", Foreground = Brushes.Gray });
                                    productPanel.Children.Add(ratingPanel);
                                    TextBlock priceText = new TextBlock
                                    {
                                        Text = $"{product.Price:F2} грн",
                                        FontSize = 16,
                                        FontWeight = FontWeights.Bold,
                                        Margin = new Thickness(5),
                                        Foreground = Brushes.Red,
                                        TextAlignment = TextAlignment.Center
                                    };
                                    productPanel.Children.Add(priceText);
                                    Button viewProductButton = new Button
                                    {
                                        Content = "Переглянути",
                                        Style = (Style)FindResource("AddToCartButtonStyle"),
                                        Tag = product.ProductId,
                                        Margin = new Thickness(5)
                                    };
                                    viewProductButton.Click += ViewProduct_Click;
                                    productPanel.Children.Add(viewProductButton);
                                    Button addToCartButton = new Button
                                    {
                                        Content = "Додати до кошика",
                                        Style = (Style)FindResource("AddToCartButtonStyle"),
                                        Tag = product.ProductId,
                                        Margin = new Thickness(5)
                                    };
                                    addToCartButton.Click += AddToCart_Click;
                                    productPanel.Children.Add(addToCartButton);
                                    productBorder.Child = productPanel;
                                    productsPanel.Children.Add(productBorder);
                                }

                                if (products.Any())
                                {
                                    ContentPanel.Children.Add(productsPanel);
                                }
                                else
                                {
                                    ContentPanel.Children.Add(new TextBlock
                                    {
                                        Text = "Товарів не знайдено.",
                                        FontSize = 16,
                                        Margin = new Thickness(10),
                                        Foreground = Brushes.Gray
                                    });
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка при завантаженні товарів: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ApplyFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess())
            {
                try
                {
                    // Проверяем и устанавливаем диапазон цен
                    priceFrom = null;
                    priceTo = null;

                    if (!string.IsNullOrWhiteSpace(PriceFromTextBox.Text) && decimal.TryParse(PriceFromTextBox.Text, out decimal from))
                    {
                        if (from >= 0)
                            priceFrom = from;
                        else
                            MessageBox.Show("Ціна 'Від' не може бути від'ємною.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }

                    if (!string.IsNullOrWhiteSpace(PriceToTextBox.Text) && decimal.TryParse(PriceToTextBox.Text, out decimal to))
                    {
                        if (to >= 0)
                            priceTo = to;
                        else
                            MessageBox.Show("Ціна 'До' не може бути від'ємною.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }

                    if (priceFrom.HasValue && priceTo.HasValue && priceFrom > priceTo)
                    {
                        MessageBox.Show("Ціна 'Від' не може бути більшою за ціну 'До'.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Перезагружаем товары с учетом фильтров
                    if (selectedSubCategoryId.HasValue)
                    {
                        LoadProducts(subCategoryId: selectedSubCategoryId.Value, addToHistory: false);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка при застосуванні фільтрів: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SearchProducts(string searchText)
        {
            if (Dispatcher.CheckAccess())
            {
                try
                {
                    ContentPanel.Children.Clear();
                    selectedCategoryId = null;
                    selectedSubCategoryId = null;
                    FilterPanel.Visibility = Visibility.Collapsed;

                    string query = @"
                        SELECT p.ProductId, p.Name, p.Price, p.ImageUrl, p.Rating, s.storename, s.description AS store_description
                        FROM Products p
                        JOIN sellerprofiles s ON p.sellerid = s.sellerid
                        JOIN categories c ON p.categoryid = c.categoryid
                        LEFT JOIN categories sc ON p.subcategoryid = sc.categoryid
                        WHERE p.ishidden = false
                        AND (
                            SIMILARITY(LOWER(p.Name), LOWER(@searchText)) > 0.3
                            OR SIMILARITY(LOWER(p.Description), LOWER(@searchText)) > 0.3
                            OR SIMILARITY(LOWER(p.Brand), LOWER(@searchText)) > 0.3
                            OR SIMILARITY(LOWER(c.Name), LOWER(@searchText)) > 0.3
                            OR (sc.Name IS NOT NULL AND SIMILARITY(LOWER(sc.Name), LOWER(@searchText)) > 0.3)
                        )
                        ORDER BY GREATEST(
                            SIMILARITY(LOWER(p.Name), LOWER(@searchText)),
                            SIMILARITY(LOWER(p.Description), LOWER(@searchText)),
                            SIMILARITY(LOWER(p.Brand), LOWER(@searchText)),
                            SIMILARITY(LOWER(c.Name), LOWER(@searchText)),
                            COALESCE(SIMILARITY(LOWER(sc.Name), LOWER(@searchText)), 0)
                        ) DESC
                        LIMIT 20";

                    var parameters = new List<NpgsqlParameter>
                    {
                        new NpgsqlParameter("searchText", searchText)
                    };

                    LoadProductsWithQuery(query, parameters);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка при пошуку товарів: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Logo_Click(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess())
            {
                selectedCategoryId = null;
                selectedSubCategoryId = null;
                FilterPanel.Visibility = Visibility.Collapsed;
                LoadProducts();
            }
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess())
            {
                TextBox searchBox = sender as TextBox;
                if (searchBox?.Text == "Я шукаю...")
                {
                    searchBox.Text = "";
                }
            }
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess())
            {
                TextBox searchBox = sender as TextBox;
                if (string.IsNullOrWhiteSpace(searchBox?.Text))
                {
                    searchBox.Text = "Я шукаю...";
                }
            }
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (Dispatcher.CheckAccess() && e.Key == Key.Enter)
            {
                SearchButton_Click(sender, e);
            }
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess())
            {
                string searchText = SearchBox.Text.Trim();
                if (!string.IsNullOrEmpty(searchText) && searchText != "Я шукаю...")
                {
                    SearchProducts(searchText);
                }
                else
                {
                    MessageBox.Show("Будь ласка, введіть запит для пошуку.", "Попередження", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void CategoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess() && (sender as Button)?.Tag is int categoryId)
            {
                selectedCategoryId = categoryId;
                selectedSubCategoryId = null;
                LoadSubCategories(categoryId);
                CategoryPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void SubCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess() && (sender as Button)?.Tag is int subCategoryId)
            {
                selectedSubCategoryId = subCategoryId;
                LoadProducts(subCategoryId: subCategoryId);
                CategoryPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess() && navigationIndex > 0)
            {
                navigationIndex--;
                var (category, categoryId, subCategoryId) = navigationHistory[navigationIndex];
                if (categoryId.HasValue && !subCategoryId.HasValue)
                {
                    LoadSubCategories(categoryId.Value);
                }
                else if (subCategoryId.HasValue)
                {
                    LoadProducts(subCategoryId: subCategoryId, addToHistory: false);
                }
                else
                {
                    LoadProducts(addToHistory: false);
                }
                UpdateNavigationButtons();
            }
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess() && navigationIndex < navigationHistory.Count - 1)
            {
                navigationIndex++;
                var (category, categoryId, subCategoryId) = navigationHistory[navigationIndex];
                if (categoryId.HasValue && !subCategoryId.HasValue)
                {
                    LoadSubCategories(categoryId.Value);
                }
                else if (subCategoryId.HasValue)
                {
                    LoadProducts(subCategoryId: subCategoryId, addToHistory: false);
                }
                else
                {
                    LoadProducts(addToHistory: false);
                }
                UpdateNavigationButtons();
            }
        }

        private void UpdateNavigationButtons()
        {
            if (Dispatcher.CheckAccess())
            {
                BackButton.IsEnabled = navigationIndex > 0;
                ForwardButton.IsEnabled = navigationIndex < navigationHistory.Count - 1;
            }
        }

        private void ViewProduct_Click(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess() && (sender as Button)?.Tag is int productId)
            {
                try
                {
                    using (var connection = new NpgsqlConnection(connectionString))
                    {
                        connection.Open();
                        using (var command = new NpgsqlCommand(
                            "SELECT p.ProductId, p.Name, p.Description, p.Price, p.Brand, c.Name AS CategoryName, p.ImageUrl, s.storename, s.description AS store_description " +
                            "FROM Products p " +
                            "JOIN Categories c ON p.CategoryId = c.CategoryId " +
                            "JOIN sellerprofiles s ON p.sellerid = s.sellerid " +
                            "WHERE p.ProductId = @productId AND p.ishidden = false", connection))
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
                                        CategoryName = reader.GetString(5),
                                        ImageUrl = reader.IsDBNull(6) ? "https://via.placeholder.com/150" : reader.GetString(6),
                                        StoreName = reader.GetString(7),
                                        StoreDescription = reader.IsDBNull(8) ? "Немає опису" : reader.GetString(8)
                                    };

                                    Window productWindow = new Window
                                    {
                                        Title = product.Name,
                                        Width = 600,
                                        Height = 700,
                                        WindowStartupLocation = WindowStartupLocation.CenterScreen
                                    };
                                    ScrollViewer scrollViewer = new ScrollViewer
                                    {
                                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                                        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                                    };
                                    StackPanel panel = new StackPanel
                                    {
                                        Margin = new Thickness(10),
                                        MinHeight = 650
                                    };
                                    Image productImage = new Image
                                    {
                                        Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(product.ImageUrl, UriKind.RelativeOrAbsolute)),
                                        Width = 400,
                                        Height = 400,
                                        Margin = new Thickness(0, 0, 0, 10),
                                        HorizontalAlignment = HorizontalAlignment.Center,
                                        Stretch = Stretch.Uniform
                                    };
                                    panel.Children.Add(productImage);
                                    panel.Children.Add(new TextBlock { Text = $"Назва: {product.Name}", FontWeight = FontWeights.Bold, FontSize = 16, Margin = new Thickness(0, 0, 0, 5) });
                                    panel.Children.Add(new TextBlock { Text = $"Категорія: {product.CategoryName}", FontSize = 14, Margin = new Thickness(0, 0, 0, 5) });
                                    panel.Children.Add(new TextBlock { Text = $"Бренд: {product.Brand}", FontSize = 14, Margin = new Thickness(0, 0, 0, 5) });
                                    panel.Children.Add(new TextBlock { Text = $"Ціна: {product.Price:F2} грн", FontSize = 14, Margin = new Thickness(0, 0, 0, 5) });
                                    panel.Children.Add(new TextBlock { Text = $"Опис: {product.Description}", TextWrapping = TextWrapping.Wrap, FontSize = 14, Margin = new Thickness(0, 0, 0, 10) });
                                    panel.Children.Add(new TextBlock { Text = $"Магазин: {product.StoreName}", FontWeight = FontWeights.Bold, FontSize = 14, Margin = new Thickness(0, 0, 0, 5) });
                                    panel.Children.Add(new TextBlock { Text = $"Опис магазину: {product.StoreDescription}", TextWrapping = TextWrapping.Wrap, FontSize = 14, Margin = new Thickness(0, 0, 0, 10) });
                                    TextBlock reviewsHeader = new TextBlock { Text = "Відгуки:", FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 5) };
                                    panel.Children.Add(reviewsHeader);
                                    ListBox reviewsList = new ListBox { Height = 150, Margin = new Thickness(0, 0, 0, 10), FontSize = 14 };
                                    LoadReviews(product.ProductId, reviewsList);
                                    panel.Children.Add(reviewsList);
                                    TextBox reviewTextBox = new TextBox { Height = 80, Margin = new Thickness(0, 0, 0, 10), AcceptsReturn = true, Text = "Ваш відгук...", FontSize = 14 };
                                    reviewTextBox.GotFocus += (s, args) => { if (reviewTextBox.Text == "Ваш відгук...") reviewTextBox.Text = ""; };
                                    reviewTextBox.LostFocus += (s, args) => { if (string.IsNullOrWhiteSpace(reviewTextBox.Text)) reviewTextBox.Text = "Ваш відгук..."; };
                                    panel.Children.Add(reviewTextBox);
                                    Button submitReviewButton = new Button { Content = "Залишити відгук", Width = 180, Height = 40, FontSize = 14, Style = (Style)FindResource("AddToCartButtonStyle"), Margin = new Thickness(0, 0, 0, 10), HorizontalAlignment = HorizontalAlignment.Center };
                                    submitReviewButton.Click += (s, args) => { if (userProfile?.UserId != null && !string.IsNullOrWhiteSpace(reviewTextBox.Text) && reviewTextBox.Text != "Ваш відгук...") { SaveReview(product.ProductId, ((int?)userProfile.UserId).Value, reviewTextBox.Text); LoadReviews(product.ProductId, reviewsList); reviewTextBox.Text = "Ваш відгук..."; } else MessageBox.Show("Будь ласка, увійдіть в акаунт і введіть відгук.", "Попередження", MessageBoxButton.OK, MessageBoxImage.Warning); };
                                    panel.Children.Add(submitReviewButton);
                                    Button closeButton = new Button { Content = "Закрити", Width = 180, Height = 40, FontSize = 14, Margin = new Thickness(0, 0, 0, 10), HorizontalAlignment = HorizontalAlignment.Center };
                                    closeButton.Click += (s, ev) => productWindow.Close();
                                    panel.Children.Add(closeButton);
                                    scrollViewer.Content = panel;
                                    productWindow.Content = scrollViewer;
                                    productWindow.ShowDialog();
                                }
                            }
                        }
                    }
                    }
                    catch (Exception ex)
                {
                    MessageBox.Show($"Помилка при завантаженні деталей товару: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        

        private void SaveReview(int productId, int userId, string reviewText)
        {
            if (Dispatcher.CheckAccess())
            {
                try
                {
                    using (var connection = new NpgsqlConnection(connectionString))
                    {
                        connection.Open();
                        using (var command = new NpgsqlCommand("INSERT INTO product_reviews (productid, userid, review_text, review_date) VALUES (@productId, @userId, @reviewText, @reviewDate)", connection))
                        {
                            command.Parameters.AddWithValue("productId", productId);
                            command.Parameters.AddWithValue("userId", userId);
                            command.Parameters.AddWithValue("reviewText", reviewText);
                            command.Parameters.AddWithValue("reviewDate", DateTime.Now);
                            command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка при збереженні відгуку: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadReviews(int productId, ListBox reviewsList)
        {
            if (Dispatcher.CheckAccess())
            {
                try
                {
                    reviewsList.Items.Clear();
                    using (var connection = new NpgsqlConnection(connectionString))
                    {
                        connection.Open();
                        using (var command = new NpgsqlCommand("SELECT pr.review_text, u.firstname, pr.review_date FROM product_reviews pr JOIN userdetails u ON pr.userid = u.userid WHERE pr.productid = @productId ORDER BY pr.review_date DESC", connection))
                        {
                            command.Parameters.AddWithValue("productId", productId);
                            using (var reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    string reviewText = reader.GetString(0);
                                    string userName = reader.IsDBNull(1) ? "Анонім" : reader.GetString(1);
                                    DateTime reviewDate = reader.GetDateTime(2);
                                    reviewsList.Items.Add($"{userName} ({reviewDate:dd.MM.yyyy}): {reviewText}");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка при завантаженні відгуків: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void AddToCart_Click(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess() && (sender as Button)?.Tag is int productId)
            {
                try
                {
                    using (var connection = new NpgsqlConnection(connectionString))
                    {
                        connection.Open();
                        using (var command = new NpgsqlCommand("SELECT ProductId, Name, Price, ImageUrl FROM Products WHERE ProductId = @productId AND ishidden = false", connection))
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
                                    if (cartItems == null) cartItems = new List<DbProduct>();
                                    if (!cartItems.Any(p => p.ProductId == product.ProductId))
                                    {
                                        cartItems.Add(product);
                                        MessageBox.Show($"{product.Name} додано до кошика!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                                    }
                                    else
                                    {
                                        MessageBox.Show($"{product.Name} вже є в кошику.", "Інформація", MessageBoxButton.OK, MessageBoxImage.Information);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка при додаванні товару до кошика: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CartButton_Click(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess())
            {
                try
                {
                    if (cartItems == null || !cartItems.Any())
                    {
                        MessageBox.Show("Кошик порожній. Додайте товари до кошика.", "Інформація", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    if (userProfile == null)
                    {
                        MessageBox.Show("Для перегляду кошика необхідно авторизуватися.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    CartWindow cartWindow = new CartWindow(cartItems, userProfile);
                    if (cartWindow.CanShowDialog()) cartWindow.ShowDialog();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка при відкритті кошика: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess())
            {
                ProfilePanel.Visibility = ProfilePanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private void LoadOrders()
        {
            if (Dispatcher.CheckAccess())
            {
                try
                {
                    if (!(userProfile?.UserId is int buyerId) || buyerId <= 0) return;
                    OrdersList.Items.Clear();
                    using (var connection = new NpgsqlConnection(connectionString))
                    {
                        connection.Open();
                        using (var command = new NpgsqlCommand("SELECT o.orderid, o.status, p.name FROM orders o JOIN products p ON o.productid = p.productid WHERE o.buyerid = @buyerid", connection))
                        {
                            command.Parameters.AddWithValue("buyerid", buyerId);
                            using (var reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    int orderId = reader.GetInt32(0);
                                    string status = reader.GetString(1);
                                    string productName = reader.IsDBNull(2) ? "Невідомий товар" : reader.GetString(2);
                                    OrdersList.Items.Add($"Замовлення {orderId}: {productName} - {status}");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка при завантаженні замовлень: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ReturnButton_Click(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess())
            {
                if (OrdersList.SelectedItem is string selectedOrder && int.TryParse(selectedOrder.Split(':')[0].Replace("Замовлення ", ""), out int orderId))
                {
                    try
                    {
                        using (var connection = new NpgsqlConnection(connectionString))
                        {
                            connection.Open();
                            using (var command = new NpgsqlCommand("INSERT INTO returns (orderid, reason, status) VALUES (@orderId, @reason, @status)", connection))
                            {
                                command.Parameters.AddWithValue("orderId", orderId);
                                command.Parameters.AddWithValue("reason", "Повернення за бажанням клієнта");
                                command.Parameters.AddWithValue("status", "Pending");
                                command.ExecuteNonQuery();
                            }
                        }
                        MessageBox.Show($"Запит на повернення для замовлення {orderId} надіслано.", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadOrders();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Помилка при запиті повернення: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("Виберіть замовлення для повернення.", "Попередження", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            if (Dispatcher.CheckAccess())
            {
                selectedCategoryId = null;
                selectedSubCategoryId = null;
                FilterPanel.Visibility = Visibility.Collapsed;
                LoadProducts();
                OrderPanel.Visibility = Visibility.Collapsed;
                e.Handled = true;
            }
        }

        private void OrdersButton_Click(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess())
            {
                OrderPanel.Visibility = OrderPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                if (OrderPanel.Visibility == Visibility.Visible)
                {
                    LoadOrders();
                }
            }
        }

        private void SaveProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess())
            {
                if (userProfile == null)
                {
                    MessageBox.Show("Профіль користувача не завантажено.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        MessageBox.Show("Email не може бути порожнім.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                                MessageBox.Show("Не вдалося оновити профіль. Користувача з таким email не знайдено.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                            else
                            {
                                MessageBox.Show("Профіль оновлено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка при оновленні профілю: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess())
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
                    MessageBox.Show($"Помилка при виході з системи: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CategoryToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess())
            {
                CategoryPanel.Visibility = CategoryPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            }
        }
    }
}