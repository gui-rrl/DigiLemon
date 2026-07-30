/* ========== Bracket - Dupla Eliminação ========== */

let participantsMap = new Map();
let allMatchesCache = [];
let myCodes         = new Map();   // matchId → { code, formatted, slot } do usuário logado

async function loadTournamentParticipants(tournamentId) {
    const res = await apiFetch(`${API_BASE_URL}/tournament/${tournamentId}/participants`);
    return res.json();
}

async function loadTournamentMatches(tournamentId) {
    const res = await apiFetch(`${API_BASE_URL}/tournament/${tournamentId}/matches`);
    return res.json();
}

function groupMatchesByTypeAndRound(matches) {
    const upper = new Map(), lower = new Map(), finals = [], thirdPlace = [];
    matches.forEach(m => {
        if      (m.matchType === 0) { if (!upper.has(m.round)) upper.set(m.round, []); upper.get(m.round).push(m); }
        else if (m.matchType === 1) { if (!lower.has(m.round)) lower.set(m.round, []); lower.get(m.round).push(m); }
        else if (m.matchType === 2) { finals.push(m); }
        else if (m.matchType === 4) { thirdPlace.push(m); }   // Top 4 sem lower bracket: disputa de 3º lugar
    });
    return { upper, lower, finals, thirdPlace };
}

/* ── Renderização ───────────────────────────────────────────────────────── */

// score: placar em games (melhor de 3) deste jogador na partida, ou null quando a partida
// não tem placar registrado (partidas antigas, ou ainda não jogada) — nesse caso não mostra nada.
function renderPlayerLine(tpId, isWinner, isLoser, score) {
    if (!tpId) return `<div class="player waiting"><span class="player-name">Aguardando</span></div>`;
    const p    = participantsMap.get(tpId);
    const name = p?.playerName ?? 'Desconhecido';
    const deck = p?.deck ?? null;
    const link = p?.playerId ? `/player.html?id=${p.playerId}` : null;
    const cls  = isWinner ? 'winner' : isLoser ? 'loser' : '';
    const nameHtml = link
        ? `<a href="${link}" style="color:inherit;text-decoration:none;">${escapeHtml(name)}</a>`
        : escapeHtml(name);
    const scoreHtml = (score !== null && score !== undefined)
        ? `<span class="player-score" title="Placar em games (melhor de 3)">${score}</span>` : '';
    return `
        <div class="player ${cls}">
            <div class="player-name-row">
                <span class="player-name">${nameHtml}</span>
                ${scoreHtml}
            </div>
            ${deck ? `<span class="deck-tag" title="${escapeHtml(deck)}"><i class="bi bi-layers"></i> ${escapeHtml(deck)}</span>` : ''}
        </div>`;
}

