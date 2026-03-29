using System.Security.Claims;
using BanterBotSports.DAL;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BanterBotSports.Web.Services;

public class AppUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<AppUser, IdentityRole>
{
    public AppUserClaimsPrincipalFactory(
        UserManager<AppUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options) { }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        var display = user.NombreDisplay ?? user.UserName ?? user.Id;
        identity.AddClaim(new Claim("NombreDisplay", display));
        return identity;
    }
}
