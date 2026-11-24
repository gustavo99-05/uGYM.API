using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using uGYM.API.Data;
using uGYM.API.DTOs;
using uGYM.API.Models;
using uGYM.API.Services;
namespace uGYM.API.Controllers;
[ApiController, Route("api/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AuthController(UGymContext db, IPasswordHasher<Usuario> hasher, IAntiforgery antiforgery) : ControllerBase
{
    private static readonly Usuario Dummy = new();
    private static readonly string DummyHash = new PasswordHasher<Usuario>(
        Microsoft.Extensions.Options.Options.Create(new PasswordHasherOptions { IterationCount = 210_000 }))
        .HashPassword(Dummy, Guid.NewGuid().ToString());
    [AllowAnonymous, HttpGet("csrf")]
    public IActionResult Csrf() => Ok(new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });
    [AllowAnonymous, HttpPost("login"), EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var user = await db.Usuarios.SingleOrDefaultAsync(x => x.NomeNormalizado == dto.Usuario.Trim().ToUpperInvariant());
        var result = hasher.VerifyHashedPassword(user ?? Dummy, user?.SenhaHash ?? DummyHash, dto.Senha);
        if (user is null || !user.Ativo || result == PasswordVerificationResult.Failed)
            return Unauthorized(new { mensagem = "Usuário ou senha inválidos." });
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            var novoHash = hasher.HashPassword(user, dto.Senha);
            var atualizados = await db.Usuarios.Where(x => x.Id == user.Id && x.Ativo && x.SenhaHash == user.SenhaHash)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.SenhaHash, novoHash));
            if (atualizados != 1) return Unauthorized(new { mensagem = "Usuário ou senha inválidos." });
            user.SenhaHash = novoHash;
        }
        var identity = new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Nome), new Claim(ClaimTypes.Role, "Administrador"),
            new Claim(SessaoUsuario.CredencialClaim, SessaoUsuario.CriarMarca(user.SenhaHash))
        }, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false });
        return Ok(new { nome = user.Nome });
    }
    [HttpGet("session")]
    public IActionResult Session() => Ok(new { nome = User.Identity!.Name });
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }
}
