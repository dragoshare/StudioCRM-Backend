using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StudioCRM.Application.DTOs.Auth;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Settings;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly StudioCRMDbContext _context;
    private readonly JwtSettings _jwtSettings;
    private readonly PasswordHasher<User> _passwordHasher;

    public AuthService(
        StudioCRMDbContext context,
        IOptions<JwtSettings> jwtOptions)
    {
        _context = context;
        _jwtSettings = jwtOptions.Value;
        _passwordHasher = new PasswordHasher<User>();
    }

    public async Task<CreatedAccountDto> RegisterAsync(RegisterDto request)
    {
        var email = request.Email.Trim();
        var roleName = NormalizeManualRegistrationRole(request.Role);

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("Email is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new InvalidOperationException("Password is required.");
        }

        if (!request.LocationId.HasValue)
        {
            throw new InvalidOperationException("LocationId is required.");
        }

        var existingUser = await _context.Users
            .AnyAsync(u => u.Email == email);

        if (existingUser)
        {
            throw new InvalidOperationException("User with this email already exists.");
        }

        var locationExists = await _context.Locations
            .AnyAsync(l => l.Id == request.LocationId.Value);

        if (!locationExists)
        {
            throw new InvalidOperationException("Location does not exist.");
        }

        var role = await _context.Roles
            .FirstOrDefaultAsync(r => r.Name == roleName);

        if (role is null)
        {
            throw new InvalidOperationException($"{roleName} role does not exist.");
        }

        Trainer? assignedTrainer = null;

        if (roleName == "Client" && request.TrainerId.HasValue)
        {
            assignedTrainer = await _context.Trainers
                .Include(t => t.TrainerLocations)
                .FirstOrDefaultAsync(t => t.Id == request.TrainerId.Value && !t.IsDeleted);

            if (assignedTrainer is null)
            {
                throw new InvalidOperationException("Trainer does not exist.");
            }

            var trainerHasLocation = assignedTrainer.TrainerLocations
                .Any(tl => tl.LocationId == request.LocationId.Value);

            if (!trainerHasLocation)
            {
                throw new InvalidOperationException("Trainer is not assigned to this location.");
            }
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        await _context.UserRoles.AddAsync(new UserRole
        {
            UserId = user.Id,
            RoleId = role.Id
        });

        int? clientId = null;
        int? trainerId = null;

        if (roleName == "Trainer")
        {
            var trainer = new Trainer
            {
                UserId = user.Id,
                Status = "Active",
                CreatedAt = now,
                UpdatedAt = now
            };

            await _context.Trainers.AddAsync(trainer);
            await _context.SaveChangesAsync();

            await _context.TrainerLocations.AddAsync(new TrainerLocation
            {
                TrainerId = trainer.Id,
                LocationId = request.LocationId.Value
            });

            trainerId = trainer.Id;
        }
        else
        {
            var client = new Client
            {
                UserId = user.Id,
                TrainerId = assignedTrainer?.Id,
                LocationId = request.LocationId.Value,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                ProgressPercent = 0,
                BillingStatus = "Pending",
                Status = "New",
                SubscriptionAutoRenewEnabled = true,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _context.Clients.AddAsync(client);
            await _context.SaveChangesAsync();

            clientId = client.Id;
        }

        await _context.SaveChangesAsync();

        return new CreatedAccountDto
        {
            UserId = user.Id,
            Email = user.Email,
            Role = roleName,
            ClientId = clientId,
            TrainerId = trainerId,
            LocationId = request.LocationId
        };
    }

    public async Task<AuthResponseDto?> LoginAsync(LoginRequestDto request)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user is null || !user.IsActive)
        {
            return null;
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return null;
        }

        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        var refreshToken = GenerateRefreshToken();

        await _context.RefreshTokens.AddAsync(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        return BuildAuthResponse(user, refreshToken);
    }

    public async Task<AuthResponseDto> RefreshAsync(RefreshTokenRequestDto request)
    {
        var refreshToken = await _context.RefreshTokens
            .Include(rt => rt.User)
                .ThenInclude(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken);

        if (refreshToken is null || !refreshToken.IsActive || !refreshToken.User.IsActive)
        {
            throw new InvalidOperationException("Invalid refresh token.");
        }

        refreshToken.RevokedAt = DateTime.UtcNow;

        var newRefreshToken = GenerateRefreshToken();

        await _context.RefreshTokens.AddAsync(new RefreshToken
        {
            UserId = refreshToken.UserId,
            Token = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        });

        refreshToken.User.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return BuildAuthResponse(refreshToken.User, newRefreshToken);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordDto request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user is null)
        {
            return;
        }

        var token = GenerateResetToken();

        await _context.PasswordResetTokens.AddAsync(new PasswordResetToken
        {
            UserId = user.Id,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            IsUsed = false
        });

        await _context.SaveChangesAsync();

        Console.WriteLine($"RESET TOKEN for {user.Email}: {token}");
    }

    public async Task ResetPasswordAsync(ResetPasswordDto request)
    {
        var resetToken = await _context.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t =>
                t.Token == request.Token &&
                !t.IsUsed &&
                t.ExpiresAt > DateTime.UtcNow);

        if (resetToken is null)
        {
            throw new InvalidOperationException("Invalid or expired token.");
        }

        resetToken.User.PasswordHash =
            _passwordHasher.HashPassword(resetToken.User, request.NewPassword);

        resetToken.User.UpdatedAt = DateTime.UtcNow;
        resetToken.IsUsed = true;

        await _context.SaveChangesAsync();
    }

    private AuthResponseDto BuildAuthResponse(User user, string refreshToken)
    {
        var roleNames = user.UserRoles
            .Select(ur => ur.Role.Name)
            .ToList();

        var primaryRole = roleNames.FirstOrDefault() ?? "Client";

        var token = GenerateJwtToken(user, roleNames);

        return new AuthResponseDto
        {
            Token = token,
            RefreshToken = refreshToken,
            UserId = user.Id,
            Email = user.Email,
            Role = primaryRole
        };
    }

    private string GenerateJwtToken(User user, List<string> roleNames)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Email)
        };

        foreach (var role in roleNames)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryInMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(randomBytes);
    }

    private static string GenerateResetToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(randomBytes);
    }

    private static string NormalizeManualRegistrationRole(string? role)
    {
        return role?.Trim().ToLowerInvariant() switch
        {
            "trainer" => "Trainer",
            "client" => "Client",
            _ => throw new InvalidOperationException("Manual account creation supports only Client or Trainer roles.")
        };
    }
}
