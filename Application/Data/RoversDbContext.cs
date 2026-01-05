using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Rovers_backend.Models;

namespace Rovers_backend.Data;

public class RoversDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public DbSet<Report> Reports { get; set; }

    public RoversDbContext(DbContextOptions<RoversDbContext> options)
        : base(options)
    {
    }
}