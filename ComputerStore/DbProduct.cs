using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ElmirClone
{
    public class DbProduct
    {
        public int ProductId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string Brand { get; set; }
        public string CategoryName { get; set; }
        public string SubcategoryName { get; set; }
        public string ImageUrl { get; set; }
        public double Rating { get; set; }
        public string StoreName { get; set; }
        public string StoreDescription { get; set; }
        public int ReviewCount { get; set; }
        public int Quantity { get; set; }
        public int StockQuantity { get; set; } // Новое свойство для количества на складе
        public bool IsHidden { get; internal set; }
        public int ProductId1 { get; internal set; }
        public string Name1 { get; internal set; }
        public decimal Price1 { get; internal set; }
        public string ImageUrl1 { get; internal set; }
        public double Rating1 { get; internal set; }
        public int ReviewCount1 { get; internal set; }
        public string StoreDescription1 { get; internal set; }
        public string StoreName1 { get; internal set; }
        public string SubcategoryName1 { get; internal set; }
        public string CategoryName1 { get; internal set; }
        public string Brand1 { get; internal set; }
        public string Description1 { get; internal set; }
        public bool IsHidden1 { get; internal set; }
    }
}