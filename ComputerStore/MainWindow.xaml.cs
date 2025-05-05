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

        internal MainWindow(UserProfile userProfile)
        {
            InitializeComponent();
            this.userProfile = userProfile ?? throw new ArgumentNullException(nameof(userProfile));
            cartItems = new List<DbProduct>();
            notifiedOrders = new List<int>();
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
            orderStatusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            orderStatusTimer.Tick += CheckOrderStatus;
            orderStatusTimer.Start();
        }

        private void CheckOrderStatus(object sender, EventArgs e)
        {
            try
            {
                if (!(userProfile.UserId is int buyerId) || buyerId <= 0) return;

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

        private void LoadCategories()
        {
            try
            {
                CategoryPanel.Children.Clear();
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("SELECT categoryid, name FROM categories WHERE parentcategoryid IS NULL", connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int categoryId = reader.GetInt32(0);
                                string name = reader.GetString(1);
                                Button categoryButton = new Button
                                {
                                    Content = name,
                                    Tag = categoryId,
                                    Margin = new Thickness(5),
                                    Padding = new Thickness(5),
                                    FontSize = 14,
                                    Height = 40
                                };
                                categoryButton.Click += CategoryButton_Click;
                                CategoryPanel.Children.Add(categoryButton);
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

        public void LoadProducts(string category = null, int? categoryId = null, int? subCategoryId = null)
        {
            try
            {
                ContentPanel.Children.Clear();
                selectedCategoryId = categoryId;
                selectedSubCategoryId = subCategoryId;

                if (categoryId.HasValue)
                {
                    // Display subcategories under the selected category
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
                            command.Parameters.AddWithValue("parentId", categoryId.Value);
                            using (var reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    int subCategoryIdValue = reader.GetInt32(0);
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

                                    Button subCategoryButton = new Button { Content = subCategoryBorder, Tag = subCategoryIdValue };
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

                    ContentPanel.Children.Add(subCategoryPanel);
                }

                if (subCategoryId.HasValue)
                {
                    // Load products for the selected subcategory
                    string query = "SELECT p.ProductId, p.Name, p.Price, p.ImageUrl, p.Rating, s.storename, s.description AS store_description " +
                                  "FROM Products p " +
                                  "JOIN categories sc ON p.subcategoryid = sc.categoryid " +
                                  "JOIN sellerprofiles s ON p.sellerid = s.sellerid " +
                                  "WHERE sc.categoryid = @subCategoryId LIMIT 5";
                    var parameters = new List<NpgsqlParameter>
                    {
                        new NpgsqlParameter("subCategoryId", subCategoryId.Value)
                    };

                    LoadProductsWithQuery(query, parameters);
                }
                else if (categoryId.HasValue)
                {
                    // Load products for the selected category (including all its subcategories)
                    string query = "SELECT p.ProductId, p.Name, p.Price, p.ImageUrl, p.Rating, s.storename, s.description AS store_description " +
                                  "FROM Products p " +
                                  "JOIN categories c ON p.categoryid = c.categoryid " +
                                  "JOIN sellerprofiles s ON p.sellerid = s.sellerid " +
                                  "WHERE c.categoryid = @categoryId LIMIT 5";
                    var parameters = new List<NpgsqlParameter>
                    {
                        new NpgsqlParameter("categoryId", categoryId.Value)
                    };

                    LoadProductsWithQuery(query, parameters);
                }
                else if (!string.IsNullOrEmpty(category))
                {
                    // Load products by category name (fallback)
                    string query = "SELECT p.ProductId, p.Name, p.Price, p.ImageUrl, p.Rating, s.storename, s.description AS store_description " +
                                  "FROM Products p " +
                                  "JOIN categories c ON p.categoryid = c.categoryid " +
                                  "JOIN sellerprofiles s ON p.sellerid = s.sellerid " +
                                  "WHERE c.Name = @category LIMIT 5";
                    var parameters = new List<NpgsqlParameter>
                    {
                        new NpgsqlParameter("category", category)
                    };

                    LoadProductsWithQuery(query, parameters);
                }
                else
                {
                    // Main page: Group products by category and subcategory
                    using (var connection = new NpgsqlConnection(connectionString))
                    {
                        connection.Open();

                        // Fetch all top-level categories
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

                        // Fetch subcategories for each category
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

                        // For each category and subcategory, fetch products
                        foreach (var catEntry in categories)
                        {
                            int catId = catEntry.Key;
                            string catName = catEntry.Value.Name;
                            var subCategories = catEntry.Value.SubCategories;

                            // Add category header
                            TextBlock categoryHeader = new TextBlock
                            {
                                Text = catName,
                                FontSize = 20,
                                FontWeight = FontWeights.Bold,
                                Margin = new Thickness(10, 20, 10, 10)
                            };
                            ContentPanel.Children.Add(categoryHeader);

                            // First, load products directly under the category (subcategoryid IS NULL)
                            string catQuery = "SELECT p.ProductId, p.Name, p.Price, p.ImageUrl, p.Rating, s.storename, s.description AS store_description " +
                                             "FROM Products p " +
                                             "JOIN sellerprofiles s ON p.sellerid = s.sellerid " +
                                             "WHERE p.categoryid = @categoryId AND p.subcategoryid IS NULL LIMIT 5";
                            var catParams = new List<NpgsqlParameter>
                            {
                                new NpgsqlParameter("categoryId", catId)
                            };
                            LoadProductsWithQuery(catQuery, catParams);

                            // Then, for each subcategory, load its products
                            foreach (var (subCatId, subCatName) in subCategories)
                            {
                                // Add subcategory header
                                TextBlock subCategoryHeader = new TextBlock
                                {
                                    Text = subCatName,
                                    FontSize = 16,
                                    FontWeight = FontWeights.SemiBold,
                                    Margin = new Thickness(10, 10, 10, 5)
                                };
                                ContentPanel.Children.Add(subCategoryHeader);

                                string subCatQuery = "SELECT p.ProductId, p.Name, p.Price, p.ImageUrl, p.Rating, s.storename, s.description AS store_description " +
                                                    "FROM Products p " +
                                                    "JOIN sellerprofiles s ON p.sellerid = s.sellerid " +
                                                    "WHERE p.subcategoryid = @subCategoryId LIMIT 5";
                                var subCatParams = new List<NpgsqlParameter>
                                {
                                    new NpgsqlParameter("subCategoryId", subCatId)
                                };
                                LoadProductsWithQuery(subCatQuery, subCatParams);
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

        private void LoadProductsWithQuery(string query, List<NpgsqlParameter> parameters)
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
                    }
                }
            }
        }

        private void Logo_Click(object sender, RoutedEventArgs e)
        {
            selectedCategoryId = null;
            selectedSubCategoryId = null;
            LoadProducts();
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox searchBox = sender as TextBox;
            if (searchBox?.Text == "Пошук...")
            {
                searchBox.Text = "";
                searchBox.Foreground = System.Windows.Media.Brushes.White;
            }
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox searchBox = sender as TextBox;
            if (string.IsNullOrWhiteSpace(searchBox?.Text))
            {
                searchBox.Text = "Пошук...";
                searchBox.Foreground = System.Windows.Media.Brushes.Gray;
            }
        }

        private void SearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                string searchText = (sender as TextBox)?.Text?.Trim();
                if (!string.IsNullOrEmpty(searchText) && searchText != "Пошук...")
                {
                    MessageBox.Show($"Пошук: {searchText}", "Інформація", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void CategoryButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int categoryId)
            {
                selectedCategoryId = categoryId;
                selectedSubCategoryId = null;
                LoadProducts(categoryId: categoryId);
            }
        }

        private void SubCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int subCategoryId)
            {
                selectedSubCategoryId = subCategoryId;
                LoadProducts(subCategoryId: subCategoryId);
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
                        using (var command = new NpgsqlCommand(
                            "SELECT p.ProductId, p.Name, p.Description, p.Price, p.Brand, c.Name AS CategoryName, p.ImageUrl, s.storename, s.description AS store_description " +
                            "FROM Products p " +
                            "JOIN Categories c ON p.CategoryId = c.CategoryId " +
                            "JOIN sellerprofiles s ON p.sellerid = s.sellerid " +
                            "WHERE p.ProductId = @productId", connection))
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
                                    panel.Children.Add(new TextBlock
                                    {
                                        Text = $"Назва: {product.Name}",
                                        FontWeight = FontWeights.Bold,
                                        FontSize = 16,
                                        Margin = new Thickness(0, 0, 0, 5)
                                    });
                                    panel.Children.Add(new TextBlock
                                    {
                                        Text = $"Категорія: {product.CategoryName}",
                                        FontSize = 14,
                                        Margin = new Thickness(0, 0, 0, 5)
                                    });
                                    panel.Children.Add(new TextBlock
                                    {
                                        Text = $"Бренд: {product.Brand}",
                                        FontSize = 14,
                                        Margin = new Thickness(0, 0, 0, 5)
                                    });
                                    panel.Children.Add(new TextBlock
                                    {
                                        Text = $"Ціна: {product.Price:F2} грн",
                                        FontSize = 14,
                                        Margin = new Thickness(0, 0, 0, 5)
                                    });
                                    panel.Children.Add(new TextBlock
                                    {
                                        Text = $"Опис: {product.Description}",
                                        TextWrapping = TextWrapping.Wrap,
                                        FontSize = 14,
                                        Margin = new Thickness(0, 0, 0, 10)
                                    });
                                    panel.Children.Add(new TextBlock
                                    {
                                        Text = $"Магазин: {product.StoreName}",
                                        FontWeight = FontWeights.Bold,
                                        FontSize = 14,
                                        Margin = new Thickness(0, 0, 0, 5)
                                    });
                                    panel.Children.Add(new TextBlock
                                    {
                                        Text = $"Опис магазину: {product.StoreDescription}",
                                        TextWrapping = TextWrapping.Wrap,
                                        FontSize = 14,
                                        Margin = new Thickness(0, 0, 0, 10)
                                    });

                                    // Отзывы
                                    TextBlock reviewsHeader = new TextBlock
                                    {
                                        Text = "Відгуки:",
                                        FontSize = 16,
                                        FontWeight = FontWeights.Bold,
                                        Margin = new Thickness(0, 0, 0, 5)
                                    };
                                    panel.Children.Add(reviewsHeader);
                                    ListBox reviewsList = new ListBox
                                    {
                                        Height = 150,
                                        Margin = new Thickness(0, 0, 0, 10),
                                        FontSize = 14
                                    };
                                    LoadReviews(product.ProductId, reviewsList);
                                    panel.Children.Add(reviewsList);

                                    // Поле для оставления отзыва
                                    TextBox reviewTextBox = new TextBox
                                    {
                                        Height = 80,
                                        Margin = new Thickness(0, 0, 0, 10),
                                        AcceptsReturn = true,
                                        Text = "Ваш відгук...",
                                        FontSize = 14
                                    };
                                    reviewTextBox.GotFocus += (s, args) =>
                                    {
                                        if (reviewTextBox.Text == "Ваш відгук...")
                                            reviewTextBox.Text = "";
                                    };
                                    reviewTextBox.LostFocus += (s, args) =>
                                    {
                                        if (string.IsNullOrWhiteSpace(reviewTextBox.Text))
                                            reviewTextBox.Text = "Ваш відгук...";
                                    };
                                    panel.Children.Add(reviewTextBox);

                                    Button submitReviewButton = new Button
                                    {
                                        Content = "Залишити відгук",
                                        Width = 180,
                                        Height = 40,
                                        FontSize = 14,
                                        Style = (Style)FindResource("AddToCartButtonStyle"),
                                        Margin = new Thickness(0, 0, 0, 10),
                                        HorizontalAlignment = HorizontalAlignment.Center
                                    };
                                    submitReviewButton.Click += (s, args) =>
                                    {
                                        if (userProfile?.UserId != null && !string.IsNullOrWhiteSpace(reviewTextBox.Text) && reviewTextBox.Text != "Ваш відгук...")
                                        {
                                            SaveReview(product.ProductId, ((int?)userProfile.UserId).Value, reviewTextBox.Text);
                                            LoadReviews(product.ProductId, reviewsList);
                                            reviewTextBox.Text = "Ваш відгук...";
                                        }
                                        else
                                        {
                                            MessageBox.Show("Будь ласка, увійдіть в акаунт і введіть відгук.", "Попередження", MessageBoxButton.OK, MessageBoxImage.Warning);
                                        }
                                    };
                                    panel.Children.Add(submitReviewButton);

                                    Button closeButton = new Button
                                    {
                                        Content = "Закрити",
                                        Width = 180,
                                        Height = 40,
                                        FontSize = 14,
                                        Margin = new Thickness(0, 0, 0, 10),
                                        HorizontalAlignment = HorizontalAlignment.Center
                                    };
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
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand(
                        "INSERT INTO product_reviews (productid, userid, review_text, review_date) VALUES (@productId, @userId, @reviewText, @reviewDate)",
                        connection))
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

        private void LoadReviews(int productId, ListBox reviewsList)
        {
            reviewsList.Items.Clear();
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand(
                        "SELECT pr.review_text, u.firstname, pr.review_date " +
                        "FROM product_reviews pr " +
                        "JOIN userdetails u ON pr.userid = u.userid " +
                        "WHERE pr.productid = @productId ORDER BY pr.review_date DESC",
                        connection))
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

        private void ProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProfilePanel != null)
            {
                ProfilePanel.Visibility = ProfilePanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            }
            else
            {
                MessageBox.Show("Елемент ProfilePanel не знайдено в розмітці.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadOrders()
        {
            try
            {
                if (!(userProfile.UserId is int buyerId) || buyerId <= 0) return;

                OrdersList.Items.Clear();
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand(
                        "SELECT o.orderid, o.status, p.name " +
                        "FROM orders o " +
                        "JOIN products p ON o.productid = p.productid " +
                        "WHERE o.buyerid = @buyerid", connection))
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

        private void ReturnButton_Click(object sender, RoutedEventArgs e)
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

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            selectedCategoryId = null;
            selectedSubCategoryId = null;
            LoadProducts();
            OrderPanel.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }

        private void OrdersButton_Click(object sender, RoutedEventArgs e)
        {
            OrderPanel.Visibility = OrderPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            if (OrderPanel.Visibility == Visibility.Visible)
            {
                LoadOrders();
            }
        }

        private void SaveProfileButton_Click(object sender, RoutedEventArgs e)
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
                MessageBox.Show($"Помилка при виході з системи: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}