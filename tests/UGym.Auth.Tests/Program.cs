using System.Buffers.Binary;
using System.Data.Common;
using System.Diagnostics;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using uGYM.API.Data;
using uGYM.API.Models;
using uGYM.API.Services;

var checks = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new Exception("FALHOU: " + label);
    checks++;
    Console.WriteLine("OK " + label);
}
void Rejeita(Action action, string label)
{
    try { action(); }
    catch (ErroComandoAdministradorException) { Check(true, label); return; }
    Check(false, label);
}
string Aleatoria(int tamanho) => Convert.ToBase64String(RandomNumberGenerator.GetBytes(tamanho))[..tamanho];
foreach (var tamanho in new[] { 0, 11, 257 })
    Rejeita(() => SenhaAdministrador.Validar(Aleatoria(tamanho)), $"senha com {tamanho} caracteres recusada");
foreach (var tamanho in new[] { 12, 256 })
{
    var senha = Aleatoria(tamanho);
    SenhaAdministrador.ValidarConfirmacao(senha, senha);
    Check(true, $"senha com {tamanho} caracteres aceita");
}
var senhaAnterior = Aleatoria(32);
var senhaNova = Aleatoria(32);
Rejeita(() => SenhaAdministrador.ValidarConfirmacao(senhaNova, senhaNova.ToLowerInvariant() + "x"), "confirmação diferente recusada");
var hasher = new PasswordHasher<Usuario>(Options.Create(new PasswordHasherOptions { IterationCount = 210_000 }));
var user = new Usuario { Id = 17, Nome = "Administrador de teste", NomeNormalizado = "ADMINISTRADOR DE TESTE" };
user.SenhaHash = hasher.HashPassword(user, senhaAnterior);
var hashAnterior = user.SenhaHash;
ClaimsPrincipal Principal(Usuario usuario) => new(new ClaimsIdentity(new[]
{
    new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
    new Claim(SessaoUsuario.CredencialClaim, SessaoUsuario.CriarMarca(usuario.SenhaHash))
}, CookieAuthenticationDefaults.AuthenticationScheme));
var principalAnterior = Principal(user);
Check(SessaoUsuario.Valida(principalAnterior, user), "sessão atual aceita");
Check(!SessaoUsuario.Valida(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()) })), user), "cookie legado sem marca recusado");
Check(!SessaoUsuario.Valida(principalAnterior, null), "usuário excluído ou inexistente recusado");
user.Ativo = false;
Check(!SessaoUsuario.Valida(principalAnterior, user), "usuário inativo recusado");
user.Ativo = true;
var outro = new Usuario { Id = 18, SenhaHash = hashAnterior };
Check(!SessaoUsuario.Valida(principalAnterior, outro), "sessão de outro usuário recusada");

// No real connection is opened: connection and UPDATE execution are both suppressed.
var comando = new AtualizacaoInterceptada();
var options = new DbContextOptionsBuilder<UGymContext>()
    .UseSqlServer("Server=unused.invalid;Database=NeverConnect;Integrated Security=true;Connect Timeout=1")
    .AddInterceptors(new ConexaoBloqueada(), comando).Options;
await using var db = new UGymContext(options);
foreach (var (senha, confirmacao) in new[] { (Aleatoria(11), Aleatoria(11)), (Aleatoria(257), Aleatoria(257)), (senhaNova, senhaAnterior) })
{
    try { await RedefinicaoSenha.Redefinir(db, hasher, user, senha, confirmacao); Check(false, "entrada inválida deve falhar"); }
    catch (ErroComandoAdministradorException) { Check(comando.Execucoes == 0 && user.SenhaHash == hashAnterior, "entrada inválida não executa UPDATE"); }
}
await RedefinicaoSenha.Redefinir(db, hasher, user, senhaNova, senhaNova);
Check(comando.Execucoes == 1, "redefinição executa um único comando SQL");
Check(comando.Sql.Contains("UPDATE ", StringComparison.Ordinal) && comando.Sql.Contains("[Usuarios]", StringComparison.Ordinal)
    && comando.Sql.Split("SET ")[1].Split("FROM ")[0].Trim().StartsWith("[u].[SenhaHash] = ", StringComparison.Ordinal)
    && !comando.Sql.Contains("[Alunos]", StringComparison.Ordinal), "UPDATE restrito ao hash de Usuarios");
