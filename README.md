# RankingDigi

Sistema web de **ranking e torneios** para jogadores de card game / competições. Permite cadastrar jogadores, registrar partidas avulsas, organizar torneios em múltiplos formatos (Dupla Eliminação, Swiss + Top Cut, Swiss Pontos Corridos) e acompanhar tudo por um ranking geral e um dashboard com gráficos.

Projeto pessoal em desenvolvimento contínuo — este README reflete o estado atual das funcionalidades já implementadas.

---

## Stack

- **Backend:** ASP.NET Core (.NET) + Entity Framework Core
- **Banco de dados:** SQL Server (LocalDB em desenvolvimento)
- **Autenticação:** JWT + ASP.NET Identity (papéis Admin / Player)
- **E-mail:** MailKit via Gmail SMTP (confirmação de cadastro e recuperação de senha)
- **Frontend:** HTML + CSS + JavaScript puro (sem framework), com Bootstrap 5 e SweetAlert2
- **Gráficos:** Chart.js
- **Documentação da API:** Swagger/OpenAPI (com suporte a Bearer token)

---

## Funcionalidades

### Autenticação e usuários
- Login com usuário/senha e emissão de token JWT (expira em 12h)
- Autorregistro público, com criação opcional de jogador vinculado à conta
- Confirmação de e-mail por link (token válido por 24h)
- Recuperação e redefinição de senha por e-mail (token válido por 2h), sem revelar se o e-mail existe na base
- Troca da própria senha (exige senha atual) e troca de senha de terceiros pelo Admin
- Gestão de usuários pelo Admin: criar, listar, trocar senha e excluir (tela `users.html`)
- Dois papéis de acesso: **Admin** (controle total) e **Player** (leitura + perfil próprio)

### Jogadores e ranking
- Cadastro de jogadores com bloqueio de nomes duplicados
- Edição de nome (Admin ou o próprio jogador vinculado)
- Upload de avatar (JPG/PNG/WEBP/GIF, até 2MB)
- Exclusão de jogador bloqueada caso ele já tenha partidas registradas, para preservar o histórico
- Ranking geral ordenável por pontuação ou nome, com busca por nome e exportação para CSV
- Perfil público por jogador: posição no ranking, evolução histórica da pontuação, vitórias/derrotas/empates e taxa de vitória, desempenho por deck, últimas partidas e histórico de torneios (com títulos conquistados em destaque)

### Partidas avulsas
- Registro de partidas fora de torneio, com atualização automática da pontuação (vitória = 3 pts, empate = 1 pt para cada)
- Sugestão automática do último deck usado por cada jogador
- Histórico filtrável por jogador, período e nome de deck, com exportação para CSV

### Torneios
- Criação de torneios com nome, data, número máximo de jogadores e escolha de formato
- Três formatos suportados:
  - **Dupla Eliminação** — chave superior, chave inferior e grande final, com avanço automático de vencedores/perdedores
  - **Swiss + Top Cut** — rodadas suíças seguidas de mata-mata (Top 4 ou Top 8)
  - **Swiss Pontos Corridos** — todas as rodadas em sistema suíço, sem mata-mata
- Cálculo automático do número de rodadas conforme a quantidade de jogadores (formatos Swiss)
- Classificação (standings) com desempate por porcentagem de vitórias dos oponentes (OMW%)
- Registro de resultado de cada partida do chaveamento, com bloqueio de resultado duplicado e encerramento automático do torneio ao concluir a etapa final
- Exclusão de torneio com remoção em cascata de participantes e partidas

### Convite e inscrição por link
- Geração de código de convite único por torneio, com opção de regenerar (invalidando o link anterior)
- Página pública de inscrição: participante entra com nome + deck, sem precisar de conta
- Associação automática ao jogador existente em caso de nome já cadastrado
- Bloqueio de inscrição duplicada e de inscrição após o torneio já ter iniciado

### Dashboard
- Cartões-resumo: total de jogadores, partidas, torneios e empates
- Gráficos: Top 5 jogadores por pontuação, decks com mais vitórias, partidas por dia (período configurável de 7 a 365 dias), proporção de resultados decididos vs. empates, e vitórias por jogador com percentual de aproveitamento

