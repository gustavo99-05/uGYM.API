using System.Text;

namespace uGYM.API.Services;

public static class SenhaAdministrador
{
    public const int TamanhoMinimo = 12;
    public const int TamanhoMaximo = 256;

    public static void Validar(string senha)
    {
        if (senha.Length is < TamanhoMinimo or > TamanhoMaximo)
            throw new ErroComandoAdministradorException("A senha deve ter de 12 a 256 caracteres.");
    }

    public static void ValidarConfirmacao(string senha, string confirmacao)
    {
        Validar(senha);
        if (!string.Equals(senha, confirmacao, StringComparison.Ordinal))
            throw new ErroComandoAdministradorException("As senhas não conferem. Nenhuma alteração realizada.");
    }

    public static void ExigirTerminalInterativo()
    {
        if (Console.IsInputRedirected)
            throw new ErroComandoAdministradorException("Use um terminal interativo. Senhas não são aceitas por redirecionamento ou pipe.");
    }

    public static string LerOculta(string mensagem)
    {
        ExigirTerminalInterativo();
        Console.Write(mensagem);
        var senha = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return senha.ToString();
            }
            if (key.Key == ConsoleKey.Escape)
            {
                Console.WriteLine();
                throw new OperationCanceledException();
            }
            if (key.Key == ConsoleKey.Backspace)
            {
                if (senha.Length > 0) senha.Length--;
            }
            else if (!char.IsControl(key.KeyChar)) senha.Append(key.KeyChar);
        }
    }
}

public sealed class ErroComandoAdministradorException(string mensagem) : InvalidOperationException(mensagem);
