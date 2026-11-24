# uGYM — documentação técnica e contexto para o ChatGPT

**Data de referência:** 23/11/2026.
**Finalidade:** descrever o estado atual do projeto para análise, manutenção e planejamento de próximas etapas.
**Origem das informações:** código do workspace, documentação existente e testes executados durante a revisão. Este documento não contém credenciais, connection strings reais ou registros de alunos.

## 1. Visão geral e situação atual

O uGYM é um projeto de gestão de academia com uma API ASP.NET Core, banco SQL Server e duas interfaces web: desktop e mobile. O núcleo funcional cobre autenticação da equipe, cadastro e consulta de alunos, alteração da situação da matrícula e registro de check-in.

A interface mobile é uma PWA e utiliza a mesma API e a mesma sessão do desktop. Não existe aplicativo nativo Android/iOS neste escopo.

O núcleo foi implementado e testado em ambiente de desenvolvimento. Isso permite uma validação local acompanhada, mas não comprova prontidão para produção com dados reais. A instalação definitiva, o acesso por celular físico e os procedimentos operacionais ainda precisam de validação.

Financeiro, planos, treinos e parte do dashboard desktop possuem conteúdo demonstrativo. Não devem ser tratados como módulos completos conectados ao banco. O dashboard mobile consulta contadores reais da API.

## 2. Tecnologias e arquitetura

| Camada | Implementação atual |
| --- | --- |
| Backend | C#, ASP.NET Core, destino `net8.0` |
| Persistência | Entity Framework Core e provedor SQL Server, referências na versão 8.0.0 |
| Banco | SQL Server; contexto `UGymContext` |
| Documentação da API | Swagger/Swashbuckle 6.5.0, habilitado em Development |
| Desktop | HTML, CSS e JavaScript |
| Mobile | HTML, CSS e JavaScript, manifest e service worker |
| Autenticação | Cookie do ASP.NET Core e `PasswordHasher<Usuario>` |
| Testes | Executores console .NET e testes no Chrome por Puppeteer Core |
| Scripts locais | PowerShell para execução conjunta e geração de pacote |

Fluxo principal:

```text
Desktop ou PWA mobile
        |
        v
api-client.js — cookies, CSRF, timeout e mensagens de erro
        |
        v
ASP.NET Core — HTTPS, autenticação, autorização e controllers
        |
        v
Entity Framework Core / operações SQL transacionais
        |
        v
SQL Server
```

Backend e frontend ficam em pastas separadas no workspace. Em desenvolvimento, a API pode servir a pasta do frontend configurada em `Frontend:Path`. No pacote gerado pelo script, os arquivos são copiados para `wwwroot`. A execução pela mesma origem HTTPS é a configuração recomendada.

## 3. Funcionalidades por interface

| Funcionalidade | Desktop | Mobile |
| --- | --- | --- |
| Login e logout da equipe | Implementado | Implementado |
| Cadastro de aluno | Implementado, com validação no backend | Não oferecido |
| Listagem, busca por nome e filtro ativo/inativo | Implementado, com paginação | Implementado, com paginação |
| Consulta de dados do aluno | Implementado | Implementado |
| Alteração de ativo/inativo | Implementado | Implementado, com confirmação |
| Check-in conectado à API | A tela desktop permanece demonstrativa | Implementado |
| Histórico individual de entradas | Não é um fluxo completo do desktop | Implementado, com filtros e paginação |
| Dashboard com contadores reais | Há indicadores demonstrativos | Implementado |
| Instalação como PWA | Não é o foco | Implementada; validação em aparelho físico pendente |
| Pagamentos, gestão de planos e treinos | Telas demonstrativas | Não oferecido |

Não há endpoint de exclusão de aluno nem edição completa de seus dados. O endpoint de atualização existente altera somente `ativo`.

## 4. Organização dos arquivos

### Backend

