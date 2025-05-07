using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ElmirClone
{
    public class DbProduct2
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
    }
   
}