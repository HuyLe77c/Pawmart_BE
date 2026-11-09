using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Pawmart.Data;
using Pawmart.Middleware;
using Pawmart.Security;
using Pawmart.Services;
using Pawmart.Services.Integrations;

var builder = WebApplication.CreateBuilder(args);
Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "uploads", "products"));
Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "uploads", "pets"));

builder.Services.AddControllers();
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 21L * 1024 * 1024);
builder.Services.AddProblemDetails();
builder.Services.AddDbContext<PawmartDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("PetFoodDb")));

var jwtKey = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(jwtKey))
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("Configure Jwt:SigningKey using an environment variable or secret store before starting outside Development.");
    }

    jwtKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    Console.Error.WriteLine("WARNING: Jwt:SigningKey is not configured. A temporary Development-only key was generated; tokens expire when the process restarts.");
}
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException("Jwt:SigningKey must contain at least 32 UTF-8 bytes.");
}

var issuer = builder.Configuration["Jwt:Issuer"] ?? "Pawmart.Api";
var audience = builder.Configuration["Jwt:Audience"] ?? "Pawmart.Client";
var tokenMinutes = int.TryParse(builder.Configuration["Jwt:AccessTokenMinutes"], out var configuredMinutes)
    ? Math.Clamp(configuredMinutes, 5, 1440)
    : 60;
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
builder.Services.AddSingleton(new JwtTokenService(signingKey, issuer, audience, tokenMinutes));
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<ProductImageStorageService>();
builder.Services.AddScoped<PetImageStorageService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<PetService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<CheckoutService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<OrderReviewService>();
builder.Services.AddScoped<PromotionService>();
builder.Services.AddScoped<StoreService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<AdminReportService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<BankQrService>();
builder.Services.AddScoped<SePayWebhookService>();
builder.Services.AddScoped<RecommendationService>();
builder.Services.AddScoped<ChatMessageService>();
builder.Services.AddHttpClient<OmnirouteClient>(client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddSingleton<IPaymentGateway, UnconfiguredPaymentGateway>();
builder.Services.AddSingleton<IShippingCalculator, UnconfiguredShippingCalculator>();
builder.Services.AddSingleton<IPushNotificationSender, UnconfiguredPushNotificationSender>();
builder.Services.AddScoped<IAiRecommendationService, OmnirouteRecommendationService>();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("customer-chat", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier) ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(new { title = "Too many requests", detail = "Bạn gửi tin nhắn quá nhanh. Vui lòng thử lại sau ít phút." }, ct);
    };
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.Admin, policy => policy.RequireRole("ADMIN"));
    options.AddPolicy(AuthorizationPolicies.Customer, policy => policy.RequireRole("CUSTOMER"));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Pawmart API", Version = "v1", Description = "API cửa hàng thức ăn thú cưng." });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập JWT access token."
    });
    options.OperationFilter<SwaggerAuthorizeOperationFilter>();
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseStatusCodePages(async (StatusCodeContext statusContext) =>
{
    var context = statusContext.HttpContext;
    var status = context.Response.StatusCode;
    var title = status switch
    {
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Not found",
        StatusCodes.Status405MethodNotAllowed => "Method not allowed",
        _ => "Request failed"
    };
    var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
    {
        Status = status,
        Title = title,
        Detail = "The request could not be completed.",
        Instance = context.Request.Path
    };
    problem.Extensions["traceId"] = context.TraceIdentifier;
    context.Response.ContentType = "application/problem+json";
    await System.Text.Json.JsonSerializer.SerializeAsync(context.Response.Body, problem, cancellationToken: context.RequestAborted);
});
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Pawmart API v1"));
}

app.UseStaticFiles();
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous().WithTags("Health");
app.MapGet("/health/ready", async (PawmartDbContext db, CancellationToken ct) =>
{
    try
    {
        return await db.Database.CanConnectAsync(ct)
            ? Results.Ok(new { status = "ready", database = "available" })
            : Results.Problem(title: "Not ready", detail: "The database is unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
        return Results.Problem(title: "Not ready", detail: "The database is unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).AllowAnonymous().WithTags("Health");
app.MapControllers();

app.Run();
