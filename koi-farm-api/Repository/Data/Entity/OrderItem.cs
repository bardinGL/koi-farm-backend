using Repository.Data.Entity;
using System.ComponentModel.DataAnnotations.Schema;

[Table("OrderItem")]
public class OrderItem : Entity
{
    public string OrderID { get; set; } // Foreign key for Order
    public int Quantity { get; set; }

    [ForeignKey(nameof(OrderID))]
    public Order Order { get; set; }

    // Link to ProductItem
    public string? ProductItemId { get; set; }
    [ForeignKey(nameof(ProductItemId))]
    public ProductItem? ProductItem { get; set; }

    // Link to ConsignmentItem if applicable
    public string? ConsignmentItemId { get; set; }
    [ForeignKey(nameof(ConsignmentItemId))]
    public ConsignmentItems? ConsignmentItem { get; set; }
}
