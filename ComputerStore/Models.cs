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
    }

    public class Category
    {
        public int CategoryId { get; set; }
        public string Name { get; set; }
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
        public string ImageUrl { get; set; }
        public double Rating { get; set; }
        public int Reviews { get; set; }
    }

    public class Order
    {
        public int OrderId { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice { get; set; }
        public DateTime OrderDate { get; set; }
        public string Status { get; set; }
    }

    public class Sale
    {
        public int OrderId { get; set; }
        public string ProductName { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal SellerRevenue { get; set; }
        public DateTime OrderDate { get; set; }
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
}