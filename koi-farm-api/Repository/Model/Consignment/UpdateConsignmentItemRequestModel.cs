namespace Repository.Model.Consignment
{
    public class UpdateConsignmentItemRequestModel
    {
        public string? Name { get; set; }
        public string? Category { get; set; }
        public string? Origin { get; set; }
        public string? Sex { get; set; }
        public int? Age { get; set; }
        public string? Size { get; set; }
        public string? Species { get; set; }
        public string? Status { get; set; }
        public string? ImageUrl { get; set; }
        public string? Personality { get; set; }
        public string? FoodAmount { get; set; }
        public string? WaterTemp { get; set; }
        public string? MineralContent { get; set; }
        public string? PH { get; set; }
        public string? Type { get; set; }
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
