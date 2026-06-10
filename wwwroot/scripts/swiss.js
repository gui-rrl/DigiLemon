/* ========== Swiss + Top Cut ========== */

const urlParams     = new URLSearchParams(window.location.search);
const tournamentId  = urlParams.get('id');
let participantsMap = new Map();   // tpId → { playerName, deck, playerId, isGuest }
let statusCache     = null;
let currentMatchId  = null;
let currentP1Id     = null;
let currentP2Id     = null;

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

        // Título
        const t = await apiFetch(`${API_BASE_URL}/tournament/${tournamentId}`).then(r => r.json());
        document.getElementById('tournamentTitle').textContent = t.name;
        document.title = `${t.name} — Swiss`;
        document.getElementById('tournamentSubtitle').textContent =
            `Swiss ${statusCache.currentSwissRound}/${statusCache.swissRounds} · Top ${statusCache.topCutSize} · ${parts.length} jogadores`;

        renderStandings(statusCache.standings, statusCache.topCutSize);
        renderRounds(statusCache.swissMatchesByRound);
        renderTopCut(statusCache);
        updateAdminButtons(statusCache);

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

    const rows = standings.map((s, idx) => {
        const posClass = s.position === 1 ? 'gold' : s.position === 2 ? 'silver' : s.position === 3 ? 'bronze' : '';
        const isTopCutLine = s.position === topCutSize;
        return `<tr class="${isTopCutLine ? 'topcut-line' : ''}">
            <td><span class="pos-badge ${posClass}">${s.position}</span></td>
            <td>
                <div style="font-weight:600;line-height:1.2;">${escapeHtml(s.playerName)}</div>
                <div class="text-muted-2" style="font-size:0.75rem;">${escapeHtml(s.deck || '')}</div>
            </td>
            <td style="font-weight:700;color:var(--accent);text-align:center;">${s.points}</td>
            <td style="text-align:center;color:var(--text-2);">${s.wins}-${s.losses}</td>
            <td style="text-align:right;color:var(--text-3);font-size:0.78rem;">${s.omw}%</td>
        </tr>`;
    }).join('');

    container.innerHTML = `
        <table class="standings-table">
            <thead>
                <tr>
                    <th style="width:34px;">#</th>
                    <th>Jogador</th>
                    <th style="text-align:center;" title="Pontos">Pts</th>
                    <th style="text-align:center;" title="Vitórias-Derrotas">V-D</th>
                    <th style="text-align:right;" title="Opponent Match Win %">OMW%</th>
                </tr>
            </thead>
            <tbody>${rows}</tbody>
        </table>
        <div class="px-3 py-2" style="font-size:0.75rem;color:var(--text-3);">
            <i class="bi bi-dash-lg me-1" style="color:var(--accent);"></i>Linha pontilhada = corte para Top ${topCutSize}
        </div>`;
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

    const rounds = Object.keys(matchesByRound).map(Number).sort((a, b) => b - a); // mais recente primeiro
    container.innerHTML = rounds.map(round => {
        const matches = matchesByRound[round];
        const allDone = matches.every(m => m.isPlayed);
        const matchRows = matches.map(m => renderMatchRow(m, round)).join('');
        return `
            <div class="swiss-round-card">
                <div class="round-header">
                    <span><i class="bi bi-collection me-2"></i>Rodada ${round}</span>
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
}

function renderMatchRow(m, round) {
    const p1Name = playerName(m.player1Id);
    const p2Name = m.isBye ? '<em class="text-muted-2">BYE</em>' : playerName(m.player2Id);

    let p1Class = '', p2Class = '';
    if (m.isPlayed && m.winnerId) {
        p1Class = m.winnerId === m.player1Id ? 'winner' : 'loser';
        p2Class = m.winnerId === m.player2Id ? 'winner' : (m.player2Id ? 'loser' : '');
    }

    const btnResult = !m.isPlayed && !m.isBye
        ? `<button class="btn btn-primary btn-sm btn-result"
                data-match-id="${m.id}"
                data-p1="${m.player1Id}"
                ${m.player2Id ? `data-p2="${m.player2Id}"` : ''}>
                <i class="bi bi-flag"></i> Resultado
           </button>`
        : m.isBye
            ? '<span class="badge bg-secondary">BYE automático</span>'
            : '<span class="status-pill live" style="font-size:0.75rem;"><i class="bi bi-check2-circle"></i> Finalizada</span>';

    return `
        <div class="swiss-match">
            <div class="player-slot ${p1Class}">${p1Name}</div>
            <span class="vs-badge">VS</span>
            <div class="player-slot ${p2Class}">${p2Name}</div>
            <div class="ms-auto">${btnResult}</div>
        </div>`;
}

// ── Top Cut ───────────────────────────────────────────────────────────────────

function renderTopCut(status) {
    const section = document.getElementById('topCutSection');
    if (!status.topCutGenerated) { section.style.display = 'none'; return; }

    section.style.display = '';
    document.getElementById('topCutLink').href = `/tournament-double-bracket.html?id=${tournamentId}`;

    const topPlayers = (status.standings || []).slice(0, status.topCutSize);
    document.getElementById('topCutInfo').innerHTML = topPlayers.map((s, i) =>
        `<span class="me-3"><strong>#${i + 1}</strong> ${escapeHtml(s.playerName)} <span class="text-muted-2">(${s.points} pts)</span></span>`
    ).join('');
}

