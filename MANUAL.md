# RankingDigi — Manual do Usuário

Bem-vindo(a) ao **RankingDigi**: o sistema para acompanhar o ranking de jogadores, registrar partidas e organizar torneios em formato de dupla eliminação.

Este manual foi escrito para o **usuário final**. Não é necessário conhecimento técnico para utilizar a aplicação.

---

## Sumário

1. [Visão geral](#1-visão-geral)
2. [Como abrir a aplicação](#2-como-abrir-a-aplicação)
3. [Conhecendo a tela](#3-conhecendo-a-tela)
4. [Página Ranking](#4-página-ranking)
5. [Página Torneios](#5-página-torneios)
6. [Página Partidas](#6-página-partidas)
7. [Página Dashboard](#7-página-dashboard)
8. [Perfil do jogador](#8-perfil-do-jogador)
9. [Convidando participantes por link](#9-convidando-participantes-por-link)
10. [Exportar dados (CSV)](#10-exportar-dados-csv)
11. [Como a pontuação funciona](#11-como-a-pontuação-funciona)
12. [Perguntas frequentes](#12-perguntas-frequentes)

---

## 1. Visão geral

O RankingDigi tem **quatro áreas principais**:

| Área | Para que serve |
|---|---|
| **Ranking** | Cadastra jogadores e mostra a classificação geral por pontuação. |
| **Torneios** | Cria torneios, define participantes e gera o chaveamento. |
| **Partidas** | Registra cada partida avulsa (fora de torneio) e mantém o histórico. |
| **Dashboard** | Gráficos com estatísticas de jogadores, decks e atividade. |

Você navega entre elas pelo menu fixo no topo da tela.

---

## 2. Como abrir a aplicação

1. Abra um navegador (Chrome, Edge, Firefox).
2. Acesse o endereço da aplicação:
   - Localmente: **http://localhost:5297/Index.html**

A primeira tela que abre é o **Ranking**.

> 💡 **Dica:** salve a página como favorito para acessar rapidamente.

---

## 3. Conhecendo a tela

No topo de toda a aplicação existe um menu fixo com:

- 🏆 **RankingDigi** — logo: clica para voltar à página inicial.
- 📊 **Ranking** — vai para a classificação.
- 🏆 **Torneios** — vai para a lista de torneios.
- 🎮 **Partidas** — vai para o registro/histórico de partidas.
- 📈 **Dashboard** — vai para os gráficos.

O item da página atual fica destacado.

Ao executar ações, **mensagens em toast** (no canto superior direito) confirmam o sucesso ou o erro. Para ações importantes (excluir, gerar chaveamento) aparece uma **caixa de confirmação** no centro da tela — você pode cancelar antes de confirmar.

---

## 4. Página Ranking

A tela inicial. É onde você gerencia os jogadores e vê a classificação.

### O que aparece

- **3 cartões no topo:**
  - **Jogadores** — total cadastrado.
  - **Líder atual** — quem está em 1º lugar.
  - **Pontuação total** — soma de todas as pontuações.

- **Formulário "Adicionar jogador"** — digite o nome e clique em "Adicionar".

- **Tabela "Classificação geral"** — lista todos os jogadores ordenados por pontuação. O 1º, 2º e 3º lugares ganham medalhas douradas/prateadas/bronze.

### Ações disponíveis

#### Adicionar um jogador

1. Digite o **nome do jogador** no campo "Nome do jogador".
2. Clique em **"Adicionar"** (ou tecle Enter).
3. Se o nome já existir, o sistema avisa.

#### Editar nome de um jogador

1. Na linha do jogador, clique no botão com o ícone de **lápis** ✏️.
2. Digite o novo nome na janela que abre.
3. Clique em **"Salvar"**.

> 💡 Útil para corrigir digitações sem perder o histórico de partidas.

#### Excluir um jogador

1. Clique no botão com o ícone de **lixeira** 🗑️ na linha desejada.
2. Confirme na caixa que aparece.

> ⚠️ Só é possível excluir jogadores que **ainda não participaram de partidas**. Se já jogou, o sistema bloqueia para preservar o histórico.

#### Buscar e ordenar

- No canto superior direito da tabela há um **campo de busca**. Digite parte do nome para filtrar rapidamente.
- **Clique no cabeçalho da coluna** "Jogador" ou "Pontuação" para alternar a ordenação (crescente/decrescente). A seta indica o sentido atual.

#### Exportar ranking

- Clique em **"Exportar CSV"** para baixar a classificação completa em um arquivo `.csv`. O arquivo abre direto no Excel/Google Sheets.

---

## 5. Página Torneios

Para organizar competições com chaveamento (dupla eliminação).

### Lista de torneios

A tabela mostra todos os torneios com:

- **#** — identificador.
- **Nome** — nome do torneio.
- **Início** — data de início.
- **Status** — uma das três etapas:
  - 🟡 **Preparação** — ainda não tem chaveamento gerado.
  - 🟢 **Em andamento** — chaveamento gerado, partidas acontecendo.
  - ⚪ **Finalizado** — todas as partidas concluídas.
- **Ações** — Configurar, Bracket, Excluir.

### Criando um novo torneio

1. Clique em **"Criar novo torneio"**.
2. Preencha:
   - **Nome do torneio** — ex.: "Copa de Verão".
   - **Data de início**.
3. Em **Participantes e decks**:
   - Clique em **"Adicionar jogador"** para incluir mais linhas.
   - Em cada linha, escolha um **jogador** e informe o **deck** que ele vai usar.
   - O contador no topo do bloco mostra quantos jogadores você tem e fica verde quando a quantidade é válida.
4. Clique em **"Criar torneio"**.

> ⚠️ **Regras importantes:**
> - O número de participantes precisa ser **potência de 2** (2, 4, 8, 16, 32…).
> - **Cada jogador só pode aparecer uma vez** no mesmo torneio. Jogadores já selecionados são automaticamente desabilitados nas outras linhas.
> - Cada jogador precisa ter um **deck** informado.

### Configurando e gerando o chaveamento

Após criar o torneio você é redirecionado para a tela de configuração:

1. Confira a lista de participantes.
2. Clique em **"Gerar chaveamento"**.
3. Confirme na caixa que aparece.

O chaveamento é criado em formato de **dupla eliminação** (chave superior + chave inferior + grande final).

### Acompanhando o torneio (Bracket)

Na tela do bracket você vê:

- **Chave Superior** (azul): rodadas iniciais. Quem perde, vai para a chave inferior.
- **Chave Inferior** (verde-água): segunda chance. Quem perde aqui é eliminado.
- **Grande Final** (destacada): vencedor da superior contra o sobrevivente da inferior.

#### Registrar resultado de uma partida

1. Clique em **"Registrar resultado"** dentro da partida em questão.
2. No modal, escolha o **vencedor** na lista.
3. Clique em **"Salvar"**.

O vencedor avança automaticamente para a próxima partida; o perdedor cai para a chave inferior (ou é eliminado, se já estiver nela).

### Excluir um torneio

Na lista de torneios, clique no ícone de **lixeira** 🗑️. Confirme. O torneio e todas as suas partidas/participantes são removidos.

---

## 6. Página Partidas

Para registrar partidas **avulsas** (fora de torneio) e consultar o histórico geral.

### Registrar uma nova partida

1. Em **"Nova partida"**:
   - Escolha o **Jogador 1** e o **Jogador 2**.
   - Informe o **Deck do Jogador 1** e o **Deck do Jogador 2**.
2. Clique em um dos botões:
   - 🏆 **Vitória do Jogador 1** (verde)
   - 🏆 **Vitória do Jogador 2** (azul)
   - 🤝 **Empate** (laranja)

> 💡 **Sugestão automática de deck:** quando você escolhe um jogador, o sistema sugere o **deck usado por ele na última partida** (aparece como placeholder do campo). Se quiser usar a sugestão, basta deixar o campo vazio e clicar em registrar — ela é aplicada automaticamente.

A pontuação é atualizada na hora.

### Filtrar histórico

Em **"Filtrar histórico"**, você pode combinar:

- **Jogador** — só partidas envolvendo aquele jogador.
- **Data início / Data fim** — recorte por período.
- **Deck** — partidas em que um dos jogadores usou aquele deck (busca parcial; ex.: "Aggro" encontra "Aggro Burn").

Clique em **"Aplicar"** para filtrar e em **"Limpar"** para voltar à lista completa.

### Exportar histórico

Clique em **"Exportar CSV"** no canto superior direito da tabela. O CSV respeita os filtros aplicados.

---

## 7. Página Dashboard

Visão geral da atividade do sistema, com gráficos interativos.

### Período

No canto superior direito há um seletor de período (**7 / 30 / 90 / 365 dias**). Ele afeta o gráfico de partidas por dia. Os demais gráficos usam todo o histórico.

### Cartões resumo

- **Jogadores** — total cadastrado.
- **Partidas** — total registrado.
- **Torneios** — total criado.
- **Empates** — total de empates já registrados.

### Gráficos

| Gráfico | O que mostra |
|---|---|
| **Top 5 jogadores por pontuação** | Os cinco melhores colocados (barras verticais). |
| **Decks com mais vitórias** | Top 8 decks que mais venceram partidas (barras horizontais). |
| **Partidas por dia** | Evolução do número de partidas registradas dia a dia (linha). |
| **Resultados (decididas vs empates)** | Proporção entre vitórias e empates (rosquinha). |
| **Vitórias por jogador** | Total de vitórias por jogador + percentual de aproveitamento (barras + linha). |

Passe o mouse sobre as barras/pontos para ver os valores exatos.

---

## 8. Perfil do jogador

Cada jogador tem uma **página de perfil pessoal** com toda a sua trajetória no sistema.

### Como abrir

- No **Ranking**, clique no nome (ou no avatar) do jogador.
- Na página **Partidas**, clique no nome do jogador em qualquer linha.
- Na lista de participantes de um torneio, clique no nome.
- Endereço direto: `http://localhost:5297/player.html?id=XX`.

### O que o perfil mostra

**Cabeçalho (hero):**
- Avatar grande com as iniciais.
- Nome, ID e pontuação total.
- Posição atual no ranking geral.
- Quantidade de títulos (campeonatos vencidos).

**4 cartões de estatística:**
- **Partidas jogadas** — total geral.
- **Vitórias** — quantas vezes venceu.
- **Derrotas** — quantas perdeu.
- **Aproveitamento** — % de vitórias.

**Gráficos:**
- **Evolução da pontuação** (linha) — como a pontuação cresceu ao longo do tempo, recalculada a partir de cada partida.
- **Resultados** (rosquinha) — proporção entre vitórias, derrotas e empates.
- **Decks utilizados** (barras horizontais) — comparação de usos × vitórias para os decks mais frequentes.

**Listas:**
- **Decks utilizados** — todos os decks que o jogador já usou, com número de usos, vitórias e taxa de vitória (badge verde / amarelo / vermelho).
- **Últimas partidas** — feed com as 10 últimas, mostrando adversário (clicável → perfil do oponente), decks dos dois lados e o resultado (Vitória / Derrota / Empate).
- **Torneios disputados** — cards com todos os torneios em que o jogador participou. Cards de torneios em que ele foi **campeão** ficam com destaque dourado e o badge "1º lugar".

> 💡 **No futuro:** quando o sistema tiver login, esta será a sua tela pessoal. Você poderá editar suas informações e os demais poderão acessar o seu perfil por aqui mesmo. A estrutura já está pronta.

---

## 9. Convidando participantes por link

Você não precisa cadastrar os participantes manualmente — pode **enviar um link** e deixar que cada um se inscreva sozinho.

### Como o organizador convida

1. Vá em **Torneios → Criar novo torneio**.
2. Preencha **nome** e **data**, e deixe a seção de participantes **vazia** (ou adicione só alguns — você pode misturar inscrição manual com inscrição por link).
3. Clique em **"Criar torneio"**.
4. Você é levado para a tela de **Configurar torneio**, que mostra no topo:
   - **URL pública** — o link completo do convite.
   - Botões:
     - 📋 **Copiar** — copia para a área de transferência.
     - ↗ **Abrir página de inscrição** — abre o link em uma nova aba (útil para conferir).
     - 🔄 **Gerar novo código** — invalida o link atual e gera um novo. Útil se o link foi compartilhado por engano.

> 💡 Você também pode copiar o link **diretamente da lista de torneios**, pelo botão "Convite" na coluna de ações (aparece apenas para torneios ainda em preparação).

5. Compartilhe o link por **WhatsApp, e-mail ou chat** com os participantes.

### Como cada convidado se inscreve

1. O participante clica no link.
2. Abre uma página com:
   - Banner com o **nome do torneio**, **data**, e quantas pessoas já se inscreveram.
   - Lista dos primeiros nomes já inscritos (chips).
   - Formulário pedindo **seu nome** e o **deck**.
3. Ele preenche e clica em **"Confirmar inscrição"**.
4. Se o nome dele **já existe no sistema** (correspondência por nome, ignorando maiúsculas/minúsculas), ele é associado ao jogador existente — preservando o histórico no ranking.
5. Se o nome **não existe**, um novo jogador é criado automaticamente com pontuação 0.
6. Aparece a confirmação "Inscrição confirmada!".

> ⚠️ A mesma pessoa **não pode se inscrever duas vezes** no mesmo torneio. O sistema bloqueia com mensagem clara.

### Acompanhando as inscrições

Na tela de **Configurar torneio**, a seção "Participantes inscritos" é atualizada em tempo real (botão 🔄 para recarregar). Cada card mostra avatar, nome (clicável → vai para o perfil) e deck. Você pode **remover** um participante com o ✕ no canto do card enquanto o torneio ainda estiver em preparação.

### Iniciando o torneio

Quando o número de inscritos chegar a uma **potência de 2** (a pílula do topo fica verde):

1. Clique em **"Iniciar torneio"**.
2. Confirme.
3. O chaveamento é gerado e você é levado para o bracket.

> ⚠️ Após iniciado, **novos participantes não conseguem mais ingressar pelo link** — a página de convite passa a mostrar "Inscrições encerradas".

### Quando o login estiver disponível

Hoje a inscrição pede apenas nome + deck (sem senha). Quando o sistema ganhar login:
- Quem já tiver conta logada entra com 1 clique no botão "Ingressar".
- Quem não tiver conta verá um botão "Criar conta" antes do "Ingressar".
- O nome será garantido pelo login, eliminando colisões.

A página de convite já é a mesma — ela só vai ganhar esses dois caminhos extras.

---

## 10. Exportar dados (CSV)

Você pode baixar os dados para abrir em Excel, Google Sheets ou Numbers:

- **Ranking** → botão **"Exportar CSV"** na página Ranking. Gera o arquivo `ranking-AAAA-MM-DD.csv`.
- **Histórico de partidas** → botão **"Exportar CSV"** na página Partidas. Gera `partidas-AAAA-MM-DD.csv` com os filtros aplicados.

> 💡 O arquivo já vem com codificação correta para acentos. É só dar duplo clique para abrir no Excel.

---

## 11. Como a pontuação funciona

Cada partida atualiza a pontuação dos jogadores assim:

| Resultado | Vencedor ganha | Perdedor ganha | Empate (cada um) |
|---|---|---|---|
| Vitória decisiva | **+3 pts** | 0 pts | — |
| Empate | — | — | **+1 pt** |

O ranking é sempre por **pontuação acumulada** (decrescente). Tanto partidas avulsas quanto partidas de torneio contam.

---

## 12. Perguntas frequentes

**Posso usar a aplicação em um celular?**
Sim — o layout é responsivo. Em telas pequenas as tabelas ganham rolagem horizontal.

**Como faço para corrigir uma partida registrada errada?**
Hoje não há edição de partida individual. A solução é registrar uma nova partida que compense, ou pedir ajuda ao administrador para ajustar no banco.

**O que é "dupla eliminação"?**
É um formato em que o jogador precisa perder **duas** partidas para ser eliminado. Quem perde na chave superior cai para a chave inferior e pode voltar à grande final.

**Por que aparece "Já existe um jogador com esse nome"?**
O sistema impede dois jogadores com o mesmo nome para evitar confusão no ranking. Use um sobrenome ou apelido para diferenciar.

**Por que não consigo gerar o chaveamento?**
A quantidade de participantes precisa ser **potência de 2** (2, 4, 8, 16…). Se você tem 6 participantes, por exemplo, adicione mais 2 ou remova 2 para chegar a 8 ou 4.

**Por que não consigo excluir um jogador?**
Jogadores que já participaram de pelo menos uma partida ficam protegidos contra exclusão para preservar o histórico. Se realmente precisa, primeiro precisaria apagar as partidas dele (ação técnica que requer acesso ao banco).

**Os dados ficam salvos?**
Sim, ficam gravados em um banco de dados SQL Server. Mesmo fechando o navegador ou o servidor, os dados continuam lá quando você voltar.

**O endereço pode ser acessado por outra pessoa na rede?**
Por padrão a aplicação roda só na sua máquina (`localhost`). Para liberar acesso em rede, é necessário configurar o servidor (peça ajuda a quem instalou).

**O link de convite expira?**
Não tem prazo, mas só funciona enquanto o torneio estiver em **Preparação**. Assim que o organizador clica em "Iniciar torneio", o link mostra "Inscrições encerradas". Se quiser invalidar o link antes disso (por engano de envio, por exemplo), use **"Gerar novo código"** na tela de configuração.

**O convidado precisa de login para usar o link?**
Não. Hoje basta preencher nome e deck. Quando o sistema ganhar login, o link ganhará botões de "Login / Criar conta" automaticamente.

**Se duas pessoas com o mesmo nome se inscreverem pelo link, o que acontece?**
A segunda pessoa será **associada ao mesmo jogador** (porque o sistema procura por nome). Por isso, na falta de login, oriente os participantes a usarem nomes únicos (com sobrenome, apelido, etc.). Isso vai parar de ser um problema quando o login chegar.

**Como vejo qual deck eu mais ganho?**
Abra seu **perfil** (clique no seu nome no ranking). Na seção "Decks utilizados" cada deck mostra: número de usos, vitórias e percentual de aproveitamento. O ordenamento já põe os mais usados em primeiro, e o badge colorido mostra o desempenho.

---

### Em caso de problema

Se algo não funcionar (uma página em branco, um botão sem reação):

1. Recarregue a página (`F5`).
2. Confira se o servidor está rodando (deve haver um terminal aberto com `dotnet run`).
3. Tente acessar `http://localhost:5297/swagger` — se essa página abrir, o servidor está OK.

Bom uso! 🏆
