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
- **Servidor:** Azure SQL Database — `rankingd.database.windows.net`, database `rankingd` (compartilhado entre notebook do serviço e de casa, sincronizado via User Secrets em cada máquina; free offer do Azure SQL).
- **Connection string, credenciais de e-mail, chave JWT e senha do admin seed:** ficam em **User Secrets** (`dotnet user-secrets`), fora do `appsettings.json` e fora do git — ver seção "Configuração de segredos" no `README.md`.
- **Seed admin:** usuário `admin`, senha definida via User Secrets (`AdminSeed:Password`).

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
