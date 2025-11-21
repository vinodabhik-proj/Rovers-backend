using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rovers_backend.Data;

namespace Rovers_backend.Api.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class ReportsController : ControllerBase
	{
		private readonly RoversDbContext _db;

		public ReportsController(RoversDbContext db)
		{
				_db = db;
		}

		[HttpGet]
		public async Task<IActionResult> GetReports()
		{
			var reports = await _db.Reports.OrderByDescending(r => r.Date).ToListAsync();

			return Ok(reports);				
		}
	}
}