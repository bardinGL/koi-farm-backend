using Microsoft.AspNetCore.Mvc;
using Repository.Data.Entity.Enum;
using Repository.Data.Entity;
using Repository.Model;
using Repository.Model.Consignment;
using Repository.Repository;
using Repository.Model.Email;
using System.Text;
using Repository.EmailService;

namespace koi_farm_api.Controllers
{
    [ApiController]
    [Route("api/controller")]
    public class ConsignmentController : ControllerBase
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly IEmailService _emailService;


        public ConsignmentController(UnitOfWork unitOfWork, IServiceProvider serviceProvider)
        {
            _unitOfWork = unitOfWork;
            _emailService = serviceProvider.GetRequiredService<IEmailService>();
        }

        [HttpGet("get-all-consignments")]
        public IActionResult GetAllConsignments()
        {
            var consignments = _unitOfWork.ConsignmentRepository.GetAll().ToList();
            if (!consignments.Any())
            {
                return NotFound(new ResponseModel
                {
                    StatusCode = 404,
                    MessageError = "No Consignment was found"
                });
            }

            var response = consignments.Select(consignment => new
            {
                ConsignmentId = consignment.Id,
                UserId = consignment.UserId,
                ContractDate = consignment.CreatedTime,
                Items = consignment.Items.Select(item => new
                {
                    ConsignmentItemId = item.Id,
                    ConsignmentItemType = item.ProductItem.ProductItemType,
                    ConsignmentItemStatus = item.Status
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
            return Ok();

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

        [HttpPost("create-consignmentitem")]
        public IActionResult CreateConsignmentItem([FromBody] CreateConsignmentItemRequestModel createModel, decimal? salePrice = null)
        {
            // Validate input model
            if (createModel == null)
            {
                return BadRequest(new ResponseModel
                {
                    StatusCode = 400,
                    MessageError = "Invalid input. CreateConsignmentItemRequestModel cannot be null."
                });
            }

            // Validate CategoryId
            if (string.IsNullOrEmpty(createModel.CategoryId))
            {
                return BadRequest(new ResponseModel
                {
                    StatusCode = 400,
                    MessageError = "CategoryId is required."
                });
            }

            var category = _unitOfWork.CategoryRepository.GetById(createModel.CategoryId);
            if (category == null)
            {
                return NotFound(new ResponseModel
                {
                    StatusCode = 404,
                    MessageError = $"Category with ID {createModel.CategoryId} not found."
                });
            }

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
            var user = _unitOfWork.UserRepository.GetById(userId);
            if (user == null)
            {
                return NotFound(new ResponseModel
                {
                    StatusCode = 404,
                    MessageError = "User not found."
                });
            }

            // Get or create consignment for the user
            var consignment = _unitOfWork.ConsignmentRepository.Get(c => c.UserId == userId).FirstOrDefault();
            if (consignment == null)
            {
                consignment = new Consignment
                {
                    UserId = userId,
                    Items = new List<ConsignmentItems>() // Initialize Items to avoid null reference
                };

                _unitOfWork.ConsignmentRepository.Create(consignment);
                _unitOfWork.SaveChange(); // Save to ensure Consignment.Id is generated
            }
            else if (consignment.Items == null)
            {
                consignment.Items = new List<ConsignmentItems>(); // Ensure Items is initialized
            }

            // Determine product type and properties
            var productType = salePrice.HasValue && salePrice.Value > 0
                ? ProductItemTypeEnum.ShopUser
                : ProductItemTypeEnum.Healthcare;

            var productPrice = salePrice ?? 0; // Default price for healthcare
            const decimal defaultFee = 25000; // Default fee for consignment items

            // Create product item
            var productItem = new ProductItem
            {
                Name = createModel.Name,
                Price = productPrice,
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
                ProductItemType = productType,
                CategoryId = createModel.CategoryId,
                Quantity = 1, // Ensure Quantity is always 1
                Type = "Approved" // Ensure Type is always "Approved"
            };

            _unitOfWork.ProductItemRepository.Create(productItem);

            // Create consignment item and link to consignment
            var consignmentItem = new ConsignmentItems
            {
                Name = productItem.Name,
                Fee = defaultFee,
                Status = "Pending",
                ProductItemId = productItem.Id,
                ConsignmentId = consignment.Id,
                ConsignmentItemType = productType // Link the type to the product item
            };

            // Add consignment item to consignment
            consignment.Items.Add(consignmentItem);
            _unitOfWork.ConsignmentItemRepository.Create(consignmentItem);

            // Save changes
            _unitOfWork.SaveChange();

            // Format response
            var response = new
            {
                ProductItemId = productItem.Id,
                ProductItemName = productItem.Name,
                ProductItemType = productType.ToString(),
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




        [HttpPut("update-consignment-item/{consignmentItemId}")]
        public IActionResult UpdateConsignmentItem(string consignmentItemId, [FromBody] UpdateConsignmentItemRequestModel updateModel)
        {
            // Check if consignment item exists
            var consignmentItem = _unitOfWork.ConsignmentItemRepository
                .Get(ci => ci.Id == consignmentItemId)
                .FirstOrDefault();

            if (consignmentItem == null)
            {
                return NotFound(new ResponseModel
                {
                    StatusCode = 404,
                    MessageError = $"Consignment item with ID '{consignmentItemId}' not found."
                });
            }

            // Check if the status is "Pending"
            if (!consignmentItem.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new ResponseModel
                {
                    StatusCode = 400,
                    MessageError = $"Consignment item with ID '{consignmentItemId}' is already '{consignmentItem.Status}' and cannot be updated."
                });
            }

            // Update consignment item properties
            if (!string.IsNullOrEmpty(updateModel.Status))
            {
                consignmentItem.Status = updateModel.Status;
            }

            if (updateModel.Fee.HasValue)
            {
                consignmentItem.Fee = updateModel.Fee.Value;
            }

            if (!string.IsNullOrEmpty(updateModel.Name))
            {
                consignmentItem.Name = updateModel.Name;
            }

            // Update related product item properties if provided
            if (updateModel.ProductItemUpdates != null)
            {
                var productItem = consignmentItem.ProductItem;

                if (productItem != null)
                {
                    if (!string.IsNullOrEmpty(updateModel.ProductItemUpdates.Name))
                    {
                        productItem.Name = updateModel.ProductItemUpdates.Name;
                    }

                    if (updateModel.ProductItemUpdates.Price.HasValue)
                    {
                        productItem.Price = updateModel.ProductItemUpdates.Price.Value;
                    }

                    // Update more fields as needed
                }
            }

            // Save changes
            _unitOfWork.SaveChange();

            return Ok(new ResponseModel
            {
                StatusCode = 200,
                Data = new
                {
                    ConsignmentItemId = consignmentItem.Id,
                    UpdatedFields = updateModel
                }
            });
        }

        [HttpDelete("delete-consignment-item/{id}")]
        public IActionResult DeleteConsignmentItem(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest(new ResponseModel
                {
                    StatusCode = 400,
                    MessageError = "ConsignmentItemId cannot be null or empty."
                });
            }

            // Retrieve the consignment item by its ID
            var consignmentItem = _unitOfWork.ConsignmentItemRepository.GetById(id);

            if (consignmentItem == null)
            {
                return NotFound(new ResponseModel
                {
                    StatusCode = 404,
                    MessageError = $"Consignment item with Id: {id} not found."
                });
            }

            // Delete the consignment item
            _unitOfWork.ConsignmentItemRepository.Delete(consignmentItem);
            _unitOfWork.SaveChange(); // Save changes after deletion

            return Ok(new ResponseModel
            {
                StatusCode = 200,
                Data = $"Consignment item with ID {id} successfully deleted."
            });
        }





    }
}
