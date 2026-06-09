using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Rovers_backend.Data;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Rovers_backend.Tests.Infrastructure;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestScheme = "TestScheme";

    private SqliteConnection? _connection;
    private readonly bool _authenticated;
    private readonly Guid _testUserId;

    public CustomWebApplicationFactory()
        : this(authenticated: false, testUserId: null)
    {
    }

    internal CustomWebApplicationFactory(bool authenticated, Guid? testUserId = null)
    {
        _authenticated = authenticated;
        _testUserId = testUserId ?? Guid.NewGuid();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Frontend:BaseUrl"] = "http://localhost:5173",
                ["AzureAd:Instance"] = "https://login.microsoftonline.com/",
                ["AzureAd:TenantId"] = "test-tenant",
                ["AzureAd:ClientId"] = "test-client",
                ["AzureAd:ClientSecret"] = "test-secret",
                ["AzureAd:CallbackPath"] = "/signin-oidc"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<RoversDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            // Setup in-memory SQLite database
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<RoversDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            // Remove Entra ID/OpenIdConnect authentication services
            var authServiceDescriptors = services
                .Where(d => d.ServiceType.ToString().Contains("OpenIdConnect") ||
                           d.ServiceType.ToString().Contains("MicrosoftIdentity"))
                .ToList();

            foreach (var authDescriptor in authServiceDescriptors)
            {
                services.Remove(authDescriptor);
            }
        });

        builder.ConfigureTestServices(services =>
        {
            // Add test authentication scheme
            services.AddAuthentication(defaultScheme: TestScheme)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestScheme, options => { });

            services.Configure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestScheme;
                options.DefaultChallengeScheme = TestScheme;
            });

            services.Configure<TestAuthOptions>(options =>
            {
                options.Authenticated = _authenticated;
                options.UserId = _testUserId;
            });
        });

        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection?.Dispose();
    }
}

public class TestAuthOptions
{
    public bool Authenticated { get; set; }
    public Guid UserId { get; set; }
}

// Test authentication handler that allows unauthenticated access
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly TestAuthOptions _testAuthOptions;

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<TestAuthOptions> testAuthOptions) : base(options, logger, encoder)
    {
        _testAuthOptions = testAuthOptions.Value;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!_testAuthOptions.Authenticated)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim(ClaimTypes.NameIdentifier, _testAuthOptions.UserId.ToString()),
        };

        var identity = new ClaimsIdentity(claims, CustomWebApplicationFactory.TestScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, CustomWebApplicationFactory.TestScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
