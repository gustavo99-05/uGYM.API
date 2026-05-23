# uGYM.API — Guia de Instalação

Este documento descreve passos práticos para preparar, executar e publicar a API `uGYM.API` em ambiente de desenvolvimento e produção.

## Requisitos
- .NET 8 SDK — verifique com `dotnet --version`
- Visual Studio 2022 (suporte .NET 8) ou CLI `dotnet`
- Git
- Banco de dados compatível (ex.: SQL Server, PostgreSQL) conforme configuração do projeto
- (Opcional) Docker / Docker Compose

## Clonar repositório
```
git clone https://github.com/gustavo99-05/uGYM.API.git
cd uGYM.API
```

## Restaurar e compilar
```
dotnet restore
dotnet build -c Debug
```

## Configuração de ambiente
Defina variáveis de ambiente ou ajuste `appsettings.json` conforme o projeto:

- PowerShell:
```
$Env:ASPNETCORE_ENVIRONMENT="Development"
$Env:ConnectionStrings__DefaultConnection="Server=localhost;Database=uGYM;User Id=sa;Password=SuaSenha;"
```

- Bash:
```
export ASPNETCORE_ENVIRONMENT=Development
export ConnectionStrings__DefaultConnection="Server=localhost;Database=uGYM;User Id=sa;Password=SuaSenha;"
```

Se o projeto usar User Secrets:
```
dotnet user-secrets init --project ./<caminho-para-o-projeto.csproj>
dotnet user-secrets set "Chave" "Valor" --project ./<caminho-para-o-projeto.csproj>
```

## Migrações (EF Core)
Instale a ferramenta se necessário:
```
dotnet tool install --global dotnet-ef
```
Aplicar migrações (especifique `--project`/`--startup-project` se preciso):
```
dotnet ef database update --project ./<ProjetoComDbContext> --startup-project ./<ProjetoInicializacao>
```

## Executar localmente
- Visual Studio: abra a solução e pressione F5.
- CLI:
```
dotnet run --project ./<caminho-para-o-projeto.csproj>
```
A API normalmente ficará em `https://localhost:5001` ou `http://localhost:5000` — verifique a saída do console.

## Testes
```
dotnet test
```

## Docker (opcional)
Construir imagem:
```
docker build -t ugym.api .
```
Executar container:
```
docker run -e "ASPNETCORE_ENVIRONMENT=Production" -p 5000:80 ugym.api
```
Se houver `docker-compose.yml`:
```
docker-compose up --build
```

## Publicar
```
dotnet publish -c Release -o ./publish
```

## Solução de problemas comuns
- Erro de conexão com o banco: valide `ConnectionStrings` e se o banco está acessível.
- Portas em uso: ajuste em `launchSettings.json` ou variáveis de ambiente.
- Migrações não aplicam: confirme o projeto de inicialização correto ao executar `dotnet ef`.

## Suporte
Abra uma Issue no repositório `https://github.com/gustavo99-05/uGYM.API` para dúvidas, bugs ou solicitações de melhoria.
