using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pawmart.Data;
using Pawmart.Middleware;
using Pawmart.Models.Contracts.Auth;
using Pawmart.Models.Entities;
using Pawmart.Security;

namespace Pawmart.Services;

public sealed class AuthService(PawmartDbContext db, JwtTokenService tokenService, IConfiguration configuration)
{
    private readonly PasswordHasher<User> passwordHasher = new();

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        if (await db.Users.AnyAsync(x => x.Email == email, cancellationToken))
        {
            throw new ApiException(StatusCodes.Status409Conflict, "Email is already registered.", "Email already registered");
        }

        var user = new User
        {
            UserId = Guid.NewGuid().ToString("N"),
            Email = email,
            FullName = request.FullName.Trim(),
            PhoneNumber = NormalizeOptional(request.PhoneNumber),
            Role = "CUSTOMER",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return CreateResult(user);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (user is null || !user.IsActive || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            throw new ApiException(StatusCodes.Status401Unauthorized, "Email or password is incorrect.", "Invalid credentials");
        }

        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = user.LastLoginAt;
        await db.SaveChangesAsync(cancellationToken);
        return CreateResult(user);
    }

    public async Task<AuthResult> GoogleLoginAsync(GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        var clientId = configuration["GoogleAuth:WebClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ApiException(StatusCodes.Status503ServiceUnavailable, "Đăng nhập Google chưa được cấu hình.", "Google sign-in unavailable");

        GoogleJsonWebSignature.Payload identity;
        try
        {
            identity = await GoogleJsonWebSignature.ValidateAsync(request.IdToken,
                new GoogleJsonWebSignature.ValidationSettings { Audience = [clientId] });
        }
        catch (InvalidJwtException)
        {
            throw new ApiException(StatusCodes.Status401Unauthorized, "Không thể xác minh đăng nhập Google.", "Invalid Google token");
        }

        if (string.IsNullOrWhiteSpace(identity.Subject) || identity.EmailVerified != true || string.IsNullOrWhiteSpace(identity.Email))
            throw new ApiException(StatusCodes.Status401Unauthorized, "Email của tài khoản Google chưa được xác minh.", "Invalid Google account");

        var email = NormalizeEmail(identity.Email);
        var user = await db.Users.SingleOrDefaultAsync(x => x.GoogleSubject == identity.Subject, cancellationToken);
        if (user is null)
        {
            if (await db.Users.AnyAsync(x => x.Email == email, cancellationToken))
                throw new ApiException(StatusCodes.Status409Conflict,
                    "Email này đã có tài khoản PawMart. Hãy đăng nhập bằng mật khẩu.", "Account already exists");

            user = new User
            {
                UserId = Guid.NewGuid().ToString("N"),
                Email = email,
                FullName = string.IsNullOrWhiteSpace(identity.Name) ? email.Split('@')[0] : identity.Name.Trim()[..Math.Min(identity.Name.Trim().Length, 100)],
                GoogleSubject = identity.Subject,
                Role = "CUSTOMER",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            user.PasswordHash = passwordHasher.HashPassword(user, Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"));
            db.Users.Add(user);
        }
        else if (!user.IsActive)
        {
            throw new ApiException(StatusCodes.Status403Forbidden, "Tài khoản này đã bị khóa.", "Account disabled");
        }

        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = user.LastLoginAt;
        await db.SaveChangesAsync(cancellationToken);
        return CreateResult(user);
    }

    public async Task<UserResponse> GetProfileAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "User was not found.", "Not found");
        return ToResponse(user);
    }

    public async Task<UserResponse> UpdateProfileAsync(string userId, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "User was not found.", "Not found");
        user.FullName = request.FullName.Trim();
        user.PhoneNumber = NormalizeOptional(request.PhoneNumber);
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(user);
    }

    private AuthResult CreateResult(User user)
    {
        var token = tokenService.CreateToken(user);
        return new AuthResult(token.Token, token.ExpiresAtUtc, ToResponse(user));
    }

    private static UserResponse ToResponse(User user) => new(user.UserId, user.Email, user.FullName, user.PhoneNumber, user.Role);
    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
