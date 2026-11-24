# uGYM — API e versão mobile

API de gestão de academia em ASP.NET Core 8 e SQL Server, utilizada pelas interfaces desktop e mobile/PWA do uGYM.

## Repositórios

- **Backend:** este repositório, [uGYM.API](https://github.com/gustavo99-05/uGYM.API).
- **Desktop e mobile:** [front-end](https://github.com/gustavo99-05/front-end), incluindo a pasta `mobile/`.

Para executar a aplicação completa, obtenha os dois repositórios. Os arquivos da interface ficam no repositório de frontend.

## Funcionalidades implementadas

- Login por cookie HttpOnly, logout, CSRF e limitação de tentativas de login.
- Cadastro de aluno pelo desktop com validação de CPF/e-mail e bloqueio de CPF duplicado, inclusive em requisições simultâneas.
- Consulta de alunos com paginação, busca sem acentos e filtro de ativo/inativo.
- Alteração da situação da matrícula.
- Mobile com dashboard, detalhes, check-in e histórico paginado.
- Uma entrada por aluno/dia no horário de São Paulo, com proteção contra concorrência.
- PWA com cache da interface estática e operações de dados somente online.

O núcleo foi testado em desenvolvimento. A validação na instalação definitiva e em celular físico continua pendente. Financeiro, planos, treinos e alguns indicadores do desktop permanecem demonstrativos.

## Execução local

Pré-requisitos: SDK/runtime .NET 8, SQL Server e a pasta do frontend. Node.js é necessário apenas para testes de interface.

Na pasta que contém `uGYM.API.csproj`:

```powershell
dotnet restore
dotnet build
dotnet dev-certs https --trust

# Ajuste para o local onde clonou o repositório front-end:
$env:Frontend__Path = 'C:\projetos\front-end'
dotnet run --launch-profile https
```

- Swagger: `https://localhost:5195/swagger`
- Desktop: `https://localhost:5195/`
- Mobile: `https://localhost:5195/mobile/`

A conexão padrão usa SQL Server local e autenticação integrada do Windows. Para outro ambiente, configure `ConnectionStrings__DefaultConnection` no ambiente de execução. Não coloque senhas ou conexões privadas no frontend ou no Git.

O banco deve estar preparado antes do uso autenticado. Em uma instalação nova, revise as migrations e a configuração do banco; em uma base existente, faça backup e revise o impacto antes de aplicar mudanças. A aplicação não executa migrations automaticamente.

Com o banco preparado, crie o primeiro administrador pelo terminal interativo, caso ainda não exista usuário:

```powershell
dotnet run --no-launch-profile -- --create-admin
```

Não há credencial padrão. Para redefinir a senha de uma conta existente, use `--reset-password` no lugar de `--create-admin`.

O certificado local de desenvolvimento cobre localhost. Para acesso no celular pela rede, siga a configuração de certificado/IP em [README-MOBILE.md](README-MOBILE.md).

## Contrato da listagem

`GET /api/Aluno` retorna `{ total, pagina, tamanho, itens }`. O padrão é 20 itens por página, com máximo de 100. Há parâmetros `pagina`, `tamanho`, `busca`, `ativo` e `visao=mobile`. As duas interfaces deste projeto já utilizam esse contrato.

## Verificações

```powershell
dotnet build -o artifacts\build
dotnet build tests\UGym.Alunos.Tests\UGym.Alunos.Tests.csproj -o artifacts\alunos-tests
dotnet artifacts\alunos-tests\UGym.Alunos.Tests.dll

# Integração HTTP/SQL em banco temporário:
dotnet build tests\UGym.Mobile.Tests\UGym.Mobile.Tests.csproj -o artifacts\tests
dotnet artifacts\tests\UGym.Mobile.Tests.dll .
```

Os testes de integração criam e removem exclusivamente um banco `UGymMobileTests_<guid>`. Não substitua esse banco temporário pelo banco real. O executor de navegador e suas dependências estão documentados no guia mobile.

## Documentação

- [Guia mobile e operação local](README-MOBILE.md)
- [Arquitetura, endpoints, regras, testes e pendências](DOCUMENTACAO-PARA-CHATGPT.md)

Arquivos de compilação, certificados, backups, configurações privadas e relatórios operacionais locais não fazem parte da publicação do código.
