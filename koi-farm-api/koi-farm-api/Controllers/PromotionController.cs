using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Repository.Repository;

namespace koi_farm_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PromotionController : ControllerBase
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public PromotionController(UnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        [HttpGet("get-all-promotion")]
        public IActionResult GetAllPromotions()
        {
            var promotions = _unitOfWork.PromotionRepository.GetAll();

            if (!promotions.Any())
            {
                return NotFound(new ResponseModel
                {
                    StatusCode = 404,
                    MessageError = "No promotions found."
                });
            }

            var responsePromotions = _mapper.Map<List<ResponsePromotionModel>>(promotions);

            return Ok(new ResponseModel
            {
                StatusCode = 200,
                Data = responsePromotions
            });
        }

        [HttpGet("get-promotion-by-code/{code}")]
        public IActionResult GetPromotionByCode(string code)
        {
            var promotion = _unitOfWork.PromotionRepository.GetAll().Where(p => p.Code == code);

            if (!promotion.Any())
            {
                return NotFound(new ResponseModel
                {
                    StatusCode = 404,
                    MessageError = "No promotions found."
                });
            }

            return Ok(new ResponseModel
            {
                StatusCode = 200,
                Data = promotion
            });
        }
    }
}
