using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using Microsoft.OpenApi.Models;
using System.Reflection;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Security.Claims;
using Repository.Helper;
using Repository.Repository;
using Repository.Helper.AutoMapper;
using Repository.Data;
using Repository.PaymentService;
using Microsoft.Extensions.Configuration;
using Repository.EmailService;
using Repository.Model.Email;
using Repository.ForgotPasswordService;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddControllers().AddJsonOptions(x =>
    x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

// Add Directory Browser
builder.Services.AddDirectoryBrowser();

// Add HTTP Context Accessor
builder.Services.AddHttpContextAccessor();

// Register Memory Cache
builder.Services.AddMemoryCache();

// Add Swagger for API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "KoiFarm", Version = "v1" });

    // Define the security scheme, without requiring the "Bearer " prefix
    c.AddSecurityDefinition("JWT", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Enter the token directly without the 'Bearer ' prefix.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "JWT"
                }
            },
            new string[] {}
        }
    });
});

// Add CORS Policy
builder.Services.AddCors(opts =>
{
    opts.AddPolicy("corspolicy", build =>
    {
        build.WithOrigins("*").AllowAnyMethod().AllowAnyHeader();
    });
});

// Configure DB Context for SQL Server
builder.Services.AddDbContext<KoiFarmDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("KoiFarm"));
});

// Add AutoMapper
builder.Services.AddAutoMapper(typeof(MappingProfiles));

// Register Custom Services
builder.Services.AddScoped<UnitOfWork>();
builder.Services.AddScoped<GenerateToken>();
builder.Services.AddScoped<IVnPayService, VnPayService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddSingleton<ITokenService, TokenService>();

// Configure Email Settings
IConfiguration configuration = builder.Configuration;
EmailSettingModel.Instance = configuration.GetSection("EmailSettings").Get<EmailSettingModel>();

// Configure JWT Authentication
builder.Services.AddAuthentication("Bearer").AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidAudience = builder.Configuration["JWT:ValidAudience"],
        ValidIssuer = builder.Configuration["JWT:ValidIssuer"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JWT:Secret"])),
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine("Authentication failed: " + context.Exception.Message);
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            var claimsIdentity = context.Principal.Identity as ClaimsIdentity;
            if (claimsIdentity != null)
            {
                Console.WriteLine("Token validated for user: " + claimsIdentity.FindFirst("username")?.Value);
            }
            return Task.CompletedTask;
        },
        OnMessageReceived = context =>
        {
            var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(token) && !token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                context.Token = token;
            }
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            Console.WriteLine("OnChallenge error: " + context.Error);
            Console.WriteLine("OnChallenge error description: " + context.ErrorDescription);
            return Task.CompletedTask;
        }
    };
});

// Build the application
var app = builder.Build();

// Apply pending migrations automatically on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<KoiFarmDbContext>();
    dbContext.Database.Migrate();
}

// Add Cross-Origin Headers for Security
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("Cross-Origin-Opener-Policy", "same-origin");
    context.Response.Headers.Add("Cross-Origin-Embedder-Policy", "require-corp");
    await next();
});

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("corspolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
