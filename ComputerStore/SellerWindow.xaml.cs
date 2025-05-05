using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Npgsql;
using System.Configuration;
using Microsoft.Win32; // Для OpenFileDialog
using ElmirClone.Models; // Используем модели из Models.cs
using System.IO;

namespace ElmirClone
{
    public partial class SellerWindow : Window
    {
        private string connectionString;
        private int sellerId;
        private string selectedImagePath;

        public SellerWindow(int sellerId)
        {
            InitializeComponent();
            this.sellerId = sellerId;
            connectionString = ConfigurationManager.ConnectionStrings["ElitePCConnection"]?.ConnectionString;
            if (string.IsNullOrEmpty(connectionString))
            {
                MessageBox.Show("Рядок підключення до бази даних не знайдено.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            LoadCategories();
            LoadProducts();
            LoadOrders();
            LoadFinancials();
            LoadStoreProfile();
        }

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

        private void SelectImageButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|All files (*.*)|*.*",
                Title = "Виберіть зображення товару"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                selectedImagePath = openFileDialog.FileName;
                NewProductImagePath.Text = selectedImagePath;
            }
        }

        private void LoadCategories()
        {
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("SELECT categoryid, name FROM categories WHERE parentcategoryid IS NULL", connection))
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
                            ProductCategory.SelectionChanged += ProductCategory_SelectionChanged;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при завантаженні категорій: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ProductCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadSubcategories();
        }

