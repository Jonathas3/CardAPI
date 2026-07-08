# Cards API — Prova Técnica Dev .NET Pleno

API REST em **.NET 8** para gestão de cartões de crédito de um usuário autenticado,
usando **PostgreSQL** + **Entity Framework Core**, com **Swagger** habilitado.

> Nenhum arquivo SQL foi anexado à prova recebida (apenas o PDF de enunciado). O
> schema foi desenhado do zero para cobrir todos os requisitos (inclusive PIN
> criptografado e trilha de auditoria) e é aplicado via **EF Core Migrations**
> (`src/CardsApi.Infrastructure/Migrations/`), conforme permitido pelo próprio enunciado
> ("documente a decisão no README e entregue os scripts/migrations correspondentes").
> A migration aplica schema **e** seed juntos, automaticamente, na subida da API.

---

## 1. Stack

- C# / .NET 8 (ASP.NET Core Web API)
- PostgreSQL 16 + Entity Framework Core (Npgsql)
- Autenticação por **JWT** com sessão controlada no banco (suporta expiração e
  rotação real, não apenas expiração do token)
- Swagger / OpenAPI (Swashbuckle)
- BCrypt.Net-Next para hash da senha de login (`users.password_hash`)
- AES-256-GCM (com nonce aleatório e tag de autenticação por valor) para número do cartão e PIN

Arquitetura em camadas **físicas**: cada camada é um projeto `.csproj` próprio, com a
regra de dependência clássica (camadas internas nunca referenciam as externas).

```
CardsApi.sln
src/
  CardsApi.Domain/                     (zero dependências de pacotes)
    Entities/                         -> Card, User, Session, PinAccessLog, CardStatus
                                          (namespace CardsApi.Domain.Entities)

  CardsApi.Application/                (referencia só Domain; zero pacotes de infra)
    Interfaces/
      Repositories/                   -> ICardRepository, ISessionRepository, IUserRepository
      Services/                       -> ICardService, ITokenService, IAuthService,
                                          ICryptoService, ISessionValidator, IPasswordHasher
    Services/                         -> CardService, AuthService (regra de negócio pura,
                                          usam só interfaces: ICardRepository/IUserRepository/
                                          IPasswordHasher/ITokenService)
    Dtos/
      Auth/                          -> LoginRequestDto, TokenResponseDto
      Cards/                         -> DTOs de criação, atualização, resposta e PIN
      Common/                        -> PagedResultDto
    Common/                           -> ApiException
    DependencyInjection.cs            -> AddApplication()

  CardsApi.Infrastructure/             (referencia Domain + Application; implementa as interfaces)
    Data/                             -> AppDbContext (EF Core)
      Configurations/                 -> IEntityTypeConfiguration<T> por entidade
                                          (UserConfiguration, CardConfiguration,
                                          SessionConfiguration, PinAccessLogConfiguration)
    Migrations/                       -> EF Core migrations (schema + seed)
    Repositories/                     -> CardRepository, SessionRepository, UserRepository
                                          (recebem DbSet<T> injetado direto por entidade, mais
                                          AppDbContext só onde precisam de SaveChangesAsync; o
                                          desacoplamento de Application em relação ao EF Core já
                                          vem de ICardRepository/ISessionRepository/IUserRepository)
    Services/                         -> TokenService, CryptoService, SessionValidator,
                                          BCryptPasswordHasher (JWT, AES/configuração,
                                          sessão em banco e BCrypt)
    DependencyInjection.cs            -> AddInfrastructure()

  CardsApi/                             (referencia Application + Infrastructure; composition root; projeto executável)
    Controllers/                      -> HTTP, validação de entrada, mapeamento de claims
    Middleware/                       -> tratamento global de exceções
    Common/                           -> ErrorResponse
    Configuration/AuthenticationExtensions.cs -> AddApiAuthentication() (JWT bearer + eventos)
    Program.cs                       -> composition root enxuto: AddApplication() +
                                         AddInfrastructure() + AddApiAuthentication() +
                                         Swagger + pipeline + Database.Migrate()
```

