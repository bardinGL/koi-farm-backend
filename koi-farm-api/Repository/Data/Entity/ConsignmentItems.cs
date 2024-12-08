using Repository.Data.Entity.Enum;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Repository.Data.Entity
{
    [Table("ConsignmentItem")]
    public class ConsignmentItems : Entity
    {
        public string Name { get; set; }
        public decimal Fee { get; set; }
        public string Status { get; set; }
        public string ConsignmentId { get; set; }
        [ForeignKey(nameof(ConsignmentId))]
        public Consignment Consignment { get; set; }

        public string ProductItemId { get; set; }
        [ForeignKey(nameof(ProductItemId))]
        public ProductItem ProductItem { get; set; }
        public ProductItemTypeEnum? ConsignmentItemType { get; set; }
    }
}