### Interface
- Tema **claro e escuro**, com alternância manual (botão na navbar ou flutuante nas telas sem navbar), respeito à preferência do sistema operacional e persistência da escolha entre sessões
- Layout responsivo (desktop e mobile)
- Notificações e confirmações estilizadas (SweetAlert2/toasts)
- Exportação de tabelas para CSV (ranking e histórico de partidas)

### Infraestrutura
- Migrations do EF Core aplicadas automaticamente na inicialização
- Seed automático de usuário Admin padrão
- Compressão de resposta (Brotli + Gzip)
- CORS liberado e Swagger habilitado em desenvolvimento
- Supressão do header `WWW-Authenticate` no 401 (evita popup de Basic Auth do navegador ao usar túnel ngrok)

---

## Estrutura do projeto

```
Controller/   — Controllers da API (Auth, Player, Match, Tournament, TournamentMatch, Dashboard)
Models/       — Entidades EF Core e DTOs
Services/     — Lógica de torneio (Double Elimination, Swiss, geração de convite) e envio de e-mail
View/         — Razor (mínimo)
wwwroot/      — Frontend estático (HTML + JS + CSS)
```

### Páginas do frontend

| Página | Função |
|---|---|
| `Index.html` | Ranking geral de jogadores |
| `login.html` / `register.html` | Autenticação e criação de conta |
| `forgot-password.html` / `reset-password.html` | Recuperação de senha |
| `confirm-email.html` | Confirmação de e-mail |
| `tournaments.html` | Lista de torneios |
| `create-tournament.html` | Criação de torneio |
| `tournament-setup.html` | Configuração e convite antes do início |
| `join-tournament.html` | Inscrição de participante via link |
| `tournament-bracket.html` / `tournament-double-bracket.html` | Chaveamento visual (dupla eliminação) |
| `tournament-swiss.html` | Rodadas e classificação do sistema suíço |
| `match.html` | Registro de partidas avulsas |
| `player.html` | Perfil do jogador |
| `profile.html` | Perfil da própria conta |
| `dashboard.html` | Painel com gráficos e estatísticas |
| `users.html` | Gestão de usuários (Admin) |

---

## Como rodar localmente

```bash
git clone <repo>
cd RankingDigi
dotnet run
```

A aplicação sobe em `http://localhost:5297` (ver `Properties/launchSettings.json`). O banco de dados é lido de `ConnectionStrings:DefaultConnection` e as migrations são aplicadas automaticamente no primeiro start.

**Credenciais padrão (seed):**
- Usuário: `admin`
- Senha: definida via User Secrets (ver abaixo)

### Configuração de segredos (User Secrets)

`appsettings.json` **não contém segredos** — connection string, credenciais de e-mail, chave JWT e senha do admin ficam fora do controle de versão via [User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) do .NET. Em uma máquina nova, configure com:

```bash
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<sua connection string>"
dotnet user-secrets set "Email:Username" "<email do remetente>"
dotnet user-secrets set "Email:Password" "<senha de app do Gmail>"
dotnet user-secrets set "Jwt:Key" "<chave aleatória longa>"
dotnet user-secrets set "AdminSeed:Password" "<senha do admin seed>"
```

Como os segredos ficam fora do repositório, cada máquina (trabalho/casa) precisa configurá-los uma vez. Se todas as máquinas apontarem para a **mesma** `ConnectionStrings:DefaultConnection` (ex.: um banco remoto compartilhado, como Azure SQL Database), o banco fica sempre sincronizado entre elas — sem precisar copiar `.mdf`/backup manualmente.

Para envio de e-mails (confirmação de cadastro/recuperação de senha), as credenciais SMTP também vêm do User Secrets (`Email:Username` / `Email:Password`), com host/porta/remetente configuráveis em `appsettings.json`.

---

## Manual do usuário

Para instruções passo a passo de uso (sem necessidade de conhecimento técnico), veja [MANUAL.md](MANUAL.md).
