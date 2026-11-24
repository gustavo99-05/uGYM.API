# uGYM Mobile

Versão mobile independente do desktop, em HTML/CSS/JavaScript e ASP.NET Core 8 com o SQL Server existente.

**Situação da entrega:** núcleo funcional implementado e testado em desenvolvimento, com validação operacional pendente. A aplicação da migration `20261110222453_MobileAuthCheckIn` ao banco existente foi registrada em uma etapa anterior, após autorização, backup e verificações de integridade. Os registros originais foram preservados. O relatório operacional `MIGRATION-MOBILE-RESULTADO.md` permanece somente no ambiente local, fora do Git, por conter caminhos de backup. Confirme a conta administrativa na instalação definitiva. Não há credencial padrão nem implantação pública comprovada nesta revisão.

## Funcionalidades

- Login real por cookie HttpOnly, sessão de oito horas com renovação, logout, verificação da conta ativa e invalidação após troca de senha.
- Senhas com PasswordHasher do ASP.NET Core Identity (PBKDF2, 210.000 iterações), proteção CSRF inclusive no login e limitação de tentativas por IP.
- Dashboard com total de alunos, ativos e entradas no dia de São Paulo.
- Pesquisa de nome no servidor, inclusive sem acentos, filtros de situação, páginas de 20 alunos e cards sem CPF, telefone ou e-mail.
- Detalhes com plano, instrutor, cadastro válido, última entrada e resumo de frequência.
- Alteração de ativo/inativo com confirmação e feedback.
- Check-in apenas de aluno ativo e existente, com operador, instante UTC e dia de São Paulo.
- Uma entrada por aluno/dia, protegida contra requisições simultâneas.
- Histórico individual com paginação, total, mês atual, última entrada e filtros Hoje, 7 dias, Este mês e Todos.
- Conta, orientações de instalação e navegação inferior.
- PWA com manifest, ícones próprios, cache apenas da interface e bloqueio de operações offline.
- Desktop mantém cadastro, consulta e edição; agora usa o mesmo login, cookie e CSRF.
- Cadastro no desktop com CPF validado pelos dígitos verificadores, e-mail opcional validado e bloqueio de CPF duplicado, inclusive em requisições simultâneas.

A versão mobile não oferece cadastro, exclusão, edição completa ou pagamentos. Os módulos ilustrativos que já existiam no desktop não foram transformados em novos módulos de negócio.

## Estrutura e arquivos

Backend (pasta deste README):

```text
Program.cs                         autenticação, CSRF, CORS, HTTPS e arquivos estáticos
Controllers/
  AlunosController.cs              protegido; mantém os endpoints do desktop
  AuthController.cs                login, sessão, CSRF e logout
  CheckInController.cs             registro, histórico e resumos
DTOs/
  AtualizarStatusAlunoDto.cs        contrato existente
  MobileDtos.cs                    contratos de login/check-in/resumo
  CadastrarAlunoDto.cs             validação do cadastro e limites dos campos
  ConsultaAlunosDto.cs             paginação e filtros da listagem
Models/
  aluno.cs                         preservado
  CheckIn.cs                       adiciona DiaLocal e OperadorId
  Usuario.cs                       nome normalizado e senha em hash
Data/Data/UGymContext.cs            adiciona usuários e índices
Services/
  RelogioAcademia.cs               conversão São Paulo/UTC
  AdministradorInicial.cs          provisionamento local do primeiro usuário
  RedefinicaoSenha.cs              redefinição local da senha de usuário existente
  SenhaAdministrador.cs            regras compartilhadas e entrada oculta no terminal
  SessaoUsuario.cs                 vínculo entre cookie e credencial atual
  DocumentoCpf.cs                  validação e normalização de CPF
Migrations/
  20261110222453_MobileAuthCheckIn.cs
  20261110222453_MobileAuthCheckIn.Designer.cs
  UGymContextModelSnapshot.cs
appsettings.Mobile.example.json    exemplo, não é carregado automaticamente
tools/
  Start-Mobile.ps1                  execução conjunta do frontend e API
  Package-Mobile.ps1                gera pacote local, sem implantação
tests/
  UGym.Mobile.Tests/                executor HTTP/SQL isolado
  UGym.Alunos.Tests/                validações de cadastro/consulta e teste opcional HTTPS
  browser.cjs                      QA em Chrome/Edge headless
  package.json                     dependência de desenvolvimento do QA
artifacts/                         saídas ignoradas pelo Git
```