Nenhuma regra de negócio fica em controller: controllers apenas fazem parsing/roteamento
e delegam para `ICardService` / `ITokenService`. `CardService` (Application) nunca toca
EF Core diretamente — ele depende só de `ICardRepository` (interface); quem implementa
com `AppDbContext`/LINQ-to-SQL é `CardRepository`, na Infrastructure. Isso é o que torna a
separação física útil de verdade: se `Application`/`Domain` referenciassem `Infrastructure`
só para usar o `AppDbContext` direto, a separação em projetos não teria efeito prático
nenhum além de mais arquivos `.csproj` para manter.

`Program.cs` não registra serviço nenhum diretamente: cada projeto expõe seu próprio método
de extensão (`AddApplication()`, `AddInfrastructure()`, `AddApiAuthentication()`), chamado
uma vez no composition root. Isso inclui o `OnTokenValidated` do JWT — antes acessava
`AppDbContext` direto dentro de `Program.cs`; agora passa por `ISessionValidator` (interface
em `Application`, implementada em `Infrastructure`). O único lugar que ainda toca o
`AppDbContext` concreto é o `Database.Migrate()` no startup, que não tem equivalente via
interface e é responsabilidade exclusiva do composition root.

---

## 2. Como rodar localmente

```bash
# 1) sobe o Postgres (container vazio - o schema/seed vêm da migration, não de um script)
docker compose up -d

# 2) restaura pacotes e roda a API (projeto executável é o CardsApi)
cd src/CardsApi
dotnet restore
dotnet run
```

Na subida, a API chama `Database.Migrate()` automaticamente (ver `Program.cs`), o que
cria o schema completo (tabelas, índices, `CHECK` constraints, foreign keys) e insere
o seed de usuários/cartões — tudo em uma única migration
(`CardsApi.Infrastructure/Migrations/InitialCreate`). Não é preciso rodar
`dotnet ef database update` manualmente nem `psql` algum: um `docker compose up -d` +
`dotnet run` já deixa o banco pronto do zero.

A API sobe em `http://localhost:5080` (perfil `http` em `launchSettings.json`) e o
Swagger fica disponível em `http://localhost:5080/swagger`.

Caso queira aplicar/gerar migrations manualmente (sem depender do `Database.Migrate()`
no startup), com a ferramenta `dotnet-ef` instalada (`dotnet tool install --global dotnet-ef`).
Como o `DbContext`/migrations vivem em `CardsApi.Infrastructure` mas o composition root
(`Program.cs`, DI) vive em `CardsApi`, é preciso apontar os dois projetos:

```bash
# a partir da raiz do repositório
dotnet ef database update --project src/CardsApi.Infrastructure --startup-project src/CardsApi
dotnet ef migrations add NomeDaMigration --project src/CardsApi.Infrastructure --startup-project src/CardsApi
```

### Testes automatizados

A solution inclui testes unitários em `tests/CardsApi.Application.Tests`, cobrindo regras
centrais da prova sem depender do PostgreSQL: paginação fixa em 10 itens, rejeição de
intervalo de vencimento inválido, isolamento por usuário, mascaramento/criptografia de
dados sensíveis na criação, PUT/PATCH/DELETE (incluindo rejeição de `status`/`cardNumber`
inválidos) e auditoria do endpoint exclusivo de PIN.

```bash
dotnet test CardsApi.sln
```

### Connection string / segredos

`appsettings.json` já vem com uma connection string e uma chave JWT/AES de
demonstração, funcionais para rodar localmente. **Em qualquer ambiente real**, mova
`Jwt:SigningKey` e `Crypto:AesKeyBase64` para variáveis de ambiente ou
`dotnet user-secrets` — nunca versione segredos reais.

---

## 3. Usuários de teste (seed)

