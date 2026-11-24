using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using uGYM.API.Data;
using uGYM.API.Models;
using uGYM.API.Services;

var root = Path.GetFullPath(args.FirstOrDefault() ?? Directory.GetCurrentDirectory());
using var config = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "appsettings.json")));
var source = config.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString();
var name = "UGymMobileTests_" + Guid.NewGuid().ToString("N");
var conn = new SqlConnectionStringBuilder(source) { InitialCatalog = name, ConnectTimeout = 8 };
var options = new DbContextOptionsBuilder<UGymContext>().UseSqlServer(conn.ConnectionString).Options;
await using var db = new UGymContext(options);
Process? server = null;
var created = false;
var checks = 0;
var password = Guid.NewGuid().ToString("N") + "aA!";
void Check(bool condition, string label) { if (!condition) throw new Exception("FALHOU: " + label); checks++; Console.WriteLine("OK " + label); }
try
{
    // This name is generated internally. Never migrate, seed, or delete the configured UGymDB.
    created = true;
    await db.Database.MigrateAsync();
    var admin = new Usuario { Nome = "Equipe Teste", NomeNormalizado = "EQUIPE TESTE" };
    admin.SenhaHash = new PasswordHasher<Usuario>().HashPassword(admin, password);
    db.Usuarios.Add(admin);
    Aluno Student(string nome, bool ativo) => new() { Nome = nome, Ativo = ativo, CPF = "00000000000", Email = "teste@example.invalid", Telefone = "00000000000", Plano = "Mensal", Instrutor = "Equipe", Observacoes = "Registro fictício de teste isolado", DataCadastro = DateTime.UtcNow };
    var active = Student("Ana Movimento", true);
    var inactive = Student("Bruno Inativo", false);
    var browserStudent = Student("Carla Treino", true);
    var boundary = Student("Daniel Histórico", true);
    db.Alunos.AddRange(active, inactive, browserStudent, boundary);
    await db.SaveChangesAsync();
    var day = RelogioAcademia.Dia(DateTime.UtcNow);
    var start = RelogioAcademia.InicioUtc(day);
    db.CheckIns.AddRange(new CheckIn { AlunoId = boundary.Id, Permitido = true, DataHora = start.AddMinutes(-1) },
        new CheckIn { AlunoId = boundary.Id, Permitido = true, DataHora = start.AddMinutes(1) });
    await db.SaveChangesAsync();
    Check(RelogioAcademia.Dia(new DateTime(2026,11,11,2,59,0,DateTimeKind.Utc)) == new DateOnly(2026,11,10), "dia São Paulo antes de 03:00 UTC");
    Check(RelogioAcademia.Dia(new DateTime(2026,11,11,3,0,0,DateTimeKind.Utc)) == new DateOnly(2026,11,11), "virada do dia São Paulo");
    var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
    var url = "http://localhost:" + port;
    var info = new ProcessStartInfo("dotnet") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
    info.ArgumentList.Add(Path.Combine(root, "artifacts/build/uGYM.API.dll"));
    info.Environment["ASPNETCORE_URLS"] = url;
    info.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    info.Environment["Security__AllowInsecureLocalTesting"] = "true";
    info.Environment["ConnectionStrings__DefaultConnection"] = conn.ConnectionString;
    info.Environment["Logging__LogLevel__Default"] = "Warning";
    info.Environment["Logging__EventLog__LogLevel__Default"] = "None";
    info.Environment["Frontend__Path"] = Environment.GetEnvironmentVariable("UGYM_FRONTEND_PATH") ?? "";
    // Provisioning must parse its flag and refuse a second administrator before asking for input.
    info.ArgumentList.Add("--create-admin");
    using(var bootstrap=Process.Start(info)!) {
        var outputTask=bootstrap.StandardOutput.ReadToEndAsync();
        var errorTask=bootstrap.StandardError.ReadToEndAsync();
        await bootstrap.WaitForExitAsync();
        var bootstrapOutput=await outputTask+await errorTask;
        Check(bootstrap.ExitCode!=0 && bootstrapOutput.Contains("Administrador já configurado"),"provisionamento recusa segundo administrador");
    }
    info.ArgumentList.RemoveAt(info.ArgumentList.Count-1);
    server = Process.Start(info)!;
    // Drain without printing request or database details.
    server.OutputDataReceived += (_, _) => { }; server.ErrorDataReceived += (_, _) => { };
    server.BeginOutputReadLine(); server.BeginErrorReadLine();
    using var handler = new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false };
    using var client = new HttpClient(handler) { BaseAddress = new Uri(url) };
    for (var i=0;i<60;i++)
    {
        if (server.HasExited) throw new Exception("API de teste encerrou antes de iniciar.");
        try { if ((await client.GetAsync("/api/auth/csrf")).IsSuccessStatusCode) break; } catch(HttpRequestException) { }
        await Task.Delay(250);
    }
    async Task<string> Csrf() { var data = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf"); return data.GetProperty("token").GetString()!; }
    async Task<HttpResponseMessage> Send(string path, object body, HttpMethod? method = null, bool csrf = true)
    {
        using var request = new HttpRequestMessage(method ?? HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        if (csrf) request.Headers.Add("X-CSRF-TOKEN", await Csrf());
        return await client.SendAsync(request);
    }
    Check((await client.GetAsync("/api/Aluno")).StatusCode == HttpStatusCode.Unauthorized, "alunos exigem autenticação");
    Check((await client.GetAsync("/api/CheckIn/resumo")).StatusCode == HttpStatusCode.Unauthorized, "resumo exige autenticação");
    using(var cors = new HttpRequestMessage(HttpMethod.Options,"/api/Aluno")) {
        cors.Headers.Add("Origin","http://localhost:8080"); cors.Headers.Add("Access-Control-Request-Method","PUT");
        cors.Headers.Add("Access-Control-Request-Headers","content-type,x-csrf-token");
        var response=await client.SendAsync(cors);
        Check(response.Headers.TryGetValues("Access-Control-Allow-Origin",out var origins) && origins.Single()=="http://localhost:8080" && response.Headers.GetValues("Access-Control-Allow-Credentials").Single()=="true","CORS permite desktop com credenciais");
    }
    using(var cors = new HttpRequestMessage(HttpMethod.Options,"/api/Aluno")) {
        cors.Headers.Add("Origin","https://origem-nao-autorizada.invalid"); cors.Headers.Add("Access-Control-Request-Method","PUT");
        var response=await client.SendAsync(cors);
        Check(!response.Headers.Contains("Access-Control-Allow-Origin"),"CORS rejeita origem não autorizada");
    }
    Check((await Send("/api/auth/login", new { usuario=admin.Nome, senha="incorreta" })).StatusCode == HttpStatusCode.Unauthorized, "login inválido");
    Check((await Send("/api/auth/login", new { usuario=admin.Nome, senha=password }, csrf:false)).StatusCode == HttpStatusCode.BadRequest, "login exige CSRF");
    Check((await Send("/api/auth/login", new { usuario=admin.Nome, senha=password })).IsSuccessStatusCode, "login válido");
    Check((await client.GetAsync("/api/auth/session")).IsSuccessStatusCode, "sessão autenticada");
    var rows = await client.GetFromJsonAsync<JsonElement>("/api/Aluno?visao=mobile");
    Check(rows.GetProperty("total").GetInt32()==4 && rows.GetProperty("itens").GetArrayLength()==4 && !rows.GetProperty("itens")[0].TryGetProperty("cpf",out _), "consulta mobile paginada sem CPF");
    Check((await client.GetAsync("/api/Aluno/"+active.Id)).IsSuccessStatusCode, "busca de aluno por ID");
    Check((await client.GetAsync("/api/Aluno/2147483647")).StatusCode==HttpStatusCode.NotFound, "aluno inexistente");
    Check((await Send("/api/Aluno/"+active.Id,new {ativo=false},HttpMethod.Put,csrf:false)).StatusCode==HttpStatusCode.BadRequest, "alteração exige CSRF");
    Check((await Send("/api/Aluno/"+active.Id,new {ativo=false},HttpMethod.Put)).IsSuccessStatusCode, "inativação de aluno");
    Check((await Send("/api/CheckIn",new {alunoId=active.Id})).StatusCode==HttpStatusCode.Conflict, "aluno inativo bloqueado");
    Check((await Send("/api/Aluno/"+active.Id,new {ativo=true},HttpMethod.Put)).IsSuccessStatusCode, "reativação de aluno");
    Check((await Send("/api/CheckIn",new {alunoId=0})).StatusCode==HttpStatusCode.BadRequest, "check-in inválido");
    Check((await Send("/api/CheckIn",new {alunoId=int.MaxValue})).StatusCode==HttpStatusCode.NotFound, "check-in de aluno inexistente");
    // Reuse one valid authenticated CSRF token across concurrent requests.
    var token=await Csrf();
    async Task<HttpResponseMessage> Concurrent() {
        using var req = new HttpRequestMessage(HttpMethod.Post,"/api/CheckIn") {Content=JsonContent.Create(new {alunoId=active.Id})};
        req.Headers.Add("X-CSRF-TOKEN",token); return await client.SendAsync(req);
    }
    var concurrent=await Task.WhenAll(Enumerable.Range(0,5).Select(_=>Concurrent()));
    Check(concurrent.Count(r=>r.StatusCode==HttpStatusCode.Created)==1 && concurrent.Count(r=>r.StatusCode==HttpStatusCode.Conflict)==4,"cinco check-ins simultâneos: um 201 e quatro 409");
    Check((await Send("/api/CheckIn",new {alunoId=active.Id})).StatusCode==HttpStatusCode.Conflict,"duplicidade sequencial bloqueada");
    Check((await Send("/api/CheckIn",new {alunoId=boundary.Id})).StatusCode==HttpStatusCode.Conflict,"histórico legado também bloqueia duplicidade");
    var history=await client.GetFromJsonAsync<JsonElement>("/api/CheckIn/aluno/"+active.Id);
    Check(history.GetProperty("total").GetInt32()==1 && history.GetProperty("itens")[0].GetProperty("dataHora").GetString()!.EndsWith('Z'),"histórico persistido com data UTC");
    var summary=await client.GetFromJsonAsync<JsonElement>("/api/CheckIn/aluno/"+active.Id+"/resumo");
    Check(summary.GetProperty("total").GetInt32()==1 && summary.GetProperty("mesAtual").GetInt32()==1,"resumo individual");
    var today=await client.GetFromJsonAsync<JsonElement>("/api/CheckIn/aluno/"+boundary.Id+"?periodo=hoje");
    Check(today.GetProperty("total").GetInt32()==1,"filtro hoje respeita São Paulo");
    var paged=await client.GetFromJsonAsync<JsonElement>("/api/CheckIn/aluno/"+boundary.Id+"?tamanho=1&pagina=2");
    Check(paged.GetProperty("total").GetInt32()==2 && paged.GetProperty("itens").GetArrayLength()==1,"paginação do histórico");
    Check((await client.GetAsync("/api/CheckIn/aluno/"+active.Id+"?periodo=invalido")).StatusCode==HttpStatusCode.BadRequest,"filtro inválido");
    var empty=await client.GetFromJsonAsync<JsonElement>("/api/CheckIn/aluno/"+inactive.Id);
    Check(empty.GetProperty("total").GetInt32()==0,"histórico vazio");
    await using(var verification=new UGymContext(options)) {
        var saved=await verification.CheckIns.SingleAsync(x=>x.AlunoId==active.Id);
        Check(saved.OperadorId==admin.Id && saved.DiaLocal==day,"persistência no SQL Server e operador responsável");
        Check(await verification.Usuarios.AnyAsync(x=>x.SenhaHash!=password),"senha armazenada como hash");
    }
    var dashboard=await client.GetFromJsonAsync<JsonElement>("/api/CheckIn/resumo");
    Check(dashboard.GetProperty("totalAlunos").GetInt32()==4 && dashboard.GetProperty("checkInsHoje").GetInt32()==2,"dashboard com dados do banco");
    // More than one page, repeated names, accents and filters exercise the actual SQL queries.
    var extras = Enumerable.Range(0, 25).Select(i => Student("Aluno Paginação", i % 2 == 0)).ToArray();
    db.Alunos.AddRange(extras);
    await db.SaveChangesAsync();
    var primeira = await client.GetFromJsonAsync<JsonElement>("/api/Aluno?visao=mobile");
    var segunda = await client.GetFromJsonAsync<JsonElement>("/api/Aluno?visao=mobile&pagina=2");
    var primeirosIds = primeira.GetProperty("itens").EnumerateArray().Select(x => x.GetProperty("id").GetInt32()).ToArray();
    var segundosIds = segunda.GetProperty("itens").EnumerateArray().Select(x => x.GetProperty("id").GetInt32()).ToArray();
    Check(primeira.GetProperty("total").GetInt32() == 29 && primeirosIds.Length == 20 && segunda.GetProperty("itens").GetArrayLength() == 9,
        "alunos: primeira página limitada a 20 e segunda com os nove restantes");
    Check(!primeirosIds.Intersect(segundosIds).Any() && primeirosIds.SequenceEqual(extras.Take(20).Select(x => x.Id)),
        "alunos: ordem estável por nome/ID sem repetir registros entre páginas");
    var buscaAlunos = await client.GetFromJsonAsync<JsonElement>("/api/Aluno?visao=mobile&busca=PAGINACAO&ativo=false&tamanho=100");
    Check(buscaAlunos.GetProperty("total").GetInt32() == 12 && buscaAlunos.GetProperty("itens").EnumerateArray().All(x => !x.GetProperty("ativo").GetBoolean()),
        "alunos: busca no SQL ignora acentos/maiúsculas e combina filtro de inativos");
    var paginaVazia = await client.GetFromJsonAsync<JsonElement>("/api/Aluno?pagina=100000&tamanho=100");
    Check(paginaVazia.GetProperty("total").GetInt32() == 29 && paginaVazia.GetProperty("itens").GetArrayLength() == 0, "alunos: página além do fim retorna lista vazia");
    var buscaVazia = await client.GetFromJsonAsync<JsonElement>("/api/Aluno?busca=%25");
    Check(buscaVazia.GetProperty("total").GetInt32() == 0, "alunos: busca trata percentual como texto literal");
    foreach (var filtroInvalido in new[] { "pagina=0", "pagina=100001", "tamanho=0", "tamanho=101", "ativo=invalido", "visao=invalida", "busca=" + new string('a', 151) })
        Check((await client.GetAsync("/api/Aluno?" + filtroInvalido)).StatusCode == HttpStatusCode.BadRequest, "alunos: rejeita filtro/paginação inválidos");
    db.Alunos.RemoveRange(extras);
    await db.SaveChangesAsync();
    // Registration regressions run only in this executor's disposable database.
    var cpfCadastro = "52998224725";
    object Cadastro(string cpf, string email = "") => new { nome = "Cadastro fictício", cpf, email, plano = "Mensal" };
    Check((await Send("/api/Aluno", Cadastro("52998224724"))).StatusCode == HttpStatusCode.BadRequest, "cadastro rejeita CPF com verificador inválido");
    Check((await Send("/api/Aluno", Cadastro(cpfCadastro, "invalido"))).StatusCode == HttpStatusCode.BadRequest, "cadastro rejeita e-mail inválido");
    Check((await Send("/api/Aluno", new { nome = "   ", cpf = cpfCadastro, plano = "Mensal" })).StatusCode == HttpStatusCode.BadRequest, "cadastro rejeita nome vazio");
    Check((await Send("/api/Aluno", Cadastro("529.982.247-25"))).IsSuccessStatusCode, "cadastro aceita CPF com máscara e opcionais vazios");
    Check((await Send("/api/Aluno", Cadastro(cpfCadastro))).StatusCode == HttpStatusCode.Conflict, "cadastro rejeita CPF duplicado sem máscara");
    var tokenCadastro = await Csrf();
    async Task<HttpResponseMessage> CadastrarConcorrente()
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/Aluno") { Content = JsonContent.Create(Cadastro("11144477735")) };
        req.Headers.Add("X-CSRF-TOKEN", tokenCadastro);
        return await client.SendAsync(req);
    }
    var cadastros = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => CadastrarConcorrente()));
    Check(cadastros.Count(r => r.IsSuccessStatusCode) == 1 && cadastros.Count(r => r.StatusCode == HttpStatusCode.Conflict) == 3,
        "quatro cadastros simultâneos: um sucesso e três conflitos");
    await using (var verification = new UGymContext(options))
    {
        var salvo = await verification.Alunos.SingleAsync(x => x.CPF == cpfCadastro);
        Check(salvo.Ativo && salvo.Email == "" && salvo.Instrutor == "" && salvo.DataCadastro > DateTime.UtcNow.AddMinutes(-5), "cadastro normaliza CPF e preenche campos do servidor");
        // Simulate an old formatted CPF and check that it is also recognized.
        salvo.CPF = "529.982.247-25";
        await verification.SaveChangesAsync();
        Check((await Send("/api/Aluno", Cadastro(cpfCadastro))).StatusCode == HttpStatusCode.Conflict, "cadastro reconhece duplicidade de CPF legado com máscara");
        verification.Alunos.RemoveRange(await verification.Alunos.Where(x => x.CPF == "529.982.247-25" || x.CPF == "11144477735").ToListAsync());
        await verification.SaveChangesAsync();
    }
    Check((await Send("/api/auth/logout",new {})).StatusCode==HttpStatusCode.NoContent,"logout");
    Check((await client.GetAsync("/api/auth/session")).StatusCode==HttpStatusCode.Unauthorized,"sessão encerrada");
    Console.WriteLine($"Backend: {checks} verificações passaram.");
    if(args.Contains("--browser")) {
        var browserInfo=new ProcessStartInfo("node") { WorkingDirectory=root, UseShellExecute=false };
        browserInfo.ArgumentList.Add(Path.Combine(root,"tests/browser.cjs"));
        browserInfo.Environment["UGYM_TEST_URL"]=url;
        browserInfo.Environment["UGYM_TEST_USER"]=admin.Nome;
        browserInfo.Environment["UGYM_TEST_PASSWORD"]=password;
        using var browser=Process.Start(browserInfo)!; await browser.WaitForExitAsync();
        Check(browser.ExitCode==0,"testes de interface no navegador");
    }
    // Verify fail-closed transport after disabling the synthetic HTTP exception.
    server.Kill(true); await server.WaitForExitAsync();
    info.Environment["Security__AllowInsecureLocalTesting"] = "false";
    server=Process.Start(info)!;
    server.OutputDataReceived += (_,_)=>{};server.ErrorDataReceived += (_,_)=>{};
    server.BeginOutputReadLine();server.BeginErrorReadLine();
    HttpResponseMessage? transport=null;
    for(var i=0;i<60;i++) {
        try { transport=await client.GetAsync("/api/auth/csrf");break; }catch(HttpRequestException){await Task.Delay(250);}
    }
    Check(transport?.StatusCode==HttpStatusCode.BadRequest,"HTTP bloqueado sem exceção explícita para testes fictícios");
    Console.WriteLine($"Executor completo: {checks} verificações passaram, incluindo transporte e subprocesso de UI quando solicitado.");
}
finally
{
    if(server is {HasExited:false}) { server.Kill(true); await server.WaitForExitAsync(); }
    SqlConnection.ClearAllPools();
    if(created && conn.InitialCatalog==name && name.StartsWith("UGymMobileTests_",StringComparison.Ordinal)) {
        await db.Database.EnsureDeletedAsync(); Console.WriteLine("Banco temporário de testes removido. UGymDB preservado.");
    }
}
