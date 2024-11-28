using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Repository.Helper;
using Repository.Model;
using Repository.Model.Auth;
using Repository.Model.User;
using Repository.Repository;

namespace koi_farm_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly GenerateToken _generateToken;

        public AuthController(
            UnitOfWork unitOfWork,
            GenerateToken generateToken,
            IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _generateToken = generateToken;
        }

        [HttpPost("signin")]
        public IActionResult SignIn([FromBody] SignInModel signInModel)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var user = _unitOfWork.UserRepository.GetSingle(
                    u => u.Email == signInModel.Email,
                    includeProperties: q => q.Role
                );
                if (user == null || user.Password != signInModel.Password)
                    return Unauthorized("Invalid credentials.");

                var token = _generateToken.GenerateTokenModel(user);
                return Ok(new ResponseModel
                {
                    StatusCode = StatusCodes.Status200OK,
                    Data = new ResponseTokenModel
                    {
                        Token = token.Token,
                        RefreshToken = token.RefreshToken,
                        User = new ResponseUserModel
                        {
                            Name = user.Name,
                            Email = user.Email,
                            Address = user.Address,
                            Phone = user.Phone,
                            RoleId = user.RoleId
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }
    }
}
