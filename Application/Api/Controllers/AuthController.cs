using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Rovers_backend.Models;
using Rovers_backend.Options;

namespace Rovers_backend.Controllers;

[ApiController]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly FrontendOptions _frontend;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IOptions<FrontendOptions> frontendOptions)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _frontend = frontendOptions.Value;
    }

    [HttpGet("auth/entra/login")]
    public IActionResult EntraLogin(string? returnUrl = null)
    {
        // Store return URL for after authentication
        var properties = new AuthenticationProperties
        {
            RedirectUri = string.IsNullOrEmpty(returnUrl) 
                ? _frontend.BaseUrl 
                : $"{_frontend.BaseUrl}{returnUrl}",
            IsPersistent = true
        };

        return Challenge(properties, OpenIdConnectDefaults.AuthenticationScheme);
    }

    // [HttpGet("auth/entra/callback")]
    // public async Task<IActionResult> EntraCallback(string? returnUrl = null)
    // {
    //     try
    //     {
    //         Console.WriteLine("Point Reached!");
    //         // The user is already authenticated at this point via OpenIdConnect middleware
    //         if (User?.Identity?.IsAuthenticated == true)
    //         {
    //             Console.WriteLine("User is Authenticated");
    //             var email = User.FindFirst("preferred_username")?.Value
    //                        ?? User.FindFirst("email")?.Value;

    //             if (!string.IsNullOrEmpty(email))
    //             {
    //                 var user = await _userManager.FindByEmailAsync(email);

    //                 if (user != null)
    //                 {
    //                     // Sign in with Identity to establish session
    //                     await _signInManager.SignInAsync(user, isPersistent: true);
    //                 }
    //             }

    //             var finalUrl = string.IsNullOrEmpty(returnUrl)
    //                 ? _frontend.BaseUrl
    //                 : $"{_frontend.BaseUrl}{returnUrl}";

    //             return Redirect(finalUrl);
    //         }

    //         Console.WriteLine("User is not authenticated");
    //         return Redirect($"{_frontend.BaseUrl}/login?error=auth_failed");
    //     }
    //     catch (Exception ex)
    //     {
    //         Console.WriteLine($"Exception in EntraCallback: {ex.Message}");
    //         return Redirect($"{_frontend.BaseUrl}/login?error=exception");
    //     }
    // }

    [Authorize]
    [HttpGet("auth/user")]
    public async Task<IActionResult> Me()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            UserName = user.UserName,
            Roles = roles
        });
    }

    [Authorize]
    [HttpPost("auth/logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        
        // Sign out from Entra ID
        var callbackUrl = Url.Action(nameof(SignOutCallback), "Auth", null, Request.Scheme);
        
        return SignOut(
            new AuthenticationProperties { RedirectUri = callbackUrl },
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme
        );
    }

    [HttpGet("auth/signout-callback")]
    public IActionResult SignOutCallback()
    {
        return Redirect(_frontend.BaseUrl);
    }
}