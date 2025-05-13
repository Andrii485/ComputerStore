using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Npgsql;
using System.Configuration;
using BCrypt.Net;
using Microsoft.Win32;
using ElmirClone.Models;

namespace ElmirClone 
{
    public partial class AdminWindow : Window
    {
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
        private string selectedImagePath;
        private List<Category> allCategories;

        public AdminWindow()
        {
            InitializeComponent();
            connectionString = ConfigurationManager.ConnectionStrings["ElitePCConnection"]?.ConnectionString;
            if (string.IsNullOrEmpty(connectionString))
            {
                MessageBox.Show("Рядок підключення до бази даних не знайдено.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            allCategories = new List<Category>();
            this.Loaded += AdminWindow_Loaded;
        }

        private void AdminWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadUsers();
            LoadCategories();
            LoadProducts();
            LoadPaymentMethods();
            LoadCourierServices();
            LoadPickupPoints();
            LoadRegionsForPickupPoints();
        }

        private void LoadRegionsForPickupPoints()
        {
            if (NewPickupPointRegion == null)
            {
                MessageBox.Show("Елемент NewPickupPointRegion не знайдено у розмітці.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            NewPickupPointRegion.ItemsSource = regions;
            if (regions.Any() && NewPickupPointRegion.Items.Count > 0)
            {
                NewPickupPointRegion.SelectedIndex = 0;
            }
        }

        private void ShowUsersPanel_Click(object sender, RoutedEventArgs e)
        {
            if (UsersPanel != null) UsersPanel.Visibility = Visibility.Visible;
            if (CatalogPanel != null) CatalogPanel.Visibility = Visibility.Collapsed;
            if (FinancePanel != null) FinancePanel.Visibility = Visibility.Collapsed;
            if (LogisticsPanel != null) LogisticsPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowCatalogPanel_Click(object sender, RoutedEventArgs e)
        {
            if (UsersPanel != null) UsersPanel.Visibility = Visibility.Collapsed;
            if (CatalogPanel != null) CatalogPanel.Visibility = Visibility.Visible;
            if (FinancePanel != null) FinancePanel.Visibility = Visibility.Collapsed;
            if (LogisticsPanel != null) LogisticsPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowFinancePanel_Click(object sender, RoutedEventArgs e)
        {
            if (UsersPanel != null) UsersPanel.Visibility = Visibility.Collapsed;
            if (CatalogPanel != null) CatalogPanel.Visibility = Visibility.Collapsed;
            if (FinancePanel != null) FinancePanel.Visibility = Visibility.Visible;
            if (LogisticsPanel != null) LogisticsPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowLogisticsPanel_Click(object sender, RoutedEventArgs e)
        {
            if (UsersPanel != null) UsersPanel.Visibility = Visibility.Collapsed;
            if (CatalogPanel != null) CatalogPanel.Visibility = Visibility.Collapsed;
            if (FinancePanel != null) FinancePanel.Visibility = Visibility.Collapsed;
            if (LogisticsPanel != null) LogisticsPanel.Visibility = Visibility.Visible;
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            LoginWindow loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }

        private void LoadUsers()
        {
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("SELECT uc.userid, uc.username, ud.email, uc.role, uc.isblocked FROM usercredentials uc JOIN userdetails ud ON uc.userid = ud.userid WHERE uc.role != 'Admin'", connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            var users = new List<User>();
                            while (reader.Read())
                            {
                                string role = reader.GetString(3);
                                users.Add(new User
                                {
                                    UserId = reader.GetInt32(0),
                                    Username = reader.GetString(1),
                                    Email = reader.GetString(2),
                                    Role = role == "Buyer" ? "Покупець" : role == "Seller" ? "Продавець" : role,
                                    IsBlocked = reader.GetBoolean(4)
                                });
                            }
                            if (UsersList != null)
                            {
                                UsersList.ItemsSource = users;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час завантаження користувачів: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SearchUserByEmail_Click(object sender, RoutedEventArgs e)
        {
            if (SearchUserEmail == null)
            {
                MessageBox.Show("Елемент SearchUserEmail не знайдено у розмітці.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            string email = SearchUserEmail.Text?.Trim();
            if (string.IsNullOrWhiteSpace(email))
            {
                MessageBox.Show("Введіть електронну пошту для пошуку.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                LoadUsers();
                return;
            }

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("SELECT uc.userid, uc.username, ud.email, uc.role, uc.isblocked FROM usercredentials uc JOIN userdetails ud ON uc.userid = ud.userid WHERE uc.role != 'Admin' AND ud.email ILIKE @email", connection))
                    {
                        command.Parameters.AddWithValue("email", $"%{email}%");
                        using (var reader = command.ExecuteReader())
                        {
                            var users = new List<User>();
                            while (reader.Read())
                            {
                                string role = reader.GetString(3);
                                users.Add(new User
                                {
                                    UserId = reader.GetInt32(0),
                                    Username = reader.GetString(1),
                                    Email = reader.GetString(2),
                                    Role = role == "Buyer" ? "Покупець" : role == "Seller" ? "Продавець" : role,
                                    IsBlocked = reader.GetBoolean(4)
                                });
                            }
                            if (UsersList != null)
                            {
                                UsersList.ItemsSource = users;
                            }
                            if (!users.Any())
                            {
                                MessageBox.Show("Користувача з такою електронною поштою не знайдено.", "Інформація", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час пошуку користувача: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RegisterUser_Click(object sender, RoutedEventArgs e)
        {
            if (NewUserUsername == null || NewUserEmail == null || NewUserPassword == null || NewUserRole == null)
            {
                MessageBox.Show("Одна або кілька полів для введення даних користувача не знайдено у розмітці.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            string username = NewUserUsername.Text?.Trim();
            string email = NewUserEmail.Text?.Trim();
            string password = NewUserPassword.Password;
            string roleDisplay = (NewUserRole.SelectedItem as ComboBoxItem)?.Content?.ToString();
            string role = roleDisplay == "Покупець" ? "Buyer" : "Seller";

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(role))
            {
                MessageBox.Show("Заповніть усі поля.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!(email.EndsWith("@gmail.com") || email.EndsWith("@outlook.com")))
            {
                MessageBox.Show("Електронна пошта повинна закінчуватися на @gmail.com або @outlook.com.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var checkUsernameCommand = new NpgsqlCommand("SELECT COUNT(*) FROM usercredentials WHERE username = @username", connection))
                    {
                        checkUsernameCommand.Parameters.AddWithValue("username", username);
                        long usernameCount = (long)checkUsernameCommand.ExecuteScalar();
                        if (usernameCount > 0)
                        {
                            MessageBox.Show("Користувач з таким ім'ям вже існує.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }

                    using (var checkEmailCommand = new NpgsqlCommand("SELECT COUNT(*) FROM userdetails WHERE email = @email", connection))
                    {
                        checkEmailCommand.Parameters.AddWithValue("email", email);
                        long emailCount = (long)checkEmailCommand.ExecuteScalar();
                        if (emailCount > 0)
                        {
                            MessageBox.Show("Користувач з такою електронною поштою вже є в базі даних.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }

                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            int userId;
                            using (var command = new NpgsqlCommand("INSERT INTO userdetails (firstname, email) VALUES (@firstName, @email) RETURNING userid", connection))
                            {
                                command.Parameters.AddWithValue("firstName", username);
                                command.Parameters.AddWithValue("email", email);
                                command.Transaction = transaction;
                                userId = (int)command.ExecuteScalar();
                            }

                            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
                            using (var command = new NpgsqlCommand("INSERT INTO usercredentials (userid, username, password, role) VALUES (@userId, @username, @password, @role)", connection))
                            {
                                command.Parameters.AddWithValue("userId", userId);
                                command.Parameters.AddWithValue("username", username);
                                command.Parameters.AddWithValue("password", hashedPassword);
                                command.Parameters.AddWithValue("role", role);
                                command.Transaction = transaction;
                                command.ExecuteNonQuery();
                            }

                            if (role == "Seller")
                            {
                                using (var command = new NpgsqlCommand("INSERT INTO sellerprofiles (sellerid, storename, description, contactinfo) VALUES (@sellerId, @storeName, @description, @contactInfo)", connection))
                                {
                                    command.Parameters.AddWithValue("sellerId", userId);
                                    command.Parameters.AddWithValue("storeName", $"Магазин {username}");
                                    command.Parameters.AddWithValue("description", "Ласкаво просимо до мого магазину!");
                                    command.Parameters.AddWithValue("contactInfo", "Не вказано");
                                    command.Transaction = transaction;
                                    command.ExecuteNonQuery();
                                }
                            }

                            transaction.Commit();
                            MessageBox.Show("Користувача успішно зареєстровано!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                            LoadUsers();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            MessageBox.Show($"Помилка під час реєстрації: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Виникла помилка: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BlockUser_Click(object sender, RoutedEventArgs e)
        {
            int userId = (int)((Button)sender).Tag;
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("UPDATE usercredentials SET isblocked = NOT isblocked WHERE userid = @userId", connection))
                    {
                        command.Parameters.AddWithValue("userId", userId);
                        command.ExecuteNonQuery();
                    }
                }
                LoadUsers();
                MessageBox.Show("Статус блокування користувача змінено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час блокування користувача: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteUser_Click(object sender, RoutedEventArgs e)
        {
            int userId = (int)((Button)sender).Tag;
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            string role = "";
                            using (var command = new NpgsqlCommand("SELECT role FROM usercredentials WHERE userid = @userId", connection))
                            {
                                command.Parameters.AddWithValue("userId", userId);
                                command.Transaction = transaction;
                                var result = command.ExecuteScalar();
                                if (result != null)
                                {
                                    role = result.ToString();
                                }
                            }

                            if (role == "Seller")
                            {
                                using (var command = new NpgsqlCommand("DELETE FROM sellerfees WHERE sellerid = @userId", connection))
                                {
                                    command.Parameters.AddWithValue("userId", userId);
                                    command.Transaction = transaction;
                                    command.ExecuteNonQuery();
                                }
                            }

                            using (var command = new NpgsqlCommand("DELETE FROM sellerprofiles WHERE sellerid = @userId", connection))
                            {
                                command.Parameters.AddWithValue("userId", userId);
                                command.Transaction = transaction;
                                command.ExecuteNonQuery();
                            }

                            using (var command = new NpgsqlCommand("DELETE FROM usercredentials WHERE userid = @userId", connection))
                            {
                                command.Parameters.AddWithValue("userId", userId);
                                command.Transaction = transaction;
                                command.ExecuteNonQuery();
                            }

                            using (var command = new NpgsqlCommand("DELETE FROM userdetails WHERE userid = @userId", connection))
                            {
                                command.Parameters.AddWithValue("userId", userId);
                                command.Transaction = transaction;
                                command.ExecuteNonQuery();
                            }

                            transaction.Commit();
                            MessageBox.Show("Користувача видалено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                            LoadUsers();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            MessageBox.Show($"Помилка під час видалення: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Виникла помилка: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadCategories()
        {
            try
            {
                var categories = new List<Category>();

                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("SELECT c1.categoryid, c1.name, c2.name AS parentname, c1.image_url, c1.parentcategoryid FROM categories c1 LEFT JOIN categories c2 ON c1.parentcategoryid = c2.categoryid", connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var category = new Category
                                {
                                    CategoryId = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    ParentCategoryName = reader.IsDBNull(2) ? "Немає" : reader.GetString(2),
                                    ImageUrl = reader.IsDBNull(3) ? null : reader.GetString(3),
                                    ParentCategoryId = reader.IsDBNull(4) ? null : (int?)reader.GetInt32(4),
                                    Products = new List<ProductDetails>(),
                                    Subcategories = new List<Category>()
                                };
                                categories.Add(category);
                            }
                        }
                    }
                }

                foreach (var category in categories)
                {
                    using (var connection = new NpgsqlConnection(connectionString))
                    {
                        connection.Open();
                        using (var productCommand = new NpgsqlCommand(
                            "SELECT p.productid, p.name, p.description, p.price, p.brand, c.name AS categoryname, sc.name AS subcategoryname, p.image_url, p.ishidden " +
                            "FROM products p " +
                            "JOIN categories c ON p.categoryid = c.categoryid " +
                            "LEFT JOIN categories sc ON p.subcategoryid = sc.categoryid " +
                            "WHERE p.categoryid = @categoryId OR p.subcategoryid = @categoryId", connection))
                        {
                            productCommand.Parameters.AddWithValue("categoryId", category.CategoryId);
                            using (var productReader = productCommand.ExecuteReader())
                            {
                                while (productReader.Read())
                                {
                                    ((List<ProductDetails>)category.Products).Add(new ProductDetails
                                    {
                                        ProductId = productReader.GetInt32(0),
                                        Name = productReader.GetString(1),
                                        Description = productReader.IsDBNull(2) ? "" : productReader.GetString(2),
                                        Price = productReader.GetDecimal(3),
                                        Brand = productReader.GetString(4),
                                        CategoryName = productReader.GetString(5),
                                        SubcategoryName = productReader.IsDBNull(6) ? "Не вказано" : productReader.GetString(6),
                                        ImageUrl = productReader.IsDBNull(7) ? "https://via.placeholder.com/150" : productReader.GetString(7),
                                        IsHidden = productReader.GetBoolean(8)
                                    });
                                }
                            }
                        }
                    }
                }

                allCategories = categories;

                var categoryIds = new HashSet<int>();
                foreach (var category in allCategories)
                {
                    var currentCategory = category;
                    var visited = new HashSet<int>();
                    while (currentCategory.ParentCategoryId.HasValue)
                    {
                        if (visited.Contains(currentCategory.ParentCategoryId.Value))
                        {
                            MessageBox.Show($"Виявлено циклічну залежність у категорії {currentCategory.Name} (ID: {currentCategory.CategoryId}). Перевірте parentcategoryid у базі даних.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                        visited.Add(currentCategory.ParentCategoryId.Value);
                        currentCategory = allCategories.FirstOrDefault(c => c.CategoryId == currentCategory.ParentCategoryId);
                        if (currentCategory == null)
                        {
                            break;
                        }
                    }

                    if (!categoryIds.Add(category.CategoryId))
                    {
                        MessageBox.Show($"Дублювання CategoryId ({category.CategoryId}) у базі даних. Перевірте таблицю categories.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                var rootCategories = new List<Category>();
                foreach (var category in allCategories)
                {
                    if (category.ParentCategoryId == null)
                    {
                        rootCategories.Add(category);
                    }
                    else
                    {
                        var parent = allCategories.FirstOrDefault(c => c.CategoryId == category.ParentCategoryId);
                        if (parent != null)
                        {
                            parent.Subcategories.Add(category);
                        }
                    }
                }

                if (CategoriesTree != null)
                {
                    CategoriesTree.ItemsSource = rootCategories;
                }
                else
                {
                    MessageBox.Show("CategoriesTree не ініціалізований. Перевірте XAML-розмітку.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                if (ParentCategory != null)
                {
                    ParentCategory.Items.Clear();
                    ParentCategory.Items.Add(new ComboBoxItem { Content = "Немає", Tag = null });
                    foreach (var category in rootCategories)
                    {
                        ParentCategory.Items.Add(new ComboBoxItem { Content = category.Name, Tag = category.CategoryId });
                    }
                    if (ParentCategory.Items.Count > 0)
                    {
                        ParentCategory.SelectedIndex = 0;
                    }
                }
                else
                {
                    MessageBox.Show("ParentCategory не ініціалізований. Перевірте XAML-розмітку.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час завантаження категорій: {ex.Message}\nПеревірте підключення до бази даних або структуру таблиці categories.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                allCategories = new List<Category>();
                if (CategoriesTree != null)
                {
                    CategoriesTree.ItemsSource = allCategories;
                }
            }
        }

        private void SearchCategoryName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchCategoryName == null || CategoriesTree == null || allCategories == null)
            {
                MessageBox.Show("Елемент SearchCategoryName або CategoriesTree не знайдено у розмітці.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            string searchText = SearchCategoryName.Text?.Trim().ToLower();
            if (string.IsNullOrWhiteSpace(searchText) || searchText == "пошук за назвою категорії")
            {
                CategoriesTree.ItemsSource = allCategories.Where(c => c.ParentCategoryId == null).ToList();
                return;
            }

            var filteredCategories = new List<Category>();
            var rootCategories = new List<Category>();

            foreach (var category in allCategories)
            {
                if (category.Name.ToLower().Contains(searchText))
                {
                    var copyCategory = new Category
                    {
                        CategoryId = category.CategoryId,
                        Name = category.Name,
                        ParentCategoryName = category.ParentCategoryName,
                        ImageUrl = category.ImageUrl,
                        ParentCategoryId = category.ParentCategoryId,
                        Products = category.Products,
                        Subcategories = new List<Category>()
                    };
                    filteredCategories.Add(copyCategory);
                }
            }

            foreach (var category in filteredCategories)
            {
                if (category.ParentCategoryId == null)
                {
                    rootCategories.Add(category);
                }
                else
                {
                    var parent = filteredCategories.FirstOrDefault(c => c.CategoryId == category.ParentCategoryId);
                    if (parent != null)
                    {
                        parent.Subcategories.Add(category);
                    }
                }
            }

            CategoriesTree.ItemsSource = rootCategories;
        }

        private void ClearCategorySearch_Click(object sender, RoutedEventArgs e)
        {
            if (SearchCategoryName == null || CategoriesTree == null || allCategories == null)
            {
                MessageBox.Show("Елемент SearchCategoryName або CategoriesTree не знайдено у розмітці.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            SearchCategoryName.Text = "Пошук за назвою категорії";
            CategoriesTree.ItemsSource = allCategories.Where(c => c.ParentCategoryId == null).ToList();
        }

        private void LoadProducts()
        {
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand(
                        "SELECT p.productid, p.name, p.description, p.price, p.brand, c.name AS categoryname, sc.name AS subcategoryname, p.image_url, p.ishidden " +
                        "FROM products p " +
                        "JOIN categories c ON p.categoryid = c.categoryid " +
                        "LEFT JOIN categories sc ON p.subcategoryid = sc.categoryid", connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            var products = new List<ProductDetails>();
                            while (reader.Read())
                            {
                                products.Add(new ProductDetails
                                {
                                    ProductId = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    Price = reader.GetDecimal(3),
                                    Brand = reader.GetString(4),
                                    CategoryName = reader.GetString(5),
                                    SubcategoryName = reader.IsDBNull(6) ? "Не вказано" : reader.GetString(6),
                                    ImageUrl = reader.IsDBNull(7) ? "https://via.placeholder.com/150" : reader.GetString(7),
                                    IsHidden = reader.GetBoolean(8)
                                });
                            }
                            if (ProductsList != null)
                            {
                                ProductsList.ItemsSource = products;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час завантаження товарів: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SearchProductById_Click(object sender, RoutedEventArgs e)
        {
            if (SearchProductId == null)
            {
                MessageBox.Show("Елемент SearchProductId не знайдено у розмітці.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            string productIdText = SearchProductId.Text?.Trim();
            if (string.IsNullOrWhiteSpace(productIdText) || !int.TryParse(productIdText, out int productId))
            {
                MessageBox.Show("Введіть коректний ID товару (числовий формат).", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                LoadProducts();
                return;
            }

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand(
                        "SELECT p.productid, p.name, p.description, p.price, p.brand, c.name AS categoryname, sc.name AS subcategoryname, p.image_url, p.ishidden " +
                        "FROM products p " +
                        "JOIN categories c ON p.categoryid = c.categoryid " +
                        "LEFT JOIN categories sc ON p.subcategoryid = sc.categoryid " +
                        "WHERE p.productid = @productId", connection))
                    {
                        command.Parameters.AddWithValue("productId", productId);
                        using (var reader = command.ExecuteReader())
                        {
                            var products = new List<ProductDetails>();
                            while (reader.Read())
                            {
                                products.Add(new ProductDetails
                                {
                                    ProductId = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    Price = reader.GetDecimal(3),
                                    Brand = reader.GetString(4),
                                    CategoryName = reader.GetString(5),
                                    SubcategoryName = reader.IsDBNull(6) ? "Не вказано" : reader.GetString(6),
                                    ImageUrl = reader.IsDBNull(7) ? "https://via.placeholder.com/150" : reader.GetString(7),
                                    IsHidden = reader.GetBoolean(8)
                                });
                            }
                            if (ProductsList != null)
                            {
                                ProductsList.ItemsSource = products;
                            }
                            if (!products.Any())
                            {
                                MessageBox.Show("Товар з таким ID не знайдено.", "Інформація", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час пошуку товару: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ToggleProductVisibility_Click(object sender, RoutedEventArgs e)
        {
            int productId = (int)((Button)sender).Tag;
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("UPDATE products SET ishidden = NOT ishidden WHERE productid = @productId", connection))
                    {
                        command.Parameters.AddWithValue("productId", productId);
                        command.ExecuteNonQuery();
                    }
                }
                LoadProducts();
                LoadCategories();
                MessageBox.Show("Статус видимості товару змінено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час зміни статусу товару: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectImage_Click(object sender, RoutedEventArgs e)
        {
            if (ParentCategory == null || ImagePathTextBox == null)
            {
                MessageBox.Show("Елемент ParentCategory або ImagePathTextBox не знайдено у розмітці.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            int? parentCategoryId = (ParentCategory.SelectedItem as ComboBoxItem)?.Tag as int?;
            if (!parentCategoryId.HasValue)
            {
                MessageBox.Show("Додавання зображення можливе лише для підкатегорій. Оберіть батьківську категорію.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Файли зображень (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png|Усі файли (*.*)|*.*",
                Title = "Оберіть зображення для підкатегорії"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                selectedImagePath = openFileDialog.FileName;
                ImagePathTextBox.Text = selectedImagePath;
            }
        }

        private void AddCategory_Click(object sender, RoutedEventArgs e)
        {
            if (NewCategoryName == null || ParentCategory == null || ImagePathTextBox == null)
            {
                MessageBox.Show("Елемент NewCategoryName, ParentCategory або ImagePathTextBox не знайдено у розмітці.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            string name = NewCategoryName.Text?.Trim();
            int? parentCategoryId = (ParentCategory.SelectedItem as ComboBoxItem)?.Tag as int?;

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Введіть назву категорії.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string imageUrl = null;
            if (parentCategoryId.HasValue)
            {
                if (string.IsNullOrEmpty(selectedImagePath))
                {
                    MessageBox.Show("Будь ласка, оберіть зображення для підкатегорії.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                imageUrl = selectedImagePath;
            }
            else if (!string.IsNullOrEmpty(selectedImagePath))
            {
                MessageBox.Show("Додавання зображення для кореневих категорій не дозволено.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("INSERT INTO categories (name, parentcategoryid, image_url) VALUES (@name, @parentId, @imageUrl)", connection))
                    {
                        command.Parameters.AddWithValue("name", name);
                        command.Parameters.AddWithValue("parentId", parentCategoryId == null ? (object)DBNull.Value : parentCategoryId);
                        command.Parameters.AddWithValue("imageUrl", string.IsNullOrEmpty(imageUrl) ? (object)DBNull.Value : imageUrl);
                        command.ExecuteNonQuery();
                    }
                }
                LoadCategories();
                NewCategoryName.Text = "";
                ImagePathTextBox.Text = "";
                selectedImagePath = null;
                MessageBox.Show("Категорію успішно додано!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час додавання категорії: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditCategory_Click(object sender, RoutedEventArgs e)
        {
            int categoryId = (int)((Button)sender).Tag;

            Category categoryToEdit = null;
            if (allCategories != null)
            {
                foreach (Category category in allCategories)
                {
                    if (category.CategoryId == categoryId)
                    {
                        categoryToEdit = category;
                        break;
                    }
                }
            }

            if (categoryToEdit == null)
            {
                MessageBox.Show("Категорію не знайдено.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string newName = Microsoft.VisualBasic.Interaction.InputBox("Введіть нову назву категорії:", "Редагування категорії", categoryToEdit.Name);
            if (string.IsNullOrWhiteSpace(newName))
            {
                MessageBox.Show("Назва категорії не може бути порожньою.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string newImageUrl = categoryToEdit.ImageUrl;
            bool isSubCategory = categoryToEdit.ParentCategoryName != "Немає";
            if (isSubCategory)
            {
                MessageBoxResult result = MessageBox.Show("Бажаєте змінити зображення підкатегорії?", "Редагування зображення", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    OpenFileDialog openFileDialog = new OpenFileDialog
                    {
                        Filter = "Файли зображень (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png|Усі файли (*.*)|*.*",
                        Title = "Оберіть нове зображення для підкатегорії"
                    };

                    if (openFileDialog.ShowDialog() == true)
                    {
                        newImageUrl = openFileDialog.FileName;
                    }
                    else
                    {
                        return;
                    }
                }
            }
            else if (!string.IsNullOrEmpty(newImageUrl))
            {
                MessageBox.Show("Зображення для кореневих категорій не підтримуються.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("UPDATE categories SET name = @name, image_url = @imageUrl WHERE categoryid = @categoryId", connection))
                    {
                        command.Parameters.AddWithValue("name", newName);
                        command.Parameters.AddWithValue("imageUrl", string.IsNullOrEmpty(newImageUrl) ? (object)DBNull.Value : newImageUrl);
                        command.Parameters.AddWithValue("categoryId", categoryId);
                        command.ExecuteNonQuery();
                    }
                }
                LoadCategories();
                MessageBox.Show("Категорію успішно оновлено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час оновлення категорії: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<int> GetAllSubcategoryIds(int categoryId, List<Category> categories)
        {
            var subcategoryIds = new List<int> { categoryId };
            var subcategories = categories.Where(c => c.ParentCategoryId == categoryId).ToList();

            foreach (var subcategory in subcategories)
            {
                subcategoryIds.AddRange(GetAllSubcategoryIds(subcategory.CategoryId, categories));
            }

            return subcategoryIds;
        }

        private int GetOrCreateNoCategory()
        {
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    int noCategoryId;
                    using (var command = new NpgsqlCommand("SELECT categoryid FROM categories WHERE name = 'Без категорії' AND parentcategoryid IS NULL LIMIT 1", connection))
                    {
                        var result = command.ExecuteScalar();
                        if (result != null)
                        {
                            noCategoryId = (int)result;
                            return noCategoryId;
                        }
                    }

                    using (var command = new NpgsqlCommand("INSERT INTO categories (name, parentcategoryid, image_url) VALUES ('Без категорії', NULL, NULL) RETURNING categoryid", connection))
                    {
                        noCategoryId = (int)command.ExecuteScalar();
                        return noCategoryId;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час створення категорії 'Без категорії': {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return -1;
            }
        }

        private void DeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            int categoryId = (int)((Button)sender).Tag;

            bool isRootCategory;
            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();
                using (var command = new NpgsqlCommand("SELECT parentcategoryid IS NULL FROM categories WHERE categoryid = @categoryId", connection))
                {
                    command.Parameters.AddWithValue("categoryId", categoryId);
                    isRootCategory = (bool)command.ExecuteScalar();
                }
            }

            if (isRootCategory)
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("SELECT COUNT(*) FROM categories WHERE parentcategoryid IS NULL AND categoryid != @categoryId", connection))
                    {
                        command.Parameters.AddWithValue("categoryId", categoryId);
                        long rootCategoryCount = (long)command.ExecuteScalar();
                        if (rootCategoryCount == 0)
                        {
                            MessageBox.Show("Не можна видалити останню кореневу категорію. Додайте іншу кореневу категорію перед видаленням.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }
                }
            }

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            var allSubcategoryIds = GetAllSubcategoryIds(categoryId, allCategories);

                            int noCategoryId = GetOrCreateNoCategory();
                            if (noCategoryId == -1)
                            {
                                transaction.Rollback();
                                return;
                            }

                            long productCount;
                            using (var command = new NpgsqlCommand("SELECT COUNT(*) FROM products WHERE categoryid = ANY(@categoryIds) OR subcategoryid = ANY(@categoryIds)", connection))
                            {
                                command.Parameters.AddWithValue("categoryIds", allSubcategoryIds);
                                command.Transaction = transaction;
                                productCount = (long)command.ExecuteScalar();
                            }

                            if (productCount > 0)
                            {
                                using (var command = new NpgsqlCommand("UPDATE products SET categoryid = @noCategoryId WHERE categoryid = ANY(@categoryIds)", connection))
                                {
                                    command.Parameters.AddWithValue("noCategoryId", noCategoryId);
                                    command.Parameters.AddWithValue("categoryIds", allSubcategoryIds);
                                    command.Transaction = transaction;
                                    command.ExecuteNonQuery();
                                }

                                using (var command = new NpgsqlCommand("UPDATE products SET subcategoryid = NULL WHERE subcategoryid = ANY(@categoryIds)", connection))
                                {
                                    command.Parameters.AddWithValue("categoryIds", allSubcategoryIds);
                                    command.Transaction = transaction;
                                    command.ExecuteNonQuery();
                                }
                            }

                            using (var command = new NpgsqlCommand("DELETE FROM categories WHERE categoryid = ANY(@categoryIds)", connection))
                            {
                                command.Parameters.AddWithValue("categoryIds", allSubcategoryIds);
                                command.Transaction = transaction;
                                command.ExecuteNonQuery();
                            }

                            transaction.Commit();
                            LoadCategories();
                            MessageBox.Show("Категорію та її підкатегорії успішно видалено! Товари переназначені до категорії 'Без категорії'.", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            MessageBox.Show($"Помилка під час видалення категорії: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час видалення категорії: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadPaymentMethods()
        {
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("SELECT methodid, name, is_active FROM payment_methods", connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            var methods = new List<PaymentMethod>();
                            while (reader.Read())
                            {
                                methods.Add(new PaymentMethod
                                {
                                    MethodId = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    IsActive = reader.GetBoolean(2)
                                });
                            }
                            if (PaymentMethodsList != null)
                            {
                                PaymentMethodsList.ItemsSource = methods;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час завантаження способів оплати: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddPaymentMethod_Click(object sender, RoutedEventArgs e)
        {
            if (NewPaymentMethodName == null)
            {
                MessageBox.Show("Елемент NewPaymentMethodName не знайдено у розмітці.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            string name = NewPaymentMethodName.Text?.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Введіть назву способу оплати.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var checkCommand = new NpgsqlCommand("SELECT COUNT(*) FROM payment_methods WHERE name = @name", connection))
                    {
                        checkCommand.Parameters.AddWithValue("name", name);
                        long count = (long)checkCommand.ExecuteScalar();
                        if (count > 0)
                        {
                            MessageBox.Show("Спосіб оплати з такою назвою вже існує.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }

                    using (var command = new NpgsqlCommand("INSERT INTO payment_methods (name, is_active) VALUES (@name, @isActive)", connection))
                    {
                        command.Parameters.AddWithValue("name", name);
                        command.Parameters.AddWithValue("isActive", true);
                        command.ExecuteNonQuery();
                    }
                }
                LoadPaymentMethods();
                NewPaymentMethodName.Text = "";
                MessageBox.Show("Спосіб оплати успішно додано!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час додавання способу оплати: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditPaymentMethod_Click(object sender, RoutedEventArgs e)
        {
            int methodId = (int)((Button)sender).Tag;

            PaymentMethod methodToEdit = null;
            if (PaymentMethodsList != null)
            {
                foreach (PaymentMethod method in PaymentMethodsList.Items)
                {
                    if (method.MethodId == methodId)
                    {
                        methodToEdit = method;
                        break;
                    }
                }
            }

            if (methodToEdit == null)
            {
                MessageBox.Show("Спосіб оплати не знайдено.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string newName = Microsoft.VisualBasic.Interaction.InputBox("Введіть нову назву способу оплати:", "Редагування способу оплати", methodToEdit.Name);
            if (string.IsNullOrWhiteSpace(newName))
            {
                MessageBox.Show("Назва способу оплати не може бути порожньою.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            MessageBoxResult result = MessageBox.Show("Спосіб оплати активний?", "Редагування стану", MessageBoxButton.YesNo, MessageBoxImage.Question);
            bool isActive = (result == MessageBoxResult.Yes);

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var checkCommand = new NpgsqlCommand("SELECT COUNT(*) FROM payment_methods WHERE name = @name AND methodid != @methodId", connection))
                    {
                        checkCommand.Parameters.AddWithValue("name", newName);
                        checkCommand.Parameters.AddWithValue("methodId", methodId);
                        long count = (long)checkCommand.ExecuteScalar();
                        if (count > 0)
                        {
                            MessageBox.Show("Спосіб оплати з такою назвою вже існує.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }

                    using (var command = new NpgsqlCommand("UPDATE payment_methods SET name = @name, is_active = @isActive WHERE methodid = @methodId", connection))
                    {
                        command.Parameters.AddWithValue("name", newName);
                        command.Parameters.AddWithValue("isActive", isActive);
                        command.Parameters.AddWithValue("methodId", methodId);
                        command.ExecuteNonQuery();
                    }
                }
                LoadPaymentMethods();
                MessageBox.Show("Спосіб оплати успішно оновлено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час оновлення способу оплати: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeletePaymentMethod_Click(object sender, RoutedEventArgs e)
        {
            int methodId = (int)((Button)sender).Tag;

            MessageBoxResult result = MessageBox.Show("Ви впевнені, що хочете видалити цей спосіб оплати?", "Підтвердження видалення", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    using (var checkCommand = new NpgsqlCommand("SELECT COUNT(*) FROM orders WHERE payment_method_id = @methodId", connection))
                    {
                        checkCommand.Parameters.AddWithValue("methodId", methodId);
                        long orderCount = (long)checkCommand.ExecuteScalar();
                        if (orderCount > 0)
                        {
                            int? newMethodId = null;
                            using (var selectCommand = new NpgsqlCommand("SELECT methodid FROM payment_methods WHERE is_active = TRUE AND methodid != @methodId LIMIT 1", connection))
                            {
                                selectCommand.Parameters.AddWithValue("methodId", methodId);
                                var resultObj = selectCommand.ExecuteScalar();
                                if (resultObj != null)
                                {
                                    newMethodId = (int)resultObj;
                                }
                            }

                            if (newMethodId == null)
                            {
                                MessageBox.Show("Немає активних альтернативних способів оплати для заміни. Активуйте інший спосіб оплати перед видаленням.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }

                            using (var updateCommand = new NpgsqlCommand("UPDATE orders SET payment_method_id = @newMethodId WHERE payment_method_id = @methodId", connection))
                            {
                                updateCommand.Parameters.AddWithValue("newMethodId", newMethodId);
                                updateCommand.Parameters.AddWithValue("methodId", methodId);
                                updateCommand.ExecuteNonQuery();
                            }
                        }
                    }

                    using (var command = new NpgsqlCommand("DELETE FROM payment_methods WHERE methodid = @methodId", connection))
                    {
                        command.Parameters.AddWithValue("methodId", methodId);
                        command.ExecuteNonQuery();
                    }
                }
                LoadPaymentMethods();
                MessageBox.Show("Спосіб оплати успішно видалено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час видалення способу оплати: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadCourierServices()
        {
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("SELECT serviceid, name, isactive FROM courierservices", connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            var services = new List<CourierService>();
                            while (reader.Read())
                            {
                                services.Add(new CourierService
                                {
                                    ServiceId = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    IsActive = reader.GetBoolean(2)
                                });
                            }
                            if (CourierServicesList != null)
                            {
                                CourierServicesList.ItemsSource = services;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час завантаження кур'єрських служб: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddCourierService_Click(object sender, RoutedEventArgs e)
        {
            if (NewCourierService == null)
            {
                MessageBox.Show("Елемент NewCourierService не знайдено у розмітці.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            string name = NewCourierService.Text?.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Введіть назву служби.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("INSERT INTO courierservices (name, isactive) VALUES (@name, @isActive)", connection))
                    {
                        command.Parameters.AddWithValue("name", name);
                        command.Parameters.AddWithValue("isActive", true);
                        command.ExecuteNonQuery();
                    }
                }
                LoadCourierServices();
                NewCourierService.Text = "";
                MessageBox.Show("Кур'єрську службу успішно додано!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час додавання служби: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditCourierService_Click(object sender, RoutedEventArgs e)
        {
            int serviceId = (int)((Button)sender).Tag;

            CourierService serviceToEdit = null;
            if (CourierServicesList != null)
            {
                foreach (CourierService service in CourierServicesList.Items)
                {
                    if (service.ServiceId == serviceId)
                    {
                        serviceToEdit = service;
                        break;
                    }
                }
            }

            if (serviceToEdit == null)
            {
                MessageBox.Show("Кур'єрську службу не знайдено.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string newName = Microsoft.VisualBasic.Interaction.InputBox("Введіть нову назву служби:", "Редагування служби", serviceToEdit.Name);
            if (string.IsNullOrWhiteSpace(newName))
            {
                MessageBox.Show("Назва служби не може бути порожньою.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            MessageBoxResult result = MessageBox.Show("Служба активна?", "Редагування стану", MessageBoxButton.YesNo, MessageBoxImage.Question);
            bool isActive = (result == MessageBoxResult.Yes);

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("UPDATE courierservices SET name = @name, isactive = @isActive WHERE serviceid = @serviceId", connection))
                    {
                        command.Parameters.AddWithValue("name", newName);
                        command.Parameters.AddWithValue("isActive", isActive);
                        command.Parameters.AddWithValue("serviceId", serviceId);
                        command.ExecuteNonQuery();
                    }
                }
                LoadCourierServices();
                MessageBox.Show("Кур'єрську службу успішно оновлено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час оновлення служби: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteCourierService_Click(object sender, RoutedEventArgs e)
        {
            int serviceId = (int)((Button)sender).Tag;

            MessageBoxResult result = MessageBox.Show("Ви впевнені, що хочете видалити цю кур'єрську службу?", "Підтвердження видалення", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    using (var command = new NpgsqlCommand("DELETE FROM courierservices WHERE serviceid = @serviceId", connection))
                    {
                        command.Parameters.AddWithValue("serviceId", serviceId);
                        command.ExecuteNonQuery();
                    }
                }
                LoadCourierServices();
                MessageBox.Show("Кур'єрську службу успішно видалено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час видалення служби: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateCourierService_Click(object sender, RoutedEventArgs e)
        {
            int serviceId = (int)((Button)sender).Tag;
            var selectedRow = CourierServicesList?.SelectedItem as CourierService;
            if (selectedRow == null)
            {
                MessageBox.Show("Оберіть службу для оновлення.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            bool isActive = selectedRow.IsActive;

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("UPDATE courierservices SET isactive = @isActive WHERE serviceid = @serviceId", connection))
                    {
                        command.Parameters.AddWithValue("isActive", isActive);
                        command.Parameters.AddWithValue("serviceId", serviceId);
                        command.ExecuteNonQuery();
                    }
                }
                LoadCourierServices();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час оновлення служби: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadPickupPoints()
        {
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("SELECT pickup_point_id, address, region FROM pickup_points", connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            var points = new List<PickupPoint>();
                            while (reader.Read())
                            {
                                points.Add(new PickupPoint
                                {
                                    PickupPointId = reader.GetInt32(0),
                                    Address = reader.GetString(1),
                                    Region = reader.GetString(2)
                                });
                            }
                            if (PickupPointsList != null)
                            {
                                PickupPointsList.ItemsSource = points;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час завантаження пунктів самовивозу: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddPickupPoint_Click(object sender, RoutedEventArgs e)
        {
            if (NewPickupPointAddress == null || NewPickupPointRegion == null)
            {
                MessageBox.Show("Елемент NewPickupPointAddress або NewPickupPointRegion не знайдено у розмітці.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            string address = NewPickupPointAddress.Text?.Trim();
            string region = NewPickupPointRegion.SelectedItem?.ToString();

            if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(region))
            {
                MessageBox.Show("Заповніть усі поля.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var checkCommand = new NpgsqlCommand("SELECT COUNT(*) FROM pickup_points WHERE address = @address", connection))
                    {
                        checkCommand.Parameters.AddWithValue("address", address);
                        long count = (long)checkCommand.ExecuteScalar();
                        if (count > 0)
                        {
                            MessageBox.Show("Пункт самовивозу з такою адресою вже існує.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }

                    using (var command = new NpgsqlCommand("INSERT INTO pickup_points (address, region) VALUES (@address, @region)", connection))
                    {
                        command.Parameters.AddWithValue("address", address);
                        command.Parameters.AddWithValue("region", region);
                        command.ExecuteNonQuery();
                    }
                }
                LoadPickupPoints();
                NewPickupPointAddress.Text = "";
                NewPickupPointRegion.SelectedIndex = 0;
                MessageBox.Show("Пункт самовивозу успішно додано!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час додавання пункту самовивозу: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdatePickupPoint_Click(object sender, RoutedEventArgs e)
        {
            int pickupPointId = (int)((Button)sender).Tag;

            PickupPoint pointToEdit = null;
            if (PickupPointsList != null)
            {
                foreach (PickupPoint point in PickupPointsList.Items)
                {
                    if (point.PickupPointId == pickupPointId)
                    {
                        pointToEdit = point;
                        break;
                    }
                }
            }

            if (pointToEdit == null)
            {
                MessageBox.Show("Пункт самовивозу не знайдено.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Window editWindow = new Window
            {
                Title = "Редагування пункту самовивозу",
                Width = 400,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            StackPanel panel = new StackPanel { Margin = new Thickness(10) };

            TextBlock addressLabel = new TextBlock { Text = "Нова адреса:", Margin = new Thickness(0, 0, 0, 5) };
            TextBox addressBox = new TextBox { Text = pointToEdit.Address, Width = 300, Margin = new Thickness(0, 0, 0, 10) };

            TextBlock regionLabel = new TextBlock { Text = "Новий регіон:", Margin = new Thickness(0, 0, 0, 5) };
            ComboBox regionBox = new ComboBox { Width = 300, Margin = new Thickness(0, 0, 0, 10) };
            foreach (var region in regions)
            {
                regionBox.Items.Add(new ComboBoxItem { Content = region });
            }
            regionBox.SelectedItem = regionBox.Items.Cast<ComboBoxItem>().FirstOrDefault(item => item.Content.ToString() == pointToEdit.Region);

            Button saveButton = new Button { Content = "Зберегти", Width = 100, Margin = new Thickness(0, 20, 0, 0) };
            saveButton.Click += (s, ev) =>
            {
                string newAddress = addressBox.Text?.Trim();
                string newRegion = (regionBox.SelectedItem as ComboBoxItem)?.Content?.ToString();

                if (string.IsNullOrWhiteSpace(newAddress) || string.IsNullOrWhiteSpace(newRegion))
                {
                    MessageBox.Show("Заповніть усі поля.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                try
                {
                    using (var connection = new NpgsqlConnection(connectionString))
                    {
                        connection.Open();
                        using (var checkCommand = new NpgsqlCommand("SELECT COUNT(*) FROM pickup_points WHERE address = @address AND pickup_point_id != @pickupPointId", connection))
                        {
                            checkCommand.Parameters.AddWithValue("address", newAddress);
                            checkCommand.Parameters.AddWithValue("pickupPointId", pickupPointId);
                            long count = (long)checkCommand.ExecuteScalar();
                            if (count > 0)
                            {
                                MessageBox.Show("Пункт самовивозу з такою адресою вже існує.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                        }

                        using (var command = new NpgsqlCommand("UPDATE pickup_points SET address = @address, region = @region WHERE pickup_point_id = @pickupPointId", connection))
                        {
                            command.Parameters.AddWithValue("address", newAddress);
                            command.Parameters.AddWithValue("region", newRegion);
                            command.Parameters.AddWithValue("pickupPointId", pickupPointId);
                            command.ExecuteNonQuery();
                        }
                    }
                    LoadPickupPoints();
                    MessageBox.Show("Пункт самовивозу успішно оновлено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                    editWindow.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка під час оновлення пункту самовивозу: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            panel.Children.Add(addressLabel);
            panel.Children.Add(addressBox);
            panel.Children.Add(regionLabel);
            panel.Children.Add(regionBox);
            panel.Children.Add(saveButton);

            editWindow.Content = panel;
            editWindow.ShowDialog();
        }

        private void DeletePickupPoint_Click(object sender, RoutedEventArgs e)
        {
            int pickupPointId = (int)((Button)sender).Tag;

            MessageBoxResult result = MessageBox.Show("Ви впевнені, що хочете видалити цей пункт самовивозу?", "Підтвердження видалення", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    using (var checkCommand = new NpgsqlCommand("SELECT COUNT(*) FROM orders WHERE pickup_point_id = @pickupPointId", connection))
                    {
                        checkCommand.Parameters.AddWithValue("pickupPointId", pickupPointId);
                        long orderCount = (long)checkCommand.ExecuteScalar();
                        if (orderCount > 0)
                        {
                            MessageBox.Show("Цей пункт самовивозу використовується в активних замовленнях. Видаліть або змініть пов'язані замовлення перед видаленням.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }

                    using (var command = new NpgsqlCommand("DELETE FROM pickup_points WHERE pickup_point_id = @pickupPointId", connection))
                    {
                        command.Parameters.AddWithValue("pickupPointId", pickupPointId);
                        command.ExecuteNonQuery();
                    }
                }
                LoadPickupPoints();
                MessageBox.Show("Пункт самовивозу успішно видалено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час видалення пункту самовивозу: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}