        private void LoadSubcategories()
        {
            try
            {
                int? selectedCategoryId = ProductCategory.SelectedValue as int?;
                if (selectedCategoryId == null)
                {
                    ProductSubcategory.ItemsSource = null;
                    ProductSubcategory.SelectedValue = null; // Очищаем выбор подкатегории
                    return;
                }

                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("SELECT categoryid, name FROM categories WHERE parentcategoryid = @categoryid", connection))
                    {
                        command.Parameters.AddWithValue("categoryid", selectedCategoryId.Value);
                        using (var reader = command.ExecuteReader())
                        {
                            var subcategories = new List<Subcategory>();
                            while (reader.Read())
                            {
                                subcategories.Add(new Subcategory
                                {
                                    SubcategoryId = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    CategoryId = selectedCategoryId.Value
                                });
                            }
                            ProductSubcategory.ItemsSource = subcategories;
                            ProductSubcategory.DisplayMemberPath = "Name";
                            ProductSubcategory.SelectedValuePath = "SubcategoryId";
                            ProductSubcategory.SelectedValue = null; // Сбрасываем выбор подкатегории при изменении категории
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при завантаженні підкатегорій: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadProducts()
        {
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("SELECT p.productid, p.name, p.description, p.price, p.brand, c.name AS categoryname, sc.name AS subcategoryname, p.image_url FROM products p JOIN categories c ON p.categoryid = c.categoryid LEFT JOIN categories sc ON p.subcategoryid = sc.categoryid WHERE p.sellerid = @sellerid", connection))
                    {
                        command.Parameters.AddWithValue("sellerid", sellerId);
                        using (var reader = command.ExecuteReader())
                        {
                            var products = new List<DbProduct>();
                            while (reader.Read())
                            {
                                string imageUrl = reader.IsDBNull(7) ? "https://via.placeholder.com/150" : reader.GetString(7);
                                products.Add(new DbProduct
                                {
                                    ProductId = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    Price = reader.GetDecimal(3),
                                    Brand = reader.GetString(4),
                                    CategoryName = reader.GetString(5),
                                    SubcategoryName = reader.IsDBNull(6) ? "Не вказано" : reader.GetString(6),
                                    ImageUrl = imageUrl
                                });
                            }
                            ProductsList.ItemsSource = products;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при завантаженні товарів: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddProduct_Click(object sender, RoutedEventArgs e)
        {
            string name = NewProductName.Text.Trim();
            string description = NewProductDescription.Text.Trim();
            if (!decimal.TryParse(NewProductPrice.Text, out decimal price))
            {
                MessageBox.Show("Введіть коректну ціну.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            string brand = NewProductBrand.Text.Trim();
            int? categoryId = ProductCategory.SelectedValue as int?;
            int? subcategoryId = ProductSubcategory.SelectedValue as int?;

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(brand) || categoryId == null)
            {
                MessageBox.Show("Заповніть усі обов'язкові поля (назва, бренд, категорія).", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    // Проверяем, существует ли subcategoryId в таблице categories, если он выбран
                    if (subcategoryId.HasValue)
                    {
                        using (var checkCommand = new NpgsqlCommand("SELECT COUNT(*) FROM categories WHERE categoryid = @subcategoryid", connection))
                        {
                            checkCommand.Parameters.AddWithValue("subcategoryid", subcategoryId.Value);
                            long count = (long)checkCommand.ExecuteScalar();
                            if (count == 0)
                            {
                                MessageBox.Show("Вибрана підкатегорія не існує в базі даних. Будь ласка, оберіть іншу підкатегорію або залиште поле порожнім.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                        }

                        // Дополнительно проверяем, принадлежит ли subcategoryId к выбранной категории
                        using (var parentCheckCommand = new NpgsqlCommand("SELECT COUNT(*) FROM categories WHERE categoryid = @subcategoryid AND parentcategoryid = @categoryid", connection))
                        {
                            parentCheckCommand.Parameters.AddWithValue("subcategoryid", subcategoryId.Value);
                            parentCheckCommand.Parameters.AddWithValue("categoryid", categoryId.Value);
                            long parentCount = (long)parentCheckCommand.ExecuteScalar();
                            if (parentCount == 0)
                            {
                                MessageBox.Show("Вибрана підкатегорія не належить до обраної категорії. Будь ласка, перевірте вибір.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                        }
                    }

                    // Проверяем, существует ли categoryId в таблице categories
                    using (var categoryCheckCommand = new NpgsqlCommand("SELECT COUNT(*) FROM categories WHERE categoryid = @categoryid", connection))
                    {
                        categoryCheckCommand.Parameters.AddWithValue("categoryid", categoryId.Value);
                        long categoryCount = (long)categoryCheckCommand.ExecuteScalar();
                        if (categoryCount == 0)
                        {
                            MessageBox.Show("Вибрана категорія не існує в базі даних. Будь ласка, оберіть іншу категорію.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }

                    // Загружаем изображение на сервер или используем локальный путь
                    string imageUrl = string.IsNullOrWhiteSpace(selectedImagePath) ? "https://via.placeholder.com/150" : UploadImageToServer(selectedImagePath);

                    // Вставляем продукт
                    using (var command = new NpgsqlCommand("INSERT INTO products (name, description, price, brand, categoryid, subcategoryid, sellerid, image_url) VALUES (@name, @description, @price, @brand, @categoryid, @subcategoryid, @sellerid, @imageurl)", connection))
                    {
                        command.Parameters.AddWithValue("name", name);
                        command.Parameters.AddWithValue("description", description);
                        command.Parameters.AddWithValue("price", price);
                        command.Parameters.AddWithValue("brand", brand);
                        command.Parameters.AddWithValue("categoryid", categoryId);
                        command.Parameters.AddWithValue("subcategoryid", subcategoryId ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("sellerid", sellerId);
                        command.Parameters.AddWithValue("imageurl", imageUrl);
                        command.ExecuteNonQuery();
                    }
                }
                selectedImagePath = null;
                NewProductImagePath.Text = "Шлях до зображення";
                LoadProducts();
                MessageBox.Show("Товар успішно додано!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при додаванні товару: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string UploadImageToServer(string localPath)
        {
            try
            {
                // Здесь предполагается, что у вас есть сервер для загрузки изображений
                // Пример: загружаем изображение на сервер и возвращаем URL
                string serverPath = "http://yourserver.com/images/"; // Замените на реальный URL вашего сервера
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(localPath);
                string fullServerPath = serverPath + fileName;

                // Копируем файл на сервер (нужно реализовать серверную часть)
                // Это пример, замените на реальную логику загрузки (например, через FTP или API)
                File.Copy(localPath, @"C:\YourServerPath\" + fileName, true); // Локальный пример, замените на серверную загрузку

                return fullServerPath;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при завантаженні зображення: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return "https://via.placeholder.com/150"; // Возвращаем заглушку в случае ошибки
            }
        }

        private void EditProduct_Click(object sender, RoutedEventArgs e)
        {
            int productId = (int)((Button)sender).Tag;
            var product = (DbProduct)ProductsList.Items.Cast<DbProduct>().First(p => p.ProductId == productId);

            ProductCategory.SelectedValue = ProductCategory.Items.Cast<Category>().First(c => c.Name == product.CategoryName).CategoryId;
            LoadSubcategories();

            NewProductName.Text = product.Name;
            NewProductDescription.Text = product.Description;
            NewProductPrice.Text = product.Price.ToString();
            NewProductBrand.Text = product.Brand;
            selectedImagePath = product.ImageUrl == "https://via.placeholder.com/150" ? null : product.ImageUrl;
            NewProductImagePath.Text = selectedImagePath ?? "Шлях до зображення";

            if (product.SubcategoryName != "Не вказано")
            {
                ProductSubcategory.SelectedValue = ProductSubcategory.Items.Cast<Subcategory>().FirstOrDefault(sc => sc.Name == product.SubcategoryName)?.SubcategoryId;
            }
            else
            {
                ProductSubcategory.SelectedValue = null;
            }

            Window editWindow = new Window
            {
                Title = "Редагувати товар",
                Width = 400,
                Height = 650,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            StackPanel panel = new StackPanel { Margin = new Thickness(10) };
            panel.Children.Add(new TextBlock { Text = "Назва:", Margin = new Thickness(0, 0, 0, 5) });
            TextBox nameBox = new TextBox { Text = product.Name, Margin = new Thickness(0, 0, 0, 10) };
            panel.Children.Add(nameBox);

            panel.Children.Add(new TextBlock { Text = "Опис:", Margin = new Thickness(0, 0, 0, 5) });
            TextBox descBox = new TextBox { Text = product.Description, AcceptsReturn = true, Height = 100, Margin = new Thickness(0, 0, 0, 10) };
            panel.Children.Add(descBox);

            panel.Children.Add(new TextBlock { Text = "Ціна:", Margin = new Thickness(0, 0, 0, 5) });
            TextBox priceBox = new TextBox { Text = product.Price.ToString(), Margin = new Thickness(0, 0, 0, 10) };
            panel.Children.Add(priceBox);

            panel.Children.Add(new TextBlock { Text = "Бренд:", Margin = new Thickness(0, 0, 0, 5) });
            TextBox brandBox = new TextBox { Text = product.Brand, Margin = new Thickness(0, 0, 0, 10) };
            panel.Children.Add(brandBox);

            panel.Children.Add(new TextBlock { Text = "Категорія:", Margin = new Thickness(0, 0, 0, 5) });
            ComboBox categoryBox = new ComboBox { ItemsSource = ProductCategory.ItemsSource, DisplayMemberPath = "Name", SelectedValuePath = "CategoryId", SelectedValue = ProductCategory.SelectedValue, Margin = new Thickness(0, 0, 0, 10) };
            panel.Children.Add(categoryBox);

            panel.Children.Add(new TextBlock { Text = "Підкатегорія:", Margin = new Thickness(0, 0, 0, 5) });
            ComboBox subcategoryBox = new ComboBox { ItemsSource = ProductSubcategory.ItemsSource, DisplayMemberPath = "Name", SelectedValuePath = "SubcategoryId", SelectedValue = ProductSubcategory.SelectedValue, Margin = new Thickness(0, 0, 0, 10) };
            panel.Children.Add(subcategoryBox);

            categoryBox.SelectionChanged += (s, ev) =>
            {
                try
                {
                    int? selectedCategoryId = categoryBox.SelectedValue as int?;
                    if (selectedCategoryId == null)
                    {
                        subcategoryBox.ItemsSource = null;
                        return;
                    }

                    using (var connection = new NpgsqlConnection(connectionString))
                    {
                        connection.Open();
                        using (var command = new NpgsqlCommand("SELECT categoryid, name FROM categories WHERE parentcategoryid = @categoryid", connection))
                        {
                            command.Parameters.AddWithValue("categoryid", selectedCategoryId.Value);
                            using (var reader = command.ExecuteReader())
                            {
                                var subcategories = new List<Subcategory>();
                                while (reader.Read())
                                {
                                    subcategories.Add(new Subcategory
                                    {
                                        SubcategoryId = reader.GetInt32(0),
                                        Name = reader.GetString(1),
                                        CategoryId = selectedCategoryId.Value
                                    });
                                }
                                subcategoryBox.ItemsSource = subcategories;
                                subcategoryBox.DisplayMemberPath = "Name";
                                subcategoryBox.SelectedValuePath = "SubcategoryId";
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка при завантаженні підкатегорій: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            panel.Children.Add(new TextBlock { Text = "Зображення:", Margin = new Thickness(0, 0, 0, 5) });
            TextBox imagePathBox = new TextBox { Text = NewProductImagePath.Text, IsReadOnly = true, Margin = new Thickness(0, 0, 0, 5) };
            panel.Children.Add(imagePathBox);
            Button selectImageButton = new Button { Content = "Вибрати зображення", Margin = new Thickness(0, 0, 0, 10) };
            selectImageButton.Click += (s, ev) =>
            {
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|All files (*.*)|*.*",
                    Title = "Виберіть зображення товару"
                };
                if (openFileDialog.ShowDialog() == true)
                {
                    selectedImagePath = openFileDialog.FileName;
                    imagePathBox.Text = selectedImagePath;
                }
            };
            panel.Children.Add(selectImageButton);

            Button saveButton = new Button { Content = "Зберегти", Width = 100, Margin = new Thickness(0, 10, 0, 0) };
            saveButton.Click += (s, ev) =>
            {
                try
                {
                    using (var connection = new NpgsqlConnection(connectionString))
                    {
                        connection.Open();
                        using (var command = new NpgsqlCommand("UPDATE products SET name = @name, description = @description, price = @price, brand = @brand, categoryid = @categoryid, subcategoryid = @subcategoryid, image_url = @imageurl WHERE productid = @productid AND sellerid = @sellerid", connection))
                        {
                            command.Parameters.AddWithValue("name", nameBox.Text.Trim());
                            command.Parameters.AddWithValue("description", descBox.Text.Trim());
                            command.Parameters.AddWithValue("price", decimal.Parse(priceBox.Text));
                            command.Parameters.AddWithValue("brand", brandBox.Text.Trim());
                            command.Parameters.AddWithValue("categoryid", (int)categoryBox.SelectedValue);
                            command.Parameters.AddWithValue("subcategoryid", subcategoryBox.SelectedValue ?? (object)DBNull.Value);
                            command.Parameters.AddWithValue("imageurl", string.IsNullOrWhiteSpace(selectedImagePath) ? product.ImageUrl : UploadImageToServer(selectedImagePath));
                            command.Parameters.AddWithValue("productid", productId);
                            command.Parameters.AddWithValue("sellerid", sellerId);
                            command.ExecuteNonQuery();
                        }
                    }
                    selectedImagePath = null;
                    NewProductImagePath.Text = "Шлях до зображення";
                    LoadProducts();
                    MessageBox.Show("Товар успішно оновлено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                    editWindow.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка при редагуванні товару: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            panel.Children.Add(saveButton);

            editWindow.Content = panel;
            editWindow.ShowDialog();
        }

        private void DeleteProduct_Click(object sender, RoutedEventArgs e)
        {
            int productId = (int)((Button)sender).Tag;
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("DELETE FROM products WHERE productid = @productid AND sellerid = @sellerid", connection))
                    {
                        command.Parameters.AddWithValue("productid", productId);
                        command.Parameters.AddWithValue("sellerid", sellerId);
                        command.ExecuteNonQuery();
                    }
                }
                LoadProducts();
                MessageBox.Show("Товар успішно видалено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при видаленні товару: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadOrders()
        {
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand(
                        "SELECT o.orderid, p.name AS productname, o.quantity, o.totalprice, o.orderdate, o.status, " +
                        "o.contact_last_name, o.contact_first_name, o.contact_middle_name, o.contact_phone, o.shipping_region, pp.address " +
                        "FROM orders o " +
                        "JOIN products p ON o.productid = p.productid " +
                        "JOIN pickup_points pp ON o.pickup_point_id = pp.pickup_point_id " +
                        "WHERE o.sellerid = @sellerid", connection))
                    {
                        command.Parameters.AddWithValue("sellerid", sellerId);
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
                                    TotalPrice = (decimal)reader.GetDouble(3),
                                    OrderDate = reader.GetDateTime(4),
                                    Status = reader.GetString(5),
                                    ContactLastName = reader.IsDBNull(6) ? "Не вказано" : reader.GetString(6),
                                    ContactFirstName = reader.IsDBNull(7) ? "Не вказано" : reader.GetString(7),
                                    ContactMiddleName = reader.IsDBNull(8) ? "Не вказано" : reader.GetString(8),
                                    ContactPhone = reader.IsDBNull(9) ? "Не вказано" : reader.GetString(9),
                                    ShippingRegion = reader.IsDBNull(10) ? "Не вказано" : reader.GetString(10),
                                    PickupPointAddress = reader.IsDBNull(11) ? "Не вказано" : reader.GetString(11),
                                    ProductId = reader.GetInt32(0) // Используем OrderId как ProductId (можно уточнить логику)
                                });
                            }
                            OrdersList.ItemsSource = orders;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при завантаженні замовлень: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    using (var command = new NpgsqlCommand("UPDATE orders SET status = @status WHERE orderid = @orderid AND sellerid = @sellerid", connection))
                    {
                        command.Parameters.AddWithValue("status", status);
                        command.Parameters.AddWithValue("orderid", orderId);
                        command.Parameters.AddWithValue("sellerid", sellerId);
                        command.ExecuteNonQuery();
                    }
                }
                LoadOrders();
                LoadFinancials();
                MessageBox.Show("Статус замовлення оновлено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при оновленні статусу замовлення: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SendOrder_Click(object sender, RoutedEventArgs e)
        {
            int orderId = (int)((Button)sender).Tag;
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("UPDATE orders SET status = 'Відправлено' WHERE orderid = @orderid AND sellerid = @sellerid", connection))
                    {
                        command.Parameters.AddWithValue("orderid", orderId);
                        command.Parameters.AddWithValue("sellerid", sellerId);
                        int rowsAffected = command.ExecuteNonQuery();
                        if (rowsAffected > 0)
                        {
                            MessageBox.Show("Замовлення успішно відправлено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                            LoadOrders();
                            LoadFinancials();
                        }
                        else
                        {
                            MessageBox.Show("Не вдалося оновити статус замовления.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при відправці замовлення: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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

                    using (var command = new NpgsqlCommand("SELECT feevalue FROM sellerfees WHERE sellerid = @sellerid AND feetype = 'Percentage'", connection))
                    {
                        command.Parameters.AddWithValue("sellerid", sellerId);
                        var result = command.ExecuteScalar();
                        if (result != null)
                        {
                            feePercentage = (decimal)result / 100;
                        }
                    }

                    using (var command = new NpgsqlCommand("SELECT o.orderid, p.name AS productname, o.totalprice, o.orderdate FROM orders o JOIN products p ON o.productid = p.productid WHERE o.sellerid = @sellerid AND o.status = 'Доставлено'", connection))
                    {
                        command.Parameters.AddWithValue("sellerid", sellerId);
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                decimal totalPrice = (decimal)reader.GetDouble(2);
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
                MessageBox.Show($"Помилка при завантаженні фінансової інформації: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadStoreProfile()
        {
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("SELECT storename, description, contactinfo FROM sellerprofiles WHERE sellerid = @sellerid", connection))
                    {
                        command.Parameters.AddWithValue("sellerid", sellerId);
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
                MessageBox.Show($"Помилка при завантаженні профілю магазину: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveProfile_Click(object sender, RoutedEventArgs e)
        {
            string storeName = StoreNameTextBox.Text.Trim();
            string description = StoreDescriptionTextBox.Text.Trim();
            string contactInfo = StoreContactInfoTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(storeName))
            {
                MessageBox.Show("Назва магазину не може бути порожньою.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("INSERT INTO sellerprofiles (sellerid, storename, description, contactinfo) VALUES (@sellerid, @storename, @description, @contactinfo) ON CONFLICT (sellerid) DO UPDATE SET storename = @storename, description = @description, contactinfo = @contactinfo", connection))
                    {
                        command.Parameters.AddWithValue("sellerid", sellerId);
                        command.Parameters.AddWithValue("storename", storeName);
                        command.Parameters.AddWithValue("description", description);
                        command.Parameters.AddWithValue("contactinfo", contactInfo);
                        command.ExecuteNonQuery();
                    }
                }
                MessageBox.Show("Профіль магазину успішно збережено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при збереженні профілю магазину: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}