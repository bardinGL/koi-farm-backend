using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

        [HttpPost("refresh")]
        public IActionResult RefreshToken([FromBody] string refreshToken)
        {
            if (string.IsNullOrEmpty(refreshToken))
                return BadRequest("Refresh token is required");

            try
            {
                var storedToken = _unitOfWork.UserRefreshTokenRepository.GetSingle(t => t.RefreshToken == refreshToken && !t.isUsed);
                if (storedToken == null || storedToken.ExpireTime < DateTime.Now)
                    return Unauthorized("Invalid or expired refresh token.");

                storedToken.isUsed = true;
                _unitOfWork.UserRefreshTokenRepository.Update(storedToken);

                var user = _unitOfWork.UserRepository.GetById(storedToken.User_Id);
                var newToken = _generateToken.GenerateTokenModel(user);

                return Ok(newToken);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }
        [HttpPost("google-signin")]
        public async Task<IActionResult> GoogleSignIn([FromBody] string idToken)
        {
            try
            {
                if (string.IsNullOrEmpty(idToken) || idToken.Split('.').Length != 3)
                {
                    return BadRequest("Invalid idToken format.");
                }

                var payload = await GoogleJsonWebSignature.ValidateAsync(idToken);
                var user = _unitOfWork.UserRepository.GetSingle(u => u.Email == payload.Email,
                    includeProperties: q => q.Role);

                if (user == null)
                {
                    user = new User
                    {
                        Name = payload.Name,
                        Email = payload.Email,
                        Password = "123456",
                        RoleId = "0"
                    };
                    _unitOfWork.UserRepository.Create(user);
                    user = _unitOfWork.UserRepository.GetSingle(u => u.Email == payload.Email, includeProperties: q => q.Role);
                }

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
                return StatusCode(500, "Internal server error: " + ex.InnerException);
            }
        }
    }
}
