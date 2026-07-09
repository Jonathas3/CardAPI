# Cards API — Prova Técnica Dev .NET Pleno

API REST em **.NET 8** para gestão de cartões de crédito de um usuário autenticado,
usando **PostgreSQL** + **Entity Framework Core**, com **Swagger** habilitado.

> Nenhum arquivo SQL foi anexado à prova recebida (apenas o PDF de enunciado). O
> schema foi desenhado do zero e é aplicado via **EF Core Migrations**
> (`src/Cards.Infrastructure/Migrations/`), conforme permitido pelo próprio enunciado
> ("documente a decisão no README e entregue os scripts/migrations correspondentes").
> A migration aplica schema **e** seed juntos, automaticamente, na subida da API.

---

## 1. Stack e arquitetura

- C# / .NET 8 (ASP.NET Core Web API)
- PostgreSQL 16 + Entity Framework Core (Npgsql)
- JWT com sessão controlada no banco (suporta revogação e rotação real, não só expiração do token)
- Swagger / OpenAPI (Swashbuckle)
- BCrypt.Net-Next para hash da senha de login
- AES-256-GCM (nonce aleatório + tag de autenticação por valor) para número do cartão e PIN

Arquitetura em camadas **físicas** — cada camada é um projeto `.csproj` próprio, com a
regra de dependência clássica (camadas internas nunca referenciam as externas):

```
CardsApi.sln
src/
  Cards.Domain/             -> Entities: Card, User, Session, PinAccessLog, CardStatus
  Cards.Application/        -> Interfaces/{Repositories,Services}, Services (CardService,
                                AuthService), Dtos, Common/ApiException, DependencyInjection.cs
  Cards.Infrastructure/     -> Data/AppDbContext + Configurations/, Migrations/, Repositories/
                                (GenericRepository<T> base + CardRepository, SessionRepository,
                                UserRepository), Services/ (TokenService, CryptoService,
                                SessionValidator, BCryptPasswordHasher), DependencyInjection.cs
  Cards.Api/                -> Controllers, Middleware, Configuration/AuthenticationExtensions.cs,
                                Program.cs (composition root, projeto executável)
```

`CardService`/`AuthService` (Application) dependem só de interfaces (`ICardRepository`,
`IUserRepository`, `IPasswordHasher`, `ITokenService`) — nunca de EF Core diretamente.
Quem implementa com `AppDbContext` é a Infrastructure. Isso é o que torna a separação
física útil de verdade: sem esse desacoplamento, os projetos separados não teriam efeito
prático além de mais `.csproj` para manter. `Program.cs` é um composition root enxuto:
cada projeto expõe seu próprio método de extensão de DI (`AddApplication()`,
`AddInfrastructure()`, `AddApiAuthentication()`), chamado uma vez no startup. O único
lugar que ainda toca o `AppDbContext` concreto é o `Database.Migrate()` do startup.

---

## 2. Como rodar localmente

```bash
# 1) sobe o Postgres (container vazio - o schema/seed vêm da migration, não de um script)
docker compose up -d

# 2) restaura pacotes e roda a API (projeto executável é o Cards.Api)
cd src/Cards.Api
dotnet restore
dotnet run
```

Na subida, a API chama `Database.Migrate()` automaticamente (`Program.cs`), criando o
schema completo (tabelas, índices, `CHECK` constraints, foreign keys) e inserindo o seed
de usuários/cartões — tudo em uma única migration (`Migrations/InitialCreate`). Não é
preciso rodar `dotnet ef database update` nem `psql` manualmente.

A API sobe em `http://localhost:5080` e o Swagger fica em `http://localhost:5080/swagger`.

Para gerar/aplicar migrations manualmente (`dotnet tool install --global dotnet-ef`),
como o `DbContext` vive em `Cards.Infrastructure` mas o composition root vive em
`Cards.Api`, é preciso apontar os dois projetos:

```bash
dotnet ef database update --project src/Cards.Infrastructure --startup-project src/Cards.Api
dotnet ef migrations add NomeDaMigration --project src/Cards.Infrastructure --startup-project src/Cards.Api
```

### Testes automatizados

`tests/Cards.Application.Tests` cobre as regras centrais da prova sem depender do
Postgres: paginação fixa em 10 itens, rejeição de intervalo de vencimento inválido,
isolamento por usuário, mascaramento/criptografia na criação, PUT/PATCH/DELETE
(incluindo rejeição de `status`/`cardNumber` inválidos) e auditoria do endpoint de PIN.