Frontend: repositório [front-end](https://github.com/gustavo99-05/front-end), em uma pasta separada. Configure `Frontend__Path` ou informe `-FrontendPath` com a pasta em que ele foi clonado.

```text
index.html                         desktop: diálogo de login e logout adicionados
script.js                          desktop: acesso autenticado à API
style.css                          desktop: estilos do diálogo de login
mascote.png                        preservado; arquivo original tem 0 bytes
config.js                          endereço da API por ambiente
api-client.js                      cliente compartilhado, cookie/CSRF/erros
mobile/
  index.html
  mobile.css
  mobile.js
  manifest.json
  service-worker.js
  assets/
    icon.svg
    icon-192.png
    icon-512.png
    icon-maskable.png
```

O arquivo de mascote vazio não permite reaproveitar sua ilustração. A interface utiliza o símbolo “u” do desktop e o nome uGYM. Nenhum mascote alternativo foi inventado.

## Banco: revisão e autorização antes da aplicação

As migrations anteriores já criam `Alunos`, `Pagamentos` e `CheckIns`. Essas tabelas e os registros foram preservados. A connection string existente não foi alterada.

A migration `20261110222453_MobileAuthCheckIn`:

1. Cria `Usuarios`, com índice único em `NomeNormalizado`.
2. Adiciona `CheckIns.DiaLocal` (date nullable) e `CheckIns.OperadorId` (int nullable).
3. Cria índice de consulta em AlunoId/DataHora.
4. Cria índice único em AlunoId/DiaLocal para entradas permitidas com dia preenchido.

Não apaga dados, não recria o banco e não transforma os horários históricos. O `Down` está bloqueado para evitar exclusão acidental de usuários e histórico.

Registros legados ficam com DiaLocal/OperadorId nulos. A validação transacional consulta também o DataHora legado, presumindo UTC. Se a base já tiver horários antigos em hora local, confirme sua convenção antes do uso; a aplicação não tenta converter registros reais automaticamente. Duplicidades históricas são preservadas.

No registro, uma transação serializável com `UPDLOCK, HOLDLOCK` sobre o aluno impede a corrida entre check-ins e mudanças de status. O índice único fornece proteção adicional no banco para novas entradas. O horário vem do servidor. A janela diária é calculada por America/Sao_Paulo, sem offset fixo.

A migration já está aplicada neste workspace. Os comandos abaixo ficam como referência para outras instalações e exigem revisão, backup e autorização antes da aplicação:

```powershell
# Na pasta do backend. Instalação necessária apenas se a ferramenta não existir.
dotnet tool install dotnet-ef --version 8.0.0 --tool-path .tools

# Gerar SQL para revisão, sem aplicar:
.\.tools\dotnet-ef migrations script 20260522061029_AtualizacaoAluno --idempotent --output artifacts\MobileAuthCheckIn.sql

# SOMENTE APÓS AUTORIZAÇÃO para o UGymDB:
.\.tools\dotnet-ef database update 20261110222453_MobileAuthCheckIn
```

Não executar `database drop`, `EnsureDeleted` ou downgrade contra a base existente. O executor de testes remove somente seu próprio banco com nome aleatório e prefixo fixo.

## Primeiro administrador

Com a migration aplicada, execute em um terminal interativo na pasta do backend:

```powershell
dotnet run --project .\uGYM.API.csproj --configuration Mobile --no-launch-profile -- --create-admin
```

Informe o nome (3–80 caracteres) e a senha (12–256 caracteres) no terminal. A entrada de senha é oculta, não usa argumento de comando, arquivo ou JavaScript. O comando encerra após a criação e se recusa a criar outro administrador quando já houver usuário. Não há endpoint público de cadastro/primeiro acesso.

Não envie senhas pelo chat nem registre credenciais no Git. O gerenciamento de múltiplos operadores não faz parte desta versão.

## Redefinir a senha de um administrador existente

Encerre a API pelo terminal que a iniciou. Na pasta do backend, execute no PowerShell interativo:

```powershell
dotnet run --project .\uGYM.API.csproj --configuration Mobile --no-launch-profile -- --reset-password
```

Informe o nome de usuário já cadastrado (o mesmo do login), a nova senha e a confirmação. O nome ignora diferenças entre maiúsculas/minúsculas e espaços nas extremidades. As duas entradas de senha são ocultas. A regra existente é de 12 a 256 caracteres, sem novos requisitos de composição. Esc ou Ctrl+C cancela antes da gravação. Pipe/redirecionamento de entrada não é aceito. A senha não deve ser passada em argumentos, variáveis de ambiente ou arquivos.

O comando atualiza somente `Usuarios.SenhaHash`, usando o mesmo `IPasswordHasher<Usuario>` configurado na API (PBKDF2, 210.000 iterações). Preserva ID, nome, situação ativa/inativa e demais dados; não cria nem exclui usuários, não altera alunos, não modifica o esquema e não executa migrations. Usuário inexistente, senha inválida, confirmação diferente ou alteração concorrente da conta encerram com erro. O comando termina sem iniciar o servidor HTTP; não pode ser combinado com `--create-admin`.

Reinicie a API com o código atualizado após a redefinição. Cada sessão passa a conter uma marca derivada do hash, protegida pelo cookie de autenticação e conferida com o banco a cada requisição. A troca do hash invalida as sessões anteriores na próxima requisição, inclusive se a nova senha for igual à anterior. Cookies emitidos pela versão antiga, sem essa marca, também exigem novo login ao atualizar a API. Não há novas colunas ou migrations para esse controle; instâncias antigas precisam ser reiniciadas para aplicá-lo.

Verificação específica, sem conexão com banco ou execução de migrations (o SQL é interceptado em memória):

```powershell
dotnet run --project .\tests\UGym.Auth.Tests\UGym.Auth.Tests.csproj -- .\bin\Debug\net8.0\uGYM.API.dll
```

Execute `dotnet build` antes desse teste para gerar o executável da API. O executor cobre os limites da senha, confirmação, hash, atualização restrita/concorrência, invalidação de sessões e recusa de entrada redirecionada.

## Execução local e HTTPS

Pré-requisitos: SDK .NET 8 ou superior com runtime .NET 8, SQL Server acessível pela connection string existente e as duas pastas do workspace. Node.js é necessário apenas para verificações de sintaxe e QA, não para executar a aplicação.

O modo recomendado serve API e frontend pela mesma origem. Assim, o mobile usa URLs relativas, o cookie funciona e não é necessário liberar CORS na rede.

O perfil padrão do Visual Studio e de `dotnet run` é `https`, em `https://localhost:5195`, e abre o Swagger. Para preparar o certificado local de desenvolvimento, execute `dotnet dev-certs https --trust`. Para servir também o frontend, use o script abaixo ou configure `Frontend__Path`. O certificado padrão de localhost não substitui o certificado com IP necessário para acesso pelo celular.

Para dados reais, use HTTPS também no computador. HTTP da API é bloqueado por padrão. A exceção `Security__AllowInsecureLocalTesting=true` só funciona em Development e deve ser usada exclusivamente com dados fictícios em rede isolada. O executor de testes usa essa exceção apenas no seu banco temporário.

Para gerar um certificado de desenvolvimento confiável, uma opção é [mkcert, documentação oficial](https://github.com/FiloSottile/mkcert). Instale a ferramenta pelo procedimento oficial. Os próximos comandos são preparação manual; não foram executados automaticamente:

```powershell
ipconfig
# Anote o IPv4 do adaptador Wi-Fi/Ethernet em uso, por exemplo 192.168.1.20.
$ipAcademia = '192.168.1.20'
$pastaCertificados = Join-Path $env:USERPROFILE '.ugym-certs'
New-Item -ItemType Directory -Force -Path $pastaCertificados | Out-Null
mkcert -install
mkcert -cert-file "$pastaCertificados\ugym.pem" -key-file "$pastaCertificados\ugym-key.pem" $ipAcademia localhost 127.0.0.1 ::1

# Na pasta do backend:
.\tools\Start-Mobile.ps1 -LanIp $ipAcademia -CertificatePath "$pastaCertificados\ugym.pem" -KeyPath "$pastaCertificados\ugym-key.pem"
```

A pasta frontend deste workspace é reconhecida pelo script. Em outro computador, informe `-FrontendPath 'C:\caminho\front end'`. O script não executa migrations. Para iniciar somente no computador, omita `-LanIp` e mantenha os argumentos de certificado.

No computador, use a origem informada pelo script:
- Mobile: `https://IP-DO-PC:5195/mobile/`.
- Desktop: `https://IP-DO-PC:5195/`.

Para acesso apenas no computador com certificado para localhost: `https://localhost:5195/mobile/` e `https://localhost:5195/`.

O comando equivalente sem script configura `Frontend__Path`, `ASPNETCORE_ENVIRONMENT=Development`, os caminhos PEM em `Kestrel__Certificates__Default__Path` e `Kestrel__Certificates__Default__KeyPath`, e executa `dotnet run --no-launch-profile --urls https://localhost:5195`.

Se a API anterior estiver rodando na porta 5195, encerre-a pelo terminal que a iniciou e reinicie com a versão atual. O trabalho não encerra processos preexistentes. Se o executável estiver bloqueado, `dotnet build -o artifacts\build` valida em outra pasta.

## Acesso pelo celular

1. Conecte celular e computador à mesma rede Wi-Fi privada. Redes de convidados podem bloquear comunicação entre dispositivos.
2. Use o IPv4 obtido por `ipconfig`; localhost no celular aponta para o próprio celular.
3. Gere o certificado incluindo esse IP e inicie pelo script com `-LanIp`.
4. Instale no aparelho **somente o certificado público** `rootCA.pem`, localizado em `mkcert -CAROOT`, e habilite a confiança conforme o sistema. No iPhone, a confiança de certificados instalados manualmente pode exigir ativação nas configurações de certificados. Nunca transfira `rootCA-key.pem` nem a chave privada do servidor.
5. Abra `https://IP-DO-PC:5195/mobile/`. O certificado deve ser aceito como confiável, sem avisos. Não substitua essa etapa por ignorar alertas do navegador.
6. Se o Windows bloquear a conexão, crie uma regra limitada à porta, perfil privado e sub-rede local, em um PowerShell administrativo:

```powershell
New-NetFirewallRule -DisplayName 'uGYM Mobile LAN 5195' -Direction Inbound -Action Allow -Protocol TCP -LocalPort 5195 -Profile Private -RemoteAddress LocalSubnet
# Para remover a regra depois dos testes:
Remove-NetFirewallRule -DisplayName 'uGYM Mobile LAN 5195'
```

Não é necessário desativar o firewall, abrir a porta do SQL Server, configurar encaminhamento no roteador nem publicar na internet. O script aceita somente IPv4 privado para a opção LAN.

Para dados reais fora do desenvolvimento, use um host ASP.NET Core com HTTPS válido. Cookies são sempre Secure fora de Development. O suporte a proxy reverso/terminação TLS exige configuração específica e não foi habilitado implicitamente.

## Configuração da API, desktop e CORS

`config.js` é o ponto de configuração pública do frontend; não deve conter segredos. Em origem única, deixe apiBase vazio. A regra existente para a porta 8080 aponta para a API no mesmo hostname e porta 5195; evita usar localhost fixo no celular.

O desktop pode continuar sendo servido separadamente em 8080 nos testes fictícios por HTTP. Para uso real, prefira o desktop na origem HTTPS da API, pois cookies SameSite e esquemas/origens diferentes podem impedir a sessão. Não misture uma página HTTP com dados reais.

A única origem adicional padrão permitida é `http://localhost:8080`. Caso haja uma necessidade real de separar as origens, configure explicitamente `Frontend__AllowedOrigins__0`, `__1`, etc., e ajuste apiBase. CORS inclui credenciais e apenas GET/POST/PUT e os cabeçalhos necessários; não há `AllowAnyOrigin`.

`appsettings.Mobile.example.json` é uma referência. Não substitua o appsettings existente nem copie a connection string para o frontend.

## Instalação da PWA

Com HTTPS confiável, abra o mobile:
- Android/Chrome: menu → Instalar aplicativo/Adicionar à tela inicial, ou botão Instalar na conta quando suportado.
- iPhone/Safari: Compartilhar → Adicionar à Tela de Início.
- A disponibilidade e os rótulos variam conforme o navegador. Consulte a [orientação da MDN sobre instalação](https://developer.mozilla.org/en-US/docs/Web/Progressive_web_apps/Guides/Installing).

O manifest usa nome uGYM Mobile, tema escuro, modo standalone e ícones 192/512/maskable. O service worker tem escopo /mobile/ e uma lista fechada de arquivos estáticos, incluindo os clientes de configuração compartilhados. Não armazena respostas /api, senhas, tokens ou dados de alunos em Cache Storage, localStorage ou IndexedDB. O token CSRF fica apenas em memória e a sessão em cookie HttpOnly.

Offline, a interface continua disponível, mas os dados visíveis são removidos e o usuário recebe orientação para conectar. Não há fila de entradas ou check-in offline. Alterações no service worker devem incrementar a versão do cache. Uma versão nova é ativada pelo ciclo normal do navegador, sem forçar recarga durante um formulário.

## Endpoints

Todos os endpoints de alunos e check-ins exigem sessão. Operações de escrita exigem também o cabeçalho X-CSRF-TOKEN. As respostas autenticadas usam Cache-Control: no-store.

| Método | Caminho | Uso |
| --- | --- | --- |
| GET | /api/auth/csrf | Token CSRF; público, sem dados pessoais |
| POST | /api/auth/login | Login; CSRF obrigatório |
| GET | /api/auth/session | Nome do usuário da sessão |
| POST | /api/auth/logout | Encerra o cookie da sessão |
| GET | /api/Aluno?pagina=1&tamanho=20&busca=ana&ativo=true | Consulta paginada do desktop com filtros opcionais |
| GET | /api/Aluno?visao=mobile&pagina=1&tamanho=20 | Consulta paginada; cada item contém apenas id, nome, plano e ativo |
| GET | /api/Aluno/{id} | Detalhes do aluno |
| POST | /api/Aluno | Cadastro existente do desktop, agora protegido |
| PUT | /api/Aluno/{id} | Altera apenas ativo, contrato preservado |
| POST | /api/CheckIn | Corpo {"alunoId":8}; 201 no sucesso |
| GET | /api/CheckIn/resumo | Contadores do dashboard |
| GET | /api/CheckIn/aluno/{id}/resumo | Total, mês e último check-in |
| GET | /api/CheckIn/aluno/{id}?periodo=todos&pagina=1&tamanho=20 | Histórico paginado |

Períodos: `hoje`, `7dias`, `mes`, `todos`. Tamanho de página: 1–100. Erros: 400 validação/CSRF/HTTPS, 401 sessão ausente/credencial inválida, 403 autorização quando aplicável, 404 aluno ausente, 409 inatividade/duplicidade, 429 limite de tentativas, 503 indisponibilidade. Como o único perfil provisionado é administrador, não há fluxo de perfis restritos nesta versão.

### Contratos de cadastro e listagem de alunos

`GET /api/Aluno` agora retorna `{ "total": 42, "pagina": 1, "tamanho": 20, "itens": [...] }`, em ambas as visões. A resposta anterior era um array; os dois frontends deste workspace já foram atualizados. Consumidores externos devem ler `itens` e navegar pelas páginas. O padrão é página 1 com 20 itens, o limite é 100 itens e a ordenação é nome/ID. `busca` pesquisa nome sem diferenciar acentos e maiúsculas; `ativo=true/false` filtra a situação. Filtros são aplicados antes da contagem e da paginação.

`POST /api/Aluno` recebe `nome`, `cpf`, `plano` e, opcionalmente, `email`, `telefone`, `instrutor` e `observacoes`. Nome e plano são obrigatórios; CPF aceita onze dígitos ou a máscara `000.000.000-00`, exige verificadores válidos e é salvo sem pontuação. E-mail pode ficar vazio. A API atribui ID, situação ativa e data de cadastro; não aceita esses campos como controle do cliente. Validações devolvem HTTP 400 com mensagens por campo, exibidas no desktop. CPF já cadastrado devolve HTTP 409.

Cadastros do mesmo CPF são serializados em uma transação com `sys.sp_getapplock`; a consulta reconhece também CPFs legados com pontuação. Essa proteção cobre os cadastros feitos pelo endpoint. Nenhuma restrição única ou migration foi adicionada ao banco existente, e os CPFs antigos não são regravados.

## UI e UX

Verde #15ff00 e #0bcc00 do desktop, preto #0a0a0a, superfícies escuras e texto branco. Tipografia de sistema evita dependências externas e mantém carregamento leve. O símbolo e o nome da marca são preservados.

O design system em mobile.css concentra cores, fonte, espaçamento, raios, sombras e duração das transições. Cards arredondados, hierarquia clara, busca por nome, estados vazios e botões de pelo menos 44 px favorecem uso com uma mão. A navegação fixa considera safe-area e o conteúdo tem espaço inferior para rolagem. Badges mostram Ativo/Inativo por texto e cor. Dialogs nativos fornecem foco modal e fechamento por teclado.

As animações usam principalmente transform/opacity: entrada de login/telas/cards em 220–300 ms, reação ao toque, abertura de detalhes, confirmação de check-in e toast. Skeleton e spinner indicam carregamento real. prefers-reduced-motion desativa movimentos e transições. Nenhuma biblioteca de animação ou fonte remota é necessária.

## Testes e evidências

Revisão de 23/11/2026: as três correções foram verificadas com 27 validações de cadastro/consulta sem banco, dois testes HTTPS com validação real do certificado, 54 verificações HTTP/SQL e 53 verificações no Chrome. O executor completo registrou 56 verificações ao incluir a saída do navegador e o bloqueio de HTTP. Foram cobertos CPF inválido/duplicado/com máscara, cadastros simultâneos, páginas com mais de 20 alunos, ordenação estável, filtros combinados, busca sem acentos e navegação nas duas interfaces. O banco temporário foi removido; UGymDB não recebeu alterações. Saída do executor: `artifacts/revision-test-results.txt`. A compilação passou com os oito avisos de nulabilidade já existentes nos modelos Aluno/Pagamento.

Verificações realizadas em 10/11/2026:
- 35 verificações de comportamento do backend (incluindo o bloqueio de HTTP fora do modo de teste) e 45 verificações de interface passaram. O executor contabiliza também uma verificação adicional do resultado do subprocesso de navegador.
- `dotnet build --no-restore -o artifacts\build`: sucesso, sem erros; a recompilação completa mantém oito avisos CS8618 que já existiam em Aluno/Pagamento.
- `dotnet publish` pelo Package-Mobile.ps1: pacote local criado.
- Pacote executado por alguns segundos exclusivamente em localhost: desktop e /mobile/ responderam HTTP 200, e o ícone foi servido como image/png. O processo de verificação foi encerrado.
- Sintaxe de config.js, api-client.js, script.js, mobile.js, service-worker.js e testes de navegador.
- Integração HTTP/SQL: login válido/inválido, CSRF, sessão, consultas, status, aluno inexistente/inativo, duplicidade, concorrência, UTC, dia de São Paulo, filtros, paginação, histórico vazio, resumo, operador e logout.
- Chrome headless com API real e banco SQL descartável: login/logout, dashboard real, nome/filtros, detalhes, mudança de status, check-in, histórico, skeleton, erro HTTP, offline, cache estático e compatibilidade do desktop.
- Matriz de login, dashboard, alunos, detalhes, check-in, histórico e conta em 360, 390 e 430 px. Sem rolagem horizontal; capturas aguardam o fim das animações.
- Dados inteiramente fictícios, em banco `UGymMobileTests_<guid>`, removido ao final; UGymDB não recebe migrations, inserts, updates ou deletes nos testes.

Relatório: `artifacts/test-results.txt`. Capturas: `artifacts/screenshots/`, com nomes por tela e largura.

Para repetir:

```powershell
dotnet build -o artifacts\build
dotnet build tests\UGym.Mobile.Tests\UGym.Mobile.Tests.csproj -o artifacts\tests
dotnet artifacts\tests\UGym.Mobile.Tests.dll .

# Cadastro/consulta sem acesso a banco:
dotnet build tests\UGym.Alunos.Tests\UGym.Alunos.Tests.csproj -o artifacts\alunos-tests
dotnet artifacts\alunos-tests\UGym.Alunos.Tests.dll
# Inclua o executável para testar também a inicialização HTTPS com certificado confiável:
dotnet artifacts\alunos-tests\UGym.Alunos.Tests.dll artifacts\build\uGYM.API.dll

# QA visual opcional; instala apenas a dependência de desenvolvimento:
npm install --prefix tests
$env:UGYM_FRONTEND_PATH = 'C:\caminho\front end'
# Se necessário, selecione Chrome ou Edge:
$env:UGYM_BROWSER_PATH = 'C:\Program Files\Google\Chrome\Application\chrome.exe'
dotnet artifacts\tests\UGym.Mobile.Tests.dll . --browser

# Verificações rápidas do frontend:
node --check "$env:UGYM_FRONTEND_PATH\mobile\mobile.js"
node --check "$env:UGYM_FRONTEND_PATH\api-client.js"
node --check "$env:UGYM_FRONTEND_PATH\mobile\service-worker.js"
```

O usuário do Windows que executa os testes precisa poder criar/remover o banco temporário no SQL Server. Não altere o executor para usar UGymDB. As credenciais de teste são aleatórias, passadas aos processos somente em memória e nunca impressas.

Para gerar o pacote local:
```powershell
.\tools\Package-Mobile.ps1 -FrontendPath 'C:\caminho\front end'
```

Ele inclui o desktop e mobile em `artifacts/package/wwwroot`; a API os serve sem depender do caminho original. Não publica nada e não aplica migrations.

## Pendências externas e limites

- Aplicação da migration ao banco existente registrada em etapa anterior; relatório operacional mantido localmente, fora do Git.
- Criação do administrador no terminal, depois da migration.
- Certificado confiável no computador e celular, IPv4 e eventual regra de firewall.
- Validação em aparelho físico: teclado/notch, confiança do certificado, instalação standalone no Android/iPhone e comportamento específico do Safari.
- A instalação real no aparelho e o uso dos dados existentes não foram simulados como concluídos.
- mascote.png original está vazio; fornecer um arquivo válido permitirá incorporar a ilustração em trabalho posterior.
- Pagamentos, edição completa, cadastro mobile, recuperação de senha pela interface e múltiplos perfis permanecem fora do escopo. A redefinição local pelo terminal está disponível com `--reset-password`.
