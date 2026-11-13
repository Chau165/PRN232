using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Infrastructure.Data;

namespace PRN232.Infrastructure.Repositories;

public sealed class UserContactLookup : IUserContactLookup
{
    private readonly AppDbContext _context;

    public UserContactLookup(AppDbContext context)
    {
        _context = context;
    }

    public Task<bool> HasPhoneNumberAsync(int userId)
    {
        return _context.Users.AnyAsync(user =>
            user.UserId == userId && user.PhoneNumber != null && user.PhoneNumber.Trim() != "");
    }
}
