using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ElmirClone.Models;

namespace ElmirClone
{
    public partial class CartWindow : Window
    {
        private List<DbProduct> cartItems;
        private UserProfile userProfile;

        public CartWindow(List<DbProduct> cartItems, UserProfile userProfile)
        {
            InitializeComponent();
            this.cartItems = cartItems;
            this.userProfile = userProfile;
            CartItemsGrid.ItemsSource = cartItems;
        }

        private void RemoveFromCart_Click(object sender, RoutedEventArgs e)
        {
            int productId = (int)((Button)sender).Tag;
            var productToRemove = cartItems.FirstOrDefault(p => p.ProductId == productId);
            if (productToRemove != null)
            {
                cartItems.Remove(productToRemove);
                CartItemsGrid.ItemsSource = null;
                CartItemsGrid.ItemsSource = cartItems;
                MessageBox.Show($"{productToRemove.Name} удален из корзины.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void PlaceOrder_Click(object sender, RoutedEventArgs e)
        {
            if (cartItems == null || cartItems.Count == 0)
            {
                MessageBox.Show("Корзина пуста. Добавьте товары перед оформлением заказа.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            OrderWindow orderWindow = new OrderWindow(cartItems, userProfile);
            orderWindow.ShowDialog();
            this.Close(); // Закрываем окно корзины
        }
    }
}