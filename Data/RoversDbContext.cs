using Microsoft.EntityFrameworkCore;
using Rovers_backend.Models;

namespace Rovers_backend.Data
{
	public class RoversDbContext : DbContext
	{
		public DbSet<Report> Reports { get; set; }
		public RoversDbContext(DbContextOptions<RoversDbContext> options) : base(options) { }
	}
}