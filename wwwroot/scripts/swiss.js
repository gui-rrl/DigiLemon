/* ========== Swiss + Top Cut ========== */

const urlParams     = new URLSearchParams(window.location.search);
const tournamentId  = urlParams.get('id');
let participantsMap = new Map();   // tpId → { playerName, deck, playerId, isGuest }
let statusCache     = null;
let currentMatchId  = null;
let currentP1Id     = null;
let currentP2Id     = null;
let myCodes         = new Map();   // matchId → { code, formatted, slot } do usuário logado

// ── Carregamento principal ────────────────────────────────────────────────────

async function loadAll() {
    if (!tournamentId) { notifyError('ID do torneio não informado.'); return; }

    try {
        // Participantes (para nomes)
        const parts = await apiFetch(`${API_BASE_URL}/tournament/${tournamentId}/participants`).then(r => r.json());
        participantsMap.clear();
        parts.forEach(p => participantsMap.set(p.id, p));

        // Status Swiss completo
        statusCache = await apiFetch(`${API_BASE_URL}/tournament/${tournamentId}/swiss/status`).then(r => r.json());

        // Códigos do DCGO: só em torneio online, só pra quem está logado e só se houver
        // partida em aberto — sem isso não há código nenhum pra mostrar e a requisição seria
        // desperdiçada. Falha aqui não pode derrubar a página: sem código, some só o botão.
        myCodes.clear();
        const temPartidaAberta = []
            .concat(...Object.values(statusCache.swissMatchesByRound || {}))
            .concat(statusCache.topCutMatches || [])
            .some(m => !m.isPlayed && !m.isBye);

        if (statusCache.mode === 1 && temPartidaAberta && typeof authToken === 'function' && authToken()) {
            try {
                const codes = await apiFetch(`${API_BASE_URL}/tournament/${tournamentId}/my-report-codes`).then(r => r.json());
                codes.forEach(c => myCodes.set(c.matchId, c));
            } catch (err) {
                console.warn('Não foi possível carregar os códigos do DCGO:', err.message);
            }
        }

        // Título
        const t = await apiFetch(`${API_BASE_URL}/tournament/${tournamentId}`).then(r => r.json());
        document.getElementById('tournamentTitle').textContent = t.name;
        document.title = `${t.name} — Swiss`;
        const isPure = statusCache.format === 2;
        document.getElementById('tournamentSubtitle').textContent = isPure
            ? `Swiss Pontos Corridos · Rodada ${statusCache.currentSwissRound}/${statusCache.swissRounds} · ${parts.length} jogadores`
            : `Swiss ${statusCache.currentSwissRound}/${statusCache.swissRounds} · Top ${statusCache.topCutSize} · ${parts.length} jogadores`;

        renderStandings(statusCache.standings, statusCache.topCutSize);
        renderRounds(statusCache.swissMatchesByRound);
        renderTopCut(statusCache);
        updateAdminButtons(statusCache);

        const recapLink = document.getElementById('backToRecapLink');
        if (recapLink && statusCache.status === 2) {
            recapLink.href = `/tournament-recap.html?id=${tournamentId}`;
            recapLink.style.display = '';
        }

    } catch (err) {
        console.error(err);
        notifyError('Erro ao carregar torneio: ' + err.message);
    }
}

// ── Standings ─────────────────────────────────────────────────────────────────

