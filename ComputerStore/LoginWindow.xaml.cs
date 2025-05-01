using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading; // Для DispatcherTimer
using Npgsql;
using System.Configuration;
using BCrypt.Net;
using ElmirClone.Models;

namespace ElmirClone
{
    public partial class LoginWindow : Window
    {
        private bool isLoginMode = true;
        private bool isResetPasswordMode = false;
        private DispatcherTimer resetPasswordTimer; // Таймер для отсчета 20 секунд
        private string connectionString;

        public LoginWindow()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка в InitializeComponent: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }

            // Инициализация строки подключения
            connectionString = ConfigurationManager.ConnectionStrings["ElitePCConnection"]?.ConnectionString;
            if (string.IsNullOrEmpty(connectionString))
            {
                MessageBox.Show("Строка подключения к базе данных не найдена.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            // Инициализация таймера
            resetPasswordTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(20) // Устанавливаем интервал 20 секунд
            };
            resetPasswordTimer.Tick += ResetPasswordTimer_Tick; // Обработчик события таймера

            // Проверяем, что таймер инициализирован
            if (resetPasswordTimer == null)
            {
                MessageBox.Show("resetPasswordTimer не был инициализирован в конструкторе!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            // Устанавливаем начальное состояние после инициализации таймера
            if (LoginRadioButton != null)
            {
                LoginRadioButton.IsChecked = true; // Устанавливаем режим "Вход" по умолчанию
            }
            else
            {
                MessageBox.Show("LoginRadioButton is null. Проверьте XAML.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoginRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            isLoginMode = true;
            isResetPasswordMode = false;

            // Проверяем таймер перед использованием
            if (resetPasswordTimer == null)
            {
                MessageBox.Show("resetPasswordTimer is null in LoginRadioButton_Checked!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            resetPasswordTimer.Stop(); // Останавливаем таймер при возврате в режим входа

            if (TitleTextBlock != null)
            {
                TitleTextBlock.Text = "Вход";
            }
            else
            {
                MessageBox.Show("TitleTextBlock is null. Проверьте XAML.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            if (LoginFields != null && RegisterFieldsScrollViewer != null && ResetPasswordFields != null)
            {
                LoginFields.Visibility = Visibility.Visible;
                RegisterFieldsScrollViewer.Visibility = Visibility.Collapsed;
                ResetPasswordFields.Visibility = Visibility.Collapsed;
            }
            else
            {
                MessageBox.Show("Один из элементов (LoginFields, RegisterFieldsScrollViewer, ResetPasswordFields) is null. Проверьте XAML.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RegisterRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            isLoginMode = false;
            isResetPasswordMode = false;

            // Проверяем таймер перед использованием
            if (resetPasswordTimer == null)
            {
                MessageBox.Show("resetPasswordTimer is null in RegisterRadioButton_Checked!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            resetPasswordTimer.Stop(); // Останавливаем таймер при переходе в режим регистрации

            if (TitleTextBlock != null)
            {
                TitleTextBlock.Text = "Регистрация";
            }
            else
            {
                MessageBox.Show("TitleTextBlock is null. Проверьте XAML.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            if (LoginFields != null && RegisterFieldsScrollViewer != null && ResetPasswordFields != null)
            {
                LoginFields.Visibility = Visibility.Collapsed;
                RegisterFieldsScrollViewer.Visibility = Visibility.Visible;
                ResetPasswordFields.Visibility = Visibility.Collapsed;
            }
            else
            {
                MessageBox.Show("Один из элементов (LoginFields, RegisterFieldsScrollViewer, ResetPasswordFields) is null. Проверьте XAML.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetPasswordButton_Click(object sender, RoutedEventArgs e)
        {
            isResetPasswordMode = true;

            // Проверка TitleTextBlock
            if (TitleTextBlock == null)
            {
                MessageBox.Show("TitleTextBlock is null. Check x:Name in XAML.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            TitleTextBlock.Text = "Сброс пароля";

            // Проверяем наличие всех элементов
            if (LoginFields == null)
            {
                MessageBox.Show("LoginFields is null. Check x:Name in XAML.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (RegisterFieldsScrollViewer == null)
            {
                MessageBox.Show("RegisterFieldsScrollViewer is null. Check x:Name in XAML.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (ResetPasswordFields == null)
            {
                MessageBox.Show("ResetPasswordFields is null. Check x:Name in XAML.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Если все элементы найдены, выполняем переключение видимости
            LoginFields.Visibility = Visibility.Collapsed;
            RegisterFieldsScrollViewer.Visibility = Visibility.Collapsed;
            ResetPasswordFields.Visibility = Visibility.Visible;

            // Проверяем таймер перед использованием
            if (resetPasswordTimer == null)
            {
                MessageBox.Show("resetPasswordTimer is null in ResetPasswordButton_Click!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            resetPasswordTimer.Stop(); // Останавливаем, если уже был запущен
            resetPasswordTimer.Start();
        }

        private void ResetPasswordTimer_Tick(object sender, EventArgs e)
        {
            if (resetPasswordTimer == null)
            {
                MessageBox.Show("resetPasswordTimer is null in ResetPasswordTimer_Tick!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            resetPasswordTimer.Stop(); // Останавливаем таймер

            // Проверяем, заполнены ли поля
            bool isAnyFieldFilled =
                !string.IsNullOrWhiteSpace(EmailResetTextBox?.Text) ||
                !string.IsNullOrWhiteSpace(NewPasswordBox?.Password) ||
                !string.IsNullOrWhiteSpace(ConfirmNewPasswordBox?.Password);

            // Если поля заполнены или не заполнены, в любом случае переводим в режим входа
            if (isResetPasswordMode)
            {
                if (isAnyFieldFilled)
                {
                    MessageBox.Show("Время ожидания истекло. Пожалуйста, попробуйте снова.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                // Переводим в режим входа
                if (LoginRadioButton != null)
                {
                    LoginRadioButton.IsChecked = true;
                }
                // Очищаем поля сброса пароля
                if (EmailResetTextBox != null) EmailResetTextBox.Text = "";
                if (NewPasswordBox != null) NewPasswordBox.Password = "";
                if (ConfirmNewPasswordBox != null) ConfirmNewPasswordBox.Password = "";
            }
        }

        private void ConfirmResetButton_Click(object sender, RoutedEventArgs e)
        {
            // Получаем данные из полей
            string email = EmailResetTextBox?.Text?.Trim();
            string newPassword = NewPasswordBox?.Password;
            string confirmNewPassword = ConfirmNewPasswordBox?.Password;

            // Проверка на пустые поля
            if (string.IsNullOrWhiteSpace(email))
            {
                MessageBox.Show("Пожалуйста, введите вашу почту.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(newPassword))
            {
                MessageBox.Show("Пожалуйста, введите новый пароль.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(confirmNewPassword))
            {
                MessageBox.Show("Пожалуйста, повторите новый пароль.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Проверка совпадения паролей
            if (newPassword != confirmNewPassword)
            {
                MessageBox.Show("Пароли не совпадают.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    // Проверяем, существует ли пользователь с указанным email
                    int userId;
                    using (var command = new NpgsqlCommand("SELECT UserId FROM UserDetails WHERE Email = @email", connection))
                    {
                        command.Parameters.AddWithValue("email", email);
                        var result = command.ExecuteScalar();
                        if (result == null)
                        {
                            MessageBox.Show("Пользователь с указанным email не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                        userId = (int)result;
                    }

                    // Хешируем новый пароль
                    string hashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);

                    // Обновляем пароль в таблице UserCredentials
                    using (var command = new NpgsqlCommand("UPDATE UserCredentials SET Password = @password WHERE UserId = @userId", connection))
                    {
                        command.Parameters.AddWithValue("password", hashedPassword);
                        command.Parameters.AddWithValue("userId", userId);
                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            MessageBox.Show("Пароль успешно сброшен! Теперь вы можете войти с новым паролем.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                            // Останавливаем таймер
                            if (resetPasswordTimer == null)
                            {
                                MessageBox.Show("resetPasswordTimer is null in ConfirmResetButton_Click!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                            resetPasswordTimer.Stop();

                            // Переключаемся обратно в режим входа
                            if (LoginRadioButton != null)
                            {
                                LoginRadioButton.IsChecked = true;
                            }
                            if (EmailResetTextBox != null) EmailResetTextBox.Text = "";
                            if (NewPasswordBox != null) NewPasswordBox.Password = "";
                            if (ConfirmNewPasswordBox != null) ConfirmNewPasswordBox.Password = "";
                        }
                        else
                        {
                            MessageBox.Show("Не удалось сбросить пароль. Пожалуйста, попробуйте снова.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (isLoginMode)
            {
                // Режим входа
                string username = UsernameTextBox?.Text?.Trim();
                string password = PasswordBox?.Password;

                // Проверка на пустые поля
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    MessageBox.Show("Пожалуйста, заполните все поля для входа.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                try
                {
                    using (var connection = new NpgsqlConnection(connectionString))
                    {
                        connection.Open();

                        // 1. Проверяем, является ли пользователь администратором (таблица AdminCredentials)
                        using (var adminCommand = new NpgsqlCommand("SELECT Password FROM AdminCredentials WHERE Username = @username", connection))
                        {
                            adminCommand.Parameters.AddWithValue("username", username);

                            using (var adminReader = adminCommand.ExecuteReader())
                            {
                                if (adminReader.Read())
                                {
                                    string hashedPassword = adminReader.GetString(0);

                                    if (BCrypt.Net.BCrypt.Verify(password, hashedPassword))
                                    {
                                        MessageBox.Show($"Вход успешен! Добро пожаловать, администратор {username}!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                                        // Открываем окно администратора
                                        AdminWindow adminWindow = new AdminWindow();
                                        adminWindow.Show();
                                        this.Close();
                                        return; // Выходим из метода, так как администратор успешно вошел
                                    }
                                    else
                                    {
                                        MessageBox.Show("Неверный пароль.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                                        return;
                                    }
                                }
                            }
                        }

                        // 2. Если пользователь не администратор, проверяем в таблице UserCredentials
                        using (var userCommand = new NpgsqlCommand("SELECT uc.UserId, uc.Password, uc.Role, ud.FirstName, ud.LastName, ud.Email, ud.Balance FROM UserCredentials uc JOIN UserDetails ud ON uc.UserId = ud.UserId WHERE uc.Username = @username AND uc.IsBlocked = FALSE", connection))
                        {
                            userCommand.Parameters.AddWithValue("username", username);

                            using (var userReader = userCommand.ExecuteReader())
                            {
                                if (userReader.Read())
                                {
                                    int userId = userReader.GetInt32(0);
                                    string hashedPassword = userReader.GetString(1);
                                    string role = userReader.GetString(2);
                                    string firstName = userReader.GetString(3);
                                    string lastName = userReader.IsDBNull(4) ? "Не вказане" : userReader.GetString(4);
                                    string email = userReader.GetString(5);
                                    decimal balance = userReader.IsDBNull(6) ? 0 : userReader.GetDecimal(6);

                                    if (BCrypt.Net.BCrypt.Verify(password, hashedPassword))
                                    {
                                        MessageBox.Show($"Вход успешен! Добро пожаловать, {firstName}!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                                        // Создаем объект профиля пользователя
                                        var userProfile = new UserProfile
                                        {
                                            UserId = userId,
                                            FirstName = firstName,
                                            LastName = lastName,
                                            Phone = "+38 (050) 244 75 49",
                                            Email = email,
                                            Balance = balance // Устанавливаем баланс
                                        };

                                        // Проверяем роль пользователя
                                        if (role == "Seller")
                                        {
                                            SellerWindow sellerWindow = new SellerWindow(userId);
                                            sellerWindow.Show();
                                            this.Close();
                                        }
                                        else
                                        {
                                            MainWindow mainWindow = new MainWindow(userProfile);
                                            mainWindow.Show();
                                            this.Close();
                                        }
                                    }
                                    else
                                    {
                                        MessageBox.Show("Неверный пароль.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                                    }
                                }
                                else
                                {
                                    MessageBox.Show($"Пользователь с именем '{username}' не найден или заблокирован.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Произошла ошибка при входе: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                // Режим регистрации
                string firstName = FirstNameTextBox?.Text?.Trim();
                string lastName = LastNameTextBox?.Text?.Trim();
                string email = EmailTextBox?.Text?.Trim();
                string password = PasswordBoxRegister?.Password;
                string confirmPassword = ConfirmPasswordBox?.Password;
                string username = firstName; // Используем имя как username

                // Проверка на пустые поля
                if (FirstNameTextBox == null || LastNameTextBox == null || EmailTextBox == null || PasswordBoxRegister == null || ConfirmPasswordBox == null)
                {
                    MessageBox.Show("Ошибка: одно из полей формы регистрации не инициализировано. Проверьте XAML.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (string.IsNullOrWhiteSpace(firstName))
                {
                    MessageBox.Show("Пожалуйста, введите имя.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (string.IsNullOrWhiteSpace(email))
                {
                    MessageBox.Show("Пожалуйста, введите почту.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (string.IsNullOrWhiteSpace(password))
                {
                    MessageBox.Show("Пожалуйста, введите пароль.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (string.IsNullOrWhiteSpace(confirmPassword))
                {
                    MessageBox.Show("Пожалуйста, повторите пароль.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Проверка совпадения паролей
                if (password != confirmPassword)
                {
                    MessageBox.Show("Пароли не совпадают.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                try
                {
                    using (var connection = new NpgsqlConnection(connectionString))
                    {
                        connection.Open();

                        // Проверяем, существует ли пользователь с таким Username
                        using (var checkCommand = new NpgsqlCommand("SELECT COUNT(*) FROM UserCredentials WHERE Username = @username", connection))
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
                        using (var checkEmailCommand = new NpgsqlCommand("SELECT COUNT(*) FROM UserDetails WHERE Email = @email", connection))
                        {
                            checkEmailCommand.Parameters.AddWithValue("email", email);
                            long emailCount = (long)checkEmailCommand.ExecuteScalar();
                            if (emailCount > 0)
                            {
                                MessageBox.Show("Пользователь с таким email уже существует. Пожалуйста, используйте другой email.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                        }

                        // Начинаем транзакцию
                        using (var transaction = connection.BeginTransaction())
                        {
                            try
                            {
                                // 1. Вставляем данные в таблицу UserDetails с начальным балансом только для роли Buyer
                                int userId;
                                decimal initialBalance = 0;
                                if (string.IsNullOrWhiteSpace(lastName) || lastName == "Не вказане") // Предполагаем, что Seller или Admin могут не указывать LastName
                                {
                                    initialBalance = 0;
                                }
                                else
                                {
                                    initialBalance = 1000000m; // Начальный баланс только для Buyer
                                }
                                using (var command = new NpgsqlCommand("INSERT INTO UserDetails (FirstName, LastName, Email, Balance) VALUES (@firstName, @lastName, @email, @balance) RETURNING UserId", connection))
                                {
                                    command.Parameters.AddWithValue("firstName", firstName);
                                    command.Parameters.AddWithValue("lastName", string.IsNullOrWhiteSpace(lastName) ? (object)DBNull.Value : lastName);
                                    command.Parameters.AddWithValue("email", email);
                                    command.Parameters.AddWithValue("balance", initialBalance);
                                    command.Transaction = transaction;

                                    userId = (int)command.ExecuteScalar();
                                }

                                // 2. Вставляем данные в таблицу UserCredentials
                                string role = "Buyer"; // По умолчанию покупатель
                                if (string.IsNullOrWhiteSpace(lastName) || lastName == "Не вказане")
                                {
                                    role = "Seller"; // Если LastName не указано, предполагаем Seller
                                }
                                using (var command = new NpgsqlCommand("INSERT INTO UserCredentials (UserId, Username, Password, Role) VALUES (@userId, @username, @password, @role)", connection))
                                {
                                    command.Parameters.AddWithValue("userId", userId);
                                    command.Parameters.AddWithValue("username", username);
                                    command.Parameters.AddWithValue("password", BCrypt.Net.BCrypt.HashPassword(password));
                                    command.Parameters.AddWithValue("role", role);
                                    command.Transaction = transaction;

                                    command.ExecuteNonQuery();
                                }

                                // Подтверждаем транзакцию
                                transaction.Commit();

                                string message = "Регистрация успешна! Теперь вы можете войти.";
                                if (role == "Buyer")
                                {
                                    message += "";
                                }
                                MessageBox.Show(message, "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                                // Переключаемся в режим входа и очищаем поля
                                if (LoginRadioButton != null)
                                {
                                    LoginRadioButton.IsChecked = true;
                                }
                                FirstNameTextBox.Text = "";
                                LastNameTextBox.Text = "";
                                EmailTextBox.Text = "";
                                PasswordBoxRegister.Password = "";
                                ConfirmPasswordBox.Password = "";
                                if (UsernameTextBox != null) UsernameTextBox.Text = firstName; // Заполняем поле входа именем пользователя
                            }
                            catch (Exception ex)
                            {
                                // Откатываем транзакцию
                                transaction.Rollback();
                                MessageBox.Show($"Ошибка при регистрации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                }
                catch (NpgsqlException ex)
                {
                    MessageBox.Show($"Ошибка подключения к базе данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Произошла ошибка при регистрации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            // Этот метод больше не нужен, так как регистрация обрабатывается в ActionButton_Click
            MessageBox.Show("Кнопка регистрации устарела. Используйте ActionButton_Click.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}