```bash
dotnet test CardsApi.sln
```

### Connection string / segredos

`appsettings.json` já vem com uma connection string e chaves JWT/AES de demonstração,
funcionais para rodar localmente. **Em qualquer ambiente real**, mova `Jwt:SigningKey` e
`Crypto:AesKeyBase64` para variáveis de ambiente ou `dotnet user-secrets`.

---

## 3. Usuários de teste (seed)

| username        | password    | observação                              |
|-----------------|-------------|------------------------------------------|
| `mariana.alves` | `Senha@123` | possui **12 cartões** (>10, para validar paginação) |
| `carlos.silva`  | `Senha@123` | possui 3 cartões (para validar isolamento entre usuários) |

Não é necessário endpoint de cadastro — conforme o enunciado, os usuários já vêm prontos
no seed, com senha em hash (BCrypt) no banco.

---

## 4. Autenticação e rotação de token

- `POST /api/auth/login` — recebe `username`/`password` e retorna um **JWT** válido por
  **30 minutos** (`Jwt:AccessTokenMinutes`).
- Toda rota de `/api/cards/**` exige `Authorization: Bearer <token>`.
- O JWT carrega `sub` (id do usuário) e `jti` (id da sessão). Cada login cria uma linha em
  `sessions` com `expires_at = now + 30min`. Um `OnTokenValidated` (em
  `Configuration/AuthenticationExtensions.cs`) confere via `ISessionValidator` se a sessão
  ainda está ativa — isso permite **revogar** um token antes do prazo, algo que um JWT
  puro (stateless) não permite sozinho.
- `POST /api/auth/refresh` (autenticado) — **rotaciona** o token: marca a sessão atual como
  revogada e cria uma nova com validade de 30 minutos. A sessão antiga nunca mais autentica.
- Token expirado é sempre rejeitado com `401`; nesse caso o cliente deve chamar
  `/api/auth/login` novamente.
- Nenhuma senha, PIN, número de cartão ou token é escrito em log.

```bash
curl -X POST http://localhost:5080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"mariana.alves","password":"Senha@123"}'
# -> { "accessToken": "...", "tokenType": "Bearer", "expiresAtUtc": "..." }

curl http://localhost:5080/api/cards -H "Authorization: Bearer <accessToken>"
curl -X POST http://localhost:5080/api/auth/refresh -H "Authorization: Bearer <accessToken>"
```

---

## 5. Endpoints de cartões

Todos exigem `Authorization: Bearer <token>` e operam **apenas** sobre cartões do usuário
autenticado (acesso a cartão de outro usuário retorna `404`, não `403`, para não revelar
se o id pertence a outra pessoa).

### `GET /api/cards`
Lista os cartões do usuário, do mais recente para o mais antigo (`createdAt DESC`), em
**blocos fixos de 10 itens**.

Query params: `page` (padrão `1`), `expirationFrom`/`expirationTo` (`yyyy-MM-dd`, filtro
de vencimento aplicado antes de materializar os dados).

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
Retorna um cartão específico (mesmo formato do item acima).

### `POST /api/cards`
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
`status` é opcional (padrão `ACTIVE`; aceita `ACTIVE`/`BLOCKED`/`CANCELLED`).
`creditLimit >= 0`; `cardNumber` com 13–19 dígitos; `pin` com exatamente 4 dígitos.
`cardNumber` e `pin` nunca voltam em texto puro — a resposta traz `maskedNumber`.

### `PUT /api/cards/{id}` — atualização completa
Substitui **todos** os campos editáveis; todos são obrigatórios no corpo. Campo ausente
= `400`. Não há "manter valor antigo" no PUT.

### `PATCH /api/cards/{id}` — atualização parcial
DTO próprio (todos os campos opcionais); só os campos enviados são alterados.
```json
{ "nickname": "Uso diário", "creditLimit": 15500.00 }
```

Campos editáveis (PUT e PATCH): `cardholderName`, `nickname`, `brand`, `cardNumber`,
`expirationDate`, `creditLimit`, `status`, `pin`. Não editáveis: `id`, `userId`, `createdAt`.

### `DELETE /api/cards/{id}`
**Soft delete**: marca `isDeleted = true` e some das consultas comuns, mas o registro
(e seu histórico de `pin_access_logs`) é preservado para rastreabilidade.
```json
{ "id": "uuid", "deleted": true, "message": "Card removed successfully." }
```