// ── Botões admin ──────────────────────────────────────────────────────────────

function updateAdminButtons(status) {
    const btnAdvance = document.getElementById('btnAdvance');
    const btnTopCut  = document.getElementById('btnTopCut');

    // Só admin vê botões de ação
    const user = typeof authUser === 'function' ? authUser() : null;
    if (!user || user.role !== 'Admin') {
        btnAdvance.style.display = 'none';
        btnTopCut.style.display  = 'none';
        return;
    }

    const canAdvance = status.currentRoundDone
        && !status.allSwissDone
        && !status.topCutGenerated;

    const canTopCut  = status.allSwissDone && !status.topCutGenerated;

    btnAdvance.style.display = canAdvance ? '' : 'none';
    btnTopCut.style.display  = canTopCut  ? '' : 'none';

    if (canAdvance) {
        btnAdvance.textContent = '';
        btnAdvance.innerHTML   = `<i class="bi bi-arrow-right-circle"></i> Avançar para Rodada ${status.currentSwissRound + 1}`;
    }
}

// ── Modal resultado ───────────────────────────────────────────────────────────

function openResultModal(matchId, p1Id, p2Id) {
    currentMatchId = matchId;
    currentP1Id    = p1Id;
    currentP2Id    = p2Id;

    const sel = document.getElementById('winnerSelect');
    sel.innerHTML = '<option value="">Selecione o vencedor…</option>';
    [p1Id, p2Id].forEach(tpId => {
        if (!tpId) return;
        const p    = participantsMap.get(tpId);
        const name = p ? (p.playerName || 'Desconhecido') : 'Desconhecido';
        const deck = p ? (p.deck || 'Sem deck') : 'Sem deck';
        sel.innerHTML += `<option value="${tpId}">${escapeHtml(name)} (${escapeHtml(deck)})</option>`;
    });
    new bootstrap.Modal(document.getElementById('resultModal')).show();
}

document.getElementById('saveResultBtn').addEventListener('click', async () => {
    const winnerId = document.getElementById('winnerSelect').value;
    if (!winnerId) { notifyWarning('Selecione o vencedor antes de salvar.'); return; }

    const loserId = currentP1Id && currentP2Id
        ? (parseInt(winnerId) === currentP1Id ? currentP2Id : currentP1Id)
        : null;

    try {
        await apiFetch(`${API_BASE_URL}/tournamentmatch/${currentMatchId}/result`, {
            method: 'POST',
            body: JSON.stringify({ winnerId: parseInt(winnerId), loserId }),
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

// ── Init ──────────────────────────────────────────────────────────────────────

loadAll();
