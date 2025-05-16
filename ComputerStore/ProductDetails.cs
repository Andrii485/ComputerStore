namespace ElmirClone
{
    internal class ProductDetails2
    {
        public int ProductId { get; internal set; }
        public string Name { get; internal set; }
        public string Description { get; internal set; }
        public decimal Price { get; internal set; }
        public string Brand { get; internal set; }
        public string CategoryName { get; internal set; }
        public string SubcategoryName { get; internal set; }
        public string ImageUrl { get; internal set; }
        public bool IsHidden { get; internal set; }
        public object ReviewCount { get; internal set; }
        public int StockQuantity { get; internal set; }
        public string StoreName { get; internal set; }
        public string StoreDescription { get; internal set; }
        public double Rating { get; internal set; }
    }
}