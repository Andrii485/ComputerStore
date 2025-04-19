using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ElmirClone
{
    public partial class MainWindow : Window
    {
        private bool isProfilePanelOpen = false;
        private UserProfile userProfile; // Данные пользователя

        public MainWindow(UserProfile userProfile)
        {
            InitializeComponent();
            this.userProfile = userProfile ?? new UserProfile(); // Инициализация профиля, если null, создаем пустой
            LoadAdditionalProducts();
            LoadPopularProducts();
            LoadUserProfile(); // Загружаем данные пользователя в поля
        }

        // Загрузка данных пользователя в поля профиля
        private void LoadUserProfile()
        {
            FirstNameTextBox.Text = userProfile.FirstName;
            BindingTextBlock.Text = ""; // Пример привязки, можно заменить на реальную логику
            MiddleNameTextBox.Text = userProfile.MiddleName;
            PhoneTextBox.Text = userProfile.Phone;
            EmailTextBox.Text = userProfile.Email;
        }

        // Сохранение изменений профиля
        private void SaveProfileButton_Click(object sender, RoutedEventArgs e)
        {
            userProfile.FirstName = FirstNameTextBox.Text;
            userProfile.MiddleName = MiddleNameTextBox.Text;
            userProfile.Phone = PhoneTextBox.Text;
            userProfile.Email = EmailTextBox.Text;

            MessageBox.Show("Профіль успішно збережено!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Очистка поля поиска
        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox.Text == "Поиск...")
            {
                textBox.Text = "";
                textBox.Foreground = System.Windows.Media.Brushes.White;
            }
        }

        // Обработчик клика по логотипу
        private void Logo_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Добро пожаловать в ElitePC Store! Нажмите, чтобы вернуться на главную.", "ElitePC", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Обработчик клика по категориям
        private void CategoryButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button != null)
            {
                MessageBox.Show($"Выбрана категория: {button.Content}", "Категория", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // Обработчик кнопки Заказать
        private void OrderButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Функция заказа пока в разработке.", "Заказ", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Обработчик кнопки Профиль
        private void ProfileButton_Click(object sender, RoutedEventArgs e)
        {
            isProfilePanelOpen = !isProfilePanelOpen;
            ProfilePanel.Visibility = isProfilePanelOpen ? Visibility.Visible : Visibility.Collapsed;
        }

        // Обработчик кнопки Корзина
        private void CartButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Корзина пока в разработке.", "Корзина", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Обработчик кнопки Выход
        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            LoginWindow loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }

        // Вторая сетка товаров
        private void LoadAdditionalProducts()
        {
            var additionalProducts = new List<Product>
            {
                new Product { Name = "Клавиатуры", ImageUrl = "https://content1.rozetka.com.ua/goods/images/big/462458947.jpg" },
                new Product { Name = "Мыши", ImageUrl = "https://content1.rozetka.com.ua/goods/images/big/497934730.jpg" },
                new Product { Name = "Комплект (клавиатура + мышь)", ImageUrl = "https://b.428.ua/img/4092769/600/600/klaviatura_+_mysh_cougar_combat~1258~393.jpg" },
                new Product { Name = "Килимки для мыши", ImageUrl = "https://content2.rozetka.com.ua/goods/images/big/531537999.jpg" },
                new Product { Name = "Звуковые карты", ImageUrl = "https://via.placeholder.com/180x120?text=Звуковые+карты" },
                new Product { Name = "Акустические системы", ImageUrl = "https://via.placeholder.com/180x120?text=Акустика" },
                new Product { Name = "Наушники и гарнитура", ImageUrl = "https://via.placeholder.com/180x120?text=Наушники" },
                new Product { Name = "Аксессуары для наушников", ImageUrl = "https://via.placeholder.com/180x120?text=Аксессуары+наушников" },
                new Product { Name = "Микрофоны", ImageUrl = "https://via.placeholder.com/180x120?text=Микрофоны" },
                new Product { Name = "Кабели, переходники, контроллеры", ImageUrl = "https://via.placeholder.com/180x120?text=Кабели" },
                new Product { Name = "Программное обеспечение", ImageUrl = "https://via.placeholder.com/180x120?text=Программное+обеспечение" },
                new Product { Name = "Флеш память", ImageUrl = "https://via.placeholder.com/180x120?text=Флеш+память" },
                new Product { Name = "Зовнішні жорсткі диски", ImageUrl = "https://via.placeholder.com/180x120?text=Зовнішні+диски" },
                new Product { Name = "Зовнішні SSD", ImageUrl = "https://via.placeholder.com/180x120?text=Зовнішні+SSD" },
                new Product { Name = "Мережеве обладнання", ImageUrl = "https://via.placeholder.com/180x120?text=Мережеве+обладнання" },
                new Product { Name = "Чохли для жорстких дисків", ImageUrl = "https://via.placeholder.com/180x120?text=Чохли" },
                new Product { Name = "Серверне обладнання", ImageUrl = "https://via.placeholder.com/180x120?text=Серверне+обладнання" },
                new Product { Name = "Мережеві диски та подовжувачі (ДБЖ)", ImageUrl = "https://via.placeholder.com/180x120?text=Мережеві+диски" },
                new Product { Name = "Джерела безперебійного живлення (ДБЖ)", ImageUrl = "https://via.placeholder.com/180x120?text=ДБЖ" },
                new Product { Name = "Стабилизаторы напряжения", ImageUrl = "https://via.placeholder.com/180x120?text=Стабилизаторы" },
                new Product { Name = "Зарядные станции", ImageUrl = "https://via.placeholder.com/180x120?text=Зарядные+станции" },
                new Product { Name = "Допоміжне обладнання до ДБЖ", ImageUrl = "https://via.placeholder.com/180x120?text=Допоміжне+до+ДБЖ" },
                new Product { Name = "Приставки смарт-телестоловая", ImageUrl = "https://via.placeholder.com/180x120?text=Приставки" },
                new Product { Name = "Инверторы", ImageUrl = "https://via.placeholder.com/180x120?text=Инверторы" },
                new Product { Name = "Web-камеры", ImageUrl = "https://via.placeholder.com/180x120?text=Web-камеры" },
                new Product { Name = "Картриджы", ImageUrl = "https://via.placeholder.com/180x120?text=Картриджы" },
                new Product { Name = "Графічні планшети (дигитайзеры)", ImageUrl = "https://via.placeholder.com/180x120?text=Графічні+планшети" },
                new Product { Name = "Оптичні приводи", ImageUrl = "https://via.placeholder.com/180x120?text=Оптичні+приводи" },
                new Product { Name = "Диски", ImageUrl = "https://via.placeholder.com/180x120?text=Диски" },
                new Product { Name = "Пристрої відеозахвату", ImageUrl = "https://via.placeholder.com/180x120?text= format Пристрої+відеозахвату" }
            };
            AdditionalProductsGrid.ItemsSource = additionalProducts;
        }

        // Третья сетка товаров (популярные товары)
        private void LoadPopularProducts()
        {
            var popularProducts = new List<Product>
            {
                new Product { Name = "SSD-накопитель 2.5 M.2 1TB Kingston NV2 (SNV2S/1000G)", Price = "2 859 грн", Rating = 5.0, Reviews = 50, ImageUrl = "https://via.placeholder.com/180x120?text=SSD+Kingston" },
                new Product { Name = "Процессор AMD Ryzen 7 5700X3D (AM4, 4.1GHz, 8MB)", Price = "10 999 грн", Rating = 4.9, Reviews = 13, ImageUrl = "https://via.placeholder.com/180x120?text=Процессор+AMD" },
                new Product { Name = "Роутер TP-Link Archer C64", Price = "1 299 грн", Rating = 4.9, Reviews = 41, ImageUrl = "https://via.placeholder.com/180x120?text=Роутер+TP-Link" },
                new Product { Name = "SSD-накопитель 2.5 SATA 1TB Kingston Canvas Select Plus A1 (SNV2S/1000G)", Price = "1 999 грн", Rating = 4.8, Reviews = 8, ImageUrl = "https://via.placeholder.com/180x120?text=SSD+Kingston" },
                new Product { Name = "Видеокарта Asus RX 6700 XT 8GB DC", Price = "15 769 грн", Rating = 4.7, Reviews = 4, ImageUrl = "https://via.placeholder.com/180x120?text=Видеокарта+Asus" },
                new Product { Name = "Видеокарта MSI GeForce RTX 3060 16GB DDR6", Price = "8 999 грн", Rating = 4.8, Reviews = 12, ImageUrl = "https://via.placeholder.com/180x120?text=Видеокарта+MSI" },
                new Product { Name = "Видеокарта Asus RX 6700 XT 8GB DDR6 PRIME", Price = "32 999 грн", Rating = 4.9, Reviews = 44, ImageUrl = "https://via.placeholder.com/180x120?text=Видеокарта+Asus" },
                new Product { Name = "Процессор AMD Ryzen 9 7950X3D (AM5, 5.7GHz, 128MB)", Price = "30 999 грн", Rating = 4.9, Reviews = 10, ImageUrl = "https://via.placeholder.com/180x120?text=Процессор+AMD" },
                new Product { Name = "Компьютер Artline Gaming X43", Price = "0 грн", Rating = 4.5, Reviews = 5, ImageUrl = "https://via.placeholder.com/180x120?text=Компьютер+Artline" },
                new Product { Name = "Мышь Bloody R72 Ultra Renegade Sunset", Price = "1 889 грн", Rating = 4.6, Reviews = 3, ImageUrl = "https://via.placeholder.com/180x120?text=Мышь+Bloody" },
                new Product { Name = "Кабель HDMI -> Optical 2.1 Cab", Price = "4 499 грн", Rating = 4.7, Reviews = 2, ImageUrl = "https://via.placeholder.com/180x120?text=Кабель+HDMI" },
                new Product { Name = "SSD-накопитель 2.5 SATA 1TB Goodram CX400 Gen.2 (SSDPR)", Price = "2 685 грн", Rating = 4.8, Reviews = 6, ImageUrl = "https://via.placeholder.com/180x120?text=SSD+Goodram" },
                new Product { Name = "SSD-накопитель 2.5 SATA 1TB Samsung 870 EVO MZ", Price = "2 519 грн", Rating = 4.9, Reviews = 29, ImageUrl = "https://via.placeholder.com/180x120?text=SSD+Samsung" },
                new Product { Name = "SSD-накопитель 2.5 SATA 256GB Patriot P220", Price = "709 грн", Rating = 4.8, Reviews = 7, ImageUrl = "https://via.placeholder.com/180x120?text=SSD+Patriot" },
                new Product { Name = "Роутер TP-Link Archer C80", Price = "1 699 грн", Rating = 4.9, Reviews = 30, ImageUrl = "https://via.placeholder.com/180x120?text=Роутер+TP-Link" },
                new Product { Name = "Видеокарта Asus RX 6700 XT 8GB DDR6 (DUAL)", Price = "15 769 грн", Rating = 4.7, Reviews = 4, ImageUrl = "https://via.placeholder.com/180x120?text=Видеокарта+Asus" },
                new Product { Name = "Модуль памяти DDR4 16GB 2x8", Price = "1 599 грн", Rating = 4.8, Reviews = 2, ImageUrl = "https://via.placeholder.com/180x120?text=Модуль+памяти" }
            };
            PopularProductsGrid.ItemsSource = popularProducts;
        }
    }

    // Модель товара
    public class Product
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public string ImageUrl { get; set; }
        public string Price { get; set; }
        public double Rating { get; set; }
        public int Reviews { get; set; }
    }

    // Модель профиля пользователя
    public class UserProfile
    {
        public string FirstName { get; set; } = "Наталія"; // Данные по умолчанию из скриншота
        public string Binding { get; set; } = "";
        public string MiddleName { get; set; } = "Не вказане";
        public string Phone { get; set; } = "+38 (050) 256 75 49";
        public string Email { get; set; } = "andrey53bondarenko@gmail.com";
    }
}