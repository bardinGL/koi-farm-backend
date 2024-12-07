using Microsoft.AspNetCore.Mvc;
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
                ConsingmentId = consignment.Id,
                UserId = consignment.UserId,
                ContractDate = consignment.CreatedTime,
                Items = consignment.Items.Select(item => new
                {
                    ConsignmentItemId = item.Id,
                    ConsignmentItemType = item.ProductItem.ProductItemType,
                    ConsignmentItemStatus = item.Status,
                }).ToList()
                ConsignmentItemType = item.ProductItem.ProductItemType,
            })

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
    

        [HttpPut("create-consignment/{saleprice}")]
        public IActionResult CreateConsignment(Decimal saleprice ,[FromBody]CreateConsignmentItemRequestModel createModel)
        {
            if (createModel == null)
            {
                return BadRequest(new ResponseModel
                {
                    StatusCode = 400,
                    MessageError = "Invalid ProductItem data. Every field is required."
                });
            }

            var productExists = _unitOfWork.CategoryRepository.GetById(createModel.CategoryId);
            if (productExists == null)
            {
                return BadRequest(new ResponseModel
                {
                    StatusCode = 400,
                    MessageError = "Invalid CategoryId. Product does not exist."
                });
            }


        }

    }
}