function renderMatchCard(match) {
    let winner = null, loser = null;
    if (match.winnerId) {
        winner = match.winnerId;
        if (match.player1Id && match.player2Id)
            loser = match.winnerId === match.player1Id ? match.player2Id : match.player1Id;
    }

    // Placar em games (melhor de 3): só mostra quando a partida foi jogada e tem os dois
    // valores registrados — partidas antigas, de antes do placar por game, ficam sem número
    // (mesma convenção usada na tela de Swiss), em vez de inventar um 2x0 que ninguém digitou.
    const temPlacar = match.isPlayed
        && match.player1GameWins !== null && match.player1GameWins !== undefined
        && match.player2GameWins !== null && match.player2GameWins !== undefined;
    const p1Score = temPlacar ? match.player1GameWins : null;
    const p2Score = temPlacar ? match.player2GameWins : null;

    const canRegister = !match.isPlayed && match.player1Id && match.player2Id;
    const estado = match.reportState || 'none';   // relatos do DCGO
    const rc = myCodes.get(match.id);

    const btnCodigo = (rc && !match.isPlayed) ? `
        <button class="btn btn-ghost btn-sm btn-report-code" data-code="${escapeHtml(rc.code)}"
                title="Cole este código no DCGO para reportar o resultado desta partida">
          <i class="bi bi-key"></i> ${escapeHtml(rc.formatted || rc.code)}
        </button>` : '';

    const conflito = (!match.isPlayed && estado === 'conflict')
        ? (typeof authIsAdmin === 'function' && authIsAdmin()
            ? `<button class="btn btn-sm btn-resolve" style="background:var(--danger);border-color:var(--danger);color:#fff;" data-match-id="${match.id}"><i class="bi bi-exclamation-triangle-fill"></i> Resolver conflito</button>`
            : `<div class="match-status" style="color:var(--danger);"><i class="bi bi-exclamation-triangle"></i> Relatos divergentes</div>`)
        : '';

    const aguardando = (!match.isPlayed && estado === 'awaiting')
        ? `<div class="match-status"><i class="bi bi-hourglass-split"></i> Aguardando o adversário</div>` : '';

    // Estado/ação da partida (botão de registrar, conflito, aguardando ou "Finalizada") vem
    // primeiro na coluna; o código do DCGO (quando existe) fica embaixo dele.
    const acao = `${conflito}${aguardando}${canRegister && estado !== 'conflict' ? `<button class="btn btn-primary btn-sm result-btn" data-match-id="${match.id}"><i class="bi bi-flag"></i> Registrar resultado</button>` : ''}${match.isPlayed ? `<div class="match-status"><i class="bi bi-check2-circle"></i> Finalizada</div>` : ''}`;

    // Tudo fica dentro do .match-actions de propósito: alignBracketMatches mede a altura
    // depois de renderizar e matchCenterY usa os .player, então os conectores não saem do lugar.
    const temAcoes = acao || btnCodigo;
    const actions = temAcoes ? `
        <div class="match-actions">
            ${acao}
            ${btnCodigo}
        </div>` : '';

    return `
        <div class="match-wrapper">
            <div class="match ${match.isPlayed ? 'match-done' : ''}">
                <div class="match-players">
                    ${renderPlayerLine(match.player1Id, winner === match.player1Id, loser === match.player1Id, p1Score)}
                    ${renderPlayerLine(match.player2Id, winner === match.player2Id, loser === match.player2Id, p2Score)}
                </div>
                ${actions}
            </div>
        </div>`;
}

function renderBracketSection(containerId, matchesByRound) {
    const container = document.getElementById(containerId);
    container.innerHTML = '';
    const sortedRounds = Array.from(matchesByRound.keys()).sort((a, b) => a - b);
    if (!sortedRounds.length) {
        container.innerHTML = `<div class="empty-state" style="padding:1.5rem;"><div class="icon"><i class="bi bi-hourglass"></i></div><div class="title">Sem partidas</div></div>`;
        return;
    }
    for (const round of sortedRounds) {
        const matches = matchesByRound.get(round);
        container.insertAdjacentHTML('beforeend', `
            <div class="round">
                <h5>Rodada ${round}</h5>
                ${matches.map(renderMatchCard).join('')}
            </div>`);
    }
}

function renderGrandFinals(finals, containerId) {
    const container = document.getElementById(containerId);
    container.innerHTML = '';
    if (!finals.length) {
        container.innerHTML = `<div class="empty-state" style="padding:1.5rem;"><div class="icon"><i class="bi bi-hourglass"></i></div><div class="title">Aguardando</div></div>`;
        return;
    }
    finals.forEach(m => container.insertAdjacentHTML('beforeend', renderMatchCard(m)));
}

/* ── Renomear última rodada ─────────────────────────────────────────────── */

// isTopFour: true quando o chaveamento é o Top 4 novo (semifinais + 3º lugar, sem lower
// bracket) — nesse caso "Final da Upper" não faz sentido, é só a Semifinal mesmo, e não há
// "Final da Lower" pra renomear porque não existe lower bracket.
function renameLastRounds(isTopFour) {
    const up = document.querySelectorAll('#upperBracketRoot > .round');
    if (up.length) up[up.length - 1].querySelector('h5').textContent = isTopFour ? 'Semifinal' : 'Final da Upper';

    if (isTopFour) return;
    const lo = document.querySelectorAll('#lowerBracketRoot > .round');
    if (lo.length) lo[lo.length - 1].querySelector('h5').textContent = 'Final da Lower';
}

/* ── Alinhamento vertical (bracket tree) ────────────────────────────────── */

/**
 * Posiciona os .match-wrapper de cada round em absolute, centralizando
 * cada match no ponto médio dos dois parents na rodada anterior.
 * Isso produz o efeito visual clássico de bracket (árvore).
 */
