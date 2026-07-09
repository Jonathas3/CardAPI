# Fluxo de teste manual — CardsApi

Checklist para validar, na ordem, todos os requisitos do enunciado (`prova_tecnica_dev_pleno_dotnet_api.pdf`).
Marque `[x]` conforme for validando. Comandos em `curl.exe` (PowerShell) — pode usar o Swagger em
`http://localhost:5080/swagger` como alternativa visual para os mesmos passos.

## Histórico

### Execução completa via script SQL (2026-07-06, versão anterior)

Todo o checklist abaixo foi executado de ponta a ponta contra a API rodando (não só leitura de código),
na época em que o schema/seed eram aplicados via `sql/001_schema_and_seed.sql` (`docker-entrypoint-initdb.d`).
4 bugs reais foram encontrados e corrigidos no processo:

1. `AppDbContext` não mapeava nomes de coluna para snake_case — EF gerava `"Id"` em vez de `id`, quebrando
   toda query (login incluso). Corrigido em `AppDbContext.cs`.
2. `Configure<ApiBehaviorOptions>` estava registrado *antes* de `AddControllers()` — o factory de erro
   customizado era sobrescrito pelo padrão do MVC; erros de validação não usavam `ErrorResponse`. Corrigido
   a ordem em `Program.cs`.
3. Casing inconsistente entre fontes de erro: validação em `camelCase`, mas `ExceptionHandlingMiddleware`/
   `OnChallenge` em `PascalCase` (serialização manual sem `JsonSerializerOptions`). Corrigido com
   `ErrorResponse.JsonOptions` compartilhado.
4. Volume do Postgres já existente tinha o seed antigo em AES-CBC, de antes da migração para AES-GCM —
   `GET /{id}/pin` retornava `500` para os 15 cartões originais. Corrigido com `UPDATE` pontual no banco
   (sem apagar o volume).

Também confirmado num volume recriado do zero (`docker compose down -v` + `up`), e em 4 casos extras:
isolamento de usuário em `PATCH`/`DELETE`, página além do total (`?page=99` → `200` com `items: []`), e
token JWT malformado → `401`. Nenhum bug novo nesses casos extras.

### Migração para EF Core Migrations (2026-07-06, versão atual)

O projeto passou a usar **EF Core Migrations** em vez do script SQL avulso (ambos são aceitos igualmente
pelo enunciado — "script SQL executado ou instrução de migration utilizada"). O que mudou:

- `sql/001_schema_and_seed.sql` foi **removido** (pasta `sql/` também). O schema + seed agora vivem em
  `Migrations/InitialCreate` (gerada a partir do `AppDbContext`, com o seed incluído via `migrationBuilder.Sql(...)`).
- `AppDbContext.OnModelCreating` ganhou `HasCheckConstraint` explícito para `status` e `credit_limit`,
  pra migration gerada não perder essas garantias que existiam no script SQL.
- `Program.cs` chama `Database.Migrate()` na subida da API — schema e seed são aplicados automaticamente,
  sem precisar de `dotnet ef database update` manual nem `psql`.
- `docker-compose.yml` não monta mais `docker-entrypoint-initdb.d` — o Postgres sobe vazio, e é a própria
  API quem cria tudo.

**Validado após a troca** (volume recriado do zero com `docker compose down -v` + `up`, container
Postgres 100% vazio antes do `dotnet run`):
- [x] `docker logs cardsapi-postgres` limpo, sem erro de inicialização (container não roda mais nenhum script).
- [x] API cria `__EFMigrationsHistory`, `users`, `cards`, `sessions`, `pin_access_logs` sozinha ao subir.
- [x] Seed carregado: 2 usuários, 15 cartões.
- [x] Login (`mariana.alves`) e listagem (`page=1`, `totalItems=12`, `totalPages=2`) funcionando.
- [x] `GET /{id}/pin` de um cartão original do seed retorna o PIN correto (AES-GCM gravado na migration
      está correto por si só, sem depender de nenhum ajuste manual).
