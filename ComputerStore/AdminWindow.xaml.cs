using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Npgsql;
using System.Configuration;
using BCrypt.Net;
using Microsoft.Win32; // Для OpenFileDialog

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
        private string selectedImagePath; // Для зберігання шляху до обраного зображення

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

            LoadUsers();
            LoadCategories();
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
            if (regions.Any())
            {
                NewPickupPointRegion.SelectedIndex = 0;
            }
        }

        // Перемикання панелей
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

        // Керування користувачами
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
                            UsersList.ItemsSource = users;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час завантаження користувачів: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RegisterUser_Click(object sender, RoutedEventArgs e)
        {
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

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    // Перевіряємо, чи існує користувач з таким Username
                    using (var checkCommand = new NpgsqlCommand("SELECT COUNT(*) FROM usercredentials WHERE username = @username", connection))
                    {
                        checkCommand.Parameters.AddWithValue("username", username);
                        long count = (long)checkCommand.ExecuteScalar();
                        if (count > 0)
                        {
                            MessageBox.Show("Користувач з таким ім'ям уже існує. Будь ласка, виберіть інше ім'я.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }

                    // Перевіряємо, чи існує користувач з таким Email
                    using (var checkEmailCommand = new NpgsqlCommand("SELECT COUNT(*) FROM userdetails WHERE email = @email", connection))
                    {
                        checkEmailCommand.Parameters.AddWithValue("email", email);
                        long emailCount = (long)checkEmailCommand.ExecuteScalar();
                        if (emailCount > 0)
                        {
                            MessageBox.Show("Користувач з такою електронною поштою уже існує. Будь ласка, використовуйте іншу електронну пошту.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }

                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // Реєстрація користувача
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

                            // Якщо роль Seller, створюємо профіль магазину
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
                            // Отримуємо роль користувача
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

                            // Якщо роль Seller, видаляємо пов'язані записи з sellerfees
                            if (role == "Seller")
                            {
                                using (var command = new NpgsqlCommand("DELETE FROM sellerfees WHERE sellerid = @userId", connection))
                                {
                                    command.Parameters.AddWithValue("userId", userId);
                                    command.Transaction = transaction;
                                    command.ExecuteNonQuery();
                                }
                            }

                            // Видаляємо профіль магазину, якщо він існує
                            using (var command = new NpgsqlCommand("DELETE FROM sellerprofiles WHERE sellerid = @userId", connection))
                            {
                                command.Parameters.AddWithValue("userId", userId);
                                command.Transaction = transaction;
                                command.ExecuteNonQuery();
                            }

                            // Видаляємо користувача з usercredentials
                            using (var command = new NpgsqlCommand("DELETE FROM usercredentials WHERE userid = @userId", connection))
                            {
                                command.Parameters.AddWithValue("userId", userId);
                                command.Transaction = transaction;
                                command.ExecuteNonQuery();
                            }

                            // Видаляємо деталі користувача
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

        // Керування каталогом
        private void LoadCategories()
        {
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("SELECT c1.categoryid, c1.name, c2.name AS parentname, c1.image_url FROM categories c1 LEFT JOIN categories c2 ON c1.parentcategoryid = c2.categoryid", connection))
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
                                    ParentCategoryName = reader.IsDBNull(2) ? "Немає" : reader.GetString(2),
                                    ImageUrl = reader.IsDBNull(3) ? null : reader.GetString(3)
                                });
                            }
                            CategoriesList.ItemsSource = categories;

                            // Заповнюємо ComboBox для вибору батьківської категорії
                            ParentCategory.Items.Clear();
                            ParentCategory.Items.Add(new ComboBoxItem { Content = "Немає", Tag = null });
                            foreach (var category in categories)
                            {
                                if (category.ParentCategoryName == "Немає") // Тільки кореневі категорії як батьківські
                                {
                                    ParentCategory.Items.Add(new ComboBoxItem { Content = category.Name, Tag = category.CategoryId });
                                }
                            }
                            if (ParentCategory.Items.Count > 0)
                            {
                                ParentCategory.SelectedIndex = 0;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час завантаження категорій: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectImage_Click(object sender, RoutedEventArgs e)
        {
            // Перевіряємо, чи обрано батьківську категорію
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
            string name = NewCategoryName.Text?.Trim();
            int? parentCategoryId = (ParentCategory.SelectedItem as ComboBoxItem)?.Tag as int?;

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Введіть назву категорії.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Перевіряємо, чи є категорія підкатегорією
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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час додавання категорії: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditCategory_Click(object sender, RoutedEventArgs e)
        {
            int categoryId = (int)((Button)sender).Tag;

            // Отримуємо поточну категорію
            Category categoryToEdit = null;
            foreach (Category category in CategoriesList.Items)
            {
                if (category.CategoryId == categoryId)
                {
                    categoryToEdit = category;
                    break;
                }
            }

            if (categoryToEdit == null)
            {
                MessageBox.Show("Категорію не знайдено.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Запитуємо нову назву
            string newName = Microsoft.VisualBasic.Interaction.InputBox("Введіть нову назву категорії:", "Редагування категорії", categoryToEdit.Name);
            if (string.IsNullOrWhiteSpace(newName))
            {
                MessageBox.Show("Назва категорії не може бути порожньою.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Запитуємо нове зображення (якщо це підкатегорія)
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
                        return; // Якщо користувач скасував вибір зображення, перериваємо редагування
                    }
                }
            }
            else if (!string.IsNullOrEmpty(newImageUrl))
            {
                MessageBox.Show("Зображення для кореневих категорій не підтримуються.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Оновлюємо категорію в базі даних
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
                MessageBox.Show($"Помилка під час видалення категорії: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Фінансові налаштування
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
                MessageBox.Show($"Помилка під час завантаження способів оплати: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddPaymentMethod_Click(object sender, RoutedEventArgs e)
        {
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
                    // Перевіряємо, чи не існує вже спосіб оплати з такою назвою
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

            // Отримуємо поточний спосіб оплати
            PaymentMethod methodToEdit = null;
            foreach (PaymentMethod method in PaymentMethodsList.Items)
            {
                if (method.MethodId == methodId)
                {
                    methodToEdit = method;
                    break;
                }
            }

            if (methodToEdit == null)
            {
                MessageBox.Show("Спосіб оплати не знайдено.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Запитуємо нову назву
            string newName = Microsoft.VisualBasic.Interaction.InputBox("Введіть нову назву способу оплати:", "Редагування способу оплати", methodToEdit.Name);
            if (string.IsNullOrWhiteSpace(newName))
            {
                MessageBox.Show("Назва способу оплати не може бути порожньою.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Запитуємо стан активності
            MessageBoxResult result = MessageBox.Show("Спосіб оплати активний?", "Редагування стану", MessageBoxButton.YesNo, MessageBoxImage.Question);
            bool isActive = (result == MessageBoxResult.Yes);

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    // Перевіряємо, чи не існує вже спосіб оплати з такою назвою
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

                    // Перевіряємо, чи є замовлення з цим способом оплати
                    using (var checkCommand = new NpgsqlCommand("SELECT COUNT(*) FROM orders WHERE payment_method_id = @methodId", connection))
                    {
                        checkCommand.Parameters.AddWithValue("methodId", methodId);
                        long orderCount = (long)checkCommand.ExecuteScalar();
                        if (orderCount > 0)
                        {
                            // Шукаємо активний альтернативний спосіб оплати
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

                            // Оновлюємо всі замовлення з новим payment_method_id
                            using (var updateCommand = new NpgsqlCommand("UPDATE orders SET payment_method_id = @newMethodId WHERE payment_method_id = @methodId", connection))
                            {
                                updateCommand.Parameters.AddWithValue("newMethodId", newMethodId);
                                updateCommand.Parameters.AddWithValue("methodId", methodId);
                                updateCommand.ExecuteNonQuery();
                            }
                        }
                    }

                    // Виконуємо видалення способу оплати
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

        // Логістика та доставка
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
                MessageBox.Show($"Помилка під час завантаження кур'єрських служб: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddCourierService_Click(object sender, RoutedEventArgs e)
        {
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

            // Отримуємо поточну службу
            CourierService serviceToEdit = null;
            foreach (CourierService service in CourierServicesList.Items)
            {
                if (service.ServiceId == serviceId)
                {
                    serviceToEdit = service;
                    break;
                }
            }

            if (serviceToEdit == null)
            {
                MessageBox.Show("Кур'єрську службу не знайдено.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Запитуємо нову назву
            string newName = Microsoft.VisualBasic.Interaction.InputBox("Введіть нову назву служби:", "Редагування служби", serviceToEdit.Name);
            if (string.IsNullOrWhiteSpace(newName))
            {
                MessageBox.Show("Назва служби не може бути порожньою.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Запитуємо новий стан активності
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

                    // Виконуємо видалення служби
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
                            PickupPointsList.ItemsSource = points;
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
                    // Перевіряємо, чи не існує вже пункт самовивозу з такою адресою
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

            // Отримуємо поточний пункт самовивозу
            PickupPoint pointToEdit = null;
            foreach (PickupPoint point in PickupPointsList.Items)
            {
                if (point.PickupPointId == pickupPointId)
                {
                    pointToEdit = point;
                    break;
                }
            }

            if (pointToEdit == null)
            {
                MessageBox.Show("Пункт самовивозу не знайдено.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Запитуємо нову адресу
            string newAddress = Microsoft.VisualBasic.Interaction.InputBox("Введіть нову адресу пункту самовивозу:", "Редагування пункту самовивозу", pointToEdit.Address);
            if (string.IsNullOrWhiteSpace(newAddress))
            {
                MessageBox.Show("Адреса не може бути порожньою.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Запитуємо новий регіон через діалогове вікно
            var regionWindow = new Window
            {
                Title = "Оберіть регіон",
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };
            var regionStackPanel = new StackPanel { Margin = new Thickness(10) };
            var regionComboBox = new ComboBox
            {
                ItemsSource = regions,
                SelectedItem = pointToEdit.Region,
                Width = 260,
                Margin = new Thickness(0, 0, 0, 10)
            };
            var confirmButton = new Button
            {
                Content = "Підтвердити",
                Width = 100,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            confirmButton.Click += (s, ev) => regionWindow.DialogResult = true;
            regionStackPanel.Children.Add(new TextBlock { Text = "Оберіть регіон:" });
            regionStackPanel.Children.Add(regionComboBox);
            regionStackPanel.Children.Add(confirmButton);
            regionWindow.Content = regionStackPanel;

            string newRegion = pointToEdit.Region;
            if (regionWindow.ShowDialog() == true && regionComboBox.SelectedItem != null)
            {
                newRegion = regionComboBox.SelectedItem.ToString();
            }
            else
            {
                return; // Якщо користувач скасував вибір регіону, перериваємо редагування
            }

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    // Перевіряємо, чи не існує вже пункт самовивозу з такою адресою
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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час оновлення пункту самовивозу: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeletePickupPoint_Click(object sender, RoutedEventArgs e)
        {
            int pickupPointId = (int)((Button)sender).Tag;

            MessageBoxResult confirmResult = MessageBox.Show("Ви впевнені, що хочете видалити цей пункт самовивозу?", "Підтвердження видалення", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirmResult != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    // Перевіряємо, чи є замовлення, пов'язані з цим пунктом самовивозу
                    using (var checkCommand = new NpgsqlCommand("SELECT COUNT(*) FROM orders WHERE pickup_point_id = @pickupPointId", connection))
                    {
                        checkCommand.Parameters.AddWithValue("pickupPointId", pickupPointId);
                        long orderCount = (long)checkCommand.ExecuteScalar();
                        if (orderCount > 0)
                        {
                            // Шукаємо альтернативний пункт самовивозу
                            int? newPickupPointId = null;
                            using (var selectCommand = new NpgsqlCommand("SELECT pickup_point_id FROM pickup_points WHERE pickup_point_id != @pickupPointId LIMIT 1", connection))
                            {
                                selectCommand.Parameters.AddWithValue("pickupPointId", pickupPointId);
                                var resultObj = selectCommand.ExecuteScalar();
                                if (resultObj != null)
                                {
                                    newPickupPointId = (int)resultObj;
                                }
                            }

                            if (newPickupPointId == null)
                            {
                                MessageBox.Show("Немає інших пунктів самовивозу для заміни. Додайте новий пункт самовивозу перед видаленням цього.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }

                            // Оновлюємо всі замовлення, замінюючи pickup_point_id на новий
                            using (var updateCommand = new NpgsqlCommand("UPDATE orders SET pickup_point_id = @newPickupPointId WHERE pickup_point_id = @pickupPointId", connection))
                            {
                                updateCommand.Parameters.AddWithValue("newPickupPointId", newPickupPointId);
                                updateCommand.Parameters.AddWithValue("pickupPointId", pickupPointId);
                                updateCommand.ExecuteNonQuery();
                            }
                        }
                    }

                    // Виконуємо видалення пункту самовивозу
                    using (var command = new NpgsqlCommand("DELETE FROM pickup_points WHERE pickup_point_id = @pickupPointId", connection))
                    {
                        command.Parameters.AddWithValue("pickupPointId", pickupPointId);
                        command.ExecuteNonQuery();
                    }

                    LoadPickupPoints();
                    MessageBox.Show("Пункт самовивозу успішно видалено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час видалення пункту самовивозу: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // Моделі даних
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
        public string ImageUrl { get; set; }
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
        public string SubcategoryName { get; internal set; }
    }

    public class PaymentMethod
    {
        public int MethodId { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }
        public int PaymentMethodId { get; internal set; }
    }

    public class CourierService
    {
        public int ServiceId { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }
    }

    public class PickupPoint
    {
        public int PickupPointId { get; set; }
        public string Address { get; set; }
        public string Region { get; set; }
    }
}