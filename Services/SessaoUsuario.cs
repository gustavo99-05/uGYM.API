using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using uGYM.API.Models;

namespace uGYM.API.Services;

public static class SessaoUsuario
{
    public const string CredencialClaim = "ugym:credencial";

    // The protected authentication cookie contains only a digest, never the password or its stored hash.
    public static string CriarMarca(string senhaHash) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(senhaHash)));

    public static bool Valida(ClaimsPrincipal? principal, Usuario? usuario)
    {
        if (usuario is null || !usuario.Ativo ||
            principal?.FindFirstValue(ClaimTypes.NameIdentifier) != usuario.Id.ToString()) return false;
        var marca = principal.FindFirstValue(CredencialClaim);
        return marca is { Length: 64 } && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(marca), Encoding.UTF8.GetBytes(CriarMarca(usuario.SenhaHash)));
    }
}
