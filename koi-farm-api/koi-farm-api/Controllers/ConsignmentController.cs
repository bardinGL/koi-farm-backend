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


        [HttpPost("notify-seller/{productItemId}")]
        public IActionResult NotifySeller(string productItemId)
        {
            // Validate the ProductItemId
            if (string.IsNullOrEmpty(productItemId))
            {
                return BadRequest(new ResponseModel
                {
                    StatusCode = 400,
                    MessageError = "ProductItemId cannot be null or empty."
                });
            }

            // Retrieve the ProductItem
            var productItem = _unitOfWork.ProductItemRepository.GetById(productItemId);
            if (productItem == null)
            {
                return NotFound(new ResponseModel
                {
                    StatusCode = 404,
                    MessageError = $"ProductItem with ID {productItemId} not found."
                });
            }

            // Check if ProductItemType is ShopUser
            if (productItem.ProductItemType != ProductItemTypeEnum.ShopUser)
            {
                return BadRequest(new ResponseModel
                {
                    StatusCode = 400,
                    MessageError = $"ProductItem with ID {productItemId} is not of type 'ShopUser'."
                });
            }

            // Retrieve the ConsignmentItem associated with this ProductItem
            var consignmentItem = _unitOfWork.ConsignmentItemRepository
                .Get(ci => ci.ProductItemId == productItemId)
                .FirstOrDefault();

            if (consignmentItem == null)
            {
                return NotFound(new ResponseModel
                {
                    StatusCode = 404,
                    MessageError = $"ConsignmentItem associated with ProductItem ID {productItemId} not found."
                });
            }

            // Retrieve the Consignment associated with this ConsignmentItem
            var consignment = _unitOfWork.ConsignmentRepository
                .Get(c => c.Id == consignmentItem.ConsignmentId)
                .FirstOrDefault();

            if (consignment == null)
            {
                return NotFound(new ResponseModel
                {
                    StatusCode = 404,
                    MessageError = $"Consignment associated with ConsignmentItem ID {consignmentItem.Id} not found."
                });
            }

            // Retrieve the User associated with this Consignment
            var user = _unitOfWork.UserRepository.GetById(consignment.UserId);
            if (user == null)
            {
                return NotFound(new ResponseModel
                {
                    StatusCode = 404,
                    MessageError = $"User associated with Consignment ID {consignment.Id} not found."
                });
            }

            // Create email content
            var emailContent = new StringBuilder();
            emailContent.AppendLine($"<p>Dear {user.Name},</p>");
            emailContent.AppendLine($"<p>Your product, <strong>{productItem.Name}</strong>, has been sold through our system.</p>");
            emailContent.AppendLine("<p>You can now retrieve the funds from your account.</p>");
            emailContent.AppendLine("<br>");
            emailContent.AppendLine("<p>Thank you for using our platform!</p>");
            emailContent.AppendLine("<p>Best regards,</p>");
            emailContent.AppendLine("<p>KoiShop</p>");

            // Create and send email
            var emailModel = new SendMailModel
            {
                ReceiveAddress = user.Email,
                Title = "Your Product Has Been Sold!",
                Content = emailContent.ToString()
            };

            try
            {
                _emailService.SendMail(emailModel);
            }
            catch (Exception ex)
            {
                // Log exception
                return StatusCode(500, new ResponseModel
                {
                    StatusCode = 500,
                    MessageError = $"Failed to send email notification: {ex.Message}"
                });
            }

            return Ok(new ResponseModel
            {
                StatusCode = 200,
                Data = $"Notification email sent successfully to {user.Email}."
            });
        }


    }
}