| username        | password    | observação                              |
|-----------------|-------------|------------------------------------------|
| `mariana.alves` | `Senha@123` | possui **12 cartões** (>10, para validar paginação) |
| `carlos.silva`  | `Senha@123` | possui 3 cartões (para validar isolamento entre usuários) |

Não é necessário endpoint de cadastro — conforme o enunciado, os usuários já vêm
prontos no seed, com senha em hash (BCrypt) no banco.

---

## 4. Autenticação e rotação de token

- `POST /api/auth/login` — recebe `username`/`password` e retorna um **JWT** com
  validade de **30 minutos** (`Jwt:AccessTokenMinutes`), mais o instante de expiração.
- Toda rota de `/api/cards/**` exige `Authorization: Bearer <token>`.
- O JWT carrega `sub` (id do usuário) e `jti` (id da sessão). Cada login cria uma
  linha em `sessions` com `expires_at = now + 30min`.
- Além da validação padrão de assinatura/expiração do JWT, um `OnTokenValidated`
  (em `CardsApi/Configuration/AuthenticationExtensions.cs`) chama `ISessionValidator`
  para verificar se a sessão referenciada por `jti` ainda está ativa (não expirada e não
  revogada) — isso é o que permite **revogar** um token antes do seu prazo natural, algo
  que um JWT puro (stateless) não permite sozinho.
- `POST /api/auth/refresh` (autenticado com o token atual, ainda válido) — **rotaciona**
  o token: a sessão atual é marcada como revogada (`revoked_at`) e uma nova sessão é
  criada com nova validade de 30 minutos; o novo token é retornado. A sessão antiga
  nunca mais autentica (mesmo que o JWT em si ainda não tivesse expirado).
- Token expirado é sempre rejeitado com `401` — não há renovação automática de um
  token que já expirou; nesse caso o cliente deve chamar `/api/auth/login` novamente.
- Nenhuma senha, PIN, número de cartão ou token é escrito em log. O middleware global
  de exceções (`ExceptionHandlingMiddleware`) nunca ecoa corpo de requisição/stack trace
  para o cliente.

Exemplo de uso:

```bash
curl -X POST http://localhost:5080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"mariana.alves","password":"Senha@123"}'

# -> { "accessToken": "...", "tokenType": "Bearer", "expiresAtUtc": "..." }

curl http://localhost:5080/api/cards \
  -H "Authorization: Bearer <accessToken>"

curl -X POST http://localhost:5080/api/auth/refresh \
  -H "Authorization: Bearer <accessToken>"
```

---

## 5. Endpoints de cartões

Todos exigem `Authorization: Bearer <token>` e operam **apenas** sobre cartões do
usuário autenticado (qualquer tentativa de acessar cartão de outro usuário retorna
`404`, para não revelar se o id pertence a outra pessoa).

### `GET /api/cards`
Lista os cartões do usuário autenticado, do mais recente para o mais antigo
(`createdAt DESC`), em **blocos fixos de 10 itens**.

Query params:
- `page` (opcional, padrão `1`)
- `expirationFrom` (opcional, `yyyy-MM-dd`) — vencimento a partir de
- `expirationTo` (opcional, `yyyy-MM-dd`) — vencimento até

O filtro de vencimento é aplicado na `IQueryable` (traduzido para `WHERE` no SQL)
antes de `Skip/Take`, ou seja, antes de materializar os dados.

Resposta:
```json
{
  "page": 1,
  "pageSize": 10,
  "totalItems": 12,
  "totalPages": 2,
  "items": [
    {
      "id": "uuid",
      "nickname": "Principal",
      "brand": "VISA",
      "maskedNumber": "5321 **** **** 5336",
      "expirationDate": "2028-01-31",
      "creditLimit": 12000.00,
      "status": "ACTIVE",
      "createdAt": "2026-06-30T09:10:00Z"
    }
  ]
}
```

### `GET /api/cards/{id}`
Retorna um cartão específico do usuário autenticado (mesmo formato do item acima).