### `GET /api/cards/{id}/pin` — consulta exclusiva da senha
Endpoint **separado** das consultas comuns, com o mesmo controle de posse do cartão. O
PIN é armazenado criptografado (AES-256-GCM, nunca em texto puro) e só é descriptografado
aqui. Cada acesso é registrado em `pin_access_logs` (quem, qual cartão, quando, IP) —
nunca o valor do PIN.

---

## 6. Regras de negócio e validações implementadas

1. Toda operação é restrita ao usuário autenticado (`userId` extraído do JWT).
2. Token com validade de 30 minutos + rotação (seção 4).
3. Listagem em blocos fixos de 10 itens (`mariana.alves` tem 12 cartões no seed).
4. Ordenação sempre por `createdAt DESC`.
5. Filtro por período de vencimento, aplicado antes de paginar/materializar.
6. `status` restrito a `ACTIVE`/`BLOCKED`/`CANCELLED` (validado na aplicação + `CHECK` no banco).
7. Número do cartão nunca retornado por inteiro em consultas comuns — só `maskedNumber`.
8. `creditLimit` nunca negativo (`[Range]` na aplicação + `CHECK` no banco).
9. PIN criptografado em repouso, nunca logado, nunca retornado fora do endpoint exclusivo.
10. Consulta do PIN só existe em endpoint próprio, com autorização por dono + auditoria.
11. Erros padronizados (`ErrorResponse`: `errorCode`, `message`, `errors?`, `traceId`)
    com status HTTP coerente (`400`, `401`, `404`, `500`).

---

## 7. Decisões técnicas

- **EF Core Migrations em vez de script SQL avulso**: o enunciado aceita os dois como
  entregável equivalente. A seed foi incluída na própria migration
  (`migrationBuilder.Sql(...)`), e as `CHECK` constraints de `status`/`credit_limit` são
  declaradas em `AppDbContext` para não perder essas garantias a nível de banco.
- **JWT + sessão em banco (híbrido)**: um JWT puro não permite revogação antes da
  expiração. A validade real de cada token é controlada pela tabela `sessions` (o JWT
  carrega só o `jti`), tornando a rotação determinística e auditável sem cache externo.
- **Criptografia reversível (AES-256-GCM), não hash, para número/PIN**: ambos precisam
  ser recuperados (o PIN literalmente volta no endpoint exclusivo), o que exclui hash.
  Nonce aleatório + tag de autenticação por valor evitam que PINs iguais gerem o mesmo
  ciphertext e detectam adulteração do dado em repouso.
- **`cardNumberFirst4`/`cardNumberLast4` em colunas separadas**: monta a máscara sem
  nunca precisar descriptografar o número em consultas comuns.
- **Soft delete em `cards`**: preserva rastreabilidade (histórico de acesso ao PIN)
  mesmo após remoção, via `HasQueryFilter(!IsDeleted)`.
- **404 (não 403) para cartão de outro usuário**: evita revelar que um id pertence a
  outra pessoa.
- **`cardNumber` validado só por formato (13–19 dígitos), sem checksum de Luhn**: o
  próprio número de exemplo do enunciado não passa em Luhn.
- **PATCH com DTO próprio** (em vez de JSON Patch/RFC 6902): mais simples de documentar
  e testar pelo Swagger, com o mesmo efeito de atualização parcial.
- **Camadas em projetos físicos separados + Repository**: o enunciado só pede "camadas
  ou abordagem equivalente" (pastas já bastariam). Optamos por separar fisicamente para
  que a regra de dependência fosse imposta pelo compilador, não só por convenção —
  o que só tem efeito real com `ICardRepository`/`ISessionRepository`/`IUserRepository`
  desacoplando `CardService`/`AuthService` do EF Core. `CardRepository`/`SessionRepository`/
  `UserRepository` herdam de `GenericRepository<TEntity>`, que centraliza `AddAsync`/
  `SaveChangesAsync` (únicos membros repetidos entre eles).

---

## 8. Testando pelo Swagger

1. Rode a API (`dotnet run`) e abra `http://localhost:5080/swagger`.
2. Chame `POST /api/auth/login` com um dos usuários da seção 3 e copie `accessToken`.
3. Clique em **Authorize** e cole o token (sem o prefixo `Bearer`).
4. Use normalmente os endpoints de `/api/cards`.

---

## 9. Possíveis evoluções (fora do escopo mínimo da prova)

- Rate limiting dedicado no endpoint de PIN.
- Testes de integração com `WebApplicationFactory` + Testcontainers para o Postgres.
- Paginação por cursor (`keyset pagination`) para volumes muito maiores de dados.
