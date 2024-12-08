namespace Repository.Model.Consignment
{
    public class UpdateConsignmentItemRequestModel
    {
        public string? Name { get; set; }
        public string? Status { get; set; }
        public decimal? Fee { get; set; }

        public UpdateProductItemRequestModel? ProductItemUpdates { get; set; }
    }

    public class UpdateProductItemRequestModel
    {
        public string? Name { get; set; }
        public decimal? Price { get; set; }
        public int? Quantity { get; set; }
        public string? Type { get; set; }
        public string? ImageUrl { get; set; }
    }
}
