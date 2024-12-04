using Microsoft.AspNetCore.Mvc;
using Repository.Model;
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

    }
}
