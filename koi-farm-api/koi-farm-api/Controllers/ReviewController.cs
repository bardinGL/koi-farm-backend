using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Repository.Repository;

namespace koi_farm_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReviewController : ControllerBase
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public ReviewController(UnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        private string GetUserIdFromClaims()
        {
            return User.FindFirst("UserID")?.Value;
        }

        [HttpGet("get-all-reviews")]
        public IActionResult GetAllReviews()
        {
            var reviews = _unitOfWork.ReviewRepository.GetAll();

            if (!reviews.Any())
            {
                return NotFound(new ResponseModel
                {
                    StatusCode = 404,
                    MessageError = "No reviews found."
                });
            }

            var responseReviews = _mapper.Map<List<ResponseReviewModel>>(reviews);

            return Ok(new ResponseModel
            {
                StatusCode = 200,
                Data = responseReviews
            });
        }

        [HttpGet("get-review/{id}")]
        public IActionResult GetReview(string id)
        {
            var review = _unitOfWork.ReviewRepository.GetById(id);

            if (review == null)
            {
                return NotFound(new ResponseModel
                {
                    StatusCode = 404,
                    MessageError = "Review not found."
                });
            }

            return Ok(new ResponseModel
            {
                StatusCode = 200,
                Data = review
            });
        }
    }
}
