using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Rovers_backend.Data;
using Rovers_backend.Models;
using Rovers_backend.Tests.Infrastructure;
using Xunit;

namespace Rovers_backend.Tests.Controllers;

public class AuthControllerIntegrationTests
{
    [Fact]
    public async Task Me_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = CreateClientWithoutRedirects(factory);

        var response = await client.GetAsync("/auth/user");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_ReturnsUser_WhenAuthenticated()
    {
        var userId = Guid.NewGuid();
        using var factory = new CustomWebApplicationFactory(authenticated: true, userId);
        await SeedUserAsync(factory, userId);
        using var client = CreateClientWithoutRedirects(factory);

        var response = await client.GetAsync("/auth/user");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var authUser = await response.Content.ReadFromJsonAsync<AuthUserResponse>();
        authUser.Should().NotBeNull();
        authUser!.Id.Should().Be(userId);
        authUser.Email.Should().Be("test@example.com");
        authUser.FirstName.Should().Be("Test");
        authUser.LastName.Should().Be("User");
        authUser.UserName.Should().Be("test@example.com");
        authUser.Roles.Should().ContainSingle("Admin");
    }

    [Fact]
    public async Task SignOutCallback_RedirectsToFrontend()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = CreateClientWithoutRedirects(factory);

        var response = await client.GetAsync("/auth/signout-callback");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().Be("http://localhost:5173");
    }

    private static HttpClient CreateClientWithoutRedirects(CustomWebApplicationFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    private static async Task SeedUserAsync(CustomWebApplicationFactory factory, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RoversDbContext>();
        await db.Database.EnsureCreatedAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid> { Name = "Admin" });
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "test@example.com",
            UserName = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user);
        result.Succeeded.Should().BeTrue(string.Join(", ", result.Errors.Select(e => e.Description)));

        var roleResult = await userManager.AddToRoleAsync(user, "Admin");
        roleResult.Succeeded.Should().BeTrue(string.Join(", ", roleResult.Errors.Select(e => e.Description)));
    }

    private sealed class AuthUserResponse
    {
        public Guid Id { get; set; }
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? UserName { get; set; }
        public List<string> Roles { get; set; } = [];
    }
}