### `POST /api/cards`
Cria um cartão. Corpo:
```json
{
  "cardholderName": "MARIANA ALVES",
  "nickname": "Cartão Eventos",
  "brand": "VISA",
  "cardNumber": "5321123412345336",
  "expirationDate": "2029-12-31",
  "creditLimit": 6500.00,
  "status": "ACTIVE",
  "pin": "1234"
}
```
- `status` é opcional (padrão `ACTIVE`); quando informado, deve ser `ACTIVE`,
  `BLOCKED` ou `CANCELLED`.
- `creditLimit >= 0`; `cardNumber` com 13–19 dígitos; `pin` com exatamente 4 dígitos.
- `cardNumber` e `pin` **nunca** voltam em texto puro na resposta: o retorno traz
  `maskedNumber` (ex.: `5321 **** **** 5336`), nunca o PIN.

### `PUT /api/cards/{id}` — atualização completa
Substitui **todos** os campos editáveis. Todos são obrigatórios no corpo
(`cardholderName`, `nickname`, `brand`, `cardNumber`, `expirationDate`,
`creditLimit`, `status`, `pin`) — não há "manter valor antigo" no PUT: campo ausente
= erro de validação `400`. Esse é o contrato documentado para "como campos ausentes
serão tratados" pedido no enunciado.

### `PATCH /api/cards/{id}` — atualização parcial
DTO próprio de atualização parcial (todos os campos opcionais/nulos); só os campos
enviados no corpo são alterados. Exemplo:
```json
{ "nickname": "Uso diário", "creditLimit": 15500.00 }
```

Campos editáveis (PUT e PATCH): `cardholderName`, `nickname`, `brand`, `cardNumber`,
`expirationDate`, `creditLimit`, `status`, `pin`. Não editáveis: `id`, `userId`,
`createdAt` (o servidor sempre atualiza `updatedAt`).

### `DELETE /api/cards/{id}`
**Soft delete**: o cartão é marcado como removido (`isDeleted = true`,
`deletedAt = now`) e some das consultas comuns (`GET /api/cards`,
`GET /api/cards/{id}`), mas o registro é preservado no banco para rastreabilidade
(ex.: histórico de `pin_access_logs` associados). Resposta:
```json
{ "id": "uuid", "deleted": true, "message": "Card removed successfully." }
```

### `GET /api/cards/{id}/pin` — consulta exclusiva da senha
Endpoint **separado** das consultas comuns, exigindo o mesmo `Bearer` token e o mesmo
controle de posse do cartão. A senha (PIN) é armazenada criptografada
(AES-256-GCM, nunca em texto puro) e só é descriptografada dentro deste endpoint.
Cada acesso é registrado em `pin_access_logs` (quem, qual cartão, quando, IP) —
nunca o valor do PIN — para dar rastreabilidade ao acesso a esse dado sensível.

---

## 6. Regras de negócio e validações implementadas

1. Toda operação é restrita ao usuário autenticado (`userId` extraído do JWT,
   nunca de parâmetro de rota/corpo).
2. Token com validade de 30 minutos + rotação documentada (seção 4).
3. Listagem em blocos fixos de 10 itens (`PageSize = 10` fixo no `CardService`).
   `mariana.alves` tem 12 cartões no seed para comprovar a paginação.
4. Ordenação sempre por `createdAt DESC`.
5. Filtro por período de vencimento (`expirationFrom` / `expirationTo`), aplicado
   antes de paginar/materializar.
6. `status` restrito a `ACTIVE` | `BLOCKED` | `CANCELLED` (validado na aplicação e
   reforçado com `CHECK` no banco).
7. Número do cartão nunca retornado por inteiro em consultas comuns — apenas
   `maskedNumber` (`AAAA **** **** ZZZZ`).
8. `creditLimit` nunca negativo (`[Range]` na aplicação + `CHECK` no banco).
9. PIN tratado como dado altamente sensível: criptografado em repouso (AES-256),
   nunca logado, nunca retornado fora do endpoint exclusivo.