function renderStandings(standings, topCutSize) {
    const container = document.getElementById('standingsContainer');
    if (!standings || !standings.length) {
        container.innerHTML = '<div class="p-3 text-muted-2 text-center">Sem dados ainda.</div>';
        return;
    }

    const isPure = statusCache?.format === 2;

    const rows = standings.map((s, idx) => {
        const posClass = s.position === 1 ? 'gold' : s.position === 2 ? 'silver' : s.position === 3 ? 'bronze' : '';
        const isTopCutLine = !isPure && s.position === topCutSize;
        const isTop4 = isPure && s.position <= 4;
        return `<tr class="${isTopCutLine ? 'topcut-line' : ''} ${isTop4 ? 'top4-row' : ''}">
            <td><span class="pos-badge ${posClass}">${s.position}</span></td>
            <td>
                <div style="font-weight:600;line-height:1.2;">${escapeHtml(s.playerName)}</div>
                <div class="text-muted-2" style="font-size:0.75rem;">${s.deck ? escapeHtml(s.deck) : '<em>Sorteio pendente</em>'}</div>
            </td>
            <td style="font-weight:700;color:var(--accent);text-align:center;">${s.points}</td>
            <td style="text-align:center;color:var(--text-2);">${s.wins}-${s.losses}</td>
            <td style="text-align:center;color:var(--text-3);">${s.wins + s.losses + s.draws}</td>
            <td style="text-align:right;color:var(--text-3);font-size:0.78rem;" title="OMW% = aproveitamento dos adversários · GW% = seu aproveitamento em games">
                ${s.omw}% <span style="opacity:0.6;">/ ${s.gw ?? 0}%</span>
            </td>
            <td style="text-align:right;color:var(--text-3);font-size:0.78rem;" title="MW% = seu aproveitamento em partidas · OGW% = aproveitamento dos adversários em games">
                ${s.mw ?? 0}% <span style="opacity:0.6;">/ ${s.ogw ?? 0}%</span>
            </td>
        </tr>`;
    }).join('');

    const bottomLegend = isPure
        ? `<div class="px-3 py-2" style="font-size:0.75rem;color:var(--text-3);">
               <i class="bi bi-star-fill me-1" style="color:var(--warning);"></i>Destaque = Top 4
           </div>`
        : `<div class="px-3 py-2" style="font-size:0.75rem;color:var(--text-3);">
               <i class="bi bi-dash-lg me-1" style="color:var(--accent);"></i>Linha pontilhada = corte para Top ${topCutSize}
           </div>`;

    container.innerHTML = `
        <div class="table-responsive">
            <table class="standings-table">
                <thead>
                    <tr>
                        <th style="width:34px;">#</th>
                        <th>Jogador</th>
                        <th style="text-align:center;" title="Pontos">Pts</th>
                        <th style="text-align:center;" title="Vitórias-Derrotas">V-D</th>
                        <th style="text-align:center;" title="Total de partidas jogadas (vitórias + derrotas + empates)">Partidas</th>
                        <th style="text-align:right;" title="OMW% = aproveitamento dos adversários (força da tabela) · GW% = seu aproveitamento em games na melhor de 3">OMW% / GW%</th>
                        <th style="text-align:right;" title="MW% = seu aproveitamento em partidas · OGW% = aproveitamento dos adversários em games">MW% / OGW%</th>
                    </tr>
                </thead>
                <tbody>${rows}</tbody>
            </table>
        </div>
        ${bottomLegend}`;
}

// ── Rodadas Swiss ─────────────────────────────────────────────────────────────

function playerName(tpId) {
    if (!tpId) return '<em class="text-muted-2">BYE</em>';
    const p = participantsMap.get(tpId);
    return p ? escapeHtml(p.playerName || 'Desconhecido') : `#${tpId}`;
}

