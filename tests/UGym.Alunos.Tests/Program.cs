using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using uGYM.API.DTOs;
using uGYM.API.Services;

var checks = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new Exception("FALHOU: " + label);
    checks++;
    Console.WriteLine("OK " + label);
}
foreach (var cpf in new[] { "52998224725", "529.982.247-25", " 529.982.247-25 " })
    Check(DocumentoCpf.Valido(cpf), "CPF válido com e sem máscara/espaços externos");
foreach (var cpf in new[] { "", "123", "52998224724", "00000000000", "11111111111", "529x982x247x25", "529.982247-25", "５２９９８２２４７２５" })
    Check(!DocumentoCpf.Valido(cpf), "CPF inválido recusado");
Check(DocumentoCpf.Normalizar(" 529.982.247-25 ") == "52998224725", "normalização preserva os onze dígitos");
CadastrarAlunoDto Dados() => new() { Nome = "Aluno fictício", CPF = "52998224725", Plano = "Mensal" };
bool Valido(CadastrarAlunoDto dados) => Validator.TryValidateObject(dados, new ValidationContext(dados), new List<ValidationResult>(), true);
Check(Valido(Dados()), "campos opcionais podem ser omitidos");
var dados = Dados(); dados.Email = ""; dados.Observacoes = "";
Check(Valido(dados), "campos opcionais vazios do desktop são aceitos");
dados.Email = "contato@example.invalid";
Check(Valido(dados), "e-mail válido aceito");
dados.Email = "e-mail inválido";
Check(!Valido(dados), "e-mail inválido recusado");
foreach (var nome in new[] { "", "   ", " A ", new string('A', 151) })
{
    dados = Dados(); dados.Nome = nome;
    Check(!Valido(dados), "nome vazio, curto ou longo recusado");
}
dados = Dados(); dados.Plano = "";
Check(!Valido(dados), "plano obrigatório");
dados = Dados(); dados.Observacoes = new string('A', 2001);
Check(!Valido(dados), "observações excessivamente longas recusadas");
Console.WriteLine($"Cadastro: {checks} verificações passaram; nenhum banco foi acessado.");
var consulta = new ConsultaAlunosDto();
Check(consulta.Pagina == 1 && consulta.Tamanho == 20, "consulta começa com vinte alunos por página");
bool ConsultaValida(ConsultaAlunosDto valor) => Validator.TryValidateObject(valor, new ValidationContext(valor), new List<ValidationResult>(), true);
foreach (var invalida in new[] { new ConsultaAlunosDto { Pagina = 0 }, new ConsultaAlunosDto { Tamanho = 101 }, new ConsultaAlunosDto { Busca = new string('a', 151) } })
    Check(!ConsultaValida(invalida), "consulta rejeita limites inválidos");
Check(ConsultaValida(new ConsultaAlunosDto { Pagina = 100000, Tamanho = 100, Ativo = false, Visao = "mobile" }), "consulta aceita limites e filtros válidos");
Console.WriteLine($"Alunos: {checks} verificações passaram; nenhum banco foi acessado.");

// Optional real HTTPS startup smoke test. It never connects to a database and keeps certificate validation enabled.
if (args.Length > 0)
{
    var api = Path.GetFullPath(args.Single());
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    var start = new ProcessStartInfo("dotnet")
    {
        WorkingDirectory = Path.GetDirectoryName(api)!, UseShellExecute = false,
        RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
    };
    start.ArgumentList.Add(api);
    start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    start.Environment["ASPNETCORE_URLS"] = $"https://localhost:{port}";
    start.Environment["Security__AllowInsecureLocalTesting"] = "false";
    start.Environment["ConnectionStrings__DefaultConnection"] = "Server=unused.invalid;Database=NeverConnect;Integrated Security=true;Connect Timeout=1";
    start.Environment["Logging__EventLog__LogLevel__Default"] = "None";
    using var server = Process.Start(start)!;
    server.OutputDataReceived += (_, _) => { }; server.ErrorDataReceived += (_, _) => { };
    server.BeginOutputReadLine(); server.BeginErrorReadLine();
    try
    {
        using var client = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(2) };
        HttpResponseMessage? response = null;
        for (var i = 0; i < 40; i++)
        {
            if (server.HasExited) throw new Exception("A API não iniciou para o teste HTTPS.");
            try { response = await client.GetAsync($"https://localhost:{port}/api/auth/csrf"); break; }
            catch (HttpRequestException) { await Task.Delay(250); }
        }
        Check(response?.IsSuccessStatusCode == true, "API inicia em HTTPS com certificado confiável e proteção de transporte ativa");
        var csrf = JsonDocument.Parse(await response!.Content.ReadAsStringAsync());
        Check(!string.IsNullOrEmpty(csrf.RootElement.GetProperty("token").GetString()), "HTTPS permite obter token CSRF sem acessar o banco");
    }
    finally
    {
        if (!server.HasExited) { server.Kill(true); await server.WaitForExitAsync(); }
    }
}
