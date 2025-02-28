using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace MyApp.Application.Purchase
{
    public class PurchaseRequest
    {
        public int CustomerId { get; set; }
        public List<Product> Products { get; set; }
        public int PaymentMethod { get; set; }
    }

    public class Product
    {
        public int ProductId { get; set; }

        public int Quantity { get; set; }
    }
}
