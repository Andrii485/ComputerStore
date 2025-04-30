using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Npgsql;
using System.Configuration;
using BCrypt.Net;

namespace ElmirClone
{
    public partial class AdminWindow : Window
    {
        private string connectionString;

        public AdminWindow()
        {
            InitializeComponent();
            connectionString = ConfigurationManager.ConnectionStrings["ElitePCConnection"]?.ConnectionString;
            if (string.IsNullOrEmpty(connectionString))
            {
                MessageBox.Show("Строка подключения к базе данных не найдена.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            LoadUsers();
            LoadCategories();
            LoadProducts();
            LoadSellerFees();
            LoadPaymentMethods();
            LoadReturns();
            LoadCourierServices();
            LoadPickupPoints();
        }

        // Переключение панелей
        private void ShowUsersPanel_Click(object sender, RoutedEventArgs e)
        {
            UsersPanel.Visibility = Visibility.Visible;
            CatalogPanel.Visibility = Visibility.Collapsed;
            FinancePanel.Visibility = Visibility.Collapsed;
            LogisticsPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowCatalogPanel_Click(object sender, RoutedEventArgs e)
        {
            UsersPanel.Visibility = Visibility.Collapsed;
            CatalogPanel.Visibility = Visibility.Visible;
            FinancePanel.Visibility = Visibility.Collapsed;
            LogisticsPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowFinancePanel_Click(object sender, RoutedEventArgs e)
        {
            UsersPanel.Visibility = Visibility.Collapsed;
            CatalogPanel.Visibility = Visibility.Collapsed;
            FinancePanel.Visibility = Visibility.Visible;
            LogisticsPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowLogisticsPanel_Click(object sender, RoutedEventArgs e)
        {
            UsersPanel.Visibility = Visibility.Collapsed;
            CatalogPanel.Visibility = Visibility.Collapsed;
            FinancePanel.Visibility = Visibility.Collapsed;
            LogisticsPanel.Visibility = Visibility.Visible;
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            LoginWindow loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }

        // Управление пользователями
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
                                users.Add(new User
                                {
                                    UserId = reader.GetInt32(0),
                                    Username = reader.GetString(1),
                                    Email = reader.GetString(2),
                                    Role = reader.GetString(3),
                                    IsBlocked = reader.GetBoolean(4)
                                });
                            }
                            UsersList.ItemsSource = users;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке пользователей: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RegisterUser_Click(object sender, RoutedEventArgs e)
        {
            string username = NewUserUsername.Text.Trim();
            string email = NewUserEmail.Text.Trim();
            string password = NewUserPassword.Password;
            string role = (NewUserRole.SelectedItem as ComboBoxItem)?.Content.ToString();

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(role))
            {
                MessageBox.Show("Заполните все поля.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    // Проверяем, существует ли пользователь с таким Username
                    using (var checkCommand = new NpgsqlCommand("SELECT COUNT(*) FROM usercredentials WHERE username = @username", connection))
                    {
                        checkCommand.Parameters.AddWithValue("username", username);
                        long count = (long)checkCommand.ExecuteScalar();
                        if (count > 0)
                        {
                            MessageBox.Show("Пользователь с таким именем уже существует. Пожалуйста, выберите другое имя.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }

                    // Проверяем, существует ли пользователь с таким Email
                    using (var checkEmailCommand = new NpgsqlCommand("SELECT COUNT(*) FROM userdetails WHERE email = @email", connection))
                    {
                        checkEmailCommand.Parameters.AddWithValue("email", email);
                        long emailCount = (long)checkEmailCommand.ExecuteScalar();
                        if (emailCount > 0)
                        {
                            MessageBox.Show("Пользователь с таким email уже существует. Пожалуйста, используйте другой email.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }

                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // Регистрация пользователя
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

                            // Если роль Seller, создаем профиль магазина и устанавливаем комиссию по умолчанию
                            if (role == "Seller")
                            {
                                // Создаем профиль магазина
                                using (var command = new NpgsqlCommand("INSERT INTO sellerprofiles (sellerid, storename, description, contactinfo) VALUES (@sellerId, @storeName, @description, @contactInfo)", connection))
                                {
                                    command.Parameters.AddWithValue("sellerId", userId);
                                    command.Parameters.AddWithValue("storeName", $"{username}'s Store");
                                    command.Parameters.AddWithValue("description", "Добро пожаловать в мой магазин!");
                                    command.Parameters.AddWithValue("contactInfo", "Не указаны");
                                    command.Transaction = transaction;
                                    command.ExecuteNonQuery();
                                }

                                // Устанавливаем комиссию по умолчанию (например, 10%)
                                using (var command = new NpgsqlCommand("INSERT INTO sellerfees (sellerid, feetype, feevalue) VALUES (@sellerId, @feeType, @feeValue)", connection))
                                {
                                    command.Parameters.AddWithValue("sellerId", userId);
                                    command.Parameters.AddWithValue("feeType", "Percentage");
                                    command.Parameters.AddWithValue("feeValue", 10.0m);
                                    command.Transaction = transaction;
                                    command.ExecuteNonQuery();
                                }
                            }

                            transaction.Commit();
                            MessageBox.Show("Пользователь успешно зарегистрирован!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                            LoadUsers();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            MessageBox.Show($"Ошибка при регистрации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при блокировке пользователя: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                            MessageBox.Show("Пользователь удален!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                            LoadUsers();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Управление каталогом
        private void LoadCategories()
        {
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("SELECT c1.categoryid, c1.name, c2.name AS parentname FROM categories c1 LEFT JOIN categories c2 ON c1.parentcategoryid = c2.categoryid", connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            var categories = new List<Category>();
                            while (reader.Read())
                            {
                                categories.Add(new Category
                                {
                                    CategoryId = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    ParentCategoryName = reader.IsDBNull(2) ? "Нет" : reader.GetString(2)
                                });
                            }
                            CategoriesList.ItemsSource = categories;

                            // Заполняем ComboBox для выбора родительской категории
                            ParentCategory.Items.Clear();
                            ParentCategory.Items.Add(new ComboBoxItem { Content = "Нет", Tag = null });
                            foreach (var category in categories)
                            {
                                ParentCategory.Items.Add(new ComboBoxItem { Content = category.Name, Tag = category.CategoryId });
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

        private void AddCategory_Click(object sender, RoutedEventArgs e)
        {
            string name = NewCategoryName.Text.Trim();
            int? parentCategoryId = (ParentCategory.SelectedItem as ComboBoxItem)?.Tag as int?;

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Введите название категории.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("INSERT INTO categories (name, parentcategoryid) VALUES (@name, @parentId)", connection))
                    {
                        command.Parameters.AddWithValue("name", name);
                        command.Parameters.AddWithValue("parentId", parentCategoryId == null ? (object)DBNull.Value : parentCategoryId);
                        command.ExecuteNonQuery();
                    }
                }
                LoadCategories();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении категории: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            int categoryId = (int)((Button)sender).Tag;
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("DELETE FROM categories WHERE categoryid = @categoryId", connection))
                    {
                        command.Parameters.AddWithValue("categoryId", categoryId);
                        command.ExecuteNonQuery();
                    }
                }
                LoadCategories();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении категории: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadProducts()
        {
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("SELECT p.productid, p.name, p.price, p.brand, c.name AS categoryname, p.ishidden FROM products p JOIN categories c ON p.categoryid = c.categoryid", connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            var products = new List<DbProduct>();
                            while (reader.Read())
                            {
                                products.Add(new DbProduct
                                {
                                    ProductId = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    Price = reader.GetDecimal(2),
                                    Brand = reader.GetString(3),
                                    CategoryName = reader.GetString(4),
                                    IsHidden = reader.GetBoolean(5)
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
            if (!decimal.TryParse(NewProductPrice.Text, out decimal price))
            {
                MessageBox.Show("Введите корректную цену.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            string brand = NewProductBrand.Text.Trim();
            int? categoryId = ProductCategory.SelectedValue as int?;

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(brand) || categoryId == null)
            {
                MessageBox.Show("Заполните все поля.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("INSERT INTO products (name, price, brand, categoryid) VALUES (@name, @price, @brand, @categoryId)", connection))
                    {
                        command.Parameters.AddWithValue("name", name);
                        command.Parameters.AddWithValue("price", price);
                        command.Parameters.AddWithValue("brand", brand);
                        command.Parameters.AddWithValue("categoryId", categoryId);
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

        private void HideProduct_Click(object sender, RoutedEventArgs e)
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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при скрытии товара: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    using (var command = new NpgsqlCommand("DELETE FROM products WHERE productid = @productId", connection))
                    {
                        command.Parameters.AddWithValue("productId", productId);
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

        // Финансовые настройки
        private void LoadSellerFees()
        {
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("SELECT sf.sellerid, uc.username, sf.feetype, sf.feevalue FROM sellerfees sf JOIN usercredentials uc ON sf.sellerid = uc.userid WHERE uc.role = 'Seller'", connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            var fees = new List<SellerFee>();
                            while (reader.Read())
                            {
                                fees.Add(new SellerFee
                                {
                                    SellerId = reader.GetInt32(0),
                                    Username = reader.GetString(1),
                                    FeeType = reader.GetString(2),
                                    FeeValue = reader.GetDecimal(3)
                                });
                            }
                            SellerFeesList.ItemsSource = fees;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке комиссий: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateSellerFee_Click(object sender, RoutedEventArgs e)
        {
            int sellerId = (int)((Button)sender).Tag;
            var stackPanel = ((Button)sender).Parent as StackPanel;
            var feeTypeCombo = stackPanel.Children[3] as ComboBox;
            var feeValueTextBox = stackPanel.Children[4] as TextBox;

            string feeType = (feeTypeCombo.SelectedItem as ComboBoxItem)?.Content.ToString();
            if (!decimal.TryParse(feeValueTextBox.Text, out decimal feeValue))
            {
                MessageBox.Show("Введите корректное значение комиссии.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("INSERT INTO sellerfees (sellerid, feetype, feevalue) VALUES (@sellerId, @feeType, @feeValue) ON CONFLICT (sellerid) DO UPDATE SET feetype = @feeType, feevalue = @feeValue", connection))
                    {
                        command.Parameters.AddWithValue("sellerId", sellerId);
                        command.Parameters.AddWithValue("feeType", feeType);
                        command.Parameters.AddWithValue("feeValue", feeValue);
                        command.ExecuteNonQuery();
                    }
                }
                LoadSellerFees();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении комиссии: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                            PaymentMethodsList.ItemsSource = methods;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке способов оплаты: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddPaymentMethod_Click(object sender, RoutedEventArgs e)
        {
            string name = NewPaymentMethodName.Text?.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Введите название способа оплаты.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("INSERT INTO payment_methods (name, is_active) VALUES (@name, @isActive)", connection))
                    {
                        command.Parameters.AddWithValue("name", name);
                        command.Parameters.AddWithValue("isActive", true);
                        command.ExecuteNonQuery();
                    }
                }
                LoadPaymentMethods();
                NewPaymentMethodName.Text = ""; // Очищаем поле после добавления
                MessageBox.Show("Способ оплаты успешно добавлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении способа оплаты: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdatePaymentMethod_Click(object sender, RoutedEventArgs e)
        {
            int methodId = (int)((Button)sender).Tag;
            var stackPanel = ((Button)sender).Parent as StackPanel;
            var isActiveCheckBox = stackPanel.Children[1] as CheckBox;

            bool isActive = isActiveCheckBox.IsChecked ?? false;

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("UPDATE payment_methods SET is_active = @isActive WHERE methodid = @methodId", connection))
                    {
                        command.Parameters.AddWithValue("isActive", isActive);
                        command.Parameters.AddWithValue("methodId", methodId);
                        command.ExecuteNonQuery();
                    }
                }
                LoadPaymentMethods();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении способа оплаты: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadReturns()
        {
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("SELECT returnid, orderid, reason, status FROM returns", connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            var returns = new List<Return>();
                            while (reader.Read())
                            {
                                returns.Add(new Return
                                {
                                    ReturnId = reader.GetInt32(0),
                                    OrderId = reader.GetInt32(1),
                                    Reason = reader.GetString(2),
                                    Status = reader.GetString(3)
                                });
                            }
                            ReturnsList.ItemsSource = returns;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке возвратов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateReturnStatus_Click(object sender, RoutedEventArgs e)
        {
            int returnId = (int)((Button)sender).Tag;
            var stackPanel = ((Button)sender).Parent as StackPanel;
            var statusCombo = stackPanel.Children[3] as ComboBox;

            string status = (statusCombo.SelectedItem as ComboBoxItem)?.Content.ToString();

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("UPDATE returns SET status = @status WHERE returnid = @returnId", connection))
                    {
                        command.Parameters.AddWithValue("status", status);
                        command.Parameters.AddWithValue("returnId", returnId);
                        command.ExecuteNonQuery();
                    }
                }
                LoadReturns();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении статуса возврата: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Логистика и доставка
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
                            CourierServicesList.ItemsSource = services;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке курьерских служб: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddCourierService_Click(object sender, RoutedEventArgs e)
        {
            string name = NewCourierService.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Введите название службы.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("INSERT INTO courierservices (name) VALUES (@name)", connection))
                    {
                        command.Parameters.AddWithValue("name", name);
                        command.ExecuteNonQuery();
                    }
                }
                LoadCourierServices();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении службы: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateCourierService_Click(object sender, RoutedEventArgs e)
        {
            int serviceId = (int)((Button)sender).Tag;
            var stackPanel = ((Button)sender).Parent as StackPanel;
            var isActiveCheckBox = stackPanel.Children[1] as CheckBox;

            bool isActive = isActiveCheckBox.IsChecked ?? false;

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
                MessageBox.Show($"Ошибка при обновлении службы: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadPickupPoints()
        {
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("SELECT pickuppointid, name, address, region, isactive FROM pickuppoints", connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            var points = new List<PickupPoint>();
                            while (reader.Read())
                            {
                                points.Add(new PickupPoint
                                {
                                    PointId = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    Address = reader.GetString(2),
                                    Region = reader.GetString(3),
                                    IsActive = reader.GetBoolean(4)
                                });
                            }
                            PickupPointsList.ItemsSource = points;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке пунктов самовывоза: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddPickupPoint_Click(object sender, RoutedEventArgs e)
        {
            string name = NewPickupPointName.Text.Trim();
            string address = NewPickupPointAddress.Text.Trim();
            string region = NewPickupPointRegion.Text.Trim();

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(region))
            {
                MessageBox.Show("Заполните все поля.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("INSERT INTO pickuppoints (name, address, region) VALUES (@name, @address, @region)", connection))
                    {
                        command.Parameters.AddWithValue("name", name);
                        command.Parameters.AddWithValue("address", address);
                        command.Parameters.AddWithValue("region", region);
                        command.ExecuteNonQuery();
                    }
                }
                LoadPickupPoints();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении пункта самовывоза: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdatePickupPoint_Click(object sender, RoutedEventArgs e)
        {
            int pointId = (int)((Button)sender).Tag;
            var stackPanel = ((Button)sender).Parent as StackPanel;
            var isActiveCheckBox = stackPanel.Children[3] as CheckBox;

            bool isActive = isActiveCheckBox.IsChecked ?? false;

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("UPDATE pickuppoints SET isactive = @isActive WHERE pickuppointid = @pointId", connection))
                    {
                        command.Parameters.AddWithValue("isActive", isActive);
                        command.Parameters.AddWithValue("pointId", pointId);
                        command.ExecuteNonQuery();
                    }
                }
                LoadPickupPoints();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении пункта самовывоза: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // Модели данных
    public class User
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public bool IsBlocked { get; set; }
    }

    public class Category
    {
        public int CategoryId { get; set; }
        public string Name { get; set; }
        public string ParentCategoryName { get; set; }
    }

    public class DbProduct
    {
        internal string? ImagePath;
        internal int DiscountedPrice;
        internal int Quantity;
        internal object SellerId;
        internal decimal OriginalPrice;
        internal bool HasDiscount;
        internal string SellerName;

        public int ProductId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string Brand { get; set; }
        public decimal Discount { get; set; }
        public string CategoryName { get; set; }
        public bool IsHidden { get; set; }
        public string ImageUrl { get; internal set; }
        public double Rating { get; internal set; }
        public int Reviews { get; internal set; }
    }

    public class SellerFee
    {
        public int SellerId { get; set; }
        public string Username { get; set; }
        public string FeeType { get; set; }
        public decimal FeeValue { get; set; }
    }

    public class PaymentMethod
    {
        public int MethodId { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }
        public int PaymentMethodId { get; internal set; }
    }

    public class Return
    {
        public int ReturnId { get; set; }
        public int OrderId { get; set; }
        public string Reason { get; set; }
        public string Status { get; set; }
    }

    public class CourierService
    {
        public int ServiceId { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }
    }

    public class PickupPoint
    {
        public int PointId { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string Region { get; set; }
        public bool IsActive { get; set; }
        public int PickupPointId { get; internal set; }
    }
}