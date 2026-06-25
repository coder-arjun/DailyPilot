using DailyPilot.Data;
using DailyPilot.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyPilot.Services;

/// <summary>Seeds a sensible set of default categories the first time a user signs in.</summary>
public interface IUserSeeder
{
    Task EnsureDefaultsAsync(string userId);
}

public class UserSeeder : IUserSeeder
{
    private readonly ApplicationDbContext _db;

    public UserSeeder(ApplicationDbContext db) => _db = db;

    private static readonly (string Name, string Color)[] Defaults =
    {
        ("Work", "#0d6efd"),
        ("Personal", "#198754"),
        ("Health", "#dc3545"),
        ("Learning", "#6f42c1"),
        ("Errands", "#fd7e14")
    };

    public async Task EnsureDefaultsAsync(string userId)
    {
        var hasCategories = await _db.Categories.AnyAsync(c => c.UserId == userId);
        if (hasCategories) return;

        foreach (var (name, color) in Defaults)
            _db.Categories.Add(new Category { Name = name, Color = color, UserId = userId });

        await _db.SaveChangesAsync();
    }
}
