using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using uGYM.API.Data;
using uGYM.API.Models;
namespace uGYM.API.Services;
public static class AdministradorInicial
{
    public static async Task Criar(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UGymContext>();
        if (await db.Usuarios.AnyAsync()) throw new ErroComandoAdministradorException("Administrador já configurado. Nenhuma alteração realizada.");
        SenhaAdministrador.ExigirTerminalInterativo();
        Console.Write("Nome do primeiro administrador: ");
        var nome = (Console.ReadLine() ?? "").Trim();
        var senha = SenhaAdministrador.LerOculta("Senha (12–256 caracteres, entrada oculta): ");
        if (nome.Length is < 3 or > 80)
            throw new ErroComandoAdministradorException("Nome deve ter 3–80 caracteres.");
        SenhaAdministrador.Validar(senha);
        var user = new Usuario { Nome = nome, NomeNormalizado = nome.ToUpperInvariant() };
        user.SenhaHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>().HashPassword(user, senha);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        if (await db.Usuarios.AnyAsync()) throw new ErroComandoAdministradorException("Administrador já configurado.");
        db.Usuarios.Add(user);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        Console.WriteLine("Administrador criado. A senha não foi armazenada em texto puro.");
    }
}