```text
uGYM.API.csproj
Program.cs
Controllers/
  AlunosController.cs       classe AlunoController; rota /api/Aluno
  AuthController.cs         autenticação, sessão e CSRF
  CheckInController.cs      registro, histórico e resumos
DTOs/
  CadastrarAlunoDto.cs      validação dos dados de cadastro
  ConsultaAlunosDto.cs      paginação e filtros
  AtualizarStatusAlunoDto.cs
  MobileDtos.cs             login, check-in e resumos
Models/
  aluno.cs
  Usuario.cs
  CheckIn.cs
  Pagamento.cs
Data/Data/UGymContext.cs
Services/
  DocumentoCpf.cs
  RelogioAcademia.cs
  SessaoUsuario.cs
  AdministradorInicial.cs
  RedefinicaoSenha.cs
  SenhaAdministrador.cs
Migrations/
Properties/launchSettings.json
appsettings.Mobile.example.json
tools/
  Start-Mobile.ps1
  Package-Mobile.ps1
tests/
  UGym.Auth.Tests/
  UGym.Alunos.Tests/
  UGym.Mobile.Tests/
  browser.cjs
  package.json
README-MOBILE.md
```

### Frontend

```text
index.html                  interface desktop
script.js                   fluxos do desktop
style.css
config.js                   configuração pública do endereço da API
api-client.js               cliente HTTP compartilhado
mobile/
  index.html
  mobile.js
  mobile.css
  manifest.json
  service-worker.js
  assets/
```

O arquivo original `mascote.png` foi identificado como vazio na entrega anterior. A interface utiliza a marca textual e os ícones disponíveis.

## 5. Modelos e persistência

| Modelo/tabela | Campos principais | Papel |
| --- | --- | --- |
| `Aluno` / `Alunos` | Id, Nome, CPF, Email, Telefone, Plano, Instrutor, Observacoes, Ativo, DataCadastro | Cadastro e situação da matrícula |
| `Usuario` / `Usuarios` | Id, Nome, NomeNormalizado, SenhaHash, Ativo | Conta da equipe |
| `CheckIn` / `CheckIns` | Id, AlunoId, DataHora, Permitido, DiaLocal, OperadorId | Histórico de entradas |
| `Pagamento` / `Pagamentos` | Id, AlunoId, Valor, DataPagamento, Vencimento, Pago, Metodo | Modelo existente; não representa um módulo financeiro completo |

O modelo configurado não declara relacionamentos com chaves estrangeiras para todos esses identificadores. Não se deve presumir que a presença de `AlunoId` ou `OperadorId` signifique uma restrição de integridade referencial no banco.

Índices configurados:

- Nome normalizado do usuário: único.
- Check-in por aluno/data e hora: índice de consulta.
- Check-in por aluno/dia local: único para entradas permitidas com dia preenchido.

`DiaLocal` e `OperadorId` são anuláveis para preservar registros antigos. A migration `20261110222453_MobileAuthCheckIn` adiciona autenticação e os campos/índices de check-in. Sua aplicação ao banco existente foi registrada em uma etapa anterior, no relatório `MIGRATION-MOBILE-RESULTADO.md`; o estado do banco real não foi novamente auditado para produzir este documento.

As três correções da revisão de 23/11/2026 não exigiram nova migration e não regravaram os dados reais.

## 6. Regras de negócio

### Cadastro de aluno

- Nome, CPF e plano são obrigatórios.
- Nome: entre 2 e 150 caracteres, com verificação adicional após remover espaços externos.
- CPF: aceita onze dígitos ou a máscara convencional `000.000.000-00`; verifica os dígitos finais e rejeita sequências de um único dígito repetido.
- Novos CPFs são gravados sem pontuação.
- E-mail é opcional; quando preenchido, seu formato é validado.
- Limites: e-mail 254, telefone 30, plano 120, instrutor 120 e observações 2000 caracteres.
- Campos opcionais omitidos são convertidos para strings vazias ao criar a entidade.
- ID é gerado pelo banco; situação ativa e data UTC do cadastro são atribuídas pelo servidor.
- CPF já cadastrado resulta em HTTP 409.