- [x] `CHECK` constraint validada: `UPDATE cards SET status = 'HACKED'` direto no banco é **rejeitado**
      pelo Postgres (`ck_cards_status`) — confirma que a migration não perdeu essa proteção.

**Atualização:** o checklist completo abaixo (seções 1 a 10) foi reexecutado por inteiro contra a API com
o banco criado via migration — não só os spot-checks acima. Todos os itens passaram, sem nenhum bug novo:
login/rotação de token, paginação/filtro/isolamento, GET por id, POST + validações, PUT, PATCH, DELETE
(soft delete), PIN exclusivo, formato de erro padronizado (`errorCode`/`message`/`errors`/`traceId` em
todas as respostas) e nenhum dado sensível em log/banco em texto puro. Confirma que a troca de script SQL
para EF Core Migrations não alterou nenhum comportamento da API.

### Divisão em projetos físicos + Repository/UnitOfWork (2026-07-06, versão atual)

O projeto único `CardsApi` foi dividido em 4 projetos `.csproj` (`CardsApi.Domain`,
`CardsApi.Application`, `CardsApi.Infrastructure`, `CardsApi.Api`), com a regra de
dependência clássica (camadas internas nunca referenciam as externas). O projeto
executável (antes `CardsApi.csproj`) agora é `CardsApi.Api.csproj`.

Para que a separação física tivesse efeito real (e não fosse só burocracia), foram
introduzidas abstrações de repositório em `Application` (`ICardRepository`,
`ISessionRepository`, `IUserRepository`), implementadas em `Infrastructure`
(`CardRepository`, `SessionRepository`, `UserRepository`). `CardService` deixou de
depender de `AppDbContext` diretamente — só de `ICardRepository`. `TokenService`,
`AuthService` e `CryptoService` ficaram em `Infrastructure` (dependem de JWT/BCrypt/EF Core,
preocupações técnicas, não regra de negócio).

**Validado após a divisão** (volume recriado do zero com `docker compose down -v` + `up`):
- [x] `dotnet build CardsApi.sln` — 0 avisos, 0 erros, os 4 projetos compilando.
- [x] API sobe normalmente.
- [x] Login, listagem (`totalItems=12`, `totalPages=2`), Swagger (`200`).
- [x] `GET /{id}/pin` de um cartão original do seed retorna o PIN correto.
- [x] `CHECK` constraint (`ck_cards_status`) ainda rejeita `UPDATE` inválido direto no banco.
- [x] Fluxo completo via `ISessionRepository`: login → refresh (rotaciona) → token antigo dá `401`.
- [x] Fluxo completo via `ICardRepository`: POST → PUT → PATCH → DELETE, todos `200`/`201` corretos.
- [x] Isolamento entre usuários (Carlos tentando acessar cartão da Mariana) → `404`.

Nenhum bug novo encontrado na divisão — o comportamento HTTP da API é idêntico ao de antes,
só a organização interna do código mudou.

### Refinamentos adicionais + rename do projeto executável (2026-07-06, versão atual)

Depois da divisão em 4 projetos, mais uma rodada de ajustes:
- Namespaces sincronizados com as pastas (`CardsApi.Domain.Entities`, `CardsApi.Application.Interfaces.Repositories`, etc.).
- DI extraído por camada: `AddApplication()` e `AddInfrastructure()` (um `DependencyInjection.cs`
  em cada projeto) e `AddApiAuthentication()` (`Configuration/AuthenticationExtensions.cs`).
- Novo `ISessionValidator`/`SessionValidator`: o `OnTokenValidated` do JWT parou de acessar
  `AppDbContext` direto em `Program.cs` — agora passa por essa abstração. O único uso restante
  do `AppDbContext` concreto no projeto executável é o `Database.Migrate()` do startup.
- Entidade `Card` ganhou métodos de comportamento (`UpdateDetails`, `UpdateCardNumber`,
  `UpdatePin`, `SoftDelete`), além do `SetStatus` já existente.