function alignBracketMatches(rootId) {
    const root = document.getElementById(rootId);
    if (!root) return;

    const rounds = Array.from(root.querySelectorAll(':scope > .round'));
    if (rounds.length < 2) return;

    const roundMatches = rounds.map(r => Array.from(r.querySelectorAll(':scope > .match-wrapper')));
    if (!roundMatches[0]?.length) return;

    // Altura do header (h5 + margin-bottom) — usa o primeiro round como referência
    const h5 = rounds[0].querySelector('h5');
    const headerH = h5
        ? h5.offsetHeight + parseFloat(getComputedStyle(h5).marginBottom || '0')
        : 0;

    const MATCH_GAP = 60; // px entre cards

    // Centros no round 0 (empilhados linearmente)
    let prevCenters = [];
    let totalBodyH  = 0;
    roundMatches[0].forEach(mw => {
        const h = mw.offsetHeight;
        prevCenters.push(totalBodyH + h / 2);
        totalBodyH += h + MATCH_GAP;
    });
    totalBodyH -= MATCH_GAP;

    const totalH = headerH + totalBodyH;

    // Centros para rodadas subsequentes
    // - se currCount === prevCount → round 1:1 (feed da lower bracket): cada match herda o centro do match anterior
    // - caso contrário → round de merge: cada match centraliza entre o par de parents
    const allCenters = [prevCenters];
    for (let r = 1; r < rounds.length; r++) {
        const currCount = roundMatches[r].length;
        const prevCount = prevCenters.length;
        const centers   = [];

        for (let i = 0; i < currCount; i++) {
            if (currCount === prevCount) {
                // round de feed 1:1 — mesma posição do match correspondente
                centers.push(prevCenters[i] ?? 0);
            } else {
                // round de merge par→1
                const pa = prevCenters[i * 2];
                const pb = prevCenters[i * 2 + 1];
                centers.push(pa !== undefined && pb !== undefined ? (pa + pb) / 2 : pa ?? 0);
            }
        }
        allCenters.push(centers);
        prevCenters = centers;
    }

    // Lê o padding do round para insetar o match-wrapper (deixa padding livre para conectores SVG)
    const roundPad = parseFloat(getComputedStyle(rounds[0]).paddingLeft) || 40;

    // Aplica position: absolute em cada .match-wrapper
    rounds.forEach((round, r) => {
        round.style.position = 'relative';
        round.style.height   = totalH + 'px';

        roundMatches[r].forEach((mw, i) => {
            const center = allCenters[r][i] ?? 0;
            const h      = mw.offsetHeight;
            mw.style.position = 'absolute';
            mw.style.left     = roundPad + 'px';  // respeita padding — não sobrepõe área dos conectores
            mw.style.right    = roundPad + 'px';
            mw.style.margin   = '0';
            mw.style.top      = Math.round(headerH + center - h / 2) + 'px';
        });
    });
}

/* ── Conectores SVG ─────────────────────────────────────────────────────── */

const CONN_COLOR  = 'rgba(22,224,189,0.40)';
const ARROW_COLOR = 'rgba(22,224,189,0.65)';

function posIn(el, container) {
    const er = el.getBoundingClientRect();
    const cr = container.getBoundingClientRect();
    return {
        left:  er.left   - cr.left,
        right: er.right  - cr.left,
        top:   er.top    - cr.top,
        bot:   er.bottom - cr.top,
        midY:  (er.top + er.bottom) / 2 - cr.top,
    };
}

// Retorna o Y do meio entre os dois jogadores (borda p1/p2), não o centro do card completo
function matchCenterY(matchWrapper, container) {
    const players = matchWrapper.querySelectorAll('.player');
    const cr = container.getBoundingClientRect();
    if (players.length >= 2) {
        const r0 = players[0].getBoundingClientRect();
        const r1 = players[1].getBoundingClientRect();
        return (r0.bottom + r1.top) / 2 - cr.top;
    }
    const er = matchWrapper.getBoundingClientRect();
    return (er.top + er.bottom) / 2 - cr.top;
}

function addLine(svg, x1, y1, x2, y2) {
    const el = document.createElementNS('http://www.w3.org/2000/svg', 'line');
    el.setAttribute('x1', x1.toFixed(1)); el.setAttribute('y1', y1.toFixed(1));
    el.setAttribute('x2', x2.toFixed(1)); el.setAttribute('y2', y2.toFixed(1));
    el.setAttribute('stroke', CONN_COLOR);
    el.setAttribute('stroke-width', '1.5');
    el.setAttribute('stroke-linecap', 'round');
    svg.appendChild(el);
}

