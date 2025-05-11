using System;

namespace ElmirClone.Models
{
    public class UserProfile
    {
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Binding { get; set; }
        public int UserId { get; internal set; }
        public object Balance { get; internal set; }
        public string? LastName { get; internal set; }
    }

    public class Category
    {
        public int CategoryId { get; set; }
        public string Name { get; set; }
        public int? ParentCategoryId { get; set; } // Добавлено для поддержки иерархии
        public string ImageUrl { get; set; }
        public string ParentCategoryName { get; internal set; }
    }

    public class Subcategory
    {
        public int SubcategoryId { get; set; }
        public string Name { get; set; }
        public int CategoryId { get; set; } // Ссылка на родительскую категорию
    }

    public class DbProduct
    {
        public int ProductId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string Brand { get; set; }
        public decimal Discount { get; set; }
        public string CategoryName { get; set; }
        public string SubcategoryName { get; set; }
        public string ImageUrl { get; set; }
        public double Rating { get; set; }
        public int Reviews { get; set; }
        public decimal Quantity { get; internal set; }
        public decimal StockQuantity { get; internal set; }
        public string StoreName { get; internal set; }
        public string StoreDescription { get; internal set; }
        public int ReviewCount { get; internal set; }
        public decimal TotalPrice { get; internal set; }
    }

    public class Order
    {
        public int OrderId { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice { get; set; }
        public DateTime OrderDate { get; set; }
        public string Status { get; set; }
        public string ContactLastName { get; set; }
        public string ContactFirstName { get; set; }
        public string ContactMiddleName { get; set; }
        public string ContactPhone { get; set; }
        public string ShippingRegion { get; set; }
        public string PickupPointAddress { get; set; }
        public int ProductId { get; set; }
    }

    public class Sale
    {
        public int OrderId { get; set; }
        public string ProductName { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal SellerRevenue { get; set; }
        public DateTime OrderDate { get; set; }
        public int ProductId { get; internal set; }
    }

    public class PickupPoint
    {
        public int PickupPointId { get; set; }
        public string Address { get; set; }
        public string Region { get; set; }
    }

    public class PaymentMethod
    {
        public int PaymentMethodId { get; set; }
        public string Name { get; set; }
    }

    public class User
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public bool IsBlocked { get; set; }
    }

    public class CourierService
    {
        public int ServiceId { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }
    }
}