- **O projeto executável foi renomeado de `CardsApi.Api` de volta para `CardsApi`**
  (pasta `src/CardsApi/`, arquivo `CardsApi.csproj`) — comando atual pra rodar:
  `dotnet run --project src\CardsApi\CardsApi.csproj`.

**Validado após essa rodada** (volume recriado do zero): build 0 erros/avisos, login, listagem,
PIN, refresh (passando pelo novo `ISessionValidator`), CRUD completo via os novos métodos da
entidade, Swagger — tudo confirmado funcionando.

### `CardStatus` vira enum, `IAppDbContext` removido, `GenericRepository<T>` (2026-07-07)

Várias mudanças pontuais na mesma sessão:

- **`CardStatus`**: de classe com constantes `string` para `enum` (`Active`/`Blocked`/`Cancelled`),
  armazenado como `integer` no banco (`ck_cards_status` virou `status IN (0,1,2)`). Contrato da API
  não mudou (`"status": "ACTIVE"` continua igual — `CardService` converte enum↔string nas bordas).
  Isso exigiu recriar o volume do Postgres (`docker compose down -v` + `up`), porque a migration
  `InitialCreate` foi editada no lugar (mesmo id) e o banco já tinha aplicado a versão antiga.
  De quebra, `Card.SetStatus`/`CardService.ApplyStatus` foram removidos (viraram wrappers triviais
  depois do enum) — `Card.Status` agora é `{ get; set; }` normal, como `Nickname`/`Brand`.
- **`IAppDbContext` removido**: interface só existia pra restringir o que os repositórios viam do
  `AppDbContext` (escondendo `Database.Migrate()`/`ChangeTracker`) — desacoplamento redundante, já
  que `ICardRepository`/`ISessionRepository`/`IUserRepository` já isolam `Application` do EF Core.
  Repositórios passaram a receber `AppDbContext` direto.
- **`GenericRepository<TEntity>`**: nova classe base abstrata que centraliza `AddAsync`/
  `SaveChangesAsync` (únicos membros repetidos entre `CardRepository`/`SessionRepository`/
  `UserRepository`). Cada repositório concreto herda dela e só implementa o que é específico
  (`ListAsync`/`FindByIdAsync` no `CardRepository`, etc.).
- **Criptografia (`CryptoService`) — explorado e revertido**: chegou a ser trocada de AES-256-GCM
  para uma cifra XOR de chave repetida (pedido explícito de "sem biblioteca"), incluindo
  reencriptação de todo o seed. Revertida na mesma sessão de volta pra AES-256-GCM original
  (`git blame`/histórico não guarda esse desvio — só este log). **Estado atual: AES-256-GCM, sem
  mudança líquida em relação à entrada anterior.**

**Validado** (volume recriado do zero pelo menos duas vezes durante essas mudanças): build 0
erros/avisos, `dotnet test` 11/11, login, listagem (`totalItems=12`), criação, `GET /{id}/pin`
com PIN correto, PATCH de status (válido e inválido), soft delete e refresh de token — todos
exercitados via requisição HTTP real após cada mudança, não só leitura de código.

### Repositório Git criado (2026-07-07)

Projeto publicado em `https://github.com/Jonathas3/CardAPI` (branch `main`). `.gitignore` já
existente cobre `bin/`, `obj/`, `.vscode/`, `.claude/` e `CLAUDE.md`.

### Projetos renomeados de `CardsApi.*` para `Cards.*` (2026-07-08)

