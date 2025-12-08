using MongoDB.Driver;
using HabitTracker.Api.Models;
using HabitTracker.Api.Models.DTOs;
using HabitTracker.Api.Config;
using Microsoft.Extensions.Options;
using BCrypt.Net;

namespace HabitTracker.Api.Services;

public class AuthService
{
    private readonly IMongoCollection<User> _usersCollection;
    private readonly TokenService _tokenService;

    public AuthService(IOptions<MongoDbSettings> mongoDbSettings, TokenService tokenService)
    {
        var mongoClient = new MongoClient(mongoDbSettings.Value.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(mongoDbSettings.Value.DatabaseName);
        _usersCollection = mongoDatabase.GetCollection<User>("Users");
        _tokenService = tokenService;
    }

    public async Task<(UserDto? User, string? ErrorMessage)> RegisterAsync(RegisterDto registerDto)
    {
        var existingUser = await _usersCollection.Find(u => u.Email == registerDto.Email || u.Username == registerDto.Username).FirstOrDefaultAsync();
        if (existingUser != null)
        {
            return (null, "User with this email or username already exists.");
        }

        var user = new User
        {
            Username = registerDto.Username,
            Email = registerDto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password)
        };

        await _usersCollection.InsertOneAsync(user);
        var userDto = new UserDto(user.Id!, user.Username, user.Email);
        return (userDto, null);
    }

    public async Task<(AuthResponseDto? AuthResponse, string? ErrorMessage)> LoginAsync(LoginDto loginDto)
    {
        var user = await _usersCollection.Find(u => u.Email == loginDto.Email).FirstOrDefaultAsync();

        if (user == null || !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
        {
            return (null, "Invalid email or password.");
        }

        var token = _tokenService.GenerateToken(user);
        var userDto = new UserDto(user.Id!, user.Username, user.Email);
        var authResponse = new AuthResponseDto(token, userDto);
        
        return (authResponse, null);
    }
}