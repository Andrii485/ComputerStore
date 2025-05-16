using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Npgsql;
using System.Configuration;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Media.Effects;

namespace ElmirClone
{
    public partial class MainWindow : Window
    {
        private UserProfile userProfile;
        private string connectionString;
        private int? selectedCategoryId;
        private int? selectedSubCategoryId;
        private List<string> selectedBrands;
        private decimal? priceFrom;
        private decimal? priceTo;
        private int? reviewCountMin;

        private List<(string Category, int? CategoryId, int? SubCategoryId)> navigationHistory;
        private int navigationIndex;

        internal MainWindow(UserProfile userProfile)
        {
            InitializeComponent();
            this.userProfile = userProfile ?? throw new ArgumentNullException(nameof(userProfile));
            navigationHistory = new List<(string, int?, int?)>();
            navigationIndex = -1;
            selectedBrands = new List<string>();
            reviewCountMin = null;

            connectionString = ConfigurationManager.ConnectionStrings["ElitePCConnection"]?.ConnectionString;
            if (string.IsNullOrEmpty(connectionString))
            {
                MessageBox.Show("Рядок підключення до бази даних не знайдено.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            DataContext = userProfile;
            LoadCategories();
            LoadProducts();
            UpdateNavigationButtons();
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

                                    if (string.IsNullOrWhiteSpace(imageUrl) || !Uri.TryCreate(imageUrl, UriKind.RelativeOrAbsolute, out _))
                                    {
                                        imageUrl = "https://via.placeholder.com/200";
                                    }

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
                    reviewCountMin = null;

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
                                        ApplyFiltersButton_Click(null, null); // Автоматичне застосування фільтрів
                                    };
                                    brandCheckBox.Unchecked += (s, e) =>
                                    {
                                        if (selectedBrands.Contains(brand)) selectedBrands.Remove(brand);
                                        ApplyFiltersButton_Click(null, null); // Автоматичне застосування фільтрів
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

                    if (addToHistory)
                    {
                        if (navigationIndex < navigationHistory.Count - 1)
                        {
                            navigationHistory.RemoveRange(navigationIndex + 1, navigationHistory.Count - navigationIndex - 1);
                        }

                        navigationHistory.Add((category, categoryId, subCategoryId));
                        navigationIndex++;
                        UpdateNavigationButtons();
                    }

                    if (subCategoryId.HasValue)
                    {
                        FilterPanel.Visibility = Visibility.Visible;
                        LoadFilters(subCategoryId.Value);

                        string query = "SELECT p.productid, p.name, p.price, p.image_url, p.rating, s.storename, s.description AS store_description, p.stock_quantity " +
                                      "FROM products p " +
                                      "JOIN categories sc ON p.subcategoryid = sc.categoryid " +
                                      "JOIN sellerprofiles s ON p.sellerid = s.sellerid " +
                                      "LEFT JOIN (SELECT productid, COUNT(*) as review_count FROM product_reviews GROUP BY productid) pr ON p.productid = pr.productid " +
                                      "WHERE sc.categoryid = @subCategoryId AND p.ishidden = false";
                        var parameters = new List<NpgsqlParameter>
                        {
                            new NpgsqlParameter("subCategoryId", subCategoryId.Value)
                        };

                        // Додаємо фільтри
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

                        if (reviewCountMin.HasValue)
                        {
                            query += " AND (pr.review_count >= @reviewCountMin OR (pr.review_count IS NULL AND @reviewCountMin = 0))";
                            parameters.Add(new NpgsqlParameter("reviewCountMin", reviewCountMin.Value));
                        }

                        query += " ORDER BY p.price LIMIT 5";
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
                        ContentPanel.Children.Add(new TextBlock
                        {
                            Text = "Рекомендовані товари",
                            FontSize = 20,
                            FontWeight = FontWeights.Bold,
                            Margin = new Thickness(10, 20, 10, 10)
                        });

                        string featuredQuery = "SELECT p.productid, p.name, p.price, p.image_url, p.rating, s.storename, s.description AS store_description, p.stock_quantity " +
                                              "FROM products p " +
                                              "JOIN sellerprofiles s ON p.sellerid = s.sellerid " +
                                              "WHERE p.ishidden = false " +
                                              "ORDER BY p.rating DESC " +
                                              "LIMIT 5";
                        LoadProductsWithQuery(featuredQuery, new List<NpgsqlParameter>());

                        LoadBestOffers();
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
                                var products = new List<ProductDetails>();
                                while (reader.Read())
                                {
                                    string imageUrl = reader.IsDBNull(3) ? "https://via.placeholder.com/150" : reader.GetString(3);

                                    if (string.IsNullOrWhiteSpace(imageUrl) || !Uri.TryCreate(imageUrl, UriKind.RelativeOrAbsolute, out _))
                                    {
                                        imageUrl = "https://via.placeholder.com/150";
                                    }

                                    int reviewCount = GetReviewCount(reader.GetInt32(0));
                                    products.Add(new ProductDetails
                                    {
                                        ProductId1 = reader.GetInt32(0),
                                        Name1 = reader.GetString(1),
                                        Price1 = reader.GetDecimal(2),
                                        ImageUrl1 = imageUrl,
                                        Rating1 = reader.IsDBNull(4) ? 0 : reader.GetDouble(4),
                                        StoreName1 = reader.IsDBNull(5) ? "Невідомий магазин" : reader.GetString(5),
                                        StoreDescription1 = reader.IsDBNull(6) ? "Немає опису" : reader.GetString(6),
                                        StockQuantity1 = reader.GetInt32(7),
                                        ReviewCount1 = reviewCount
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
                                        Height = 440,
                                        Style = (Style)FindResource("SubCategoryBorderStyle"),
                                        Cursor = Cursors.Hand,
                                        Tag = product.ProductId1
                                    };
                                    productBorder.MouseLeftButtonDown += ProductBorder_MouseLeftButtonDown;

                                    StackPanel productPanel = new StackPanel
                                    {
                                        Background = Brushes.White,
                                        HorizontalAlignment = HorizontalAlignment.Stretch
                                    };
                                    Image productImage = new Image
                                    {
                                        Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(product.ImageUrl1, UriKind.RelativeOrAbsolute)),
                                        Width = 200,
                                        Height = 205,
                                        Stretch = Stretch.Uniform,
                                        Margin = new Thickness(5),
                                        HorizontalAlignment = HorizontalAlignment.Center
                                    };
                                    productPanel.Children.Add(productImage);

                                    TextBlock productName = new TextBlock
                                    {
                                        Text = product.Name1,
                                        TextAlignment = TextAlignment.Center,
                                        TextWrapping = TextWrapping.Wrap,
                                        Height = 50,
                                        Margin = new Thickness(5),
                                        TextTrimming = TextTrimming.CharacterEllipsis
                                    };
                                    double fontSize = Math.Max(10, 16 - (product.Name1.Length - 20) * 0.2);
                                    productName.FontSize = Math.Min(16, Math.Max(10, fontSize));
                                    productName.FontWeight = FontWeights.Medium;
                                    productPanel.Children.Add(productName);

                                    TextBlock reviewCountText = new TextBlock
                                    {
                                        Text = $"({product.ReviewCount1} відгуків)",
                                        FontSize = 14,
                                        Margin = new Thickness(5),
                                        TextAlignment = TextAlignment.Center,
                                        Foreground = Brushes.Gray
                                    };
                                    productPanel.Children.Add(reviewCountText);

                                    TextBlock stockText = new TextBlock
                                    {
                                        Text = product.StockQuantity1 > 0 ? $"В наявності: {product.StockQuantity1} шт." : "Немає в наявності",
                                        FontSize = 14,
                                        Margin = new Thickness(5),
                                        TextAlignment = TextAlignment.Center,
                                        Foreground = product.StockQuantity1 > 0 ? Brushes.Green : Brushes.Red
                                    };
                                    productPanel.Children.Add(stockText);

                                    TextBlock priceText = new TextBlock
                                    {
                                        Text = $"{product.Price1:F2} грн",
                                        FontSize = 16,
                                        FontWeight = FontWeights.Bold,
                                        Margin = new Thickness(5),
                                        Foreground = Brushes.Red,
                                        TextAlignment = TextAlignment.Center
                                    };
                                    productPanel.Children.Add(priceText);
                                    Button addToCartButton = new Button
                                    {
                                        Content = "Додати до кошика",
                                        Style = (Style)FindResource("AddToCartButtonStyle"),
                                        Tag = product.ProductId1,
                                        Margin = new Thickness(5),
                                        IsEnabled = product.StockQuantity1 > 0
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
                                    string noProductsMessage = (selectedBrands.Any() || priceFrom.HasValue || priceTo.HasValue || reviewCountMin.HasValue)
                                        ? "За вибраними фільтрами товари не знайдено."
                                        : "Товарів не знайдено.";
                                    ContentPanel.Children.Add(new TextBlock
                                    {
                                        Text = noProductsMessage,
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

        private void ProductBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is int productId)
            {
                ViewProduct_Click(sender, e, productId);
            }
        }

        private void LoadBestOffers()
        {
            if (Dispatcher.CheckAccess())
            {
                try
                {
                    var existingHeader = ContentPanel.Children.OfType<StackPanel>()
                        .FirstOrDefault(sp => sp.Children.OfType<TextBlock>().Any(tb => tb.Text == "Найкращі пропозиції для вас"));
                    if (existingHeader != null)
                    {
                        ContentPanel.Children.Remove(existingHeader);
                        var nextPanel = ContentPanel.Children.OfType<WrapPanel>()
                            .FirstOrDefault(wp => ContentPanel.Children.IndexOf(wp) > ContentPanel.Children.IndexOf(existingHeader));
                        if (nextPanel != null)
                        {
                            ContentPanel.Children.Remove(nextPanel);
                        }
                    }

                    StackPanel headerPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Margin = new Thickness(10, 20, 10, 10)
                    };
                    headerPanel.Children.Add(new TextBlock
                    {
                        Text = "Найкращі пропозиції для вас",
                        FontSize = 20,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 0, 10, 0)
                    });
                    ContentPanel.Children.Add(headerPanel);

                    string query = "SELECT p.productid, p.name, p.price, p.image_url, p.rating, s.storename, s.description AS store_description, p.stock_quantity " +
                                  "FROM products p " +
                                  "JOIN sellerprofiles s ON p.sellerid = s.sellerid " +
                                  "WHERE p.ishidden = false " +
                                  "ORDER BY RANDOM() " +
                                  "LIMIT 5";
                    LoadProductsWithQuery(query, new List<NpgsqlParameter>());
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка при завантаженні найкращих пропозицій: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private int GetReviewCount(int productId)
        {
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("SELECT COUNT(*) FROM product_reviews WHERE productid = @productId", connection))
                    {
                        command.Parameters.AddWithValue("productId", productId);
                        return Convert.ToInt32(command.ExecuteScalar());
                    }
                }
            }
            catch (Exception)
            {
                return 0;
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

        private void ViewProduct_Click(object sender, RoutedEventArgs e, int productId)
        {
            if (Dispatcher.CheckAccess() && productId > 0)
            {
                try
                {
                    using (var connection = new NpgsqlConnection(connectionString))
                    {
                        connection.Open();
                        using (var command = new NpgsqlCommand(
                            "SELECT p.productid, p.name, p.description, p.price, p.brand, c.name AS categoryname, p.image_url, s.storename, s.description AS store_description, p.stock_quantity " +
                            "FROM products p " +
                            "JOIN categories c ON p.categoryid = c.categoryid " +
                            "JOIN sellerprofiles s ON p.sellerid = s.sellerid " +
                            "WHERE p.productid = @productId AND p.ishidden = false", connection))
                        {
                            command.Parameters.AddWithValue("productId", productId);
                            using (var reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    string imageUrl = reader.IsDBNull(6) ? "https://via.placeholder.com/150" : reader.GetString(6);
                                    if (string.IsNullOrWhiteSpace(imageUrl) || !Uri.TryCreate(imageUrl, UriKind.RelativeOrAbsolute, out _))
                                    {
                                        imageUrl = "https://via.placeholder.com/150";
                                    }

                                    var product = new ProductDetails
                                    {
                                        ProductId1 = reader.GetInt32(0),
                                        Name1 = reader.GetString(1),
                                        Description1 = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                        Price1 = reader.GetDecimal(3),
                                        Brand1 = reader.GetString(4),
                                        CategoryName1 = reader.GetString(5),
                                        ImageUrl1 = imageUrl,
                                        StoreName1 = reader.GetString(7),
                                        StoreDescription1 = reader.IsDBNull(8) ? "Немає опису" : reader.GetString(8),
                                        StockQuantity1 = reader.GetInt32(9)
                                    };

                                    ContentPanel.Children.Clear();
                                    StackPanel productDetailsPanel = new StackPanel
                                    {
                                        Margin = new Thickness(20),
                                        Background = Brushes.White,
                                        Width = 800,
                                        MaxWidth = 800
                                    };

                                    Button backButton = new Button
                                    {
                                        Content = "← Повернутися до товарів",
                                        Style = (Style)FindResource("ActionButtonStyle"),
                                        Margin = new Thickness(0, 0, 0, 20),
                                        Width = 200
                                    };
                                    backButton.Click += (s, args) => LoadProducts();
                                    productDetailsPanel.Children.Add(backButton);

                                    Border imageBorder = new Border
                                    {
                                        BorderBrush = Brushes.LightGray,
                                        BorderThickness = new Thickness(1),
                                        CornerRadius = new CornerRadius(10),
                                        Margin = new Thickness(0, 0, 0, 20),
                                        Effect = new DropShadowEffect { Color = Colors.Gray, Direction = 270, ShadowDepth = 3, BlurRadius = 10, Opacity = 0.5 }
                                    };
                                    Image productImage = new Image
                                    {
                                        Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(product.ImageUrl1, UriKind.RelativeOrAbsolute)),
                                        Width = 400,
                                        Height = 400,
                                        Stretch = Stretch.Uniform,
                                        HorizontalAlignment = HorizontalAlignment.Center
                                    };
                                    imageBorder.Child = productImage;
                                    productDetailsPanel.Children.Add(imageBorder);

                                    TextBlock titleText = new TextBlock
                                    {
                                        Text = product.Name1,
                                        FontSize = 24,
                                        FontWeight = FontWeights.Bold,
                                        Foreground = Brushes.DarkBlue,
                                        TextAlignment = TextAlignment.Center,
                                        Margin = new Thickness(0, 0, 0, 15)
                                    };
                                    productDetailsPanel.Children.Add(titleText);

                                    Grid infoGrid = new Grid
                                    {
                                        Margin = new Thickness(0, 0, 0, 20)
                                    };
                                    infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                                    infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                                    infoGrid.RowDefinitions.Add(new RowDefinition());
                                    infoGrid.RowDefinitions.Add(new RowDefinition());
                                    infoGrid.RowDefinitions.Add(new RowDefinition());

                                    TextBlock categoryLabel = new TextBlock { Text = "Категорія:", FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = Brushes.DarkSlateGray, Margin = new Thickness(0, 0, 10, 0) };
                                    TextBlock categoryValue = new TextBlock { Text = product.CategoryName1, FontSize = 16, Foreground = Brushes.Black };
                                    Grid.SetRow(categoryLabel, 0);
                                    Grid.SetColumn(categoryLabel, 0);
                                    Grid.SetRow(categoryValue, 0);
                                    Grid.SetColumn(categoryValue, 1);
                                    infoGrid.Children.Add(categoryLabel);
                                    infoGrid.Children.Add(categoryValue);

                                    TextBlock brandLabel = new TextBlock { Text = "Бренд:", FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = Brushes.DarkSlateGray, Margin = new Thickness(0, 10, 10, 0) };
                                    TextBlock brandValue = new TextBlock { Text = product.Brand1, FontSize = 16, Foreground = Brushes.Black };
                                    Grid.SetRow(brandLabel, 1);
                                    Grid.SetColumn(brandLabel, 0);
                                    Grid.SetRow(brandValue, 1);
                                    Grid.SetColumn(brandValue, 1);
                                    infoGrid.Children.Add(brandLabel);
                                    infoGrid.Children.Add(brandValue);

                                    TextBlock priceLabel = new TextBlock { Text = "Ціна:", FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = Brushes.DarkSlateGray, Margin = new Thickness(0, 10, 10, 0) };
                                    TextBlock priceValue = new TextBlock { Text = $"{product.Price1:F2} грн", FontSize = 16, Foreground = Brushes.Red, FontWeight = FontWeights.Bold };
                                    Grid.SetRow(priceLabel, 2);
                                    Grid.SetColumn(priceLabel, 0);
                                    Grid.SetRow(priceValue, 2);
                                    Grid.SetColumn(priceValue, 1);
                                    infoGrid.Children.Add(priceLabel);
                                    infoGrid.Children.Add(priceValue);

                                    productDetailsPanel.Children.Add(infoGrid);

                                    TextBlock stockText = new TextBlock
                                    {
                                        Text = product.StockQuantity1 > 0 ? $"В наявності: {product.StockQuantity1} шт." : "Немає в наявності",
                                        FontSize = 16,
                                        Foreground = product.StockQuantity1 > 0 ? Brushes.Green : Brushes.Red,
                                        TextAlignment = TextAlignment.Center,
                                        Margin = new Thickness(0, 0, 0, 20)
                                    };
                                    productDetailsPanel.Children.Add(stockText);

                                    Button addToCartButton = new Button
                                    {
                                        Content = "Додати до кошика",
                                        Style = (Style)FindResource("AddToCartButtonStyle"),
                                        Tag = product.ProductId1,
                                        Margin = new Thickness(0, 0, 0, 20),
                                        IsEnabled = product.StockQuantity1 > 0,
                                        HorizontalAlignment = HorizontalAlignment.Center
                                    };
                                    addToCartButton.Click += AddToCart_Click;
                                    productDetailsPanel.Children.Add(addToCartButton);

                                    Expander descriptionExpander = new Expander
                                    {
                                        Header = "Опис товару",
                                        FontSize = 18,
                                        FontWeight = FontWeights.SemiBold,
                                        Foreground = Brushes.DarkBlue,
                                        Margin = new Thickness(0, 0, 0, 15)
                                    };
                                    TextBlock descriptionText = new TextBlock
                                    {
                                        Text = product.Description1,
                                        FontSize = 14,
                                        TextWrapping = TextWrapping.Wrap,
                                        Margin = new Thickness(10),
                                        Foreground = Brushes.Black
                                    };
                                    descriptionExpander.Content = descriptionText;
                                    productDetailsPanel.Children.Add(descriptionExpander);

                                    Expander storeExpander = new Expander
                                    {
                                        Header = "Інформація про магазин",
                                        FontSize = 18,
                                        FontWeight = FontWeights.SemiBold,
                                        Foreground = Brushes.DarkBlue,
                                        Margin = new Thickness(0, 0, 0, 15)
                                    };
                                    StackPanel storePanel = new StackPanel
                                    {
                                        Margin = new Thickness(10)
                                    };
                                    storePanel.Children.Add(new TextBlock { Text = $"Магазин: {product.StoreName1}", FontSize = 14, FontWeight = FontWeights.Medium, Foreground = Brushes.Black });
                                    storePanel.Children.Add(new TextBlock { Text = product.StoreDescription1, FontSize = 14, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 0), Foreground = Brushes.Gray });
                                    storeExpander.Content = storePanel;
                                    productDetailsPanel.Children.Add(storeExpander);

                                    Expander reviewsExpander = new Expander
                                    {
                                        Header = "Відгуки",
                                        FontSize = 18,
                                        FontWeight = FontWeights.SemiBold,
                                        Foreground = Brushes.DarkBlue,
                                        Margin = new Thickness(0, 0, 0, 15)
                                    };
                                    ListBox reviewsList = new ListBox { Height = 150, Margin = new Thickness(10), FontSize = 14 };
                                    LoadReviews(product.ProductId1, reviewsList);
                                    reviewsExpander.Content = reviewsList;
                                    productDetailsPanel.Children.Add(reviewsExpander);

                                    StackPanel reviewInputPanel = new StackPanel
                                    {
                                        Margin = new Thickness(0, 0, 0, 20)
                                    };
                                    TextBox reviewTextBox = new TextBox
                                    {
                                        Height = 80,
                                        Margin = new Thickness(0, 0, 0, 10),
                                        AcceptsReturn = true,
                                        Text = "Ваш відгук...",
                                        FontSize = 14
                                    };
                                    reviewTextBox.GotFocus += (s, args) => { if (reviewTextBox.Text == "Ваш відгук...") reviewTextBox.Text = ""; };
                                    reviewTextBox.LostFocus += (s, args) => { if (string.IsNullOrWhiteSpace(reviewTextBox.Text)) reviewTextBox.Text = "Ваш відгук..."; };
                                    reviewInputPanel.Children.Add(reviewTextBox);
                                    Button submitReviewButton = new Button
                                    {
                                        Content = "Залишити відгук",
                                        Width = 180,
                                        Height = 40,
                                        FontSize = 14,
                                        Style = (Style)FindResource("AddToCartButtonStyle"),
                                        HorizontalAlignment = HorizontalAlignment.Center
                                    };
                                    submitReviewButton.Click += (s, args) =>
                                    {
                                        if (userProfile?.UserId != null && !string.IsNullOrWhiteSpace(reviewTextBox.Text) && reviewTextBox.Text != "Ваш відгук...")
                                        {
                                            SaveReview(product.ProductId1, ((int?)userProfile.UserId).Value, reviewTextBox.Text);
                                            LoadReviews(product.ProductId1, reviewsList);
                                            reviewTextBox.Text = "Ваш відгук...";
                                        }
                                        else
                                        {
                                            MessageBox.Show("Будь ласка, увійдіть в акаунт і введіть відгук.", "Попередження", MessageBoxButton.OK, MessageBoxImage.Warning);
                                        }
                                    };
                                    reviewInputPanel.Children.Add(submitReviewButton);
                                    productDetailsPanel.Children.Add(reviewInputPanel);

                                    ContentPanel.Children.Add(productDetailsPanel);
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

        private void UpdateReview(int productId, int userId, string newReviewText)
        {
            if (Dispatcher.CheckAccess())
            {
                try
                {
                    using (var connection = new NpgsqlConnection(connectionString))
                    {
                        connection.Open();
                        using (var command = new NpgsqlCommand("UPDATE product_reviews SET review_text = @reviewText, review_date = @reviewDate WHERE productid = @productId AND userid = @userId", connection))
                        {
                            command.Parameters.AddWithValue("reviewText", newReviewText);
                            command.Parameters.AddWithValue("reviewDate", DateTime.Now);
                            command.Parameters.AddWithValue("productId", productId);
                            command.Parameters.AddWithValue("userId", userId);
                            int rowsAffected = command.ExecuteNonQuery();
                            if (rowsAffected > 0)
                            {
                                MessageBox.Show("Відгук успішно оновлено.", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            else
                            {
                                MessageBox.Show("Відгук не знайдено або у вас немає прав для його редагування.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка при оновленні відгуку: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DeleteReview(int productId, int userId, ListBox reviewsList)
        {
            if (Dispatcher.CheckAccess())
            {
                try
                {
                    using (var connection = new NpgsqlConnection(connectionString))
                    {
                        connection.Open();
                        using (var command = new NpgsqlCommand("DELETE FROM product_reviews WHERE productid = @productId AND userid = @userId", connection))
                        {
                            command.Parameters.AddWithValue("productId", productId);
                            command.Parameters.AddWithValue("userId", userId);
                            int rowsAffected = command.ExecuteNonQuery();
                            if (rowsAffected > 0)
                            {
                                MessageBox.Show("Відгук успішно видалено.", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                                LoadReviews(productId, reviewsList);
                            }
                            else
                            {
                                MessageBox.Show("Відгук не знайдено або у вас немає прав для його видалення.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка при видаленні відгуку: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    bool userHasReview = false;
                    string userReviewText = null;
                    DateTime? userReviewDate = null;

                    using (var connection = new NpgsqlConnection(connectionString))
                    {
                        connection.Open();
                        using (var command = new NpgsqlCommand(
                            "SELECT pr.review_text, u.firstname, pr.review_date, pr.userid " +
                            "FROM product_reviews pr " +
                            "JOIN userdetails u ON pr.userid = u.userid " +
                            "WHERE pr.productid = @productId " +
                            "ORDER BY pr.review_date DESC", connection))
                        {
                            command.Parameters.AddWithValue("productId", productId);
                            using (var reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    string reviewText = reader.GetString(0);
                                    string userName = reader.IsDBNull(1) ? "Анонім" : reader.GetString(1);
                                    DateTime reviewDate = reader.GetDateTime(2);
                                    int reviewUserId = reader.GetInt32(3);

                                    // Використовуємо Grid для розташування тексту та кнопок
                                    Grid reviewGrid = new Grid { Margin = new Thickness(0, 5, 0, 5) };
                                    reviewGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Текст займатиме весь доступний простір
                                    reviewGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Кнопки займатимуть мінімально необхідний простір

                                    TextBlock reviewBlock = new TextBlock
                                    {
                                        Text = $"{userName} ({reviewDate:dd.MM.yyyy}): {reviewText}",
                                        TextWrapping = TextWrapping.Wrap,
                                        VerticalAlignment = VerticalAlignment.Center,
                                        Margin = new Thickness(0, 0, 10, 0) // Додаємо відступ праворуч для розділення тексту та кнопок
                                    };
                                    Grid.SetColumn(reviewBlock, 0);
                                    reviewGrid.Children.Add(reviewBlock);

                                    Button editButton = null;
                                    Button deleteButton = null;

                                    if (userProfile?.UserId == reviewUserId)
                                    {
                                        userHasReview = true;
                                        userReviewText = reviewText;
                                        userReviewDate = reviewDate;

                                        // Створюємо StackPanel для кнопок
                                        StackPanel buttonPanel = new StackPanel
                                        {
                                            Orientation = Orientation.Horizontal,
                                            HorizontalAlignment = HorizontalAlignment.Right, // Вирівнюємо кнопки по правому краю
                                            VerticalAlignment = VerticalAlignment.Center
                                        };

                                        // Кнопка редагування з іконкою
                                        editButton = new Button
                                        {
                                            Content = "✏️", // Іконка редагування
                                            Width = 25,
                                            Height = 25,
                                            Margin = new Thickness(5, 0, 5, 0),
                                            Background = Brushes.Transparent,
                                            BorderThickness = new Thickness(0),
                                            ToolTip = "Редагувати відгук"
                                        };
                                        editButton.Click += (s, e) =>
                                        {
                                            TextBox editTextBox = new TextBox
                                            {
                                                Text = reviewText,
                                                Width = 300,
                                                Height = 80,
                                                AcceptsReturn = true,
                                                Margin = new Thickness(0, 5, 0, 0)
                                            };
                                            Button saveEditButton = new Button
                                            {
                                                Content = "Зберегти",
                                                Width = 80,
                                                Height = 25,
                                                Margin = new Thickness(0, 5, 0, 0),
                                                Background = Brushes.Green,
                                                Foreground = Brushes.White
                                            };
                                            saveEditButton.Click += (ss, ee) =>
                                            {
                                                if (!string.IsNullOrWhiteSpace(editTextBox.Text))
                                                {
                                                    UpdateReview(productId, userProfile.UserId, editTextBox.Text);
                                                    LoadReviews(productId, reviewsList);
                                                }
                                            };
                                            reviewGrid.Children.Clear(); // Очищаємо попередній вміст
                                            reviewGrid.Children.Add(editTextBox);
                                            reviewGrid.Children.Add(saveEditButton);
                                        };
                                        buttonPanel.Children.Add(editButton);

                                        // Кнопка видалення з іконкою
                                        deleteButton = new Button
                                        {
                                            Content = "🗑️", // Іконка видалення
                                            Width = 25,
                                            Height = 25,
                                            Margin = new Thickness(0, 0, 0, 0), // Видаляємо зайві відступи
                                            Background = Brushes.Transparent,
                                            BorderThickness = new Thickness(0),
                                            ToolTip = "Видалити відгук"
                                        };
                                        deleteButton.Click += (s, e) =>
                                        {
                                            if (MessageBox.Show("Ви впевнені, що хочете видалити свій відгук?", "Підтвердження", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                                            {
                                                DeleteReview(productId, userProfile.UserId, reviewsList);
                                            }
                                        };
                                        buttonPanel.Children.Add(deleteButton);

                                        Grid.SetColumn(buttonPanel, 1);
                                        reviewGrid.Children.Add(buttonPanel);
                                    }

                                    reviewsList.Items.Add(reviewGrid);
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
                    if (!(userProfile?.UserId is int buyerId) || buyerId <= 0)
                    {
                        MessageBox.Show("Для додавання товару до кошика необхідно авторизуватися.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    ProductDetails product = null;
                    using (var connection = new NpgsqlConnection(connectionString))
                    {
                        connection.Open();
                        using (var command = new NpgsqlCommand(
                            "SELECT p.name, p.stock_quantity, p.price FROM products p WHERE p.productid = @productId AND p.ishidden = false", connection))
                        {
                            command.Parameters.AddWithValue("productId", productId);
                            using (var reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    product = new ProductDetails
                                    {
                                        ProductId1 = productId,
                                        Name1 = reader.GetString(0),
                                        StockQuantity1 = reader.GetInt32(1),
                                        Price1 = reader.GetDecimal(2)
                                    };
                                }
                            }
                        }

                        if (product == null)
                        {
                            MessageBox.Show("Товар не знайдено або він прихований.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }

                        if (product.StockQuantity1 <= 0)
                        {
                            MessageBox.Show($"Товар {product.Name1} відсутній у наявності.", "Попередження", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        int quantity = 1;

                        int currentQuantity = 0;
                        using (var checkCommand = new NpgsqlCommand(
                            "SELECT quantity FROM cart WHERE buyerid = @buyerid AND productid = @productid", connection))
                        {
                            checkCommand.Parameters.AddWithValue("buyerid", buyerId);
                            checkCommand.Parameters.AddWithValue("productid", productId);
                            var result = checkCommand.ExecuteScalar();
                            if (result != null)
                            {
                                currentQuantity = Convert.ToInt32(result);
                            }
                        }

                        int newQuantity = currentQuantity > 0 ? currentQuantity + quantity : quantity;

                        if (newQuantity > product.StockQuantity1)
                        {
                            MessageBox.Show($"Загальна кількість у кошику ({newQuantity}) перевищує доступну ({product.StockQuantity1}).", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }

                        if (currentQuantity > 0)
                        {
                            using (var updateCommand = new NpgsqlCommand(
                                "UPDATE cart SET quantity = @quantity WHERE buyerid = @buyerid AND productid = @productid", connection))
                            {
                                updateCommand.Parameters.AddWithValue("quantity", newQuantity);
                                updateCommand.Parameters.AddWithValue("buyerid", buyerId);
                                updateCommand.Parameters.AddWithValue("productid", productId);
                                updateCommand.ExecuteNonQuery();
                            }
                            MessageBox.Show($"Кількість товару {product.Name1} у кошику оновлено! (Кількість: {newQuantity})", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            using (var insertCommand = new NpgsqlCommand(
                                "INSERT INTO cart (buyerid, productid, quantity) VALUES (@buyerid, @productid, @quantity)", connection))
                            {
                                insertCommand.Parameters.AddWithValue("buyerid", buyerId);
                                insertCommand.Parameters.AddWithValue("productid", productId);
                                insertCommand.Parameters.AddWithValue("quantity", quantity);
                                insertCommand.ExecuteNonQuery();
                            }
                            MessageBox.Show($"Товар {product.Name1} додано до кошика! (Кількість: {quantity})", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка при додаванні товару до кошика: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CategoryToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess())
            {
                CategoryPanel.Visibility = CategoryPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                FilterPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void ApplyFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess() && selectedSubCategoryId.HasValue)
            {
                if (decimal.TryParse(PriceFromTextBox.Text, out decimal from) && from >= 0)
                {
                    priceFrom = from;
                }
                else
                {
                    priceFrom = null;
                }

                if (decimal.TryParse(PriceToTextBox.Text, out decimal to) && to >= 0)
                {
                    priceTo = to;
                }
                else
                {
                    priceTo = null;
                }

                if (priceFrom.HasValue && priceTo.HasValue && priceFrom > priceTo)
                {
                    MessageBox.Show("Мінімальна ціна не може бути більшою за максимальну.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (ReviewCountComboBox.SelectedItem is ComboBoxItem selectedItem && int.TryParse(selectedItem.Tag?.ToString(), out int minReviews))
                {
                    reviewCountMin = minReviews;
                }
                else
                {
                    reviewCountMin = null;
                }

                LoadProducts(subCategoryId: selectedSubCategoryId.Value);
            }
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess() && SearchBox.Text == "Я шукаю...")
            {
                SearchBox.Text = "";
                SearchBox.Foreground = Brushes.Black;
            }
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess() && string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                SearchBox.Text = "Я шукаю...";
                SearchBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F8C8D"));
            }
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (Dispatcher.CheckAccess() && e.Key == Key.Enter)
            {
                SearchButton_Click(sender, new RoutedEventArgs());
            }
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess() && !string.IsNullOrWhiteSpace(SearchBox.Text) && SearchBox.Text != "Я шукаю...")
            {
                string searchQuery = SearchBox.Text.Trim();
                try
                {
                    ContentPanel.Children.Clear();
                    FilterPanel.Visibility = Visibility.Collapsed;

                    string query = "SELECT p.productid, p.name, p.price, p.image_url, p.rating, s.storename, s.description AS store_description, p.stock_quantity " +
                                  "FROM products p " +
                                  "JOIN sellerprofiles s ON p.sellerid = s.sellerid " +
                                  "WHERE p.ishidden = false AND (p.name ILIKE @searchQuery OR p.description ILIKE @searchQuery) " +
                                  "LIMIT 5";
                    var parameters = new List<NpgsqlParameter>
                    {
                        new NpgsqlParameter("searchQuery", $"%{searchQuery}%")
                    };
                    LoadProductsWithQuery(query, parameters);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка при пошуку: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CartButton_Click(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess())
            {
                if (userProfile?.UserId is int buyerId && buyerId > 0)
                {
                    List<ProductDetails> cartItems = LoadCartItemsFromDatabase(buyerId);
                    CartWindow cartWindow = new CartWindow(cartItems, userProfile, connectionString, this);
                    cartWindow.ShowDialog();
                }
                else
                {
                    MessageBox.Show("Необхідно авторизуватися для перегляду кошика.", "Попередження", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private List<ProductDetails> LoadCartItemsFromDatabase(int buyerId)
        {
            List<ProductDetails> cartItems = new List<ProductDetails>();
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand(
                        "SELECT p.productid, p.name, p.price, p.image_url, p.stock_quantity, c.quantity " +
                        "FROM cart c " +
                        "JOIN products p ON c.productid = p.productid " +
                        "WHERE c.buyerid = @buyerId AND p.ishidden = false", connection))
                    {
                        command.Parameters.AddWithValue("buyerId", buyerId);
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string imageUrl = reader.IsDBNull(3) ? "https://via.placeholder.com/150" : reader.GetString(3);
                                if (string.IsNullOrWhiteSpace(imageUrl) || !Uri.TryCreate(imageUrl, UriKind.RelativeOrAbsolute, out _))
                                {
                                    imageUrl = "https://via.placeholder.com/150";
                                }

                                cartItems.Add(new ProductDetails
                                {
                                    ProductId1 = reader.GetInt32(0),
                                    Name1 = reader.GetString(1),
                                    Price1 = reader.GetDecimal(2),
                                    ImageUrl1 = imageUrl,
                                    StockQuantity1 = reader.GetInt32(4),
                                    Quantity = reader.GetInt32(5)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при завантаженні кошика: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return cartItems;
        }

        private void ProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess())
            {
                ProfileBorder.Visibility = Visibility.Visible;
            }
        }

        private void CloseProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess())
            {
                ProfileBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void SaveProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess())
            {
                try
                {
                    if (!(userProfile?.UserId is int userId) || userId <= 0)
                    {
                        MessageBox.Show("Необхідно авторизуватися.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    using (var connection = new NpgsqlConnection(connectionString))
                    {
                        connection.Open();
                        using (var command = new NpgsqlCommand(
                            "UPDATE userdetails SET firstname = @firstname, email = @email WHERE userid = @userid", connection))
                        {
                            command.Parameters.AddWithValue("userid", userId);
                            command.Parameters.AddWithValue("firstname", FirstNameTextBox.Text.Trim());
                            command.Parameters.AddWithValue("email", EmailTextBox.Text.Trim());
                            command.ExecuteNonQuery();
                        }

                        userProfile.FirstName = FirstNameTextBox.Text.Trim();
                        userProfile.Email = EmailTextBox.Text.Trim();

                        MessageBox.Show("Профіль оновлено.", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                        CloseProfileButton_Click(sender, e);
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
                if (MessageBox.Show("Ви впевнені, що хочете вийти?", "Підтвердження", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    userProfile = null;
                    CloseProfileButton_Click(sender, e);
                    LoadProducts();
                    MessageBox.Show("Ви вийшли з акаунта.", "Інформація", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess() && navigationIndex > 0)
            {
                navigationIndex--;
                var (category, categoryId, subCategoryId) = navigationHistory[navigationIndex];
                LoadProducts(category, categoryId, subCategoryId, false);
            }
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess() && navigationIndex < navigationHistory.Count - 1)
            {
                navigationIndex++;
                var (category, categoryId, subCategoryId) = navigationHistory[navigationIndex];
                LoadProducts(category, categoryId, subCategoryId, false);
            }
        }

        private void Logo_Click(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess())
            {
                navigationHistory.Clear();
                navigationIndex = -1;
                UpdateNavigationButtons();
                LoadProducts();
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess())
            {
                selectedCategoryId = null;
                selectedSubCategoryId = null;
                selectedBrands.Clear();
                priceFrom = null;
                priceTo = null;
                reviewCountMin = null;
                FilterPanel.Visibility = Visibility.Collapsed;
                CategoryPanel.Visibility = Visibility.Collapsed;
                SearchBox.Text = "Я шукаю...";
                SearchBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F8C8D"));

                navigationHistory.Clear();
                navigationIndex = -1;
                UpdateNavigationButtons();

                LoadProducts();
            }
        }

        private void OrdersButton_Click(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess())
            {
                if (userProfile?.UserId is int buyerId && buyerId > 0)
                {
                    OrdersWindow ordersWindow = new OrdersWindow(userProfile);
                    ordersWindow.ShowDialog();
                }
                else
                {
                    MessageBox.Show("Необхідно авторизуватися для перегляду замовлень.", "Попередження", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }

    public class ProductDetails
    {
        public int ProductId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string Brand { get; set; }
        public string CategoryName { get; set; }
        public string ImageUrl { get; set; }
        public double Rating { get; set; }
        public string StoreName { get; set; }
        public string StoreDescription { get; set; }
        public int StockQuantity { get; set; }
        public int Quantity { get; set; }
        public int ReviewCount { get; set; }
        public string SubcategoryName { get; internal set; }
        public bool IsHidden { get; internal set; }
        public bool IsHidden1 { get; internal set; }
        public string ImageUrl1 { get; internal set; }
        public string SubcategoryName1 { get; internal set; }
        public string CategoryName1 { get; internal set; }
        public decimal Price1 { get; internal set; }
        public string Description1 { get; internal set; }
        public int ProductId1 { get; internal set; }
        public string Brand1 { get; internal set; }
        public string Name1 { get; internal set; }
        public double Rating1 { get; internal set; }
        public int ReviewCount1 { get; internal set; }
        public int StockQuantity1 { get; internal set; }
        public string StoreDescription1 { get; internal set; }
        public string StoreName1 { get; internal set; }
    }

    public class UserProfile : System.ComponentModel.INotifyPropertyChanged
    {
        private int userId;
        private string firstName;
        private string middleName;
        private string phone;
        private string email;

        public int UserId
        {
            get => userId;
            set { userId = value; OnPropertyChanged(nameof(UserId)); }
        }
        public string FirstName
        {
            get => firstName;
            set { firstName = value; OnPropertyChanged(nameof(FirstName)); }
        }
        public string MiddleName
        {
            get => middleName;
            set { middleName = value; OnPropertyChanged(nameof(MiddleName)); }
        }
        public string Phone
        {
            get => phone;
            set { phone = value; OnPropertyChanged(nameof(Phone)); }
        }
        public string Email
        {
            get => email;
            set { email = value; OnPropertyChanged(nameof(Email)); }
        }

        public object Balance { get; internal set; }
        public string? LastName { get; internal set; }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}