A proteção contra duplicidade utiliza uma transação e `sys.sp_getapplock`, com um recurso de bloqueio por CPF normalizado. A consulta reconhece também CPFs antigos com pontuação e espaços. Requisições simultâneas do mesmo CPF são serializadas.

Essa proteção vale para cadastros feitos pelo endpoint. Não foi criada uma restrição única de CPF no esquema existente; uma gravação direta no banco não passa por esse controle da API. Registros antigos duplicados não são corrigidos automaticamente.

### Consulta de alunos

- Página inicial 1, tamanho padrão 20 e máximo de 100 itens por página.
- Número de página permitido: 1 a 100000.
- Busca por nome de até 150 caracteres, executada no SQL Server sem diferenciar acentos e maiúsculas/minúsculas.
- Filtro opcional de ativo/inativo.
- Filtros são aplicados antes da contagem e da paginação.
- Ordenação por nome e, em caso de empate, ID.
- Visão mobile limita os campos de cada item a ID, nome, plano e ativo.
- Desktop e mobile têm navegação entre páginas e espera de 300 ms durante a digitação da busca.
- Controle de versão das consultas impede que respostas antigas sobrescrevam uma busca mais recente.

### Check-in

- O aluno deve existir e estar ativo.
- É permitida uma entrada por aluno/dia, considerando `America/Sao_Paulo`.
- O instante é gerado no servidor e armazenado em UTC.
- O operador é identificado pela sessão autenticada.
- Há transação serializável, bloqueio SQL do aluno e índice único adicional para novas entradas.
- A checagem de duplicidade considera também horários de registros legados, presumindo que sejam UTC. A convenção de horários antigos precisa ser confirmada antes do uso operacional.
- Histórico: filtros `hoje`, `7dias`, `mes` e `todos`, com paginação de 1 a 100 itens.

## 7. Autenticação e proteções implementadas

- Cookie de sessão `uGYM.Session`, HttpOnly e SameSite=Lax.
- Prazo de sessão configurado em oito horas, com renovação deslizante; o cookie de login não é persistente.
- Cookies Secure obrigatórios fora de Development; em Development seguem o esquema da requisição.
- Verificação da conta ativa e da marca da credencial a cada requisição autenticada.
- Troca do hash da senha invalida sessões anteriores na próxima requisição.
- Senhas processadas pelo `PasswordHasher<Usuario>`, com PBKDF2 e 210000 iterações configuradas.
- Criação/redefinição de senha pelo terminal, com entrada oculta e limite de 12 a 256 caracteres.
- Token CSRF exigido no cabeçalho `X-CSRF-TOKEN` em operações de escrita, inclusive login.
- Login limitado a dez requisições por IP por minuto, no processo da aplicação.
- HTTPS exigido nas rotas `/api`; a exceção HTTP depende de configuração explícita e só funciona em Development.
- CORS com lista de origens, credenciais e métodos GET/POST/PUT. A origem adicional padrão é `http://localhost:8080`.
- Respostas da API com `Cache-Control: no-store`.
- Service worker guarda a interface estática, sem cache de respostas da API. Operações de consulta/escrita exigem conexão.

O único perfil funcional provisionado é administrador. Não há gestão completa de usuários, vários níveis de permissão, cadastro público de conta ou recuperação de senha pela interface. A autenticação atual usa cookies, não JWT.

## 8. Endpoints e contratos