function addArrow(svg, x, y) {
    const el = document.createElementNS('http://www.w3.org/2000/svg', 'polygon');
    el.setAttribute('points', `${x.toFixed(1)},${(y-4).toFixed(1)} ${(x+8).toFixed(1)},${y.toFixed(1)} ${x.toFixed(1)},${(y+4).toFixed(1)}`);
    el.setAttribute('fill', ARROW_COLOR);
    svg.appendChild(el);
}

function makeSvg(container, cls = 'bracket-connector-svg') {
    const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    svg.classList.add(cls);
    container.insertBefore(svg, container.firstChild); // atrás dos rounds
    return svg;
}

/**
 * Conecta pares de matches do roundA ao match correspondente em roundB.
 *
 *   matchA1 ────┐
 *               ├────► matchB
 *   matchA2 ────┘
 */
function connectRounds(svg, container, roundA, roundB) {
    const msA = Array.from(roundA.querySelectorAll(':scope > .match-wrapper'));
    const msB = Array.from(roundB.querySelectorAll(':scope > .match-wrapper'));

    // Borda visual real dos cards = posição do round ± padding do round
    // (.match-wrapper usa left:0;right:0 que abrange o padding box inteiro do round,
    //  então getBoundingClientRect do card == getBoundingClientRect do round)
    const roundPad  = parseFloat(getComputedStyle(roundA).paddingRight) || 24;
    const rA        = posIn(roundA, container);
    const rB        = posIn(roundB, container);
    const xCardRight = rA.right - roundPad;      // borda direita real do card em roundA
    const xCardLeft  = rB.left  + roundPad;      // borda esquerda real do card em roundB
    const xBar       = (xCardRight + xCardLeft) / 2; // ponto médio do gap entre rounds

    msB.forEach((mB, idx) => {
        const mA1 = msA[idx * 2];
        const mA2 = msA[idx * 2 + 1];
        if (!mA1) return;

        const yA1 = matchCenterY(mA1, container);
        const yB  = matchCenterY(mB,  container);

        if (mA2) {
            const yA2   = matchCenterY(mA2, container);
            const yJoin = (yA1 + yA2) / 2;

            addLine(svg, xCardRight, yA1,   xBar,       yA1);
            addLine(svg, xCardRight, yA2,   xBar,       yA2);
            addLine(svg, xBar,       yA1,   xBar,       yA2);
            addLine(svg, xBar,       yJoin, xCardLeft,  yB);
            addArrow(svg, xCardLeft, yB);
        } else {
            addLine(svg, xCardRight, yA1, xCardLeft, yB);
            addArrow(svg, xCardLeft, yB);
        }
    });
}

function drawConnectors() {
    document.querySelectorAll('.bracket-connector-svg').forEach(s => s.remove());

    // ── Chave Superior: conectores entre rounds ──────────────────────────
    const upperRoot   = document.getElementById('upperBracketRoot');
    const upperRounds = Array.from(upperRoot.querySelectorAll(':scope > .round'));
    if (upperRounds.length >= 2) {
        const svg = makeSvg(upperRoot);
        for (let i = 0; i < upperRounds.length - 1; i++)
            connectRounds(svg, upperRoot, upperRounds[i], upperRounds[i + 1]);
    }

    // ── Chave Inferior: conectores entre rounds ──────────────────────────
    const lowerRoot   = document.getElementById('lowerBracketRoot');
    const lowerRounds = Array.from(lowerRoot.querySelectorAll(':scope > .round'));
    if (lowerRounds.length >= 2) {
        const svg = makeSvg(lowerRoot);
        for (let i = 0; i < lowerRounds.length - 1; i++)
            connectRounds(svg, lowerRoot, lowerRounds[i], lowerRounds[i + 1]);
    }

    // ── Final da Upper → Grande Final (cross-container) ──────────────────
    const upperRow       = document.getElementById('upperBracketRow');
    const grandFinalSect = document.getElementById('grandFinalSection');
    if (upperRounds.length > 0 && grandFinalSect && upperRow) {
        const lastRound      = upperRounds[upperRounds.length - 1];
        const lastMatch      = lastRound.querySelector('.match-wrapper');
        const grandFinalMatch= grandFinalSect.querySelector('.match-wrapper');

        if (lastMatch && grandFinalMatch) {
            const svg = makeSvg(upperRow);
            upperRow.style.position = 'relative';

            // Usa as bordas visuais reais: round padding direito + grand-final padding esquerdo
            const lastRoundPad = parseFloat(getComputedStyle(lastRound).paddingRight) || 24;
            const gfContPad    = parseFloat(getComputedStyle(grandFinalSect).paddingLeft) || 20;
            const rLast = posIn(lastRound,       upperRow);
            const rGF   = posIn(grandFinalSect,  upperRow);

            const xExitRight = rLast.right - lastRoundPad;  // borda direita real do card
            const xEnterLeft = rGF.left    + gfContPad;     // borda esquerda real do GF card
            const xMid       = (xExitRight + xEnterLeft) / 2;

            const yL  = matchCenterY(lastMatch,       upperRow);
            const yGF = matchCenterY(grandFinalMatch,  upperRow);

            addLine(svg, xExitRight, yL,  xMid,       yL);   // saída horizontal
            addLine(svg, xMid,       yL,  xMid,       yGF);  // segmento vertical
            addLine(svg, xMid,       yGF, xEnterLeft, yGF);  // entrada horizontal
            addArrow(svg, xEnterLeft, yGF);
        }
    }
}

