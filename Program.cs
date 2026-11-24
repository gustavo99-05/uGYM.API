using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using uGYM.API.Data;
using uGYM.API.Models;
using uGYM.API.Services;

var criarAdministrador = args.Contains("--create-admin");
var redefinirSenha = args.Contains("--reset-password");
if (criarAdministrador && redefinirSenha)
{
    Console.Error.WriteLine("Execute apenas um comando: --create-admin ou --reset-password.");
    Environment.ExitCode = 1;
    return;
}
var builder = WebApplication.CreateBuilder(args.Where(arg => arg is not "--create-admin" and not "--reset-password").ToArray());
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
builder.Services.AddDbContext<UGymContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
builder.Services.Configure<PasswordHasherOptions>(o => o.IterationCount = 210_000);
var cookieSecurity = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.Cookie.Name = "uGYM.Session";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = cookieSecurity;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
    o.Events.OnValidatePrincipal = async ctx =>
    {
        var db = ctx.HttpContext.RequestServices.GetRequiredService<UGymContext>();
        var usuario = int.TryParse(ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Ativo)
            : null;
        if (!SessaoUsuario.Valida(ctx.Principal, usuario))
        {
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    };
});
builder.Services.AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-CSRF-TOKEN";
    o.Cookie.Name = "uGYM.Csrf";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = cookieSecurity;
});
builder.Services.AddCors(o => o.AddPolicy("Frontend", p => p
    .WithOrigins(builder.Configuration.GetSection("Frontend:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:8080" })
    .WithMethods("GET", "POST", "PUT").WithHeaders("Content-Type", "X-CSRF-TOKEN").AllowCredentials()));
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var app = builder.Build();
if (criarAdministrador || redefinirSenha)
{
    try
    {
        if (redefinirSenha) await RedefinicaoSenha.Executar(app.Services);
        else await AdministradorInicial.Criar(app.Services);
    }
    catch (ErroComandoAdministradorException ex)
    {
        Console.Error.WriteLine(ex.Message);
        Environment.ExitCode = 1;
    }
    catch (OperationCanceledException)
    {
        Console.Error.WriteLine("Operação cancelada. Nenhuma alteração realizada.");
        Environment.ExitCode = 1;
    }
    catch (Exception)
    {
        // Do not print database exception details, configuration, hashes, or credentials.
        Console.Error.WriteLine("Não foi possível concluir o comando. Verifique a conexão e a configuração do banco de dados. A API não foi iniciada.");
        Environment.ExitCode = 1;
    }
    return;
}
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["Referrer-Policy"] = "same-origin";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    if (ctx.Request.Path.StartsWithSegments("/api"))
    {
        ctx.Response.Headers.CacheControl = "no-store";
        if (!ctx.Request.IsHttps && !(app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Security:AllowInsecureLocalTesting")))
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.WriteAsJsonAsync(new { mensagem = "Use HTTPS para acessar a API pela rede." });
            return;
        }
    }
    try { await next(); }
    catch (Exception ex) when (ex is not OperationCanceledException && !ctx.Response.HasStarted)
    {
        app.Logger.LogError("Falha na requisição. Tipo: {Tipo}; referência: {Referencia}", ex.GetType().Name, ctx.TraceIdentifier);
        ctx.Response.StatusCode = 503;
        await ctx.Response.WriteAsJsonAsync(new { mensagem = "Serviço indisponível. Verifique a conexão e se as migrations foram aplicadas.", referencia = ctx.TraceIdentifier });
    }
});
// Development can serve the existing frontend folder; publish copies it to wwwroot.
app.UseDefaultFiles();
app.UseStaticFiles();
var frontendPath = app.Configuration["Frontend:Path"];
if (!string.IsNullOrEmpty(frontendPath) && Directory.Exists(frontendPath))
{
    var files = new PhysicalFileProvider(Path.GetFullPath(frontendPath));
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
}
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
if (!app.Environment.IsDevelopment()) { app.UseHsts(); app.UseHttpsRedirection(); }
app.UseRouting();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.StartsWithSegments("/api") && !HttpMethods.IsGet(ctx.Request.Method) && !HttpMethods.IsHead(ctx.Request.Method) && !HttpMethods.IsOptions(ctx.Request.Method))
    {
        try { await ctx.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(ctx); }
        catch (AntiforgeryValidationException)
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.WriteAsJsonAsync(new { mensagem = "Sessão de segurança inválida. Recarregue a tela e tente novamente.", codigo = "csrf" });
            return;
        }
    }
    await next();
});
app.MapControllers();
app.Run();
