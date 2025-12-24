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
    : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private readonly HttpClient _client;
    private readonly IServiceScope _scope;
    private readonly RoversDbContext _db;

    public ReportsControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();

        _scope = factory.Services.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<RoversDbContext>();

        _db.Database.EnsureCreated();
        SeedData();
    }


    private void SeedData()
    {
        _db.Reports.RemoveRange(_db.Reports);

        _db.Reports.AddRange(
            new Report
            {
                Date = new DateTime(2024, 1, 1),
                RoversScore = 2,
                OppoScore = 1
            },
            new Report
            {
                Date = new DateTime(2024, 1, 2),
                RoversScore = 3,
                OppoScore = 0
            }
        );

        _db.SaveChanges();
    }

    [Fact]
    public async Task GetReports_Returns200AndOrderedReports()
    {
        var response = await _client.GetAsync("/api/reports");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var reports = await response.Content.ReadFromJsonAsync<List<Report>>();
        reports.Should().HaveCount(2);
        reports![0].Date.Should().Be(new DateTime(2024, 1, 2));
    }

    public void Dispose()
    {
        _scope.Dispose();
    }
}