/* ── Modal ──────────────────────────────────────────────────────────────── */

let currentModalMatchId = null, currentPlayer1Id = null, currentPlayer2Id = null;

function openResultModal(matchId, player1Id, player2Id) {
    currentModalMatchId = matchId;
    currentPlayer1Id    = player1Id;
    currentPlayer2Id    = player2Id;
    const sel = document.getElementById('winnerSelect');
    sel.innerHTML = '<option value="">Selecione o vencedor…</option>';
    [player1Id, player2Id].forEach(tpId => {
        if (!tpId) return;
        const p    = participantsMap.get(tpId);
        const name = p?.playerName ?? 'Desconhecido';
        const deck = p?.deck ?? 'Sem deck';
        sel.innerHTML += `<option value="${tpId}">${escapeHtml(name)} (${escapeHtml(deck)})</option>`;
    });
    new bootstrap.Modal(document.getElementById('resultModal')).show();
}

document.getElementById('saveResultBtn').addEventListener('click', async () => {
    const winnerId = document.getElementById('winnerSelect').value;
    if (!winnerId) { notifyWarning('Selecione o vencedor antes de salvar.'); return; }

    const loserId = (currentPlayer1Id && currentPlayer2Id)
        ? (winnerId == currentPlayer1Id ? currentPlayer2Id : currentPlayer1Id)
        : null;

    const [winnerGames, loserGames] = document.getElementById('scoreSelect').value.split('-').map(Number);

    try {
        await apiFetch(`${API_BASE_URL}/tournamentmatch/${currentModalMatchId}/result`, {
            method: 'POST',
            body: JSON.stringify({ winnerId: parseInt(winnerId), loserId, winnerGames, loserGames }),
        });
        notifySuccess('Resultado registrado!');
        bootstrap.Modal.getInstance(document.getElementById('resultModal')).hide();
        setTimeout(() => location.reload(), 800);
    } catch (err) {
        notifyError('Não foi possível registrar o resultado: ' + err.message);
    }
});

/* ── Inicialização ──────────────────────────────────────────────────────── */