| Método | Rota | Finalidade |
| --- | --- | --- |
| GET | `/api/auth/csrf` | Obter token CSRF; público |
| POST | `/api/auth/login` | Entrar com usuário e senha; público, mas exige CSRF |
| GET | `/api/auth/session` | Consultar a sessão autenticada |
| POST | `/api/auth/logout` | Encerrar sessão |
| GET | `/api/Aluno` | Listagem paginada e filtrada |
| GET | `/api/Aluno/{id}` | Detalhes do aluno |
| POST | `/api/Aluno` | Cadastrar aluno |
| PUT | `/api/Aluno/{id}` | Alterar somente ativo/inativo |
| POST | `/api/CheckIn` | Registrar entrada |
| GET | `/api/CheckIn/resumo` | Contadores do dashboard |
| GET | `/api/CheckIn/aluno/{id}/resumo` | Total, mês atual e última entrada |
| GET | `/api/CheckIn/aluno/{id}` | Histórico paginado |

Todos os endpoints de alunos/check-ins exigem autenticação. Escritas exigem também CSRF.

Exemplo de consulta:

```http
GET /api/Aluno?visao=mobile&pagina=1&tamanho=20&busca=exemplo&ativo=true
```

Exemplo ilustrativo de resposta:

```json
{
  "total": 1,
  "pagina": 1,
  "tamanho": 20,
  "itens": [
    { "id": 123, "nome": "Aluno de exemplo", "plano": "Mensal", "ativo": true }
  ]
}
```

**Mudança de contrato:** a listagem retornava um array e agora retorna `{ total, pagina, tamanho, itens }`. Os dois frontends do workspace foram adaptados. Outros consumidores precisam ler `itens` e navegar pelas páginas.

Exemplo de corpo de cadastro; substituir o marcador por um CPF válido de teste antes de enviar:

```json
{
  "nome": "Aluno de exemplo",
  "cpf": "<CPF_VALIDO_DE_TESTE>",
  "plano": "Mensal",
  "email": "aluno@example.invalid",
  "telefone": "",
  "instrutor": "",
  "observacoes": ""
}
```

Cadastro bem-sucedido retorna HTTP 200 e mensagem de confirmação. Check-in bem-sucedido retorna HTTP 201. Atualização de situação recebe `{ "ativo": false }`; registro de check-in recebe `{ "alunoId": 123 }`.

Respostas de erro principais: 400 para validação/CSRF/transporte, 401 para ausência de sessão ou credencial inválida, 403 para acesso negado, 404 para aluno inexistente, 409 para conflitos, 429 para limite de tentativas e 503 para indisponibilidade. Validações do ASP.NET Core podem devolver mensagens no objeto `errors`; o cliente compartilhado foi adaptado para exibi-las.

## 9. Correções concluídas na revisão

| Problema | Correção | Evidência |
| --- | --- | --- |
| Perfil iniciava em HTTP, bloqueado pela própria API | Perfil padrão e seleção local do Visual Studio alterados para HTTPS em localhost:5195 | Certificado confiável confirmado e teste HTTPS real aprovado |
| Cadastro aceitava dados sem validação suficiente | DTO próprio, validação de CPF/e-mail, limites, normalização e controle transacional de duplicidade | Testes unitários e HTTP/SQL, incluindo concorrência e CPF legado com máscara |
| Listagem carregava todos os alunos | Paginação, filtros e busca no servidor; adaptação das duas interfaces | Testes com mais de vinte registros e navegação real no Chrome |

Também foram atualizados o tratamento de erros do desktop, a versão do cache da PWA e a documentação de execução/contratos. As alterações locais que já existiam no projeto foram preservadas.

## 10. Testes e evidências

Resultados registrados na revisão de 23/11/2026:

| Verificação | Resultado |
| --- | --- |
| Compilação | Sem erros; oito avisos antigos CS8618 de nulabilidade em Aluno/Pagamento |
| Cadastro e consulta sem banco | 27 verificações aprovadas |
| Inicialização HTTPS com validação real do certificado | Dois testes aprovados, sem conexão com banco |
| Núcleo de integração HTTP/SQL | 54 verificações aprovadas |
| Interface no Chrome | 53 verificações aprovadas |
| Executor completo de integração | 56 verificações aprovadas, incluindo a saída do navegador e o bloqueio de HTTP |
| Sintaxe JavaScript | Verificações aprovadas nos arquivos alterados |

