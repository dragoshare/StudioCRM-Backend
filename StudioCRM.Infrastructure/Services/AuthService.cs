using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StudioCRM.Application.DTOs.Auth;
using StudioCRM.Application.DTOs.Public;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Interfaces.Mail;
using StudioCRM.Application.Settings;
using StudioCRM.Domain.Entities;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly StudioCRMDbContext _context;
    private readonly JwtSettings _jwtSettings;
    private readonly AppSettings _appSettings;
    private readonly IEmailService _emailService;
    private readonly ILogger<AuthService> _logger;
    private readonly PasswordHasher<User> _passwordHasher;

    public AuthService(
        StudioCRMDbContext context,
        IOptions<JwtSettings> jwtOptions,
        IOptions<AppSettings> appOptions,
        IEmailService emailService,
        ILogger<AuthService> logger)
    {
        _context = context;
        _jwtSettings = jwtOptions.Value;
        _appSettings = appOptions.Value;
        _emailService = emailService;
        _logger = logger;
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
            EmailVerifiedAt = now,
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
                BillingStatus = "Pending",
                Status = "Inactive",
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

    public async Task<AuthResponseDto> RegisterPublicGroupClientAsync(PublicGroupRegisterRequest request)
    {
        var email = request.Email.Trim();

        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("Email is required.");

        if (string.IsNullOrWhiteSpace(request.Password))
            throw new InvalidOperationException("Password is required.");

        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            throw new InvalidOperationException("First name and last name are required.");

        var existingUser = await _context.Users
            .AnyAsync(u => u.Email == email);

        if (existingUser)
            throw new InvalidOperationException("User with this email already exists.");

        if (await _context.Clients.IgnoreQueryFilters().AnyAsync(c => c.Email.ToLower() == email.ToLower()))
            throw new InvalidOperationException("A client profile with this email already exists. Ask the studio for a portal invitation.");
        var locationExists = await _context.Locations
            .AnyAsync(l => l.Id == request.LocationId && l.IsActive);

        if (!locationExists)
            throw new InvalidOperationException("Location does not exist.");

        var legalRequirements = await LegalConsentManager.GetRequirementsAsync(_context, request.LocationId);
        if (legalRequirements.AcceptanceRequired &&
            (!request.AcceptTerms || !string.Equals(
                request.TermsVersion?.Trim(),
                legalRequirements.TermsVersion,
                StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Current terms must be accepted before registration.");
        }

        var role = await _context.Roles
            .FirstOrDefaultAsync(r => r.Name == "Client");

        if (role is null)
            throw new InvalidOperationException("Client role does not exist.");

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

        var client = new Client
        {
            UserId = user.Id,
            TrainerId = null,
            LocationId = request.LocationId,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            PhoneNumber = request.PhoneNumber,
            BillingStatus = "Pending",
            Source = "PublicGroupSignup",
            Status = "Inactive",
            SubscriptionAutoRenewEnabled = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _context.Clients.AddAsync(client);
        await _context.ClientLocationMemberships.AddAsync(new ClientLocationMembership
        {
            Client = client,
            LocationId = request.LocationId,
            IsHomeLocation = true,
            GroupAccessEnabled = true,
            Source = "PublicGroupSignup",
            JoinedAt = now,
            UpdatedAt = now
        });

        var refreshToken = GenerateRefreshToken();

        await _context.RefreshTokens.AddAsync(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        });

        await LegalConsentManager.AcceptAsync(
            _context,
            user.Id,
            request.LocationId,
            request.AcceptTerms,
            request.TermsVersion,
            "PublicGroupRegistration");

        var emailVerificationToken = await CreateEmailVerificationTokenAsync(user.Id);

        await _context.SaveChangesAsync();

        var clientName = $"{client.FirstName} {client.LastName}".Trim();
        var registrationSourceKey = $"public-group-registration:{user.Id}";
        await NotificationWriter.QueueAsync(
            _context,
            new[] { user.Id },
            registrationSourceKey,
            "PublicGroupClientRegistered",
            "Konto zostało utworzone",
            "Potwierdź adres e-mail, aby kupić pakiet i zapisać się na zajęcia grupowe.",
            relatedEntityType: "client",
            relatedEntityId: client.Id,
            actionUrl: "/group-classes",
            createdAt: now);
        await NotificationWriter.QueueForOwnersAsync(
            _context,
            registrationSourceKey,
            "PublicGroupClientRegistered",
            "Nowa rejestracja na zajęcia grupowe",
            $"{clientName} ({client.Email})",
            relatedEntityType: "client",
            relatedEntityId: client.Id,
            actionUrl: $"/clients/{client.Id}/workspace",
            createdAt: now);
        await _context.SaveChangesAsync();

        try
        {
            var verificationLink = BuildFrontendUrl("verify-email", ("token", emailVerificationToken));
            await _emailService.SendEmailVerificationAsync(user.Email, verificationLink);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not send email verification to user {UserId}.", user.Id);
        }

        var registeredUser = await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstAsync(u => u.Id == user.Id);

        return BuildAuthResponse(registeredUser, refreshToken);
    }

    public async Task<AuthResponseDto?> LoginAsync(LoginRequestDto request)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user is null || !user.IsActive || await ClientAccountAccess.IsBlockedAsync(_context, user.Id))
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

    public async Task<AuthMeDto?> GetMeAsync(int userId)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

        if (user is null)
        {
            return null;
        }

        var roleNames = user.UserRoles
            .Select(ur => ur.Role.Name)
            .ToList();

        var client = await _context.Clients
            .Where(c => c.UserId == user.Id)
            .Select(c => new
            {
                c.Id,
                c.TrainerId,
                c.LocationId,
                c.Source
            })
            .FirstOrDefaultAsync();

        var trainer = await _context.Trainers
            .Where(t => t.UserId == user.Id)
            .Select(t => new
            {
                t.Id
            })
            .FirstOrDefaultAsync();

        var primaryRole = roleNames.FirstOrDefault() ?? string.Empty;
        return new AuthMeDto
        {
            UserId = user.Id,
            Email = user.Email,
            Role = primaryRole,
            Roles = roleNames,
            FullName = $"{user.FirstName} {user.LastName}".Trim(),
            AvatarUrl = user.AvatarUrl,
            ClientId = client?.Id,
            TrainerId = trainer?.Id,
            LocationId = client?.LocationId,
            ClientSource = client?.Source,
            PortalAccessMode = client is null
                ? null
                : client.TrainerId.HasValue ? "FullCrm" : "GroupOnly",
            EmailVerified = user.EmailVerifiedAt.HasValue
        };
    }

    public async Task<AuthResponseDto> RefreshAsync(RefreshTokenRequestDto request)
    {
        var refreshToken = await _context.RefreshTokens
            .Include(rt => rt.User)
                .ThenInclude(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken);

        if (refreshToken is null || !refreshToken.IsActive || !refreshToken.User.IsActive ||
            await ClientAccountAccess.IsBlockedAsync(_context, refreshToken.UserId))
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
        var email = request.Email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(email))
            return;

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email);

        if (user is null)
        {
            return;
        }

        var token = GenerateResetToken();
        var activeTokens = await _context.PasswordResetTokens
            .Where(t =>
                t.UserId == user.Id &&
                !t.IsUsed &&
                t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

        foreach (var activeToken in activeTokens)
        {
            activeToken.IsUsed = true;
        }

        await _context.PasswordResetTokens.AddAsync(new PasswordResetToken
        {
            UserId = user.Id,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            IsUsed = false
        });

        await _context.SaveChangesAsync();

        var resetLink = BuildFrontendUrl("reset-password", ("token", token));

        await _emailService.SendPasswordResetEmailAsync(user.Email, resetLink);
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

    public async Task ChangePasswordAsync(int userId, ChangePasswordDto request)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
            throw new InvalidOperationException("Current password is required.");

        if (string.IsNullOrWhiteSpace(request.NewPassword))
            throw new InvalidOperationException("New password is required.");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
        if (user is null)
            throw new InvalidOperationException("User does not exist.");

        var verificationResult = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.CurrentPassword);

        if (verificationResult == PasswordVerificationResult.Failed)
            throw new InvalidOperationException("Current password is incorrect.");

        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task VerifyEmailAsync(VerifyEmailRequest request)
    {
        var tokenHash = HashEmailVerificationToken(request.Token);
        var verificationToken = await _context.EmailVerificationTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x =>
                x.TokenHash == tokenHash &&
                x.UsedAt == null &&
                x.ExpiresAt > DateTime.UtcNow);

        if (verificationToken is null)
            throw new InvalidOperationException("Invalid or expired email verification token.");

        var now = DateTime.UtcNow;
        verificationToken.UsedAt = now;
        verificationToken.User.EmailVerifiedAt ??= now;
        verificationToken.User.UpdatedAt = now;

        var otherTokens = await _context.EmailVerificationTokens
            .Where(x => x.UserId == verificationToken.UserId && x.Id != verificationToken.Id && x.UsedAt == null)
            .ToListAsync();
        foreach (var otherToken in otherTokens)
            otherToken.UsedAt = now;

        await _context.SaveChangesAsync();
    }

    public async Task ResendEmailVerificationAsync(ResendEmailVerificationRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            return;

        var user = await _context.Users.FirstOrDefaultAsync(x => x.Email.ToLower() == email && x.IsActive);
        if (user is null || user.EmailVerifiedAt.HasValue)
            return;

        var recentlyCreated = await _context.EmailVerificationTokens.AnyAsync(x =>
            x.UserId == user.Id &&
            x.UsedAt == null &&
            x.CreatedAt > DateTime.UtcNow.AddMinutes(-1));
        if (recentlyCreated)
            return;

        var rawToken = await CreateEmailVerificationTokenAsync(user.Id);
        await _context.SaveChangesAsync();

        var verificationLink = BuildFrontendUrl("verify-email", ("token", rawToken));
        await _emailService.SendEmailVerificationAsync(user.Email, verificationLink);
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
            Role = primaryRole,
            EmailVerified = user.EmailVerifiedAt.HasValue,
            EmailVerificationRequired = !user.EmailVerifiedAt.HasValue
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

    private async Task<string> CreateEmailVerificationTokenAsync(int userId)
    {
        var now = DateTime.UtcNow;
        var activeTokens = await _context.EmailVerificationTokens
            .Where(x => x.UserId == userId && x.UsedAt == null)
            .ToListAsync();
        foreach (var activeToken in activeTokens)
            activeToken.UsedAt = now;

        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        await _context.EmailVerificationTokens.AddAsync(new EmailVerificationToken
        {
            UserId = userId,
            TokenHash = HashEmailVerificationToken(rawToken),
            CreatedAt = now,
            ExpiresAt = now.AddHours(24)
        });

        return rawToken;
    }

    private static string HashEmailVerificationToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return string.Empty;

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim())))
            .ToLowerInvariant();
    }

    private string BuildFrontendUrl(
        string path,
        params (string Key, string Value)[] queryParameters)
    {
        var baseUrl = _appSettings.FrontendBaseUrl.TrimEnd('/');
        var normalizedPath = path.TrimStart('/');
        var queryString = string.Join(
            "&",
            queryParameters.Select(x =>
                $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"));

        return string.IsNullOrWhiteSpace(queryString)
            ? $"{baseUrl}/{normalizedPath}"
            : $"{baseUrl}/{normalizedPath}?{queryString}";
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
