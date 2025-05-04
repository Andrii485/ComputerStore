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
        private DispatcherTimer resetPasswordTimer; // Таймер для відліку 20 секунд
        private string connectionString;

        public LoginWindow()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка в InitializeComponent: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }

            // Ініціалізація рядка підключення
            connectionString = ConfigurationManager.ConnectionStrings["ElitePCConnection"]?.ConnectionString;
            if (string.IsNullOrEmpty(connectionString))
            {
                MessageBox.Show("Рядок підключення до бази даних не знайдено.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            // Ініціалізація таймера
            resetPasswordTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(20) // Встановлюємо інтервал 20 секунд
            };
            resetPasswordTimer.Tick += ResetPasswordTimer_Tick; // Обробник події таймера

            // Перевіряємо, що таймер ініціалізовано
            if (resetPasswordTimer == null)
            {
                MessageBox.Show("resetPasswordTimer не було ініціалізовано в конструкторі!", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            // Встановлюємо початковий стан після ініціалізації таймера
            if (LoginRadioButton != null)
            {
                LoginRadioButton.IsChecked = true; // Встановлюємо режим "Вхід" за замовчуванням
            }
            else
            {
                MessageBox.Show("LoginRadioButton є null. Перевірте XAML.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoginRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            isLoginMode = true;
            isResetPasswordMode = false;

            // Перевіряємо таймер перед використанням
            if (resetPasswordTimer == null)
            {
                MessageBox.Show("resetPasswordTimer є null у LoginRadioButton_Checked!", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            resetPasswordTimer.Stop(); // Зупиняємо таймер при поверненні в режим входу

            if (TitleTextBlock != null)
            {
                TitleTextBlock.Text = "Вхід";
            }
            else
            {
                MessageBox.Show("TitleTextBlock є null. Перевірте XAML.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            if (LoginFields != null && RegisterFieldsScrollViewer != null && ResetPasswordFields != null)
            {
                LoginFields.Visibility = Visibility.Visible;
                RegisterFieldsScrollViewer.Visibility = Visibility.Collapsed;
                ResetPasswordFields.Visibility = Visibility.Collapsed;
            }
            else
            {
                MessageBox.Show("Один із елементів (LoginFields, RegisterFieldsScrollViewer, ResetPasswordFields) є null. Перевірте XAML.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RegisterRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            isLoginMode = false;
            isResetPasswordMode = false;

            // Перевіряємо таймер перед використанням
            if (resetPasswordTimer == null)
            {
                MessageBox.Show("resetPasswordTimer є null у RegisterRadioButton_Checked!", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            resetPasswordTimer.Stop(); // Зупиняємо таймер при переході в режим реєстрації

            if (TitleTextBlock != null)
            {
                TitleTextBlock.Text = "Реєстрація";
            }
            else
            {
                MessageBox.Show("TitleTextBlock є null. Перевірте XAML.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            if (LoginFields != null && RegisterFieldsScrollViewer != null && ResetPasswordFields != null)
            {
                LoginFields.Visibility = Visibility.Collapsed;
                RegisterFieldsScrollViewer.Visibility = Visibility.Visible;
                ResetPasswordFields.Visibility = Visibility.Collapsed;
            }
            else
            {
                MessageBox.Show("Один із елементів (LoginFields, RegisterFieldsScrollViewer, ResetPasswordFields) є null. Перевірте XAML.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetPasswordButton_Click(object sender, RoutedEventArgs e)
        {
            isResetPasswordMode = true;

            // Перевірка TitleTextBlock
            if (TitleTextBlock == null)
            {
                MessageBox.Show("TitleTextBlock є null. Перевірте x:Name у XAML.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            TitleTextBlock.Text = "Скидання пароля";

            // Перевіряємо наявність усіх елементів
            if (LoginFields == null)
            {
                MessageBox.Show("LoginFields є null. Перевірте x:Name у XAML.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (RegisterFieldsScrollViewer == null)
            {
                MessageBox.Show("RegisterFieldsScrollViewer є null. Перевірте x:Name у XAML.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (ResetPasswordFields == null)
            {
                MessageBox.Show("ResetPasswordFields є null. Перевірте x:Name у XAML.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Якщо всі елементи знайдені, виконуємо перемикання видимості
            LoginFields.Visibility = Visibility.Collapsed;
            RegisterFieldsScrollViewer.Visibility = Visibility.Collapsed;
            ResetPasswordFields.Visibility = Visibility.Visible;

            // Перевіряємо таймер перед використанням
            if (resetPasswordTimer == null)
            {
                MessageBox.Show("resetPasswordTimer є null у ResetPasswordButton_Click!", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            resetPasswordTimer.Stop(); // Зупиняємо, якщо вже був запущений
            resetPasswordTimer.Start();
        }

        private void ResetPasswordTimer_Tick(object sender, EventArgs e)
        {
            if (resetPasswordTimer == null)
            {
                MessageBox.Show("resetPasswordTimer є null у ResetPasswordTimer_Tick!", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            resetPasswordTimer.Stop(); // Зупиняємо таймер

            // Перевіряємо, чи заповнені поля
            bool isAnyFieldFilled =
                !string.IsNullOrWhiteSpace(EmailResetTextBox?.Text) ||
                !string.IsNullOrWhiteSpace(NewPasswordBox?.Password) ||
                !string.IsNullOrWhiteSpace(ConfirmNewPasswordBox?.Password);

            // Якщо поля заповнені або не заповнені, у будь-якому випадку переводимо в режим входу
            if (isResetPasswordMode)
            {
                if (isAnyFieldFilled)
                {
                    MessageBox.Show("Час очікування минув. Будь ласка, спробуйте ще раз.", "Інформація", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                // Переводимо в режим входу
                if (LoginRadioButton != null)
                {
                    LoginRadioButton.IsChecked = true;
                }
                // Очищаємо поля скидання пароля
                if (EmailResetTextBox != null) EmailResetTextBox.Text = "";
                if (NewPasswordBox != null) NewPasswordBox.Password = "";
                if (ConfirmNewPasswordBox != null) ConfirmNewPasswordBox.Password = "";
            }
        }

        private void ConfirmResetButton_Click(object sender, RoutedEventArgs e)
        {
            // Отримуємо дані з полів
            string email = EmailResetTextBox?.Text?.Trim();
            string newPassword = NewPasswordBox?.Password;
            string confirmNewPassword = ConfirmNewPasswordBox?.Password;

            // Перевірка на порожні поля
            if (string.IsNullOrWhiteSpace(email))
            {
                MessageBox.Show("Будь ласка, введіть вашу пошту.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(newPassword))
            {
                MessageBox.Show("Будь ласка, введіть новий пароль.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(confirmNewPassword))
            {
                MessageBox.Show("Будь ласка, повторіть новий пароль.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Перевірка збігу паролів
            if (newPassword != confirmNewPassword)
            {
                MessageBox.Show("Паролі не збігаються.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    // Перевіряємо, чи існує користувач з вказаною поштою
                    int userId;
                    using (var command = new NpgsqlCommand("SELECT UserId FROM UserDetails WHERE Email = @email", connection))
                    {
                        command.Parameters.AddWithValue("email", email);
                        var result = command.ExecuteScalar();
                        if (result == null)
                        {
                            MessageBox.Show("Користувача з вказаною поштою не знайдено.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                        userId = (int)result;
                    }

                    // Хешуємо новий пароль
                    string hashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);

                    // Оновлюємо пароль у таблиці UserCredentials
                    using (var command = new NpgsqlCommand("UPDATE UserCredentials SET Password = @password WHERE UserId = @userId", connection))
                    {
                        command.Parameters.AddWithValue("password", hashedPassword);
                        command.Parameters.AddWithValue("userId", userId);
                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            MessageBox.Show("Пароль успішно скинуто! Тепер ви можете увійти з новим паролем.", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);

                            // Зупиняємо таймер
                            if (resetPasswordTimer == null)
                            {
                                MessageBox.Show("resetPasswordTimer є null у ConfirmResetButton_Click!", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                            resetPasswordTimer.Stop();

                            // Перемикаємося назад у режим входу
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
                            MessageBox.Show("Не вдалося скинути пароль. Будь ласка, спробуйте ще раз.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Сталася помилка: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (isLoginMode)
            {
                // Режим входу
                string username = UsernameTextBox?.Text?.Trim();
                string password = PasswordBox?.Password;

                // Перевірка на порожні поля
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    MessageBox.Show("Будь ласка, заповніть усі поля для входу.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                try
                {
                    using (var connection = new NpgsqlConnection(connectionString))
                    {
                        connection.Open();

                        // 1. Перевіряємо, чи є користувач адміністратором (таблиця AdminCredentials)
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
                                        MessageBox.Show($"Вхід успішний! Ласкаво просимо, адміністраторе {username}!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);

                                        // Відкриваємо вікно адміністратора
                                        AdminWindow adminWindow = new AdminWindow();
                                        adminWindow.Show();
                                        this.Close();
                                        return; // Виходимо з методу, оскільки адміністратор успішно увійшов
                                    }
                                    else
                                    {
                                        MessageBox.Show("Невірний пароль.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                                        return;
                                    }
                                }
                            }
                        }

                        // 2. Якщо користувач не адміністратор, перевіряємо в таблиці UserCredentials
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
                                    string lastName = userReader.IsDBNull(4) ? "Не вказано" : userReader.GetString(4);
                                    string email = userReader.GetString(5);
                                    decimal balance = userReader.IsDBNull(6) ? 0 : userReader.GetDecimal(6);

                                    if (BCrypt.Net.BCrypt.Verify(password, hashedPassword))
                                    {
                                        MessageBox.Show($"Вхід успішний! Ласкаво просимо, {firstName}!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);

                                        // Створюємо об'єкт профілю користувача
                                        var userProfile = new UserProfile
                                        {
                                            UserId = userId,
                                            FirstName = firstName,
                                            LastName = lastName,
                                            Phone = "+38 (050) 244 75 49",
                                            Email = email,
                                            Balance = balance // Встановлюємо баланс
                                        };

                                        // Перевіряємо роль користувача
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
                                        MessageBox.Show("Невірний пароль.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                                    }
                                }
                                else
                                {
                                    MessageBox.Show($"Користувача з ім'ям '{username}' не знайдено або він заблокований.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Сталася помилка при вході: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                // Режим реєстрації
                string firstName = FirstNameTextBox?.Text?.Trim();
                string lastName = LastNameTextBox?.Text?.Trim();
                string email = EmailTextBox?.Text?.Trim();
                string password = PasswordBoxRegister?.Password;
                string confirmPassword = ConfirmPasswordBox?.Password;
                string username = firstName; // Використовуємо ім'я як username

                // Перевірка на порожні поля
                if (FirstNameTextBox == null || LastNameTextBox == null || EmailTextBox == null || PasswordBoxRegister == null || ConfirmPasswordBox == null)
                {
                    MessageBox.Show("Помилка: одне з полів форми реєстрації не ініціалізовано. Перевірте XAML.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (string.IsNullOrWhiteSpace(firstName))
                {
                    MessageBox.Show("Будь ласка, введіть ім'я.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (string.IsNullOrWhiteSpace(email))
                {
                    MessageBox.Show("Будь ласка, введіть пошту.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (string.IsNullOrWhiteSpace(password))
                {
                    MessageBox.Show("Будь ласка, введіть пароль.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (string.IsNullOrWhiteSpace(confirmPassword))
                {
                    MessageBox.Show("Будь ласка, повторіть пароль.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Перевірка збігу паролів
                if (password != confirmPassword)
                {
                    MessageBox.Show("Паролі не збігаються.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                try
                {
                    using (var connection = new NpgsqlConnection(connectionString))
                    {
                        connection.Open();

                        // Перевіряємо, чи існує користувач з таким Username
                        using (var checkCommand = new NpgsqlCommand("SELECT COUNT(*) FROM UserCredentials WHERE Username = @username", connection))
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
                        using (var checkEmailCommand = new NpgsqlCommand("SELECT COUNT(*) FROM UserDetails WHERE Email = @email", connection))
                        {
                            checkEmailCommand.Parameters.AddWithValue("email", email);
                            long emailCount = (long)checkEmailCommand.ExecuteScalar();
                            if (emailCount > 0)
                            {
                                MessageBox.Show("Користувач з такою поштою уже існує. Будь ласка, використовуйте іншу пошту.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                        }

                        // Починаємо транзакцію
                        using (var transaction = connection.BeginTransaction())
                        {
                            try
                            {
                                // 1. Вставляємо дані в таблицю UserDetails з початковим балансом лише для ролі Buyer
                                int userId;
                                decimal initialBalance = 0;
                                if (string.IsNullOrWhiteSpace(lastName) || lastName == "Не вказано") // Припускаємо, що Seller або Admin можуть не вказувати LastName
                                {
                                    initialBalance = 0;
                                }
                                else
                                {
                                    initialBalance = 1000000m; // Початковий баланс лише для Buyer
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

                                // 2. Вставляємо дані в таблицю UserCredentials
                                string role = "Buyer"; // За замовчуванням покупець
                                if (string.IsNullOrWhiteSpace(lastName) || lastName == "Не вказано")
                                {
                                    role = "Seller"; // Якщо LastName не вказано, припускаємо Seller
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

                                // Підтверджуємо транзакцію
                                transaction.Commit();

                                string message = "Реєстрація успішна! Тепер ви можете увійти.";
                                if (role == "Buyer")
                                {
                                    message += "";
                                }
                                MessageBox.Show(message, "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);

                                // Перемикаємося в режим входу та очищаємо поля
                                if (LoginRadioButton != null)
                                {
                                    LoginRadioButton.IsChecked = true;
                                }
                                FirstNameTextBox.Text = "";
                                LastNameTextBox.Text = "";
                                EmailTextBox.Text = "";
                                PasswordBoxRegister.Password = "";
                                ConfirmPasswordBox.Password = "";
                                if (UsernameTextBox != null) UsernameTextBox.Text = firstName; // Заповнюємо поле входу ім'ям користувача
                            }
                            catch (Exception ex)
                            {
                                // Відкотимо транзакцію
                                transaction.Rollback();
                                MessageBox.Show($"Помилка при реєстрації: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                }
                catch (NpgsqlException ex)
                {
                    MessageBox.Show($"Помилка підключення до бази даних: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Сталася помилка при реєстрації: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            // Цей метод більше не потрібен, оскільки реєстрація обробляється в ActionButton_Click
            MessageBox.Show("Кнопка реєстрації застаріла. Використовуйте ActionButton_Click.", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}