As 56 verificações do executor incluem as 54 do núcleo; não são 56 testes adicionais. As 53 verificações de interface são executadas pelo subprocesso do navegador.

Cobertura relevante: autenticação, CSRF, aluno ativo/inativo, duplicidade e concorrência de check-in, horários de São Paulo/UTC, histórico, CPF inválido/duplicado, cadastros simultâneos, CPF legado com máscara, busca sem acentos, filtros, ordenação estável, paginação, comportamento offline e logout. Foram conferidas telas mobile em 360, 390 e 430 pixels.

Os testes de integração usam dados fictícios em um banco `UGymMobileTests_<guid>`, criado e removido pelo próprio executor. O banco real `UGymDB` não recebeu migrations nem gravações durante essa revisão.

Evidências locais: `artifacts/revision-test-results.txt` e `artifacts/screenshots/`. Esses artefatos podem estar ignorados pelo Git; não estão incorporados neste documento. Os resultados descrevem testes já executados, não uma nova execução feita para redigir esta documentação.

Os projetos de teste atuais são executores console: devem ser iniciados pelos comandos abaixo. Não presumir que apenas `dotnet test` execute essas verificações.

## 11. Execução local

Pré-requisitos: SDK compatível com .NET 8, runtime .NET 8, SQL Server acessível pela configuração local e pasta do frontend. Node.js é necessário apenas para as verificações JavaScript e testes de navegador.

Na pasta do backend:

```powershell
dotnet restore
dotnet build
dotnet dev-certs https --trust
dotnet run --launch-profile https
```

Swagger: `https://localhost:5195/swagger`.

Para servir também o frontend no mesmo endereço, configure sua pasta antes de iniciar:

```powershell
$env:Frontend__Path = 'C:\caminho\front end'
dotnet run --launch-profile https
```

Nesse modo, desktop fica em `https://localhost:5195/` e mobile em `https://localhost:5195/mobile/`. Substitua o caminho ilustrativo pela pasta que contém `index.html` e `mobile/`.

Configurações relevantes:

| Chave | Uso |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | Conexão SQL Server, mantida somente no ambiente apropriado |
| `Frontend:Path` | Pasta estática do frontend em desenvolvimento |
| `Frontend:AllowedOrigins` | Origens adicionais permitidas pelo CORS |
| `Security:AllowInsecureLocalTesting` | Exceção HTTP exclusiva de Development e testes com dados fictícios |
| `Kestrel:Certificates:Default:Path/KeyPath` | Certificado/chave PEM usados pelo script de acesso na rede |

Variáveis de ambiente usam `__` no lugar de `:`, como `Frontend__Path`. `appsettings.Mobile.example.json` é um exemplo e não é carregado automaticamente pelo aplicativo.

O certificado de desenvolvimento de localhost não cobre automaticamente o IP do computador no celular. Acesso pela rede exige certificado confiável para esse IP, configuração de rede e validação no aparelho. `tools/Start-Mobile.ps1` oferece os parâmetros para essa execução; o procedimento está detalhado no README existente.

### Conta administrativa

Quando o banco já estiver preparado, o comando abaixo cria o primeiro administrador somente se ainda não existir usuário:

```powershell
dotnet run --no-launch-profile -- --create-admin
```

Para redefinir a senha de uma conta existente:

```powershell
dotnet run --no-launch-profile -- --reset-password
```

São comandos interativos; nomes e senhas são informados no terminal. Não há credenciais padrão neste documento. A existência atual de uma conta no banco real não foi verificada para produzir este relatório.

### Repetição dos testes

