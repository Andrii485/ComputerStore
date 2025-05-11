namespace ElmirClone
{
    internal class DbProduct2
    {
        public int ProductId { get; internal set; }
        public string Name { get; internal set; }
        public string Description { get; internal set; }
        public decimal Price { get; internal set; }
        public string Brand { get; internal set; }
        public string CategoryName { get; internal set; }
        public string SubcategoryName { get; internal set; }
        public bool IsHidden { get; internal set; }
        public string? ImageUrl { get; internal set; }
    }
}