function renderRounds(matchesByRound) {
    const container = document.getElementById('roundsContainer');
    if (!matchesByRound || !Object.keys(matchesByRound).length) {
        container.innerHTML = '<div class="p-4 text-center text-muted-2">Nenhuma rodada gerada ainda.</div>';
        return;
    }

    // No todos contra todos não existem rodadas: é um bloco único com todas as partidas.
    const isRoundRobin = statusCache?.format === 3;

    const rounds = Object.keys(matchesByRound).map(Number).sort((a, b) => b - a); // mais recente primeiro
    container.innerHTML = rounds.map(round => {
        const matches = matchesByRound[round];
        const allDone = matches.every(m => m.isPlayed);
        const jogadas = matches.filter(m => m.isPlayed).length;
        const matchRows = matches.map(m => renderMatchRow(m, round)).join('');
        const titulo = isRoundRobin
            ? `<i class="bi bi-arrow-repeat me-2"></i>Todos contra todos <span class="text-muted-2" style="font-weight:400;">(${jogadas}/${matches.length} partidas)</span>`
            : `<i class="bi bi-collection me-2"></i>Rodada ${round}`;
        return `
            <div class="swiss-round-card">
                <div class="round-header">
                    <span>${titulo}</span>
                    ${allDone
                        ? '<span class="status-pill live" style="font-size:0.75rem;"><i class="bi bi-check2-all"></i> Concluída</span>'
                        : '<span class="status-pill prep" style="font-size:0.75rem;"><i class="bi bi-hourglass-split"></i> Em andamento</span>'}
                </div>
                ${matchRows}
            </div>`;
    }).join('');

    // Bind botões de resultado
    container.querySelectorAll('.btn-result').forEach(btn => {
        btn.addEventListener('click', () => openResultModal(
            parseInt(btn.dataset.matchId),
            parseInt(btn.dataset.p1),
            btn.dataset.p2 ? parseInt(btn.dataset.p2) : null,
        ));
    });

    // Copiar o código do DCGO
    container.querySelectorAll('.btn-report-code').forEach(btn => {
        btn.addEventListener('click', () =>
            copyToClipboard(btn.dataset.code, 'Código copiado! Cole no DCGO.'));
    });

    // Conflito: abre o MESMO modal de resultado, com o resumo dos relatos por cima.
    // A gravação continua indo pelo endpoint de admin de sempre — nenhum caminho novo.
    container.querySelectorAll('.btn-resolve').forEach(btn => {
        btn.addEventListener('click', () => openConflictModal(
            parseInt(btn.dataset.matchId),
            parseInt(btn.dataset.p1),
            btn.dataset.p2 ? parseInt(btn.dataset.p2) : null,
        ));
    });

    // Reverter: desfaz um resultado lançado errado (devolve os pontos e libera a partida).
    container.querySelectorAll('.btn-revert-result').forEach(btn => {
        btn.addEventListener('click', () => revertMatchResult(parseInt(btn.dataset.matchId)));
    });
}

async function revertMatchResult(matchId) {
    const confirm = await confirmAction({
        title: 'Reverter resultado?',
        text: 'A partida volta a ficar em aberto e os pontos que ela deu (no torneio e no ranking geral) são desfeitos. Use só em caso de lançamento errado.',
        confirmText: 'Reverter', cancelText: 'Cancelar', icon: 'warning',
    });
    if (!confirm.isConfirmed) return;
    try {
        await apiFetch(`${API_BASE_URL}/tournamentmatch/${matchId}/revert`, { method: 'POST' });
        notifySuccess('Resultado revertido — a partida está livre para ser relançada.');
        loadAll();
    } catch (err) {
        notifyError('Não foi possível reverter: ' + err.message);
    }
}

