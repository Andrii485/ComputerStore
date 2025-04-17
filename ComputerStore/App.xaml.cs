using System.Windows;

namespace ElmirClone
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Запускаем окно входа
            LoginWindow loginWindow = new LoginWindow();
            loginWindow.Show();
        }
    }
}