using Repository.Data.Entity;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

[Table("Order")]
public class Order : Entity
{
    public decimal Total { get; set; }
    public string Status { get; set; } // e.g., "Pending", "Completed"

    // Foreign key for the user who placed the order
    public string UserId { get; set; }
    [ForeignKey("UserId")]
    public User User { get; set; }

    // Foreign key for the staff who processed the order
    public string? StaffId { get; set; }
    [ForeignKey("StaffId")]
    public User? Staff { get; set; }

    public string? PromotionId { get; set; }
    [ForeignKey(nameof(PromotionId))]
    public Promotion? Promotion { get; set; }

    [MaxLength(200)]
    public string? Address { get; set; } // Default to user's address

    public bool? IsDelivered { get; set; }

    // Navigation property for OrderItems
    public ICollection<OrderItem> Items { get; set; }
}