/** Abre o modal de resultado já mostrando o que cada lado relatou no DCGO. */
async function openConflictModal(matchId, p1Id, p2Id) {
    openResultModal(matchId, p1Id, p2Id);

    const modalBody = document.querySelector('#resultModal .modal-body');
    if (!modalBody) return;

    document.getElementById('conflictInfo')?.remove();
    const box = document.createElement('div');
    box.id = 'conflictInfo';
    box.className = 'mb-3';
    box.style.cssText = 'background:rgba(255,93,115,0.10);border:1px solid rgba(255,93,115,0.35);border-radius:10px;padding:0.75rem 0.9rem;font-size:0.85rem;';
    box.innerHTML = '<div class="text-muted-2">Carregando relatos…</div>';
    modalBody.prepend(box);

    try {
        const relatos = await apiFetch(`${API_BASE_URL}/tournamentmatch/${matchId}/reports`).then(r => r.json());
        box.innerHTML = `
            <div style="font-weight:700;color:var(--danger);margin-bottom:0.4rem;">
                <i class="bi bi-exclamation-triangle-fill"></i> Relatos divergentes
            </div>
            ${relatos.map(r => `
                <div style="margin-bottom:0.25rem;">
                    <strong>${escapeHtml(r.playerName)}</strong> relatou:
                    vencedor <strong>${escapeHtml(r.claimedWinnerName)}</strong> (${escapeHtml(r.claimedScore)})
                    <span class="text-muted-2">
                        · ${formatDateTime(r.reportedAt)}${r.reporterNickname ? ` · nick “${escapeHtml(r.reporterNickname)}”` : ''}${r.revisionCount ? ` · ${r.revisionCount} correção(ões)` : ''}
                    </span>
                </div>`).join('')}
            <div class="text-muted-2" style="margin-top:0.4rem;">Escolha abaixo o resultado correto — ele vale sobre os relatos.</div>`;
    } catch (err) {
        box.innerHTML = `<div class="text-muted-2">Não foi possível carregar os relatos: ${escapeHtml(err.message)}</div>`;
    }
}

function renderMatchRow(m, round) {
    const p1Name = playerName(m.player1Id);
    const p2Name = m.isBye ? '<em class="text-muted-2">BYE</em>' : playerName(m.player2Id);

    let p1Class = '', p2Class = '';
    if (m.isPlayed && !m.isBye && m.winnerId == null) {
        p1Class = p2Class = 'draw';
    } else if (m.isPlayed && m.winnerId) {
        p1Class = m.winnerId === m.player1Id ? 'winner' : 'loser';
        p2Class = m.winnerId === m.player2Id ? 'winner' : (m.player2Id ? 'loser' : '');
    }

    const isAdmin = typeof authIsAdmin === 'function' && authIsAdmin();
    const estado  = m.reportState || 'none';   // relatos do DCGO: none|awaiting|conflict|resolved
    let btnResult;
    if (m.isBye) {
        btnResult = '<span class="badge bg-secondary">BYE automático</span>';
    } else if (m.isPlayed) {
        btnResult = '<span class="status-pill live" style="font-size:0.75rem;"><i class="bi bi-check2-circle"></i> Finalizada</span>'
            + (isAdmin ? `<button class="btn btn-sm btn-ghost btn-revert-result" data-match-id="${m.id}"
                    title="Desfaz este resultado — devolve os pontos (no torneio e no ranking geral) e libera a partida pra ser relançada. Use em caso de lançamento errado.">
                    <i class="bi bi-arrow-counterclockwise"></i> Reverter
               </button>` : '');
    } else if (estado === 'conflict' && isAdmin) {
        btnResult = `<button class="btn btn-sm btn-resolve"
                style="background:var(--danger);border-color:var(--danger);color:#fff;"
                data-match-id="${m.id}"
                data-p1="${m.player1Id}"
                ${m.player2Id ? `data-p2="${m.player2Id}"` : ''}>
                <i class="bi bi-exclamation-triangle-fill"></i> Resolver conflito
           </button>`;
    } else if (estado === 'conflict') {
        btnResult = '<span class="status-pill" style="font-size:0.75rem;background:rgba(255,93,115,0.15);color:var(--danger);"><i class="bi bi-exclamation-triangle"></i> Relatos divergentes</span>';
    } else if (isAdmin) {
        btnResult = `<button class="btn btn-primary btn-sm btn-result"
                data-match-id="${m.id}"
                data-p1="${m.player1Id}"
                ${m.player2Id ? `data-p2="${m.player2Id}"` : ''}>
                <i class="bi bi-flag"></i> Resultado
           </button>`;
    } else if (estado === 'awaiting') {
        btnResult = '<span class="status-pill prep" style="font-size:0.75rem;"><i class="bi bi-hourglass-split"></i> Aguardando o adversário</span>';
    } else {
        btnResult = '<span class="status-pill prep" style="font-size:0.75rem;"><i class="bi bi-hourglass-split"></i> Em andamento</span>';
    }

    // Código do DCGO: aparece só para quem é dono do slot (o backend já filtra por usuário).
    const rc = myCodes.get(m.id);
    const btnCodigo = (rc && !m.isPlayed && !m.isBye)
        ? `<button class="btn btn-ghost btn-sm btn-report-code" data-code="${escapeHtml(rc.code)}"
                   title="Cole este código no DCGO para reportar o resultado desta partida">
             <i class="bi bi-key"></i> ${escapeHtml(rc.formatted || rc.code)}
           </button>`
        : '';

    // Partida encerrada mostra o placar em games no lugar do "VS". Partidas antigas, registradas
    // antes de existir placar, continuam com "VS" — melhor que inventar um 2x0 que ninguém digitou.
    const temPlacar = m.isPlayed && !m.isBye
        && m.player1GameWins !== null && m.player1GameWins !== undefined
        && m.player2GameWins !== null && m.player2GameWins !== undefined;
    const centro = temPlacar
        ? `<span class="vs-badge score" title="Placar em games (melhor de 3)">${m.player1GameWins} x ${m.player2GameWins}</span>`
        : '<span class="vs-badge">VS</span>';

    return `
        <div class="swiss-match">
            <div class="player-slot ${p1Class}">${p1Name}</div>
            ${centro}
            <div class="player-slot ${p2Class}">${p2Name}</div>
            <div class="ms-auto d-flex align-items-center gap-2">${btnCodigo}${btnResult}</div>
        </div>`;
}