Os 4 projetos físicos e o projeto de testes foram renomeados: `CardsApi.Domain` -> `Cards.Domain`,
`CardsApi.Application` -> `Cards.Application`, `CardsApi.Infrastructure` -> `Cards.Infrastructure`,
`CardsApi` (executável) -> `Cards.Api`, `tests/CardsApi.Application.Tests` -> `tests/Cards.Application.Tests`.
Pastas, `.csproj`, `RootNamespace`, `ProjectReference` e todo `namespace`/`using` no código (52
arquivos `.cs`) foram atualizados. `CardsApi.sln` manteve o nome (não foi pedido renomear o
arquivo de solution). O rename das pastas via `Rename-Item` deu "acesso negado" pelo mesmo motivo
de sempre (processo do C# Dev Kit segurando lock) — resolvido matando o `ProjectSystem.Server.BuildHost.dll`
e, quando ainda assim não liberou, movendo o conteúdo pra pasta nova em vez de renomear a pasta
em si (mesmo workaround já documentado nesta sessão). O `.sln` perdeu as referências de projeto
durante o processo (provavelmente a IDE "corrigiu" automaticamente ao ver os caminhos quebrados) —
recriadas com `dotnet sln add`.

**Comando atual pra rodar:** `dotnet run --project src\Cards.Api\Cards.Api.csproj`.

**Validado**: `dotnet build CardsApi.sln` (0 avisos/erros, 5 projetos), `dotnet test` (11/11),
API sobe, Swagger `200` com os 5 endpoints, login + listagem (`totalItems=12`) funcionando.

## 0. Pré-requisitos

- [x] `docker compose up -d` — sobe o Postgres **vazio** (schema/seed aplicados pela API, não por script).
- [x] `docker compose ps` — `cardsapi-postgres` deve estar `Up (healthy)`.
- [x] `dotnet run --project src\Cards.Api\Cards.Api.csproj` — aplica as migrations automaticamente e sobe a
      API em `http://localhost:5080`.
- [x] Abrir `http://localhost:5080/swagger` — Swagger UI carrega sem erro.

Usuários seedados (senha em texto puro só existe aqui, nunca no banco):

| username | password | qtd. cartões |
|---|---|---|
| `mariana.alves` | `Senha@123` | 12 (para validar paginação) |
| `carlos.silva` | `Senha@123` | 3 (para validar isolamento por usuário) |

---

## 1. Autenticação e rotação de token (seção 3 do edital)

### 1.1 Login válido
```powershell
curl.exe -s -X POST http://localhost:5080/api/auth/login `
  -H "Content-Type: application/json" `
  -d '{"username":"mariana.alves","password":"Senha@123"}'
```
- [x] Retorna `200` com `accessToken`, `tokenType: Bearer`, `expiresAtUtc` ~30 min à frente.
- [x] Guardar o token em `$env:TOKEN_MARIANA` para os próximos passos.

### 1.2 Login inválido (senha errada e usuário inexistente)
```powershell
curl.exe -s -o NUL -w "%{http_code}`n" -X POST http://localhost:5080/api/auth/login `
  -H "Content-Type: application/json" -d '{"username":"mariana.alves","password":"errada"}'
curl.exe -s -X POST http://localhost:5080/api/auth/login `
  -H "Content-Type: application/json" -d '{"username":"nao_existe","password":"x"}'
```
- [x] Ambos retornam `401` com a **mesma mensagem genérica** (não revela se o usuário existe).
- [x] Nenhum dos dois aparece nos logs do console com a senha em texto puro.

### 1.3 Acesso sem token / token inválido
```powershell
curl.exe -s -o NUL -w "%{http_code}`n" http://localhost:5080/api/cards
```
- [x] Retorna `401`, corpo padronizado (`ErrorResponse`), sem stack trace.

### 1.4 Rotação de token (`/api/auth/refresh`)
```powershell
curl.exe -s -X POST http://localhost:5080/api/auth/refresh `
  -H "Authorization: Bearer $env:TOKEN_MARIANA"
```
- [x] Retorna `200` com um **novo** `accessToken` e nova `expiresAtUtc`.
- [x] Repetir a chamada de `/api/cards` com o **token antigo** ($env:TOKEN_MARIANA) → deve dar `401`
      (sessão anterior revogada).
- [x] Atualizar `$env:TOKEN_MARIANA` para o novo token e confirmar que ele funciona em `/api/cards`.

---

## 2. Listagem — GET /api/cards (seção 4.1)

```powershell
curl.exe -s http://localhost:5080/api/cards -H "Authorization: Bearer $env:TOKEN_MARIANA"
```
- [x] `page=1`, `pageSize=10`, `totalItems=12`, `totalPages=2`, `items` com 10 registros.
- [x] Itens ordenados do mais recente para o mais antigo (`createdAt` decrescente).
- [x] `maskedNumber` no formato `AAAA **** **** ZZZZ` — número completo **nunca** aparece.
- [x] Nenhum campo de PIN presente na resposta.

```powershell
curl.exe -s "http://localhost:5080/api/cards?page=2" -H "Authorization: Bearer $env:TOKEN_MARIANA"
```
- [x] Retorna os 2 itens restantes de Mariana.

### 2.1 Filtro por vencimento
```powershell
curl.exe -s "http://localhost:5080/api/cards?expirationFrom=2028-01-01&expirationTo=2029-12-31" `
  -H "Authorization: Bearer $env:TOKEN_MARIANA"
```
- [x] Só retorna cartões com `expirationDate` dentro do intervalo.
- [x] `expirationFrom > expirationTo` retorna `400`.

### 2.2 Isolamento por usuário
```powershell
curl.exe -s -X POST http://localhost:5080/api/auth/login -H "Content-Type: application/json" `
  -d '{"username":"carlos.silva","password":"Senha@123"}'
# guardar em $env:TOKEN_CARLOS
curl.exe -s http://localhost:5080/api/cards -H "Authorization: Bearer $env:TOKEN_CARLOS"
```
- [x] Retorna só os 3 cartões de Carlos — nunca os de Mariana.

---

## 3. Consulta por id — GET /api/cards/{id} (seção 4.1)

- [x] Com token da Mariana, `GET /api/cards/{id-de-um-cartao-dela}` → `200`.
- [x] Com token da Mariana, `GET /api/cards/{id-de-um-cartao-do-Carlos}` → `404` (não `403` — evita
      confirmar a existência do recurso de outro usuário).
- [x] `GET /api/cards/{guid-aleatorio-inexistente}` → `404`.

---

## 4. Criação — POST /api/cards (seção 4.2)

```powershell
curl.exe -s -X POST http://localhost:5080/api/cards -H "Authorization: Bearer $env:TOKEN_MARIANA" `
  -H "Content-Type: application/json" -d '{
    "cardholderName": "MARIANA ALVES",
    "nickname": "Cartao Teste",
    "brand": "VISA",
    "cardNumber": "5321123412345336",
    "expirationDate": "2029-12-31",
    "creditLimit": 6500.00,
    "pin": "1234"
  }'
