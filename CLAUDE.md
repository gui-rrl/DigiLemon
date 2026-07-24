# RankingDigi — CLAUDE.md

## Visão Geral
Sistema de ranking e torneios para jogadores de card game / competições. Gerencia jogadores, partidas, torneios em múltiplos formatos (Double Elimination, Swiss, Swiss+TopCut) e exibe ranking geral por pontuação.

## Stack
- **Backend:** ASP.NET Core, EF Core, Azure SQL Database (SQL Server)
- **Frontend:** HTML + CSS + Vanilla JS (sem framework)
- **Auth:** JWT + ASP.NET Identity
- **Namespace:** `RankingDigi`
- **Idioma do código:** Inglês (models, controllers, services)

## Rodar o projeto
```bash
cd "C:\Users\AAPI\OneDrive\Área de Trabalho\Ranking-master\Ranking-master"
dotnet run
# URL pública via ngrok: https://outdated-fidgety-surcharge.ngrok-free.dev/
```

## Banco de dados
- **Padrão (dev local):** SQL Server local, `localhost`, database `rankingd`, autenticação Windows (Integrated Security). Essa connection string fica em `appsettings.Development.json` — **versionada no git**, pois não tem segredo nenhum (sem usuário/senha). Rodar `dotnet run` localmente **nunca** bate no Azure por padrão, evitando custo.
- **Azure SQL** (`rankingd.database.windows.net`, database `rankingd`, free offer) existe só como ponto de sincronização manual entre o notebook do serviço e o de casa: antes de trocar de máquina, exporta o banco local pra lá (`sqlpackage`); ao chegar na outra máquina, importa de lá pro banco local dela. A connection string do Azure fica em **User Secrets**, sob a chave `ConnectionStrings:AzureConnection` — usada só manualmente via `sqlpackage`/`sqlcmd`, o app em si nunca lê essa chave.
- **Credenciais de e-mail, chave JWT e senha do admin seed:** ficam em **User Secrets** (`dotnet user-secrets`), fora do git — ver seção "Configuração de segredos" no `README.md`.
- **Seed admin:** usuário `admin`, senha definida via User Secrets (`AdminSeed:Password`).

### ⚠️ Bug conhecido: app conecta no Azure ao rodar local
**Sintoma:** você roda `dotnet run` localmente mas o app conecta no Azure SQL (`rankingd.database.windows.net`) em vez do `localhost` — gera custo e mostra dados errados.

**Causa:** existe uma chave `ConnectionStrings:DefaultConnection` apontando pro Azure dentro do **User Secrets** desta máquina. User Secrets tem prioridade **maior** que o `appsettings.Development.json`, então essa chave sobrescreve a connection string local silenciosamente (não aparece em nenhum arquivo do git). O provável autor é o Connected Service "Banco de Dados do SQL Server" do Visual Studio (aparece no Solution Explorer, sem opção de "Remover"; apagar `.vs` não resolve).

**Diagnóstico:** com o app rodando, acesse `GET /api/_debug/db` — mostra `server`/`database`/`environment` do processo vivo. Se `server` for o host do Azure, é este bug.

**Correção** (rodar no terminal, uma vez por máquina afetada):
```powershell
dotnet user-secrets remove "ConnectionStrings:DefaultConnection" --project "RankingDigi.csproj"
```
Estado correto do `secrets.json`: tem `ConnectionStrings:AzureConnection` (só pra sync manual via `sqlpackage`), e **NUNCA** `ConnectionStrings:DefaultConnection`. Confira com:
```powershell
Get-Content -Raw "$env:APPDATA\Microsoft\UserSecrets\a8e36d75-4702-4c55-97d4-0eb3aef8a335\secrets.json"
```

**Nota pro Claude:** as ferramentas de arquivo do agente podem ler uma cópia **desatualizada** do `secrets.json` (o arquivo fica em `%APPDATA%`, fora da pasta do projeto/OneDrive). Não confie nas próprias leituras desse arquivo — a fonte de verdade é o terminal do usuário (`Get-Content`) ou a leitura ao vivo via `/api/_debug/db`. Correções em User Secrets têm que ser rodadas pelo usuário no terminal dele.

## Git
- Diretório: `C:\Users\AAPI\OneDrive\Área de Trabalho\Ranking-master\Ranking-master`
- Branch: `master`

## Estrutura de pastas
```
Controller/   — API controllers
Models/       — Entidades EF Core + DTOs
Services/     — Lógica de torneio (geração de chaves, Swiss, Double Elim)
View/         — Razor (mínimo)
wwwroot/      — Frontend estático (HTML + JS + CSS)
```

## Domínios principais

### Players & Ranking
- `Player`: Id, Name, Score, AvatarUrl
- Ranking exibido por Score decrescente
- Avatares em `wwwroot/avatars/`

### Tournaments
- `Tournament`: Name, StartDate, EndDate, Status (0=preparação, 1=andamento, 2=finalizado)
- `Format`: `0=DoubleElim`, `1=Swiss+TopCut`, `2=SwissPure`
- `InviteCode`: código para jogadores entrarem no torneio
- `MaxPlayers`, `SwissRounds`, `TopCutSize` (4 ou 8), `CurrentSwissRound`
- Serviços: `TournamentService.cs`, `DoubleEliminationGenerator.cs`, `SwissService.cs`

### Matches
- `Match` e `TournamentMatch` registram resultados
- `MatchResultDto` para submeter placar

## Frontend — páginas principais
| Página | Função |
|--------|--------|
| `Index.html` | Landing / ranking geral |
| `tournaments.html` | Lista de torneios |
| `create-tournament.html` | Admin cria torneio |
| `tournament.html` | Detalhe do torneio |
| `tournament-bracket.html` | Chaveamento visual |
| `tournament-double-bracket.html` | Bracket double elim |
| `tournament-swiss.html` | Rodadas Swiss |
| `tournament-setup.html` | Configuração antes de iniciar |
| `join-tournament.html` | Jogador entra com código |
| `match.html` | Registro de resultado |
| `player.html` | Perfil do jogador |
| `profile.html` | Perfil próprio |
| `dashboard.html` | Painel admin |
| `users.html` | Gestão de usuários (admin) |

## Config relevante (`appsettings.json`)
- Email: `lemondigiacc@gmail.com` (Gmail SMTP)
- JWT expira em 12h
- `BaseUrl`: `https://outdated-fidgety-surcharge.ngrok-free.app/` (ngrok, pode mudar)

## Decisões importantes
- Suprimir header `WWW-Authenticate` no 401 para não disparar popup de Basic Auth do browser (configurado no `JwtBearerEvents.OnChallenge`)
- Response compression habilitada (Brotli + Gzip)
- Swagger habilitado em desenvolvimento
