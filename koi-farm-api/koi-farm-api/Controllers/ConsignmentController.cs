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
            // Fetch consignments with their related items using includeProperties
            var consignments = _unitOfWork.ConsignmentRepository
                .Get(includeProperties: c => c.Items)
                .ToList();

            // Check if there are no consignments
            if (!consignments.Any())
            {
                return NotFound(new ResponseModel
                {
                    StatusCode = StatusCodes.Status404NotFound,
                    MessageError = "No consignments found."
                });
            }

            // Format the response
            var response = consignments.Select(consignment => new
            {
                ConsignmentId = consignment.Id,
                UserId = consignment.UserId,
                ContractDate = consignment.CreatedTime,
                Items = consignment.Items.Select(item => new
                {
                    ConsignmentItemId = item.Id,
                    ConsignmentItemType = item.ProductItem?.ProductItemType?.ToString() ?? "Unknown",
                    ConsignmentItemStatus = item.Status
                }).ToList()
            }).ToList();

            return Ok(new ResponseModel
            {
                StatusCode = StatusCodes.Status200OK,
                Data = response
            });
        }



        [HttpGet("get-consignment-items-by-productitemtype/{productitemtype}")]
        public IActionResult GetConsignmentitemsByProductItemsType(string productitemtype)
        {
            // Parse the provided productitemtype into the enum
            if (!Enum.TryParse(productitemtype, true, out ProductItemTypeEnum productItemTypeEnum))
            {
                return BadRequest(new ResponseModel
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    MessageError = "Invalid product item type provided."
                });
            }

            // Fetch consignment items filtered by ProductItemType with related ProductItem included
            var consignmentItems = _unitOfWork.ConsignmentItemRepository
                .Get(c => c.ProductItem.ProductItemType == productItemTypeEnum,
                     includeProperties: ci => ci.ProductItem)
                .ToList();

            // Check if there are no matching consignment items
            if (!consignmentItems.Any())
            {
                return NotFound(new ResponseModel
                {
                    StatusCode = StatusCodes.Status404NotFound,
                    MessageError = "No consignment items found for the specified product item type."
                });
            }

            // Format the response
            var response = consignmentItems.Select(item => new
            {
                ConsignmentItemId = item.Id,
                ProductItemName = item.ProductItem?.Name ?? "Unknown",
                ProductItemType = item.ProductItem?.ProductItemType?.ToString() ?? "Unknown",
                ConsignmentItemStatus = item.Status,
                Fee = item.Fee
            }).ToList();

            return Ok(new ResponseModel
            {
                StatusCode = StatusCodes.Status200OK,
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
                    StatusCode = StatusCodes.Status401Unauthorized,
                    MessageError = "Unauthorized: UserID is missing from token."
                });
            }

            // Fetch consignment items for the user, including related ProductItem and Consignment
            var userConsignmentItems = _unitOfWork.ConsignmentItemRepository
                .Get(
                    ci => ci.Consignment.UserId == userId,
                    ci => ci.ProductItem,
                    ci => ci.Consignment
                )
                .ToList();

            // Check if there are no matching consignment items
            if (!userConsignmentItems.Any())
            {
                return NotFound(new ResponseModel
                {
                    StatusCode = StatusCodes.Status404NotFound,
                    MessageError = "No consignment items found for the current user."
                });
            }

            // Format the response
            var response = userConsignmentItems.Select(item => new
            {
                ConsignmentItemId = item.Id,
                ProductItemName = item.ProductItem?.Name ?? "Unknown",
                ProductItemType = item.ProductItem?.ProductItemType?.ToString() ?? "Unknown",
                ConsignmentItemStatus = item.Status,
                Fee = item.Fee,
                CreatedTime = item.CreatedTime
            }).ToList();

            return Ok(new ResponseModel
            {
                StatusCode = StatusCodes.Status200OK,
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

            // Determine product type
            var productType = salePrice.HasValue && salePrice.Value > 0
                ? ProductItemTypeEnum.ShopUser
                : ProductItemTypeEnum.Healthcare;

            // Calculate price and fee based on product type
            decimal productPrice = 0;
            decimal fee = 0;

            if (productType == ProductItemTypeEnum.ShopUser)
            {
                productPrice = salePrice.Value * 1.15m; // 115% of salePrice
                fee = salePrice.Value * 0.15m; // 15% of salePrice
            }
            else if (productType == ProductItemTypeEnum.Healthcare)
            {
                productPrice = 0; // Healthcare items have no price
                fee = 25000; // Fixed fee for Healthcare items
            }

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
                Fee = fee,
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


        [HttpPost("checkout-healthcare/{productItemId}")]
        public IActionResult CheckoutHealthcare(string productItemId)
        {
            // Validate ProductItemId
            if (string.IsNullOrEmpty(productItemId))
            {
                return BadRequest(new ResponseModel
                {
                    StatusCode = 400,
                    MessageError = "ProductItemId cannot be null or empty."
                });
            }

            // Retrieve ProductItem
            var productItem = _unitOfWork.ProductItemRepository.GetById(productItemId);
            if (productItem == null || productItem.ProductItemType != ProductItemTypeEnum.Healthcare)
            {
                return BadRequest(new ResponseModel
                {
                    StatusCode = 400,
                    MessageError = "Invalid ProductItem. Ensure it exists and is of type 'Healthcare'."
                });
            }

            // Retrieve associated ConsignmentItem
            var consignmentItem = _unitOfWork.ConsignmentItemRepository
                .Get(ci => ci.ProductItemId == productItemId)
                .FirstOrDefault();

            if (consignmentItem == null || consignmentItem.Fee <= 0)
            {
                return NotFound(new ResponseModel
                {
                    StatusCode = 404,
                    MessageError = "Valid ConsignmentItem for the specified ProductItem not found."
                });
            }

            // Calculate total based on Fee and number of days
            var currentTime = DateTime.UtcNow;
            var createdTime = consignmentItem.CreatedTime.UtcDateTime;
            var totalDays = Math.Ceiling((currentTime - createdTime).TotalHours / 24);
            var total = consignmentItem.Fee * (decimal)totalDays;

            // Retrieve user and address
            var userId = HttpContext.User.FindFirst("UserID")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new ResponseModel
                {
                    StatusCode = 401,
                    MessageError = "Unauthorized: UserID is missing from token."
                });
            }

            var user = _unitOfWork.UserRepository.GetById(userId);
            if (user == null || string.IsNullOrEmpty(user.Address))
            {
                return NotFound(new ResponseModel
                {
                    StatusCode = 404,
                    MessageError = "User not found or missing address."
                });
            }

            // Create the Order
            var order = new Order
            {
                UserId = userId,
                Total = total,
                Status = "Pending",
                Address = user.Address,
                ConsignmentId = consignmentItem.Id, // Link to the specific ConsignmentItem
                Items = new List<OrderItem>
        {
            new OrderItem
            {
                ProductItemId = productItemId,
                Quantity = 1,
                ConsignmentItemId = consignmentItem.Id // Link the OrderItem to the ConsignmentItem
            }
        }
            };

            _unitOfWork.OrderRepository.Create(order);

            // Save changes
            try
            {
                _unitOfWork.SaveChange();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ResponseModel
                {
                    StatusCode = 500,
                    MessageError = $"Failed to create the order: {ex.Message}"
                });
            }

            // Send confirmation email
            var emailContent = new StringBuilder();
            emailContent.AppendLine($"<p>Dear {user.Name},</p>");
            emailContent.AppendLine($"<p>Your Healthcare checkout for the product <strong>{productItem.Name}</strong> has been successfully completed.</p>");
            emailContent.AppendLine($"<p>Total fee for {totalDays} day(s): <strong>{total:C}</strong></p>");
            emailContent.AppendLine("<p>Thank you for using our service!</p>");
            emailContent.AppendLine("<p>Best regards,</p>");
            emailContent.AppendLine("<p>KoiShop</p>");

            var emailModel = new SendMailModel
            {
                ReceiveAddress = user.Email,
                Title = "Healthcare Checkout Confirmation",
                Content = emailContent.ToString()
            };

            try
            {
                _emailService.SendMail(emailModel);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ResponseModel
                {
                    StatusCode = 500,
                    MessageError = $"Failed to send confirmation email: {ex.Message}"
                });
            }

            // Return successful response
            return Ok(new ResponseModel
            {
                StatusCode = 201,
                Data = new
                {
                    OrderId = order.Id,
                    Total = order.Total,
                    Days = totalDays,
                    ProductItemName = productItem.Name,
                    UserName = user.Name,
                    Address = user.Address
                }
            });
        }



    }
}