Check(comando.Sql.Contains("WHERE ", StringComparison.Ordinal) && comando.Sql.Contains("[u].[Id] = ", StringComparison.Ordinal)
    && comando.Sql.Contains("[u].[SenhaHash] = ", StringComparison.Ordinal)
    && comando.Valores.Contains(user.Id) && comando.Valores.Contains(hashAnterior), "UPDATE exige mesmo ID e hash anterior para impedir concorrência");
var novoHash = comando.Valores.OfType<string>().Single(x => x != hashAnterior);
Check(novoHash != senhaNova && hasher.VerifyHashedPassword(user, novoHash, senhaNova) == PasswordVerificationResult.Success, "novo hash usa PasswordHasher e aceita nova senha");
Check(hasher.VerifyHashedPassword(user, novoHash, senhaAnterior) == PasswordVerificationResult.Failed, "novo hash recusa senha anterior");
var hashBytes = Convert.FromBase64String(novoHash);
Check(BinaryPrimitives.ReadUInt32BigEndian(hashBytes.AsSpan(5, 4)) == 210_000, "hash mantém 210.000 iterações");
user.SenhaHash = novoHash;
Check(!SessaoUsuario.Valida(principalAnterior, user), "sessão anterior recusada após redefinição");
Check(SessaoUsuario.Valida(Principal(user), user), "novo login aceito após redefinição");
var marcaAntes = SessaoUsuario.CriarMarca(user.SenhaHash);
user.SenhaHash = hasher.HashPassword(user, senhaNova);
Check(SessaoUsuario.CriarMarca(user.SenhaHash) != marcaAntes, "reutilizar senha também muda marca da sessão por causa do salt");
comando.Resultado = 0;
try { await RedefinicaoSenha.Redefinir(db, hasher, user, senhaNova, senhaNova); Check(false, "alteração concorrente deve falhar"); }
catch (ErroComandoAdministradorException) { Check(true, "nenhuma linha atualizada reporta conflito em vez de sucesso"); }

var api = Path.GetFullPath(args.Single());
async Task<(int Codigo, string Saida)> Cli(params string[] argumentos)
{
    var start = new ProcessStartInfo("dotnet")
    {
        WorkingDirectory = Path.GetDirectoryName(api)!, UseShellExecute = false,
        RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
    };
    start.ArgumentList.Add(api);
    foreach (var argumento in argumentos) start.ArgumentList.Add(argumento);
    using var process = Process.Start(start)!;
    process.StandardInput.Close();
    var saida = process.StandardOutput.ReadToEndAsync();
    var erro = process.StandardError.ReadToEndAsync();
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    try { await process.WaitForExitAsync(timeout.Token); }
    catch { process.Kill(entireProcessTree: true); throw; }
    return (process.ExitCode, await saida + await erro);
}
var redirecionado = await Cli("--reset-password");
Check(redirecionado.Codigo != 0 && redirecionado.Saida.Contains("terminal interativo", StringComparison.Ordinal), "CLI recusa entrada redirecionada antes de acessar banco");
Check(!redirecionado.Saida.Contains("Now listening", StringComparison.Ordinal), "CLI não inicia servidor HTTP");
var conflito = await Cli("--create-admin", "--reset-password");
Check(conflito.Codigo != 0 && conflito.Saida.Contains("apenas um comando", StringComparison.Ordinal), "CLI recusa comandos combinados");
Console.WriteLine($"Autenticação: {checks} verificações passaram; nenhum banco foi acessado e nenhuma migration foi executada.");

sealed class ConexaoBloqueada : DbConnectionInterceptor
{
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
        => InterceptionResult.Suppress();
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
        InterceptionResult result, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(InterceptionResult.Suppress());
}

sealed class AtualizacaoInterceptada : DbCommandInterceptor
{
    public int Execucoes { get; private set; }
    public int Resultado { get; set; } = 1;
    public string Sql { get; private set; } = "";
    public object?[] Valores { get; private set; } = Array.Empty<object?>();

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Execucoes++;
        Sql = command.CommandText;
        Valores = command.Parameters.Cast<DbParameter>().Select(x => x.Value).ToArray();
        return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(Resultado));
    }
}
