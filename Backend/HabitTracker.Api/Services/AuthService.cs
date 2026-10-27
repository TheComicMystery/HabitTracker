using MongoDB.Driver;
using HabitTracker.Api.Models;
using HabitTracker.Api.Models.DTOs;

namespace HabitTracker.Api.Services;

public class AuthService
{
    private readonly IMongoCollection<User> _usersCollection;
    private readonly TokenService _tokenService;

    public AuthService(IMongoDatabase mongoDatabase, TokenService tokenService)
    {
        _usersCollection = mongoDatabase.GetCollection<User>("Users");
        _tokenService = tokenService;
    }

    public async Task<(UserDto? User, string? ErrorMessage)> RegisterAsync(RegisterDto registerDto)
    {
        var email = registerDto.Email.Trim().ToLowerInvariant();
        var username = registerDto.Username.Trim();
        if (username.Length < 2 || username.Length > 60)
        {
            return (null, "Ім'я користувача має містити від 2 до 60 символів.");
        }
        if (registerDto.Password.Length < 8)
        {
            return (null, "Пароль має містити щонайменше 8 символів.");
        }

        var existingUser = await _usersCollection.Find(u => u.Email == email || u.Username == username).FirstOrDefaultAsync();
        if (existingUser != null)
        {
            return (null, "Користувач з таким email або ім'ям вже існує.");
        }

        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password)
        };

        await _usersCollection.InsertOneAsync(user);
        var userDto = MapToUserDto(user);
        return (userDto, null);
    }

    public async Task<(AuthResponseDto? AuthResponse, string? ErrorMessage)> LoginAsync(LoginDto loginDto)
    {
        var email = loginDto.Email.Trim().ToLowerInvariant();
        var user = await _usersCollection.Find(u => u.Email == email).FirstOrDefaultAsync();

        if (user == null || !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
        {
            return (null, "Невірний email або пароль.");
        }

        var token = _tokenService.GenerateToken(user);
        var userDto = MapToUserDto(user);
        var authResponse = new AuthResponseDto(token, userDto);
        
        return (authResponse, null);
    }

    public async Task<OnboardingProfileDto?> GetOnboardingProfileAsync(string userId)
    {
        var user = await _usersCollection.Find(u => u.Id == userId).FirstOrDefaultAsync();
        return user == null ? null : MapToOnboardingProfileDto(user);
    }

    public async Task<OnboardingProfileDto?> UpdateOnboardingProfileAsync(string userId, UpdateOnboardingProfileDto dto)
    {
        var update = Builders<User>.Update
            .Set(u => u.SubjectiveDifficulty, dto.SubjectiveDifficulty)
            .Set(u => u.OverallDisciplineSelf, dto.OverallDisciplineSelf)
            .Set(u => u.WeekendVulnerability, dto.WeekendVulnerability)
            .Set(u => u.PreferredTimeOfDay, NormalizeChoice(dto.PreferredTimeOfDay))
            .Set(u => u.InitialFailureReason, NormalizeChoice(dto.InitialFailureReason))
            .Set(u => u.OnboardingCompleted, dto.Complete)
            .Set(u => u.OnboardingCompletedAt, dto.Complete ? DateTime.UtcNow : null);

        var user = await _usersCollection.FindOneAndUpdateAsync(
            u => u.Id == userId,
            update,
            new FindOneAndUpdateOptions<User> { ReturnDocument = ReturnDocument.After });

        return user == null ? null : MapToOnboardingProfileDto(user);
    }

    private static string? NormalizeChoice(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length > 80 ? trimmed[..80] : trimmed;
    }

    private static UserDto MapToUserDto(User user) =>
        new(
            user.Id!,
            user.Username,
            user.Email,
            user.OnboardingCompleted,
            user.SubjectiveDifficulty,
            user.OverallDisciplineSelf,
            user.WeekendVulnerability,
            user.PreferredTimeOfDay,
            user.InitialFailureReason);

    private static OnboardingProfileDto MapToOnboardingProfileDto(User user) =>
        new(
            user.OnboardingCompleted,
            user.SubjectiveDifficulty,
            user.OverallDisciplineSelf,
            user.WeekendVulnerability,
            user.PreferredTimeOfDay,
            user.InitialFailureReason);
}
