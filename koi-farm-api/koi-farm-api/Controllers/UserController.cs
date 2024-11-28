using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Repository.Repository;

namespace koi_farm_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ITokenService _tokenService;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;


        public UserController(UnitOfWork unitOfWork, IMapper mapper, ITokenService tokenService, IServiceProvider serviceProvider, IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _tokenService = tokenService;
            _emailService = serviceProvider.GetRequiredService<IEmailService>();
            _configuration = configuration;
        }

        [HttpGet("get-all-users")]
        //[Authorize]
        public IActionResult GetAllUsers()
        {
            var users = _unitOfWork.UserRepository.GetAll();

            if (!users.Any())
            {
                return NotFound(new ResponseModel
                {
                    StatusCode = 404,
                    MessageError = "No users found."
                });
            }

            var responseUsers = _mapper.Map<List<ResponseUserModel>>(users);

            return Ok(new ResponseModel
            {
                StatusCode = 200,
                Data = responseUsers
            });
        }
        [HttpGet("get-users-by-role/{role}")]
        //[Authorize]
        public IActionResult GetUsersByRole(string role, int pageIndex = 1, int pageSize = 10)
        {
            if (string.IsNullOrEmpty(role))
            {
                return BadRequest(new ResponseModel
                {
                    StatusCode = 400,
                    MessageError = "Role cannot be null or empty."
                });
            }

            var users = _unitOfWork.UserRepository.GetAll().Where(u => u.RoleId.Equals(role));

            if (!users.Any())
            {
                return NotFound(new ResponseModel
                {
                    StatusCode = 404,
                    MessageError = $"No users found with role: {role}."
                });
            }

            var totalItems = users.Count();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var pagedUsers = users
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var responseUsers = _mapper.Map<List<ResponseUserModel>>(pagedUsers);

            var responseSearchModel = new ResponseSearchModel<ResponseUserModel>
            {
                PageIndex = pageIndex,
                PageSize = pageSize,
                TotalPages = totalPages,
                TotalItems = totalItems,
                Entities = responseUsers
            };

            return Ok(new ResponseModel
            {
                StatusCode = 200,
                Data = responseSearchModel
            });
        }
        [Authorize(Roles = "Manager")]
        [HttpPost("create-user-staff")]
        public IActionResult CreateUserStaff([FromBody] RequestCreateUserModel responseCreateUser)
        {
            if (responseCreateUser == null || string.IsNullOrEmpty(responseCreateUser.Email) || string.IsNullOrEmpty(responseCreateUser.Password))
            {
                return BadRequest(new ResponseModel
                {
                    StatusCode = 400,
                    MessageError = "Invalid user data. Email and password are required."
                });
            }

            var existingUser = _unitOfWork.UserRepository.GetSingle(u => u.Email == responseCreateUser.Email);
            if (existingUser != null)
            {
                return Conflict(new ResponseModel
                {
                    StatusCode = 409,
                    MessageError = "Email already exists."
                });
            }

            var user = _mapper.Map<User>(responseCreateUser);

            user.RoleId = "2";

            _unitOfWork.UserRepository.Create(user);

            return Ok(new ResponseModel
            {
                StatusCode = 201,
                Data = user
            });
        }
        [Authorize(Roles = "Manager")]
        [HttpPut("update-user/{id}")]
        public IActionResult UpdateUser(string id, [FromBody] RequestCreateUserModel updateUserModel)
        {
            if (string.IsNullOrEmpty(id) || updateUserModel == null)
            {
                return BadRequest(new ResponseModel
                {
                    StatusCode = 400,
                    MessageError = "UserId and user data cannot be null or empty."
                });
            }

            var user = _unitOfWork.UserRepository.GetById(id);

            if (user == null)
            {
                return NotFound(new ResponseModel
                {
                    StatusCode = 404,
                    MessageError = $"User with Id: {id} not found."
                });
            }

            _mapper.Map(updateUserModel, user);

            _unitOfWork.UserRepository.Update(user);

            return Ok(new ResponseModel
            {
                StatusCode = 200,
                Data = user
            });
        }

    }
}
