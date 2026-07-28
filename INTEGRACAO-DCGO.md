# RankingDigi ↔ DCGO — Integração de resultados (API v1)

Documento para quem mantém o simulador. Descreve o que o DCGO precisa chamar para que uma
partida jogada no simulador conte automaticamente no torneio do RankingDigi.

---

## Como funciona, em uma frase

Cada partida de torneio online tem **dois códigos, um por jogador**. Cada um cola o seu no DCGO;
quando **os dois reportam e os relatos batem**, o resultado é aplicado no torneio. Se divergirem,
a partida entra em conflito e um organizador resolve na mão.

Por que assim: o DCGO não tem contas, só um apelido digitado. O código resolve isso sozinho —
ele identifica **a partida e qual dos dois lados** está reportando. O simulador não precisa saber
nada sobre identidades do RankingDigi.

E por que dois relatos: assim um código vazado não muda resultado nenhum. No pior caso gera um
conflito, que aparece para o organizador em vez de corromper a classificação em silêncio.

---

## Requisitos de todo request

| | |
|---|---|
| **Base URL** | `https://<host>/api/integration` |
| **Header obrigatório** | `X-Integration-Key: <chave fornecida pelo organizador>` |
| **Content-Type** | `application/json` (nos POSTs) |
| **Transporte** | HTTPS |

Nunca exponha a chave na interface nem em logs do cliente. Não mande a chave por query string.

---

## 1) `GET /health`

Chame no boot para validar a chave e falhar cedo com mensagem clara.

```json
{ "ok": true, "server": "RankingDigi", "apiVersion": 1, "utc": "2026-07-28T13:00:00Z" }
```

---

## 2) `GET /match/{code}`

Identifica a partida e devolve o estado atual. Use para confirmar com o jogador ("você está na
partida X contra Y?") antes de reportar, e para acompanhar depois.

```json
{
  "matchId": 412,
  "tournamentName": "Copa Digimon Julho",
  "matchType": 3, "round": 2, "bestOf": 3,
  "yourSlot": 1,
  "you":      { "tournamentPlayerId": 42, "name": "Guilherme", "deck": "Blue Flare" },
  "opponent": { "tournamentPlayerId": 43, "name": "Lucas",     "deck": "Security Control" },
  "drawAllowed": true,
  "canReport": true,
  "blockedReason": null,
  "state": "pending",
  "opponentReported": false,
  "yourReport": null,
  "result": null
}
```

- **`state`**: `pending` | `awaiting_opponent` | `conflict` | `resolved`
- **`drawAllowed`**: habilite/desabilite a opção "Empate" na sua UI por **este campo**.
  Não deduza pelo formato do torneio — empate só existe em rodada Swiss.
- **`canReport: false`** vem com `blockedReason`: `tournament_finished`, `mode_not_online`,
  `match_is_bye`, `slot_empty` ou `already_resolved`. Ainda responde `200` — é para exibir a
  situação, não é erro.
- `404` = código inexistente.

---

## 3) `POST /match/{code}/report`

```json
{
  "outcome": "win",
  "yourGameWins": 2,
  "opponentGameWins": 1,
  "reporterNickname": "guipro",
  "clientVersion": "DCGO 1.4.2"
}
```

- **`outcome`**: `"win"` | `"loss"` | `"draw"` — **sempre do ponto de vista de quem reporta**.
  Não existe id de jogador no payload: o código já fixa a partida e o lado.
- **Placar válido (melhor de 3)**: vitória `2-0` ou `2-1`; derrota `0-2` ou `1-2`; empate `1-1`
  (e só quando `drawAllowed: true`). Omitir o placar assume `2-0` / `0-2` / `1-1`.
- `reporterNickname` e `clientVersion` são opcionais, usados só para auditoria.

### Respostas

| HTTP | Corpo | Significado |
|---|---|---|
| `202` | `{"state":"awaiting_opponent"}` | Registrado. Falta o adversário. |
| `202` | `{"duplicate":true}` / `{"revised":true}` | Reenvio idêntico / correção aceita. |
| `200` | `{"state":"resolved","result":{…}}` | Os dois bateram — resultado aplicado. |
| `409` | `{"state":"conflict","reason":"reports_disagree"}` | Relatos divergem. Mostre "aguardando organizador". |
| `200` | `{"state":"resolved","alreadyApplied":true}` | Já estava resolvido. Nada mudou. |
| `400` | `{"error":"…"}` | Placar ou empate inválido. A mensagem já vem em português — exiba. |
| `404` | `{"error":"Código não encontrado."}` | Código inválido. |
| `401` | `{"error":"Chave de integração inválida."}` | Chave errada ou ausente. |
| `503` | `{"error":"Integração desativada no servidor."}` | Integração desligada no servidor. |
| `429` | — | Limite excedido (ver abaixo). |

No conflito, a resposta devolve **apenas o relato de quem chamou** — nunca o do adversário
(senão bastaria tentar até casar).

---

## Regras de uso

- **Reenviar o mesmo corpo é seguro** (idempotente). Use isso como retry em falha de rede.
- Enquanto a partida está pendente, o jogador **pode corrigir** o relato reenviando com valores
  diferentes; vale a última versão. Isso resolve o conflito sozinho quando alguém só clicou errado.
- **Só reporte com a partida realmente encerrada.** Nunca reporte automaticamente por
  desconexão ou fechamento do app.
- **Guarde o código localmente por partida**, para o jogador não perdê-lo num crash.
- Após um `202`, consulte o `GET /match/{code}` a cada ~15 s (com backoff) até virar `resolved`
  ou `conflict`.
- **Limite: 20 requisições por código por hora.** O polling acima cabe folgado nisso.

---

## Fluxo sugerido no DCGO

1. No boot: `GET /health`. Se falhar, avise que a integração está indisponível e siga sem ela.
2. Antes da partida: jogador cola o código → `GET /match/{code}` → mostrar
   "Torneio X · Rodada N · você (Fulano) vs Beltrano". Se `canReport: false`, explicar o motivo.
3. Ao terminar: perguntar o resultado (habilitando "Empate" só se `drawAllowed`) →
   `POST /match/{code}/report`.
4. Se `202`: mostrar "aguardando o adversário confirmar" e fazer o polling.
5. Se `409`: mostrar "os relatos divergem, um organizador vai revisar" e oferecer corrigir.
6. Se `200 resolved`: mostrar "resultado confirmado" e parar o polling.

---

## Do lado do RankingDigi (para o organizador)

- A chave fica em User Secrets: `dotnet user-secrets set "Integration:ApiKey" "<32+ chars>"`.
  Sem ela configurada, os endpoints respondem `503` — a integração nasce desligada, de propósito.
- Trocar a chave desliga a integração inteira na hora, sem mexer em código.
- Cada jogador vê o próprio código na tela do torneio (Swiss ou bracket), com botão de copiar.
  O admin vê os dois, para ditar a quem perdeu o seu.
- Conflito aparece com botão "Resolver conflito", que mostra o que cada lado relatou e deixa o
  organizador gravar o resultado correto — que sempre prevalece sobre os relatos.