// ── Top Cut ───────────────────────────────────────────────────────────────────

function renderTopCut(status) {
    const section = document.getElementById('topCutSection');
    if (status.format === 2 || !status.topCutGenerated) { section.style.display = 'none'; return; }

    section.style.display = '';
    document.getElementById('topCutLink').href = `/tournament-double-bracket.html?id=${tournamentId}`;

    const topPlayers = (status.standings || []).slice(0, status.topCutSize);
    document.getElementById('topCutInfo').innerHTML = topPlayers.map((s, i) =>
        `<span class="me-3"><strong>#${i + 1}</strong> ${escapeHtml(s.playerName)} <span class="text-muted-2">(${s.points} pts)</span></span>`
    ).join('');
}

// ── Botões admin ──────────────────────────────────────────────────────────────

function updateAdminButtons(status) {
    const btnAdvance  = document.getElementById('btnAdvance');
    const btnTopCut   = document.getElementById('btnTopCut');
    const btnFinish   = document.getElementById('btnFinish');
    const btnEndEarly = document.getElementById('btnEndEarly');

    // Só admin vê botões de ação
    const user = typeof authUser === 'function' ? authUser() : null;
    if (!user || user.role !== 'Admin') {
        btnAdvance.style.display  = 'none';
        btnTopCut.style.display   = 'none';
        btnFinish.style.display   = 'none';
        btnEndEarly.style.display = 'none';
        return;
    }

    const isPure       = status.format === 2;
    const isRoundRobin = status.format === 3;
    // Todos contra todos não tem rodada para avançar; o Top Cut libera quando todas as
    // partidas estiverem registradas (allSwissDone já exige isso, pois é uma rodada só).
    const canAdvance = !isRoundRobin && status.currentRoundDone && !status.allSwissDone && !status.topCutGenerated;
    const canTopCut  = !isPure && status.allSwissDone && !status.topCutGenerated;
    const canFinish  = isPure && status.allSwissDone && status.status !== 2;

    // Encerramento antecipado: só faz sentido enquanto a fase de pontos está rolando,
    // em formatos que têm Top Cut, e antes de o corte já ter sido feito.
    const canEndEarly = !isPure && !status.allSwissDone && !status.topCutGenerated
        && status.status !== 2 && temPartidaJogada(status);

    btnAdvance.style.display  = canAdvance  ? '' : 'none';
    btnTopCut.style.display   = canTopCut   ? '' : 'none';
    btnFinish.style.display   = canFinish   ? '' : 'none';
    btnEndEarly.style.display = canEndEarly ? '' : 'none';

    if (canAdvance) {
        btnAdvance.innerHTML = `<i class="bi bi-arrow-right-circle"></i> Avançar para Rodada ${status.currentSwissRound + 1}`;
    }
}