10. Consulta do PIN só existe em endpoint próprio, com a mesma autorização por
    dono do cartão e auditoria de acesso.
11. Erros padronizados (`ErrorResponse`: `errorCode`, `message`, `errors?`,
    `traceId`) com status HTTP coerente (`400`, `401`, `404`, `500`).

---

## 7. Decisões técnicas

- **Sem SQL base fornecido**: como nenhum arquivo `.sql` foi anexado à prova, o
  schema foi desenhado do zero já contemplando criptografia de número/PIN, soft
  delete e auditoria de acesso ao PIN — decisão registrada aqui conforme pedido
  no enunciado.
- **EF Core Migrations em vez de script SQL avulso**: o enunciado aceita "script
  SQL executado **ou** instrução de migration utilizada" como entregável
  equivalentes. Optamos por migrations (`Migrations/InitialCreate`) por já usarmos
  EF Core como ORM — assim o schema fica versionado a partir do próprio
  `AppDbContext`, sem duplicar a definição das tabelas em dois lugares. A seed
  (usuários e cartões de teste) foi incluída dentro da própria migration via
  `migrationBuilder.Sql(...)`, e as `CHECK` constraints de `status` e
  `credit_limit` foram declaradas explicitamente em `AppDbContext.OnModelCreating`
  (`HasCheckConstraint`) para que a migration gerada não perdesse essas garantias
  a nível de banco.
- **Autenticação por JWT + sessão em banco (híbrido)**: um JWT puro não permite
  revogação antes da expiração; para cumprir "rotação deve substituir o valor
  anterior por um novo valor", a validade real de cada token é controlada pela
  tabela `sessions` (o JWT carrega apenas o `jti` da sessão). Isso torna a rotação
  (`/api/auth/refresh`) determinística e auditável, sem precisar de um serviço
  externo de cache/blacklist.
- **Criptografia reversível (AES-256-GCM) em vez de hash para número/PIN**: ambos
  precisam ser recuperados (o PIN literalmente precisa ser devolvido pelo endpoint
  exclusivo), o que exclui hash (`BCrypt`/`SHA`) como opção — hash é usado apenas
  para a senha de login, que nunca precisa ser "lida de volta". Cada valor
  criptografado usa um nonce aleatório próprio e gera uma tag de autenticação
  (ambos prefixados/anexados ao ciphertext em Base64), evitando que dois PINs
  iguais produzam o mesmo ciphertext e detectando qualquer adulteração do dado
  criptografado em repouso (ex.: edição direta no banco), o que o modo CBC puro
  não detectaria.
- **`cardNumberFirst4`/`cardNumberLast4` em colunas separadas**: permite montar a
  máscara (`5321 **** **** 5336`) sem nunca precisar descriptografar o número em
  consultas comuns (listagem/detalhe), reduzindo a superfície de exposição do dado
  sensível.
- **Soft delete em `cards`**: atende a "não quebrar rastreabilidade mínima do
  domínio" — o registro (e seu histórico de acesso ao PIN) continua existindo,
  apenas oculto de consultas comuns via `HasQueryFilter(!IsDeleted)` no EF Core.
- **404 (não 403) para cartão de outro usuário**: evita revelar a um usuário que
  determinado id de cartão existe e pertence a outra pessoa.
- **Validação de `cardNumber` apenas por formato (13–19 dígitos), sem checksum de
  Luhn**: o próprio número de exemplo do enunciado (`5321123412345336`) não passa
  na validação de Luhn; usar `[CreditCard]` do .NET rejeitaria o exemplo oficial da
  prova, por isso a validação ficou restrita ao formato.
- **PATCH com DTO próprio** (em vez de JSON Patch/RFC 6902): mais simples de
  documentar e testar manualmente pelo Swagger, com o mesmo efeito de atualização
  parcial pedido no enunciado.