async function init() {
    const id = new URLSearchParams(window.location.search).get('id');
    if (!id) { notifyError('ID do torneio não informado.'); return; }

    try {
        const participants = await loadTournamentParticipants(id);
        participants.forEach(p => participantsMap.set(p.id, p));

        allMatchesCache = await loadTournamentMatches(id);

        const tournament = await apiFetch(`${API_BASE_URL}/tournament/${id}`).then(r => r.json());
        document.getElementById('tournamentTitle').innerText = tournament.name;
        document.title = `${tournament.name} — Bracket`;

        // Códigos do DCGO: só torneio online, só logado, e só se houver partida em aberto
        // (senão a requisição volta vazia). Falhar aqui não derruba a tela.
        myCodes.clear();
        const temPartidaAberta = allMatchesCache.some(m => !m.isPlayed && !m.isBye && m.player1Id && m.player2Id);

        if (tournament.mode === 1 && temPartidaAberta && typeof authToken === 'function' && authToken()) {
            try {
                const codes = await apiFetch(`${API_BASE_URL}/tournament/${id}/my-report-codes`).then(r => r.json());
                codes.forEach(c => myCodes.set(c.matchId, c));
            } catch (err) {
                console.warn('Não foi possível carregar os códigos do DCGO:', err.message);
            }
        }

        const recapLink = document.getElementById('backToRecapLink');
        if (recapLink && tournament.status === 2) {
            recapLink.href = `/tournament-recap.html?id=${id}`;
            recapLink.style.display = '';
        }

        const { upper, lower, finals, thirdPlace } = groupMatchesByTypeAndRound(allMatchesCache);
        const isTopFour = thirdPlace.length > 0; // TopFourGenerator: sem lower bracket, com 3º lugar dedicado

        // No Top 4, Semifinais + Grande Final + Disputa de 3º lugar viram uma única caixa
        // (ver .bracket-merged em bracket.css), em vez de três caixas lado a lado.
        document.getElementById('upperBracketRow').classList.toggle('bracket-merged', isTopFour);

        renderBracketSection('upperBracketRoot', upper);
        renderGrandFinals(finals, 'grandFinalRoot');

        const lowerRow = document.getElementById('lowerBracketRow');
        if (isTopFour) {
            lowerRow.style.display = 'none';
        } else {
            lowerRow.style.display = '';
            renderBracketSection('lowerBracketRoot', lower);
        }

        const thirdPlaceSection = document.getElementById('thirdPlaceSection');
        if (isTopFour) {
            thirdPlaceSection.style.display = '';
            renderGrandFinals(thirdPlace, 'thirdPlaceRoot');
        }

        const championSection = document.getElementById('championSection');
        if (isTopFour) {
            championSection.style.display = '';
            const championNameEl = document.getElementById('championName');
            const winnerId = finals[0]?.winnerId;
            if (winnerId) {
                const p = participantsMap.get(winnerId);
                championNameEl.textContent = p?.playerName ?? 'Desconhecido';
                championNameEl.classList.remove('pending');
            } else {
                championNameEl.textContent = 'Aguardando';
                championNameEl.classList.add('pending');
            }
        }

        const subtitle = document.getElementById('bracketSubtitle');
        if (isTopFour && subtitle) {
            subtitle.textContent = 'Semifinais (sorteio aleatório entre os 4 classificados) + disputa de 3º lugar · clique em "Registrar resultado" para definir o vencedor de cada partida.';
        }
        const upperTitle = document.getElementById('upperSectionTitle');
        if (isTopFour && upperTitle) {
            upperTitle.innerHTML = '<i class="bi bi-diagram-2-fill"></i> Semifinais';
        }

        renameLastRounds(isTopFour);

        document.querySelectorAll('.result-btn').forEach(btn => {
            btn.addEventListener('click', () => {
                const match = allMatchesCache.find(m => m.id === parseInt(btn.dataset.matchId));
                if (match) openResultModal(match.id, match.player1Id, match.player2Id);
                else notifyError('Partida não encontrada.');
            });
        });

        document.querySelectorAll('.btn-report-code').forEach(btn => {
            btn.addEventListener('click', () =>
                copyToClipboard(btn.dataset.code, 'Código copiado! Cole no DCGO.'));
        });

        // Conflito abre o modal de resultado normal — a gravação segue pelo endpoint de admin.
        document.querySelectorAll('.btn-resolve').forEach(btn => {
            btn.addEventListener('click', () => {
                const match = allMatchesCache.find(m => m.id === parseInt(btn.dataset.matchId));
                if (match) openResultModal(match.id, match.player1Id, match.player2Id);
                else notifyError('Partida não encontrada.');
            });
        });

        // Alinhar + conectores após o browser calcular o layout
        requestAnimationFrame(() => requestAnimationFrame(() => {
            alignBracketMatches('upperBracketRoot');
            alignBracketMatches('lowerBracketRoot');
            drawConnectors();
        }));

    } catch (err) {
        console.error(err);
        notifyError('Erro ao carregar torneio: ' + err.message);
    }
}

window.addEventListener('resize', () => {
    clearTimeout(window._bracketResizeTimer);
    window._bracketResizeTimer = setTimeout(() => {
        alignBracketMatches('upperBracketRoot');
        alignBracketMatches('lowerBracketRoot');
        drawConnectors();
    }, 150);
});

init();
