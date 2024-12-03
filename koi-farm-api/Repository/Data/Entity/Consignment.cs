using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Repository.Data.Entity
{
    [Table("Consignment")]
    public class Consignment : Entity
    {
        public string UserId { get; set; }


        [ForeignKey(nameof(UserId))]
        public User User { get; set; }

        public ICollection<ConsignmentItems> Items { get; set; }
    }
}