// Conta partidas da fase de pontos por situação (usado pelo encerramento antecipado)
function contarPartidas(status) {
    const todas = Object.values(status.swissMatchesByRound || {}).flat();
    return { total: todas.length, jogadas: todas.filter(m => m.isPlayed).length };
}

function temPartidaJogada(status) {
    return contarPartidas(status).jogadas > 0;
}

document.getElementById('btnEndEarly').addEventListener('click', async () => {
    const { total, jogadas } = contarPartidas(statusCache || {});
    const pendentes = total - jogadas;

    const confirmacao = await confirmAction({
        title: 'Encerrar a fase de pontos agora?',
        text: pendentes > 0
            ? `O Top ${statusCache.topCutSize} será definido pela classificação atual e ${pendentes} partida(s) ainda não jogada(s) serão descartadas. Não dá para voltar atrás.`
            : `O Top ${statusCache.topCutSize} será definido pela classificação atual. Não dá para voltar atrás.`,
        confirmText: 'Encerrar e cortar', cancelText: 'Cancelar', icon: 'warning',
    });
    if (!confirmacao.isConfirmed) return;

    try {
        const resp = await apiFetch(`${API_BASE_URL}/tournament/${tournamentId}/swiss/generate-topcut?force=true`, { method: 'POST' });
        const json = await resp.json();
        await Swal.fire({ icon: 'success', title: 'Fase encerrada!', text: json.message, timer: 1800, showConfirmButton: false });
        loadAll();
    } catch (err) {
        notifyError('Não foi possível encerrar a fase: ' + err.message);
    }
});

// ── Modal resultado ───────────────────────────────────────────────────────────

function openResultModal(matchId, p1Id, p2Id) {
    currentMatchId = matchId;
    currentP1Id    = p1Id;
    currentP2Id    = p2Id;

    // Limpa o resumo de conflito de uma abertura anterior (openConflictModal reinsere).
    document.getElementById('conflictInfo')?.remove();

    const sel = document.getElementById('winnerSelect');
    sel.innerHTML = '<option value="">Selecione o vencedor…</option>';
    [p1Id, p2Id].forEach(tpId => {
        if (!tpId) return;
        const p    = participantsMap.get(tpId);
        const name = p ? (p.playerName || 'Desconhecido') : 'Desconhecido';
        const deck = p ? (p.deck || 'Sorteio pendente') : 'Sem deck';
        sel.innerHTML += `<option value="${tpId}">${escapeHtml(name)} (${escapeHtml(deck)})</option>`;
    });
    if (p1Id && p2Id) {
        sel.innerHTML += '<option value="0">Empate</option>';
    }
    sel.value = '';
    updateScoreOptions();
    new bootstrap.Modal(document.getElementById('resultModal')).show();
}

// No empate o placar da melhor de 3 é sempre 1x1, então o seletor vira informativo.
function updateScoreOptions() {
    const isDraw = document.getElementById('winnerSelect').value === '0';
    const score = document.getElementById('scoreSelect');
    score.innerHTML = isDraw
        ? '<option value="1-1">1 x 1</option>'
        : '<option value="2-0">2 x 0</option><option value="2-1">2 x 1</option>';
    score.disabled = isDraw;
}

