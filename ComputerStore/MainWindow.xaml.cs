using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Npgsql;
using System.Configuration;
using System.Windows.Media;
using System.Windows.Input;
using ElmirClone.Models;

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
        private int? minReviewCount;

        private List<(string Category, int? CategoryId, int? SubCategoryId)> navigationHistory;
        private int navigationIndex;

        internal MainWindow(UserProfile userProfile)
        {
            InitializeComponent();
            this.userProfile = userProfile ?? throw new ArgumentNullException(nameof(userProfile));
            navigationHistory = new List<(string, int?, int?)>();
            navigationIndex = -1;
            selectedBrands = new List<string>();
            minReviewCount = null;

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

            CategoryPanel.IsVisibleChanged += CategoryPanel_IsVisibleChanged;
        }

        private void CategoryPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Dispatcher.CheckAccess())
            {
                if (CategoryPanel.Visibility == Visibility.Visible)
                {
                    CategoryPanel.UpdateLayout();
                    double categoryPanelHeight = CategoryListPanel.ActualHeight + CategoryListPanel.Margin.Top + CategoryListPanel.Margin.Bottom;
                    FilterPanel.Margin = new Thickness(10, categoryPanelHeight + 5, 10, 10);
                }
                else
                {
                    FilterPanel.Margin = new Thickness(10);
                }
            }
        }

        private void LoadCategories()
        {
            if (Dispatcher.CheckAccess())
            {
                try
                {
                    CategoryListPanel.Children.Clear();
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
                                    CategoryListPanel.Children.Add(catButton);
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
                    ReviewCountComboBox.SelectedIndex = 0;
                    selectedBrands.Clear();
                    priceFrom = null;
                    priceTo = null;
                    minReviewCount = null;

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

                        if (minReviewCount.HasValue)
                        {
                            query += " AND (pr.review_count >= @minReviewCount OR pr.review_count IS NULL AND @minReviewCount = 0)";
                            parameters.Add(new NpgsqlParameter("minReviewCount", minReviewCount.Value));
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
                                        ProductId = reader.GetInt32(0),
                                        Name = reader.GetString(1),
                                        Price = reader.GetDecimal(2),
                                        ImageUrl = imageUrl,
                                        Rating = reader.IsDBNull(4) ? 0 : reader.GetDouble(4),
                                        StoreName = reader.IsDBNull(5) ? "Невідомий магазин" : reader.GetString(5),
                                        StoreDescription = reader.IsDBNull(6) ? "Немає опису" : reader.GetString(6),
                                        StockQuantity = reader.GetInt32(7),
                                        ReviewCount = reviewCount
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
                                        Style = (Style)FindResource("SubCategoryBorderStyle"),
                                        Cursor = Cursors.Hand,
                                        Tag = product.ProductId
                                    };
                                    productBorder.MouseLeftButtonDown += ProductBorder_MouseLeftButtonDown;

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
                                        TextAlignment = TextAlignment.Center,
                                        TextWrapping = TextWrapping.Wrap,
                                        Height = 50,
                                        Margin = new Thickness(5),
                                        TextTrimming = TextTrimming.CharacterEllipsis
                                    };
                                    double fontSize = Math.Max(10, 16 - (product.Name.Length - 20) * 0.2);
                                    productName.FontSize = Math.Min(16, Math.Max(10, fontSize));
                                    productName.FontWeight = FontWeights.Medium;
                                    productPanel.Children.Add(productName);

                                    TextBlock reviewCountText = new TextBlock
                                    {
                                        Text = $"({product.ReviewCount} відгуків)",
                                        FontSize = 14,
                                        Margin = new Thickness(5),
                                        TextAlignment = TextAlignment.Center,
                                        Foreground = Brushes.Gray
                                    };
                                    productPanel.Children.Add(reviewCountText);

                                    TextBlock stockText = new TextBlock
                                    {
                                        Text = product.StockQuantity > 0 ? $"В наявності: {product.StockQuantity} шт." : "Немає в наявності",
                                        FontSize = 14,
                                        Margin = new Thickness(5),
                                        TextAlignment = TextAlignment.Center,
                                        Foreground = product.StockQuantity > 0 ? Brushes.Green : Brushes.Red
                                    };
                                    productPanel.Children.Add(stockText);

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
                                    Button addToCartButton = new Button
                                    {
                                        Content = "Додати до кошика",
                                        Style = (Style)FindResource("AddToCartButtonStyle"),
                                        Tag = product.ProductId,
                                        Margin = new Thickness(5),
                                        IsEnabled = product.StockQuantity > 0
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
                                        ProductId = reader.GetInt32(0),
                                        Name = reader.GetString(1),
                                        Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                        Price = reader.GetDecimal(3),
                                        Brand = reader.GetString(4),
                                        CategoryName = reader.GetString(5),
                                        ImageUrl = imageUrl,
                                        StoreName = reader.GetString(7),
                                        StoreDescription = reader.IsDBNull(8) ? "Немає опису" : reader.GetString(8),
                                        StockQuantity = reader.GetInt32(9)
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
                                    panel.Children.Add(new TextBlock { Text = $"В наявності: {product.StockQuantity} шт.", FontSize = 14, Margin = new Thickness(0, 0, 0, 5), Foreground = product.StockQuantity > 0 ? Brushes.Green : Brushes.Red });
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
                                        ProductId = productId,
                                        Name = reader.GetString(0),
                                        StockQuantity = reader.GetInt32(1),
                                        Price = reader.GetDecimal(2)
                                    };
                                }
                            }
                        }
                    }

                    if (product == null)
                    {
                        MessageBox.Show("Товар не знайдено або він прихований.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (product.StockQuantity <= 0)
                    {
                        MessageBox.Show($"Товар {product.Name} відсутній у наявності.", "Попередження", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    Window quantityWindow = new Window
                    {
                        Title = $"Додати {product.Name} до кошика",
                        Width = 300,
                        Height = 250,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        ResizeMode = ResizeMode.NoResize
                    };
                    StackPanel panel = new StackPanel { Margin = new Thickness(10) };
                    TextBlock stockText = new TextBlock
                    {
                        Text = $"Доступно: {product.StockQuantity} шт.",
                        FontSize = 14,
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    panel.Children.Add(stockText);
                    TextBlock label = new TextBlock
                    {
                        Text = "Вкажіть кількість:",
                        FontSize = 14,
                        Margin = new Thickness(0, 0, 0, 5)
                    };
                    panel.Children.Add(label);
                    StackPanel quantityPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    Button decreaseButton = new Button
                    {
                        Content = "-",
                        Width = 30,
                        Height = 30,
                        FontSize = 14,
                        Margin = new Thickness(0, 0, 5, 0),
                        Style = (Style)FindResource("AddToCartButtonStyle")
                    };
                    TextBox quantityBox = new TextBox
                    {
                        Text = "1",
                        FontSize = 14,
                        Width = 50,
                        TextAlignment = TextAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Button increaseButton = new Button
                    {
                        Content = "+",
                        Width = 30,
                        Height = 30,
                        FontSize = 14,
                        Margin = new Thickness(5, 0, 0, 0),
                        Style = (Style)FindResource("AddToCartButtonStyle")
                    };
                    quantityPanel.Children.Add(decreaseButton);
                    quantityPanel.Children.Add(quantityBox);
                    quantityPanel.Children.Add(increaseButton);
                    panel.Children.Add(quantityPanel);
                    TextBlock totalPriceText = new TextBlock
                    {
                        Text = $"Вартість: {(product.Price * 1):F2} грн",
                        FontSize = 14,
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    panel.Children.Add(totalPriceText);
                    StackPanel buttonPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right
                    };
                    Button confirmButton = new Button
                    {
                        Content = "Додати",
                        Width = 80,
                        Height = 30,
                        Margin = new Thickness(0, 0, 10, 0),
                        Style = (Style)FindResource("AddToCartButtonStyle")
                    };
                    Button cancelButton = new Button
                    {
                        Content = "Скасувати",
                        Width = 80,
                        Height = 30,
                        Style = (Style)FindResource("ActionButtonStyle")
                    };
                    buttonPanel.Children.Add(confirmButton);
                    buttonPanel.Children.Add(cancelButton);
                    panel.Children.Add(buttonPanel);
                    quantityWindow.Content = panel;

                    decreaseButton.Click += (s, ev) =>
                    {
                        if (int.TryParse(quantityBox.Text, out int quantity) && quantity > 1)
                        {
                            quantity--;
                            quantityBox.Text = quantity.ToString();
                            totalPriceText.Text = $"Вартість: {(product.Price * quantity):F2} грн";
                        }
                    };

                    increaseButton.Click += (s, ev) =>
                    {
                        if (int.TryParse(quantityBox.Text, out int quantity) && quantity < product.StockQuantity)
                        {
                            quantity++;
                            quantityBox.Text = quantity.ToString();
                            totalPriceText.Text = $"Вартість: {(product.Price * quantity):F2} грн";
                        }
                    };

                    quantityBox.TextChanged += (s, ev) =>
                    {
                        if (int.TryParse(quantityBox.Text, out int quantity))
                        {
                            if (quantity < 1)
                            {
                                quantityBox.Text = "1";
                                quantity = 1;
                            }
                            else if (quantity > product.StockQuantity)
                            {
                                quantityBox.Text = product.StockQuantity.ToString();
                                quantity = product.StockQuantity;
                            }
                            totalPriceText.Text = $"Вартість: {(product.Price * quantity):F2} грн";
                        }
                        else
                        {
                            quantityBox.Text = "1";
                            totalPriceText.Text = $"Вартість: {(product.Price * 1):F2} грн";
                        }
                    };

                    confirmButton.Click += (s, ev) =>
                    {
                        if (int.TryParse(quantityBox.Text, out int quantity) && quantity > 0)
                        {
                            if (quantity > product.StockQuantity)
                            {
                                MessageBox.Show($"Вибрана кількість ({quantity}) перевищує доступну ({product.StockQuantity}).", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }

                            using (var connection = new NpgsqlConnection(connectionString))
                            {
                                connection.Open();

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

                                int newQuantity = currentQuantity + quantity;

                                if (newQuantity > product.StockQuantity)
                                {
                                    MessageBox.Show($"Загальна кількість у кошику ({newQuantity}) перевищує доступну ({product.StockQuantity}).", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                                    MessageBox.Show($"Кількість товару {product.Name} у кошику оновлено! (Нова кількість: {newQuantity})", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
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
                                    MessageBox.Show($"Товар {product.Name} додано до кошика! (Кількість: {quantity})", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                                }
                            }
                            quantityWindow.Close();
                        }
                        else
                        {
                            MessageBox.Show("Введіть коректну кількість (ціле число більше 0).", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    };

                    cancelButton.Click += (s, ev) => quantityWindow.Close();
                    quantityWindow.ShowDialog();
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
                if (!selectedSubCategoryId.HasValue)
                {
                    FilterPanel.Visibility = Visibility.Collapsed;
                }
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

                if (ReviewCountComboBox.SelectedItem is ComboBoxItem selectedItem && int.TryParse(selectedItem.Tag?.ToString(), out int reviewCount))
                {
                    minReviewCount = reviewCount;
                }
                else
                {
                    minReviewCount = null;
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
                                    ProductId = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    Price = reader.GetDecimal(2),
                                    ImageUrl = imageUrl,
                                    StockQuantity = reader.GetInt32(4),
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
                    using (var connection = new NpgsqlConnection(connectionString))
                    {
                        connection.Open();
                        using (var command = new NpgsqlCommand(
                            "UPDATE userdetails SET firstname = @firstname, middlename = @middlename, phone = @phone, email = @email WHERE userid = @userId", connection))
                        {
                            command.Parameters.AddWithValue("firstname", userProfile.FirstName ?? (object)DBNull.Value);
                            command.Parameters.AddWithValue("middlename", userProfile.MiddleName ?? (object)DBNull.Value);
                            command.Parameters.AddWithValue("phone", userProfile.Phone ?? (object)DBNull.Value);
                            command.Parameters.AddWithValue("email", userProfile.Email ?? (object)DBNull.Value);
                            command.Parameters.AddWithValue("userId", userProfile.UserId == 0 ? (object)DBNull.Value : userProfile.UserId);
                            command.ExecuteNonQuery();
                        }
                    }
                    MessageBox.Show("Профіль успішно оновлено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                    ProfileBorder.Visibility = Visibility.Collapsed;
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
                var result = MessageBox.Show("Ви впевнені, що хочете вийти?", "Підтвердження", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    var loginWindow = new LoginWindow();
                    loginWindow.Show();
                    Close();
                }
            }
        }

        private void OrdersButton_Click(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess())
            {
                if (userProfile?.UserId is int buyerId && buyerId > 0)
                {
                    try
                    {
                        List<OrderDetails> orders = new List<OrderDetails>();
                        using (var connection = new NpgsqlConnection(connectionString))
                        {
                            connection.Open();
                            using (var command = new NpgsqlCommand(
                                "SELECT o.orderid, o.order_date, o.total_amount, o.status, p.name, oi.quantity, oi.price " +
                                "FROM orders o " +
                                "JOIN order_items oi ON o.orderid = oi.orderid " +
                                "JOIN products p ON oi.productid = p.productid " +
                                "WHERE o.buyerid = @buyerId " +
                                "ORDER BY o.order_date DESC", connection))
                            {
                                command.Parameters.AddWithValue("buyerId", buyerId);
                                using (var reader = command.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        orders.Add(new OrderDetails
                                        {
                                            OrderId = reader.GetInt32(0),
                                            OrderDate = reader.GetDateTime(1),
                                            TotalAmount = reader.GetDecimal(2),
                                            Status = reader.GetString(3),
                                            ProductName = reader.GetString(4),
                                            Quantity = reader.GetInt32(5),
                                            Price = reader.GetDecimal(6)
                                        });
                                    }
                                }
                            }
                        }

                        Window ordersWindow = new Window
                        {
                            Title = "Мої замовлення",
                            Width = 800,
                            Height = 600,
                            WindowStartupLocation = WindowStartupLocation.CenterScreen
                        };
                        ScrollViewer scrollViewer = new ScrollViewer
                        {
                            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                        };
                        StackPanel ordersPanel = new StackPanel { Margin = new Thickness(10) };

                        if (orders.Any())
                        {
                            var groupedOrders = orders.GroupBy(o => o.OrderId);
                            foreach (var orderGroup in groupedOrders)
                            {
                                var firstOrder = orderGroup.First();
                                StackPanel orderPanel = new StackPanel
                                {
                                    Margin = new Thickness(0, 0, 0, 20),
                                    Background = Brushes.White,
                                    Width = 740
                                };
                                orderPanel.Children.Add(new TextBlock
                                {
                                    Text = $"Замовлення #{firstOrder.OrderId} від {firstOrder.OrderDate:dd.MM.yyyy}",
                                    FontSize = 16,
                                    FontWeight = FontWeights.Bold,
                                    Margin = new Thickness(10, 10, 0, 5)
                                });
                                orderPanel.Children.Add(new TextBlock
                                {
                                    Text = $"Статус: {firstOrder.Status}",
                                    FontSize = 14,
                                    Margin = new Thickness(10, 0, 0, 5)
                                });
                                orderPanel.Children.Add(new TextBlock
                                {
                                    Text = $"Загальна сума: {firstOrder.TotalAmount:F2} грн",
                                    FontSize = 14,
                                    Margin = new Thickness(10, 0, 0, 5)
                                });

                                foreach (var item in orderGroup)
                                {
                                    orderPanel.Children.Add(new TextBlock
                                    {
                                        Text = $"- {item.ProductName}: {item.Quantity} шт. x {item.Price:F2} грн",
                                        FontSize = 14,
                                        Margin = new Thickness(20, 0, 0, 5)
                                    });
                                }

                                ordersPanel.Children.Add(orderPanel);
                            }
                        }
                        else
                        {
                            ordersPanel.Children.Add(new TextBlock
                            {
                                Text = "Замовлення відсутні.",
                                FontSize = 16,
                                Margin = new Thickness(10),
                                Foreground = Brushes.Gray
                            });
                        }

                        scrollViewer.Content = ordersPanel;
                        ordersWindow.Content = scrollViewer;
                        ordersWindow.ShowDialog();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Помилка при завантаженні замовлень: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("Необхідно авторизуватися для перегляду замовлень.", "Попередження", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                UpdateNavigationButtons();
            }
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess() && navigationIndex < navigationHistory.Count - 1)
            {
                navigationIndex++;
                var (category, categoryId, subCategoryId) = navigationHistory[navigationIndex];
                LoadProducts(category, categoryId, subCategoryId, false);
                UpdateNavigationButtons();
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (Dispatcher.CheckAccess())
            {
                if (navigationIndex >= 0 && navigationIndex < navigationHistory.Count)
                {
                    var (category, categoryId, subCategoryId) = navigationHistory[navigationIndex];
                    LoadProducts(category, categoryId, subCategoryId, false);
                }
                else
                {
                    LoadProducts();
                }
            }
        }

        private void Logo_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (Dispatcher.CheckAccess())
            {
                LoadProducts();
            }
        }
    }

    public class ProductDetails
    {
        public int ProductId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string ImageUrl { get; set; }
        public double Rating { get; set; }
        public string StoreName { get; set; }
        public string StoreDescription { get; set; }
        public int StockQuantity { get; set; }
        public int Quantity { get; set; }
        public string Brand { get; set; }
        public string CategoryName { get; set; }
        public int ReviewCount { get; set; }
        public string SubcategoryName { get; internal set; }
        public bool IsHidden { get; internal set; }
    }

    public class OrderDetails
    {
        public int OrderId { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }
}