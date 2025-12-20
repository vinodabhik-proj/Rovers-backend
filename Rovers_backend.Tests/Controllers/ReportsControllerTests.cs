using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Rovers_backend.Data;
using Rovers_backend.Models;
using Rovers_backend.Tests.Infrastructure;
using Xunit;

namespace Rovers_backend.Tests.Controllers;

public class ReportsControllerTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly IServiceScope _scope;
    private readonly RoversDbContext _db;

    public ReportsControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _scope = factory.Services.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<RoversDbContext>();
        SeedData();
    }

    private void SeedData()
    {
        _db.Reports.RemoveRange(_db.Reports);
        _db.SaveChanges();

        _db.Reports.AddRange(
            new Report { Date = DateTime.UtcNow.AddDays(-1), RoversScore = 2, OppoScore = 1 },
            new Report { Date = DateTime.UtcNow, RoversScore = 3, OppoScore = 0 }
        );

        _db.SaveChanges();
    }

    [Fact]
    public async Task GetReports_Returns200AndOrderedReports()
    {
        // Act
        var response = await _client.GetAsync("/api/reports");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var reports = await response.Content.ReadFromJsonAsync<List<Report>>();
        reports.Should().NotBeNull();
        reports!.Count.Should().Be(2);
        reports[0].Date.Should().BeAfter(reports[1].Date);
    }
}