```
- [x] Retorna `201 Created` com `Location`/id do novo cartão.
- [x] `maskedNumber = "5321 **** **** 5336"`.
- [x] Resposta não contém `cardNumber` nem `pin`.

### 4.1 Validações
- [x] Campo obrigatório ausente (`cardholderName` faltando) → `400`, `ErrorResponse` com detalhes por campo.
- [x] `cardNumber` com letras ou tamanho fora de 13–19 dígitos → `400`.
- [x] `pin` com mais/menos de 4 dígitos → `400`.
- [x] `creditLimit: -100` → `400` ("creditLimit must be greater than or equal to zero").
- [x] `expirationDate` em formato inválido (ex.: `"31-12-2029"`) → `400`.

---

## 5. Atualização completa — PUT /api/cards/{id} (seção 4.3)

```powershell
curl.exe -s -X PUT http://localhost:5080/api/cards/{id} -H "Authorization: Bearer $env:TOKEN_MARIANA" `
  -H "Content-Type: application/json" -d '{
    "cardholderName": "MARIANA ALVES",
    "nickname": "Principal Atualizado",
    "brand": "VISA",
    "cardNumber": "5321123412345336",
    "expirationDate": "2029-01-31",
    "creditLimit": 14000.00,
    "status": "ACTIVE",
    "pin": "9876"
  }'
