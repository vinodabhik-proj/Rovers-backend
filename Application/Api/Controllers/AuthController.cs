using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Rovers_backend.Models;
using System.Security.Claims;
using Microsoft.Extensions.Options;
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

    [HttpGet("auth/facebook/login")]
    public IActionResult FacebookLogin()
    {
        var props = new AuthenticationProperties
        {
            RedirectUri = "/auth/facebook/callback"
        };

        return Challenge(props, "Facebook");
    }

    [HttpGet("auth/facebook/callback")]
    public async Task<IActionResult> FacebookCallback()
    {
        var info = await _signInManager.GetExternalLoginInfoAsync();

        if (info == null)
        {
            return Redirect($"{_frontend.BaseUrl}/login?error=oauth");
        }

        Console.WriteLine("Point Reached");

        var signInResult = await _signInManager.ExternalLoginSignInAsync(
            info.LoginProvider,
            info.ProviderKey,
            isPersistent: true);

        if (!signInResult.Succeeded)
        {
            var firstName = info.Principal.FindFirstValue("first_name") ?? "";
            var lastName = info.Principal.FindFirstValue("last_name") ?? "";
            var email = info.Principal.FindFirstValue("email") ?? "";

            var user = new ApplicationUser
            {
                UserName = Guid.NewGuid().ToString(),
                FirstName = firstName,
                LastName = lastName,
                Email = email
            };

            await _userManager.CreateAsync(user);
            await _userManager.AddLoginAsync(user, info);
            await _userManager.AddToRoleAsync(user, "User");

            await _signInManager.SignInAsync(user, isPersistent: true);
        }

        return Redirect(_frontend.BaseUrl);
    }
}