document.getElementById('winnerSelect').addEventListener('change', updateScoreOptions);

document.getElementById('saveResultBtn').addEventListener('click', async () => {
    const winnerId = document.getElementById('winnerSelect').value;
    if (winnerId === '') { notifyWarning('Selecione o vencedor (ou empate) antes de salvar.'); return; }

    const isDraw = parseInt(winnerId) === 0;
    const loserId = !isDraw && currentP1Id && currentP2Id
        ? (parseInt(winnerId) === currentP1Id ? currentP2Id : currentP1Id)
        : null;

    const [winnerGames, loserGames] = document.getElementById('scoreSelect').value.split('-').map(Number);

    try {
        await apiFetch(`${API_BASE_URL}/tournamentmatch/${currentMatchId}/result`, {
            method: 'POST',
            body: JSON.stringify({ winnerId: parseInt(winnerId), loserId, winnerGames, loserGames }),
        });
        bootstrap.Modal.getInstance(document.getElementById('resultModal')).hide();
        notifySuccess('Resultado registrado!');
        setTimeout(loadAll, 500);
    } catch (err) {
        notifyError('Erro ao registrar resultado: ' + err.message);
    }
});

// ── Botão avançar rodada ──────────────────────────────────────────────────────

document.getElementById('btnAdvance').addEventListener('click', async () => {
    const confirm = await confirmAction({
        title: 'Avançar rodada?',
        text: `Confirmar avanço para a Rodada ${(statusCache?.currentSwissRound || 0) + 1}?`,
        confirmText: 'Avançar',
        cancelText: 'Cancelar',
        icon: 'question',
    });
    if (!confirm.isConfirmed) return;
    try {
        const resp = await apiFetch(`${API_BASE_URL}/tournament/${tournamentId}/swiss/advance`, { method: 'POST' });
        const json = await resp.json();
        notifySuccess(json.message);
        loadAll();
    } catch (err) {
        notifyError('Erro ao avançar rodada: ' + err.message);
    }
});

// ── Botão gerar top cut ───────────────────────────────────────────────────────

document.getElementById('btnTopCut').addEventListener('click', async () => {
    const topN = statusCache?.topCutSize || 8;
    const confirm = await confirmAction({
        title: `Gerar Top ${topN}?`,
        text: `Os ${topN} melhores jogadores avançarão ao bracket de dupla eliminação.`,
        confirmText: 'Gerar Top Cut',
        cancelText: 'Cancelar',
        icon: 'question',
    });
    if (!confirm.isConfirmed) return;
    try {
        await apiFetch(`${API_BASE_URL}/tournament/${tournamentId}/swiss/generate-topcut`, { method: 'POST' });
        await Swal.fire({ icon: 'success', title: 'Top Cut gerado!', text: 'O bracket de dupla eliminação está pronto.', timer: 1800, showConfirmButton: false });
        loadAll();
    } catch (err) {
        notifyError('Erro ao gerar Top Cut: ' + err.message);
    }
});

// ── Botão encerrar torneio (Swiss Pontos Corridos) ────────────────────────────

document.getElementById('btnFinish').addEventListener('click', async () => {
    const confirm = await confirmAction({
        title: 'Encerrar torneio?',
        text: 'O torneio será finalizado com a classificação atual. Esta ação não pode ser desfeita.',
        confirmText: 'Encerrar', cancelText: 'Cancelar', icon: 'warning',
    });
    if (!confirm.isConfirmed) return;
    try {
        await apiFetch(`${API_BASE_URL}/tournament/${tournamentId}/swiss/finish`, { method: 'POST' });
        await Swal.fire({ icon: 'success', title: 'Torneio encerrado!', timer: 1600, showConfirmButton: false });
        loadAll();
    } catch (err) {
        notifyError('Erro ao encerrar torneio: ' + err.message);
    }
});

// ── Init ──────────────────────────────────────────────────────────────────────

loadAll();