- **Camadas em projetos físicos separados (`Domain`/`Application`/`Infrastructure`/`Api`)
  + Repository**: o enunciado só pede "arquitetura organizada em camadas ou
  abordagem equivalente", que já era atendido com pastas num único projeto. Optamos por
  ir além e separar fisicamente porque, sem isso, a separação em pastas não tinha uma
  regra de dependência realmente imposta pelo compilador. Para que a separação física
  tivesse efeito prático (e não fosse só burocracia), introduzimos `ICardRepository`/
  `ISessionRepository`/`IUserRepository` em `Application`, implementados em
  `Infrastructure` — assim `CardService` (a regra de negócio de cartões) não depende
  mais de `AppDbContext`/EF Core, só de uma interface. `AuthService` também fica em
  `Application`, pois orquestra a regra de login usando apenas interfaces
  (`IUserRepository`, `IPasswordHasher`, `ITokenService`). Já `TokenService`,
  `CryptoService`, `SessionValidator` e `BCryptPasswordHasher` ficam em
  `Infrastructure`, porque lidam com detalhes técnicos específicos (JWT, criptografia,
  consulta de sessão e BCrypt) que podem ser trocados sem afetar a regra de negócio.
- **DI extraído por camada (`AddApplication()`/`AddInfrastructure()`/`AddApiAuthentication()`)
  + `ISessionValidator`**: cada projeto expõe seu próprio método de extensão de
  `IServiceCollection`, chamado uma vez em `Program.cs`, em vez de todo registro viver
  solto no composition root. Isso também permitiu remover o último acesso direto ao
  `AppDbContext` fora da Infrastructure: o `OnTokenValidated` do JWT (que checava se a
  sessão ainda estava ativa) agora passa por `ISessionValidator`/`SessionValidator`
  (que usa `ISessionRepository`), então `Program.cs` só toca o `AppDbContext` concreto
  para `Database.Migrate()` — nada mais.
- **Comportamento na entidade `Card`** (`UpdateDetails`, `UpdateCardNumber`, `UpdatePin`,
  `SoftDelete`): em vez de `CardService` setar campos relacionados
  um a um (número + first4 + last4; PIN; soft-delete + timestamp), a entidade agrupa
  essas mudanças coesas em métodos próprios, reduzindo a chance de um caller esquecer
  de atualizar um campo relacionado (ex.: mudar `CardNumberEncrypted` sem atualizar
  `CardNumberFirst4`/`Last4`).
- **`IPasswordHasher` para tirar o BCrypt de `Application`**: `AuthService` chamava
  `BCrypt.Net.BCrypt.Verify(...)` diretamente, o que era o único motivo dele ficar em
  `Infrastructure` em vez de `Application` (ao contrário de `CardService`, que já só
  dependia de interfaces). Com `IPasswordHasher` (interface em `Application`,
  implementado por `BCryptPasswordHasher` em `Infrastructure`), `AuthService` passou a
  depender só de `IUserRepository`/`IPasswordHasher`/`ITokenService` — todas interfaces —
  e pôde se mudar para `Application/Services`, ficando no mesmo padrão do `CardService`.

---

## 8. Testando pelo Swagger

1. Rode a API (`dotnet run`) e abra `http://localhost:5080/swagger`.
2. Chame `POST /api/auth/login` com um dos usuários da seção 3 e copie `accessToken`.
3. Clique em **Authorize** (cadeado no topo do Swagger) e cole apenas o token (sem
   o prefixo `Bearer`, o próprio Swagger adiciona).
4. Use normalmente os endpoints de `/api/cards`.

---

## 9. Possíveis evoluções (fora do escopo mínimo da prova)

- Rate limiting dedicado no endpoint de PIN.
- Testes unitários adicionais (`TokenService`/`AuthService`/`SessionValidator`) e testes
  de integração com `WebApplicationFactory` + Testcontainers para o Postgres — hoje `CardService`
  tem 11 testes unitários (`tests/CardsApi.Application.Tests`), as demais services não.
- Paginação por cursor (`keyset pagination`) para volumes muito maiores de dados.
