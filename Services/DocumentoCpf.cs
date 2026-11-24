namespace uGYM.API.Services;

public static class DocumentoCpf
{
    public static string Normalizar(string valor) => valor.Trim().Replace(".", "").Replace("-", "");

    public static bool Valido(string valor)
    {
        var texto = valor.Trim();
        var cpf = Normalizar(texto);
        // Accept only eleven digits or the conventional 000.000.000-00 format.
        if (texto != cpf && (texto.Length != 14 || texto[3] != '.' || texto[7] != '.' || texto[11] != '-'))
            return false;
        if (cpf.Length != 11 || cpf.Any(c => !char.IsAsciiDigit(c)) || cpf.All(c => c == cpf[0]))
            return false;
        for (var digito = 9; digito <= 10; digito++)
        {
            var soma = 0;
            for (var i = 0; i < digito; i++) soma += (cpf[i] - '0') * (digito + 1 - i);
            var verificador = soma % 11 < 2 ? 0 : 11 - soma % 11;
            if (cpf[digito] - '0' != verificador) return false;
        }
        return true;
    }
}
