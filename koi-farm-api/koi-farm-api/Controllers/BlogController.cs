using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Repository.Data.Entity;
using Repository.Model;
using Repository.Model.Blog;
using Repository.Repository;
using System.Reflection.Metadata;

namespace koi_farm_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BlogController : ControllerBase
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public BlogController(UnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        private string GetUserIdFromClaims()
        {
            return User.FindFirst("UserID")?.Value;
        }

        [HttpGet("get-all-blogs")]
        public IActionResult GetAllBlogs()
        {
            var blogs = _unitOfWork.BlogRepository.GetAll();

            if (!blogs.Any())
            {
                return NotFound(new ResponseModel
                {
                    StatusCode = 404,
                    MessageError = "No blogs found."
                });
            }

            var responseBlogs = _mapper.Map<List<ResponseBlogModel>>(blogs);

            return Ok(new ResponseModel
            {
                StatusCode = 200,
                Data = responseBlogs
            });
        }

        [HttpGet("get-blog/{id}")]
        public IActionResult GetBlog(string id)
        {
            var blog = _unitOfWork.BlogRepository.GetById(id);
            if (blog == null)
            {
                return NotFound(new ResponseModel
                {
                    StatusCode = 404,
                    MessageError = "Blog not found."
                });
            }

            var responseBlog = _mapper.Map<ResponseBlogModel>(blog);
            return Ok(new ResponseModel
            {
                StatusCode = 200,
                Data = responseBlog
            });
        }

        [Authorize(Roles = "Manager,Staff")]
        [HttpPost("create-blog")]
        public IActionResult CreateBlog([FromBody] RequestCreateBlogModel blogModel)
        {
            if (blogModel == null || string.IsNullOrEmpty(blogModel.Title) || string.IsNullOrEmpty(blogModel.Description))
            {
                return BadRequest(new ResponseModel
                {
                    StatusCode = 400,
                    MessageError = "Invalid blog data. Title and Description are required."
                });
            }

            // Get the UserId from claims
            var userId = GetUserIdFromClaims();

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new ResponseModel
                {
                    StatusCode = 401,
                    MessageError = "Unauthorized: UserId not found."
                });
            }

            var blog = _mapper.Map<Blog>(blogModel);
            blog.UserId = userId;

            _unitOfWork.BlogRepository.Create(blog);

            return Ok(new ResponseModel
            {
                StatusCode = 201,
                Data = blog
            });
        }
    }
}
