﻿using System;

namespace Repository.Model.Consignment
{
    public class ConsignmentItemResponseModel
    {
        public string UserId { get; set; }
        public string ItemsId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Origin { get; set; }
        public string Sex { get; set; }
        public int Age { get; set; }
        public string Size { get; set; }
        public string Species { get; set; }
        public string Status { get; set; }
        public string? ImageUrl { get; set; }
        public string Personality { get; set; }
        public string FoodAmount { get; set; }
        public string WaterTemp { get; set; }
        public string MineralContent { get; set; }
        public string PH { get; set; }
        public string Type { get; set; }
    }
}
