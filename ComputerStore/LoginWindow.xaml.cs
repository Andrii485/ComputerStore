using System.Windows;
using System.Windows.Controls;

namespace ElmirClone
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameTextBox.Text;
            string password = PasswordBox.Password;

            // Простая проверка (замените на реальную логику аутентификации)
            if (username == "admin" && password == "password")
            {
                // Открываем основное окно магазина
                MainWindow mainWindow = new MainWindow();
                mainWindow.Show();

                // Закрываем окно входа
                this.Close();
            }
            else
            {
                MessageBox.Show("Неверное имя пользователя или пароль.", "Ошибка входа", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}