using Google.Apis.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Repository.Data.Entity;
using Repository.EmailService;
using Repository.ForgotPasswordService;
using Repository.Helper;
using Repository.Model;
using Repository.Model.Auth;
using Repository.Model.Email;
using Repository.Model.User;
using Repository.Repository;
using System.Runtime.Caching;
using Microsoft.Extensions.Caching.Memory;
using MemoryCache = System.Runtime.Caching.MemoryCache;

namespace koi_farm_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly GenerateToken _generateToken;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ITokenService _tokenService;
        private readonly IMemoryCache _memoryCache;


        public AuthController(
            UnitOfWork unitOfWork,
            GenerateToken generateToken,
            IConfiguration configuration,
        ITokenService tokenService,
            IServiceProvider serviceProvider,
            IMemoryCache memoryCache
            )
        {
            _memoryCache = memoryCache;
            _unitOfWork = unitOfWork;
            _generateToken = generateToken;
            _tokenService = tokenService;
            _emailService = serviceProvider.GetRequiredService<IEmailService>();
            _configuration = configuration;
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
                            RoleId = "User",
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        [HttpGet("verify-email/token={token}")]
        public IActionResult VerifyEmail(string token)
        {
            if (_memoryCache.Get(token) != null)
            {
                string email = (string)_memoryCache.Get(token);
                var user = _unitOfWork.UserRepository.GetSingle(x => x.Email == email, includeProperties: q => q.Role);
                user.Status = "Active";
                _unitOfWork.UserRepository.Update(user);
                _unitOfWork.SaveChange();
                _memoryCache.Remove(token);
                var JWTtoken = _generateToken.GenerateTokenModel(user);
                return Redirect($"https://www.wearefpters.xyz/token={JWTtoken.Token}"); 
            }
            else
            {
                return BadRequest(new ResponseModel
                {
                    StatusCode = 500,
                    MessageError = "EmailisExits"
                });
            }
        }

        [HttpPost("signup-email")]
        public IActionResult SignUpWithEmail([FromBody] SignUpModel signUpModel)
        {
            var user = _unitOfWork.UserRepository.GetAll().FirstOrDefault(x => x.Email == signUpModel.Email);

            if (user != null)
            {
                return BadRequest(new ResponseModel
                {
                    StatusCode = 500,
                    MessageError = "EmailisExist!"
                });
            }

            var newUser = new User();
            newUser.Id = Guid.NewGuid().ToString();
            newUser.Name = signUpModel.Name;
            newUser.Address = signUpModel.Address;
            newUser.Email = signUpModel.Email;
            newUser.Password = signUpModel.Password;
            newUser.Phone = signUpModel.Phone;
            newUser.Status = "Pending";
            newUser.RoleId = signUpModel.RoleId.ToString();
            _unitOfWork.UserRepository.Create(newUser);
            _unitOfWork.SaveChange();
            var token = Guid.NewGuid();
            var frontendUrl = _configuration["FrontEndPort:PaymentUrl"];
            

            // Send reset email
            var resetUrl = $"localhost:44365/api/Auth/verify-email/token={token}";
            _emailService.SendMail(new SendMailModel
            {
                ReceiveAddress = signUpModel.Email,
                Title = "Email Sign Up Success",
                Content = $"Click the link to verify your password: {resetUrl}"
            });
            string cachekey = token.ToString();
            string cachevalue = signUpModel.Email;
            DateTimeOffset exp = DateTimeOffset.Now.AddMinutes(5);
            _memoryCache.Set(cachekey, cachevalue, exp);
            if (_memoryCache.Get(cachekey) == null)
            {
                return BadRequest(new ResponseModel
                {
                    StatusCode = 500,
                    MessageError = "System Fail Please Reload"
                });
            }
            return Ok(new ResponseModel
            {
                StatusCode = 200,
                MessageError = "Successfully Sign Up Email"
            });
            
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
