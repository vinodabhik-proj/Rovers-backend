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
            
            // Update user names if they're empty or changed
            var firstName = context.Principal?.FindFirst("given_name")?.Value 
                           ?? context.Principal?.FindFirst("givenname")?.Value 
                           ?? context.Principal?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname")?.Value
                           ?? context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.GivenName)?.Value;
            
            var lastName = context.Principal?.FindFirst("family_name")?.Value 
                          ?? context.Principal?.FindFirst("surname")?.Value 
                          ?? context.Principal?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname")?.Value
                          ?? context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Surname)?.Value;
            
            bool needsUpdate = false;
            
            if (!string.IsNullOrEmpty(firstName) && user.FirstName != firstName)
            {
                user.FirstName = firstName;
                needsUpdate = true;
            }
            
            if (!string.IsNullOrEmpty(lastName) && user.LastName != lastName)
            {
                user.LastName = lastName;
                needsUpdate = true;
            }
            
            if (needsUpdate)
            {
                await _userManager.UpdateAsync(user);
                _logger.LogInformation("Updated user profile: {FirstName} {LastName}", user.FirstName, user.LastName);
            }
        }

        // Sign in the user with Identity
        await _signInManager.SignInAsync(user, isPersistent: true);
        _logger.LogInformation("User signed in successfully: {Email}", email);
    }

    private async Task<ApplicationUser?> CreateUserFromClaimsAsync(ClaimsPrincipal principal)
    {
        var email = principal.FindFirst("preferred_username")?.Value
                   ?? principal.FindFirst("email")?.Value 
                   ?? principal.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value
                   ?? "";
        
        // Try multiple claim type formats
        var firstName = principal.FindFirst("given_name")?.Value 
                       ?? principal.FindFirst("givenname")?.Value 
                       ?? principal.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname")?.Value
                       ?? principal.FindFirst(System.Security.Claims.ClaimTypes.GivenName)?.Value
                       ?? "";
        
        var lastName = principal.FindFirst("family_name")?.Value 
                      ?? principal.FindFirst("surname")?.Value 
                      ?? principal.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname")?.Value
                      ?? principal.FindFirst(System.Security.Claims.ClaimTypes.Surname)?.Value
                      ?? "";
        

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