```powershell
dotnet build -o artifacts\build
dotnet build tests\UGym.Alunos.Tests\UGym.Alunos.Tests.csproj -o artifacts\alunos-tests
dotnet artifacts\alunos-tests\UGym.Alunos.Tests.dll

# Testar também HTTPS, com certificado local confiável:
dotnet artifacts\alunos-tests\UGym.Alunos.Tests.dll artifacts\build\uGYM.API.dll

# Integração em banco temporário:
dotnet build tests\UGym.Mobile.Tests\UGym.Mobile.Tests.csproj -o artifacts\tests
dotnet artifacts\tests\UGym.Mobile.Tests.dll .

# Interface: requer Chrome ou Edge e permissão para criar/remover o banco temporário.
npm install --prefix tests
$env:UGYM_FRONTEND_PATH = 'C:\caminho\front end'
dotnet artifacts\tests\UGym.Mobile.Tests.dll . --browser
```

Nunca substituir o nome de banco temporário do executor pelo banco real. Para outras instalações, revisar migrations e backups antes de aplicar mudanças de estrutura. Nenhum comando de aplicação de migrations é necessário apenas para ler esta documentação.

## 12. Limites e próximas etapas

1. Validar a instalação definitiva, o login e os fluxos com os responsáveis pela academia.
2. Testar HTTPS, teclado, navegação e instalação da PWA em Android/iPhone físicos.
3. Definir e validar backup/restauração e o ambiente de hospedagem.
4. Revisar a configuração de produção, atualização das dependências, observabilidade e eventual proxy reverso; não há implantação pública comprovada nesta revisão.
5. Separar visualmente os módulos demonstrativos dos recursos operacionais.
6. Implementar financeiro, planos, treinos, edição completa ou múltiplos perfis somente quando seu escopo e suas regras forem definidos.
7. Resolver os avisos de nulabilidade existentes e avaliar melhorias futuras de integridade e índices conforme o volume real de dados.

Não foram comprovados testes de carga, funcionamento em aparelho físico, restauração operacional de backup ou operação contínua em produção. O projeto deve ser apresentado como núcleo funcional testado em desenvolvimento, com validação operacional pendente.

## 13. Contexto pronto para uma nova conversa com o ChatGPT

Copie o texto abaixo e anexe este documento:

> Estou desenvolvendo o uGYM, um sistema de academia com ASP.NET Core 8, Entity Framework Core, SQL Server e interfaces desktop/mobile em HTML, CSS e JavaScript. A versão mobile é uma PWA. O núcleo implementado inclui login por cookie com CSRF, cadastro validado de alunos, consulta paginada, alteração de ativo/inativo, check-in e histórico.
>
> Foram corrigidos o perfil HTTPS, a validação/duplicidade de CPF e a listagem sem paginação. O GET /api/Aluno agora retorna { total, pagina, tamanho, itens }; desktop e mobile já foram adaptados. O cadastro usa bloqueio transacional por CPF e não uma nova restrição única no banco. Os testes de integração usam banco temporário; o banco real foi preservado.
>
> O documento anexado descreve arquitetura, arquivos, contratos, testes, limites e pendências. Use-o como contexto da implementação existente. Não presuma que os módulos demonstrativos de financeiro, planos ou treinos estejam concluídos nem que o sistema já esteja homologado para produção. Diferencie fatos confirmados, hipóteses e sugestões. Para propor mudanças de código, indique os arquivos necessários; o documento não dá acesso automático ao repositório. Preserve os dados e as funcionalidades existentes e explique o impacto de mudanças na API ou no banco.
>
> Meu próximo objetivo é: [escrever aqui o que desejo analisar, corrigir ou implementar].

## 14. Referências locais

- `README-MOBILE.md`: operação local, HTTPS na rede, PWA, scripts e testes.
- `MIGRATION-MOBILE-RESULTADO.md`: registro operacional local da etapa anterior de migration e preservação da base. Não é versionado no GitHub, pois contém caminhos locais de backup.
- `Program.cs`, `Controllers/`, `DTOs/`, `Services/` e `Data/Data/UGymContext.cs`: implementação efetiva.
- `tests/`: executores e casos automatizados.

Este documento é uma fotografia do estado conhecido em 23/11/2026. Alterações posteriores devem atualizar os contratos, resultados e pendências aqui descritos.
