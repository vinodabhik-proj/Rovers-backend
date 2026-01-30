using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Rovers_backend.Models;
using System.Security.Claims;

namespace Rovers_backend.Services;

public class AuthenticationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<AuthenticationService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    public async Task HandleTokenValidatedAsync(TokenValidatedContext context)
    {
        _logger.LogInformation("Token validated, processing user authentication");

        var email = context.Principal?.FindFirst("preferred_username")?.Value
                   ?? context.Principal?.FindFirst("email")?.Value;

        if (string.IsNullOrEmpty(email))
        {
            _logger.LogWarning("No email found in claims");
            return;
        }

        var user = await _userManager.FindByEmailAsync(email);

        if (user == null)
        {
            user = await CreateUserFromClaimsAsync(context.Principal!);
            if (user == null)
            {
                _logger.LogError("Failed to create user from claims");
                return;
            }
        }
        else
        {
            _logger.LogInformation("User found: {Email}", email);
        }

        // Sign in the user with Identity
        await _signInManager.SignInAsync(user, isPersistent: true);
        _logger.LogInformation("User signed in successfully: {Email}", email);
    }

    private async Task<ApplicationUser?> CreateUserFromClaimsAsync(ClaimsPrincipal principal)
    {
        var email = principal.FindFirst("preferred_username")?.Value
                   ?? principal.FindFirst("email")?.Value ?? "";
        var firstName = principal.FindFirst("given_name")?.Value ?? "";
        var lastName = principal.FindFirst("family_name")?.Value ?? "";
        var oid = principal.FindFirst("oid")?.Value ?? "";

        _logger.LogInformation("Creating new user: {Email}", email);

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName
        };

        var result = await _userManager.CreateAsync(user);

        if (result.Succeeded)
        {
            _logger.LogInformation("User created successfully");
            await _userManager.AddToRoleAsync(user, "User");

            // Store the Entra ID object ID for future reference
            await _userManager.AddLoginAsync(user, new UserLoginInfo(
                "AzureAD",
                oid,
                "Microsoft Entra ID"
            ));

            return user;
        }
        else
        {
            _logger.LogError("User creation failed: {Errors}",
                string.Join(", ", result.Errors.Select(e => e.Description)));
            return null;
        }
    }

    public Task HandleAuthenticationFailedAsync(AuthenticationFailedContext context, string frontendBaseUrl)
    {
        _logger.LogError(context.Exception, "Authentication failed: {ErrorMessage}", context.Exception.Message);
        context.Response.Redirect($"{frontendBaseUrl}/login?error=auth_failed");
        context.HandleResponse();
        return Task.CompletedTask;
    }
}