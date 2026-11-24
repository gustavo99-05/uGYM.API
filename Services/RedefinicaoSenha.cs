using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using uGYM.API.Data;
using uGYM.API.Models;

namespace uGYM.API.Services;

public static class RedefinicaoSenha
{
    public static async Task Executar(IServiceProvider services)
    {
        SenhaAdministrador.ExigirTerminalInterativo();
        Console.Write("Nome do administrador existente: ");
        var nome = (Console.ReadLine() ?? "").Trim();
        if (nome.Length is < 3 or > 80)
            throw new ErroComandoAdministradorException("O nome deve ter de 3 a 80 caracteres. Nenhuma alteração realizada.");

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<UGymContext>();
        var nomeNormalizado = nome.ToUpperInvariant();
        var user = await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(x => x.NomeNormalizado == nomeNormalizado);
        if (user is null)
            throw new ErroComandoAdministradorException("Administrador não encontrado. Nenhuma alteração realizada.");

        var senha = SenhaAdministrador.LerOculta("Nova senha (12–256 caracteres, entrada oculta; Esc cancela): ");
        SenhaAdministrador.Validar(senha);
        var confirmacao = SenhaAdministrador.LerOculta("Confirme a nova senha (entrada oculta): ");
        await Redefinir(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>(), user, senha, confirmacao);

        Console.WriteLine("Senha redefinida. As sessões anteriores serão rejeitadas pela API atualizada. Entre novamente no uGYM Mobile.");
    }

    public static async Task Redefinir(UGymContext db, IPasswordHasher<Usuario> hasher, Usuario user, string senha, string confirmacao)
    {
        SenhaAdministrador.ValidarConfirmacao(senha, confirmacao);
        var novoHash = hasher.HashPassword(user, senha);
        // A single conditional UPDATE preserves every other field and refuses concurrent password changes.
        var atualizados = await db.Usuarios
            .Where(x => x.Id == user.Id && x.SenhaHash == user.SenhaHash)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.SenhaHash, novoHash));
        if (atualizados != 1)
            throw new ErroComandoAdministradorException("A conta foi alterada durante a operação. Execute o comando novamente.");
    }
}
