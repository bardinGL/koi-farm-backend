using Microsoft.AspNetCore.Mvc;
using Repository.Data.Entity.Enum;
using Repository.Data.Entity;
using Repository.Model;
using Repository.Model.Consignment;
using Repository.Repository;

namespace koi_farm_api.Controllers
{
    [ApiController]
    [Route("api/controller")]
    public class ConsignmentController : ControllerBase
    {
        private readonly UnitOfWork _unitOfWork;
        public ConsignmentController(UnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [HttpGet("get-all-consignments")]
        public IActionResult GetAllConsignments()
        {
            var consignments = _unitOfWork.ConsignmentRepository.GetAll().ToList();
            if(!consignments.Any())
            {
                return NotFound(new ResponseModel
                {
                    StatusCode = 404,
                    MessageError = "No Consignment was found"
                });

            }
            var response = consignments.Select(consignment => new
            {
                ConsingmentId = consignment.Id,
                UserId = consignment.UserId,
                ContractDate = consignment.CreatedTime,
                Items = consignment.Items.Select(item => new
                {
                    ConsignmentItemId = item.Id,
                    ConsignmentItemO = item.
                    ConsignmentItemType = item.ProductItem.ProductItemType, 
                    ConsignmentItemStatus = item.Status,
                }).ToList()

            }).ToList();
            return Ok(new ResponseModel
            {
                StatusCode = 200,
                Data = response
            });
        }

        [HttpGet("get-consignment-items-by-productitemtype/{productitemtype}")]
        public IActionResult GetConsignmentitemsByProductItemsType(string productitemtype)
        {
            var consignments = _unitOfWork.ConsignmentItemRepository.Get(c => c.ProductItem.ProductItemType.Equals(productitemtype)).ToList();
            if (!consignments.Any())
            {
                return NotFound(new ResponseModel
                {
                    StatusCode = 404,
                    MessageError = "There is no Consignment Items of that type"
                });
            }
            var response = consignments.Select(item => new
            {
                ConsignmentItemId = item.Id,
                ProductItems = item.ProductItem,
                ConsignmentItemStatus = item.Status
            }).ToList();
            return Ok(new ResponseModel
            {
                StatusCode = 200,
                Data = response
            });
        }

        [HttpGet("get-user-consignment-items")]
        public IActionResult GetUserConsignmentItems()
        {
            // Extract UserID from token claims
            var userId = HttpContext.User.FindFirst("UserID")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new ResponseModel
                {
                    StatusCode = 401,
                    MessageError = "Unauthorized: UserID is missing from token."
                });
            }

            // Query the database for consignment items created by the user
            var userConsignmentItems = _unitOfWork.ConsignmentItemRepository
                .Get(ci => ci.Consignment.UserId == userId)
                .ToList();

            // Check if the result is empty
            if (!userConsignmentItems.Any())
            {
                return NotFound(new ResponseModel
                {
                    StatusCode = 404,
                    MessageError = "No consignment items found for the current user."
                });
            }

            // Format the response
            var response = userConsignmentItems.Select(item => new
            {
                ConsignmentItemId = item.Id,
                ProductItemName = item.ProductItem.Name,
                ProductItemType = item.ProductItem.ProductItemType,
                ConsignmentItemStatus = item.Status,
                Fee = item.Fee,
                CreatedTime = item.CreatedTime
            }).ToList();

            return Ok(new ResponseModel
            {
                StatusCode = 200,
                Data = response
            });
        }


        [HttpPost("create-productitem")]
        public IActionResult CreateProductItem([FromBody] CreateConsignmentItemRequestModel createModel, decimal? salePrice = null)
        {
            // Extract UserID from token claims
            var userId = HttpContext.User.FindFirst("UserID")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new ResponseModel
                {
                    StatusCode = 401,
                    MessageError = "Unauthorized: UserID is missing from token."
                });
            }

            // Check if user exists
            var user = _unitOfWork.UserRepository.Get(u => u.Id == userId).FirstOrDefault();
            if (user == null)
            {
                return NotFound(new ResponseModel
                {
                    StatusCode = 404,
                    MessageError = "User not found."
                });
            }

            // Check if consignment already exists for the user
            var consignment = _unitOfWork.ConsignmentRepository.Get(c => c.UserId == userId).FirstOrDefault();
            if (consignment == null)
            {
                consignment = new Consignment
                {
                    UserId = userId,
                    Items = new List<ConsignmentItems>()
                };
                _unitOfWork.ConsignmentRepository.Create(consignment);
            }

            // Determine product type and properties
            var productType = salePrice.HasValue && salePrice.Value > 0
                ? ProductItemTypeEnum.ShopUser
                : ProductItemTypeEnum.Healthcare;

            var productItem = new ProductItem
            {
                Name = createModel.Name,
                Price = salePrice ?? 0, // 0 for healthcare
                Origin = createModel.Origin,
                Sex = createModel.Sex,
                Age = createModel.Age,
                Size = createModel.Size,
                Species = createModel.Species,
                Personality = createModel.Personality,
                FoodAmount = createModel.FoodAmount,
                WaterTemp = createModel.WaterTemp,
                MineralContent = createModel.MineralContent,
                PH = createModel.PH,
                ImageUrl = createModel.ImageUrl,
                Quantity = createModel.Quantity,
                Type = createModel.Type,
                ProductItemType = productType
            };
            _unitOfWork.ProductItemRepository.Create(productItem);

            // Create consignment item and link to consignment
            var consignmentItem = new ConsignmentItems
            {
                Name = productItem.Name,
                Fee = 25000, // Fee is 25,000
                Status = "Pending",
                ProductItemId = productItem.Id,
                ConsignmentId = consignment.Id
            };
            consignment.Items.Add(consignmentItem);
            _unitOfWork.ConsignmentItemRepository.Create(consignmentItem);

            // Save changes
            _unitOfWork.SaveChange();

            // Format response
            var response = new
            {
                ProductItemId = productItem.Id,
                ProductItemName = productItem.Name,
                ProductItemType = productType,
                Fee = consignmentItem.Fee,
                ConsignmentId = consignment.Id,
                ConsignmentItemId = consignmentItem.Id
            };

            return Ok(new ResponseModel
            {
                StatusCode = 201,
                Data = response
            });
        }

    }
}
