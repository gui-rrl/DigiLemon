/* ========== Bracket - Dupla Eliminação ========== */

// participantsMap: TournamentPlayer.Id → { playerName, deck, playerId, isGuest }
let participantsMap = new Map();
let allMatchesCache = [];

async function loadTournamentParticipants(tournamentId) {
    const response = await apiFetch(`${API_BASE_URL}/tournament/${tournamentId}/participants`);
    return await response.json();
}

async function loadTournamentMatches(tournamentId) {
    const response = await apiFetch(`${API_BASE_URL}/tournament/${tournamentId}/matches`);
    return await response.json();
}

function groupMatchesByTypeAndRound(matches) {
    const upper = new Map();
    const lower = new Map();
    const finals = [];
    matches.forEach(match => {
        if (match.matchType === 0) {
            if (!upper.has(match.round)) upper.set(match.round, []);
            upper.get(match.round).push(match);
        } else if (match.matchType === 1) {
            if (!lower.has(match.round)) lower.set(match.round, []);
            lower.get(match.round).push(match);
        } else if (match.matchType === 2) {
            finals.push(match);
        }
    });
    return { upper, lower, finals };
}

function renderPlayerLine(tpId, isWinner, isLoser) {
    // tpId = TournamentPlayer.Id (identificador no chaveamento)
    const cls = isWinner ? 'winner' : isLoser ? 'loser' : (!tpId ? 'waiting' : '');
    if (!tpId) {
        return `<div class="player waiting"><span class="player-name">Aguardando</span></div>`;
    }
    const p    = participantsMap.get(tpId);
    const name = p ? p.playerName : 'Desconhecido';
    const deck = p ? p.deck : null;
    const link = p && p.playerId ? `/player.html?id=${p.playerId}` : null;
    const nameHtml = link
        ? `<a href="${link}" style="color:inherit;text-decoration:none;">${escapeHtml(name)}</a>`
        : escapeHtml(name);
    return `
        <div class="player ${cls}">
            <span class="player-name">${nameHtml}</span>
            ${deck ? `<span class="deck-tag" title="${escapeHtml(deck)}"><i class="bi bi-layers"></i> ${escapeHtml(deck)}</span>` : ''}
        </div>`;
}

function renderMatchCard(match) {
    let winner = null, loser = null;
    if (match.winnerId) {
        winner = match.winnerId;
        if (match.player1Id && match.player2Id) {
            loser = match.winnerId === match.player1Id ? match.player2Id : match.player1Id;
        }
    }

    return `
        <div class="match">
            ${renderPlayerLine(match.player1Id, winner === match.player1Id, loser === match.player1Id)}
            ${renderPlayerLine(match.player2Id, winner === match.player2Id, loser === match.player2Id)}
            ${!match.isPlayed && match.player1Id && match.player2Id ? `
                <button class="btn btn-primary btn-sm result-btn" data-match-id="${match.id}">
                    <i class="bi bi-flag"></i> Registrar resultado
                </button>` : ''}
            ${match.isPlayed ? `<div class="match-status"><i class="bi bi-check2-circle"></i> Finalizada</div>` : ''}
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
        const roundHtml = `
            <div class="round">
                <h5>Rodada ${round}</h5>
                ${matches.map(renderMatchCard).join('')}
            </div>`;
        container.insertAdjacentHTML('beforeend', roundHtml);
    }
}

function renderGrandFinals(finals, containerId) {
    const container = document.getElementById(containerId);
    container.innerHTML = '';
    if (!finals.length) {
        container.innerHTML = `<div class="empty-state" style="padding:1.5rem;"><div class="icon"><i class="bi bi-hourglass"></i></div><div class="title">Aguardando</div></div>`;
        return;
    }
    finals.forEach(match => container.insertAdjacentHTML('beforeend', renderMatchCard(match)));
}

/* ==================== Modal ==================== */

let currentModalMatchId = null;
let currentPlayer1Id = null;
let currentPlayer2Id = null;

function openResultModal(matchId, player1Id, player2Id) {
    currentModalMatchId = matchId;
    currentPlayer1Id = player1Id;
    currentPlayer2Id = player2Id;
    const winnerSelect = document.getElementById('winnerSelect');
    winnerSelect.innerHTML = '<option value="">Selecione o vencedor…</option>';
    [player1Id, player2Id].forEach(tpId => {
        if (!tpId) return;
        const p           = participantsMap.get(tpId);
        const displayName = p ? (p.playerName || 'Desconhecido') : 'Desconhecido';
        const deck        = p ? (p.deck || 'Sem deck') : 'Sem deck';
        winnerSelect.innerHTML += `<option value="${tpId}">${escapeHtml(displayName)} (${escapeHtml(deck)})</option>`;
    });
    new bootstrap.Modal(document.getElementById('resultModal')).show();
}

document.getElementById('saveResultBtn').addEventListener('click', async () => {
    const winnerId = document.getElementById('winnerSelect').value;
    if (!winnerId) {
        notifyWarning('Selecione o vencedor antes de salvar.');
        return;
    }
    let loserId = null;
    if (currentPlayer1Id && currentPlayer2Id) {
        loserId = (winnerId == currentPlayer1Id) ? currentPlayer2Id : currentPlayer1Id;
    }
    try {
        await apiFetch(`${API_BASE_URL}/tournamentmatch/${currentModalMatchId}/result`, {
            method: 'POST',
            body: JSON.stringify({ winnerId: parseInt(winnerId), loserId }),
        });
        notifySuccess('Resultado registrado!');
        bootstrap.Modal.getInstance(document.getElementById('resultModal')).hide();
        setTimeout(() => location.reload(), 800);
    } catch (error) {
        notifyError('Não foi possível registrar o resultado: ' + error.message);
    }
});

/* ==================== Inicialização ==================== */

async function init() {
    const urlParams = new URLSearchParams(window.location.search);
    const tournamentId = urlParams.get('id');
    if (!tournamentId) {
        notifyError('ID do torneio não informado.');
        return;
    }

    try {
        const participants = await loadTournamentParticipants(tournamentId);
        // Mapeia TournamentPlayer.Id → dados do participante
        participants.forEach(p => participantsMap.set(p.id, p));

        allMatchesCache = await loadTournamentMatches(tournamentId);

        const tournament = await apiFetch(`${API_BASE_URL}/tournament/${tournamentId}`).then(r => r.json());
        document.getElementById('tournamentTitle').innerText = tournament.name;
        document.title = `${tournament.name} — Bracket`;

        const { upper, lower, finals } = groupMatchesByTypeAndRound(allMatchesCache);
        renderBracketSection('upperBracketRoot', upper);
        renderBracketSection('lowerBracketRoot', lower);
        renderGrandFinals(finals, 'grandFinalRoot');

        document.querySelectorAll('.result-btn').forEach(btn => {
            btn.addEventListener('click', () => {
                const matchId = parseInt(btn.dataset.matchId);
                const match = allMatchesCache.find(m => m.id === matchId);
                if (match) openResultModal(matchId, match.player1Id, match.player2Id);
                else notifyError('Partida não encontrada.');
            });
        });
    } catch (error) {
        console.error(error);
        notifyError('Erro ao carregar torneio: ' + error.message);
    }
}

init();
