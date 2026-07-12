using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.DTOs;
using backend.Models;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtService _jwt;

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "administrator", "system", "moderator", "support", "root"
    };

    public AuthController(AppDbContext db, JwtService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username))
            return BadRequest("Username is required.");

        // Reject reserved / system usernames
        if (ReservedNames.Contains(req.Username))
            return BadRequest($"\"{req.Username}\" is a reserved username. Please choose another.");

        if (req.Username.Contains("admin", StringComparison.OrdinalIgnoreCase))
            return BadRequest($"Usernames containing \"admin\" are not allowed.");

        // Check if someone with this name is currently active
        var nameInUse = await _db.UserSessions
            .AnyAsync(s => s.User.Username == req.Username && s.EndedAt == null);

        if (nameInUse)
            return Conflict("This name is already in use. Please choose a different name or wait until the user disconnects.");

        // Find existing user or create a new one
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == req.Username);
        if (user == null)
        {
            user = new User { Username = req.Username };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }

        var token = _jwt.GenerateToken(user.Id, user.Username);
        return Ok(new AuthResponse(token, user.Id, user.Username));
    }
}