```
- [x] `200 OK`, todos os campos editáveis substituídos pelos novos valores.
- [x] Omitir um campo obrigatório (ex.: `status`) → `400` (comportamento documentado no README: PUT exige
      objeto completo, não faz merge parcial).
- [x] `status: "INVALID"` → `400`.
- [x] `PUT` em cartão de outro usuário (Carlos tentando dar PUT num id da Mariana) → `404`.

---

## 6. Atualização parcial — PATCH /api/cards/{id} (seção 4.4)

```powershell
curl.exe -s -X PATCH http://localhost:5080/api/cards/{id} -H "Authorization: Bearer $env:TOKEN_MARIANA" `
  -H "Content-Type: application/json" -d '{"nickname":"Uso diario","creditLimit":15500.00}'
```
- [x] `200 OK`, só `nickname`/`creditLimit` mudam — demais campos (`brand`, `expirationDate`, etc.)
      permanecem os mesmos de antes do PATCH.
- [x] PATCH enviando só `{}` → `200`, nada muda.
- [x] PATCH com `pin` novo → PIN realmente muda (conferir no passo 8, endpoint exclusivo).

---

## 7. Remoção — DELETE /api/cards/{id} (seção 4.5)

```powershell
curl.exe -s -X DELETE http://localhost:5080/api/cards/{id} -H "Authorization: Bearer $env:TOKEN_MARIANA"
```
- [x] `200 OK`, corpo confirma a remoção sem expor detalhes internos.
- [x] `GET /api/cards/{id}` no mesmo cartão → `404` (some das consultas comuns).
- [x] `GET /api/cards` (listagem) → cartão removido não aparece mais, `totalItems` cai em 1.
- [x] Rastreabilidade preservada: consultar direto no banco (`SELECT * FROM cards WHERE id = '...'`) —
      linha ainda existe, com `is_deleted = true` e `deleted_at` preenchido (não foi hard-delete).
- [x] `DELETE` de cartão de outro usuário → `404`.
- [x] `DELETE` do mesmo cartão de novo → `404` (já removido, não redeleta).

---

## 8. Consulta exclusiva do PIN — GET /api/cards/{id}/pin (seção 4.6)

```powershell
curl.exe -s http://localhost:5080/api/cards/{id}/pin -H "Authorization: Bearer $env:TOKEN_MARIANA"
```
- [x] `200 OK`, retorna o PIN original em texto puro (só neste endpoint).
- [x] Esse PIN **nunca** aparece em `GET /api/cards` ou `GET /api/cards/{id}` (consultas comuns).
- [x] Endpoint de outro usuário → `404`.
- [x] Sem token → `401`.
- [x] Conferir no banco: `SELECT * FROM pin_access_logs ORDER BY accessed_at DESC LIMIT 1;` — registra
      `card_id`, `user_id`, `accessed_at`, `ip`, e **nunca** o valor do PIN.

---

## 9. Erros padronizados (seção 5, item 11)

- [x] Todas as respostas de erro acima seguem o mesmo formato `ErrorResponse`
      (`errorCode`, `message`, `errors`, `traceId`).
- [x] Nenhuma resposta de erro expõe stack trace, nome de tabela/coluna ou detalhes internos do EF Core.

---

## 10. Segurança / dados sensíveis (revisão geral)

- [x] Nos logs do console (`dotnet run`), confirmar que nenhuma senha, PIN ou número de cartão completo
      aparece em nenhum momento do fluxo acima.
- [x] `SELECT card_number_encrypted, pin_encrypted FROM cards LIMIT 1;` — valores são Base64 opaco
      (AES-256-GCM), não texto puro.

---

## Encerramento

```powershell
docker compose down
```
- [ ] (Opcional) Derruba o container ao final da bateria de testes. Use `docker compose down -v` só se
      quiser também apagar o volume do Postgres (perde os dados do seed). **Não executado nesta sessão —
      API e Postgres continuam rodando.**
