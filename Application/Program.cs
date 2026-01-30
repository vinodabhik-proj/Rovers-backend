using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Rovers_backend.Data;
using Rovers_backend.Models;
using Rovers_backend.Options;
using Rovers_backend.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddDbContext<RoversDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure Identity FIRST (this sets up the default Cookie scheme)
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<RoversDbContext>()
.AddDefaultTokenProviders();

// Configure Cookie settings AFTER Identity (this modifies the existing cookie scheme)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "RoversAuth";
    options.Cookie.SameSite = builder.Environment.IsDevelopment() 
        ? SameSiteMode.Lax 
        : SameSiteMode.None;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() 
        ? CookieSecurePolicy.None 
        : CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
    options.LoginPath = "/auth/entra/login";
    options.LogoutPath = "/auth/logout";
});

// Register Authentication Service
builder.Services.AddScoped<AuthenticationService>();

// Add OpenID Connect authentication for Entra ID
builder.Services.AddAuthentication()
    .AddMicrosoftIdentityWebApp(options =>
    {
        builder.Configuration.Bind("AzureAd", options);
        options.SaveTokens = true;
        options.ResponseType = "code"; // Use authorization code flow
        
        options.Events = new OpenIdConnectEvents
        {
            OnRedirectToIdentityProvider = context =>
            {
                Console.WriteLine("=== Redirecting to Identity Provider ===");
                return Task.CompletedTask;
            },
            OnAuthorizationCodeReceived = context =>
            {
                Console.WriteLine("=== Authorization Code Received ===");
                return Task.CompletedTask;
            },
            OnTokenResponseReceived = context =>
            {
                Console.WriteLine("=== Token Response Received ===");
                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var authService = context.HttpContext.RequestServices
                    .GetRequiredService<AuthenticationService>();
                await authService.HandleTokenValidatedAsync(context);
            },
            OnAuthenticationFailed = context =>
            {
                var authService = context.HttpContext.RequestServices
                    .GetRequiredService<AuthenticationService>();
                var config = context.HttpContext.RequestServices
                    .GetRequiredService<IConfiguration>();
                return authService.HandleAuthenticationFailedAsync(
                    context,
                    config["Frontend:BaseUrl"] ?? "http://localhost:5173"
                );
            }
        };
    });

builder.Services.Configure<FrontendOptions>(
    builder.Configuration.GetSection("Frontend"));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(builder.Configuration["Frontend:BaseUrl"] ?? "http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Seed roles on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await DbInitialiser.InitializeAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Only enforce strict cookie policy in production
if (!app.Environment.IsDevelopment())
{
    app.UseCookiePolicy(new CookiePolicyOptions
    {
        MinimumSameSitePolicy = SameSiteMode.None,
        Secure = CookieSecurePolicy.Always
    });
}

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }