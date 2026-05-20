/* ========== Bracket - Visualização Genérica ========== */

let currentTournamentId;
let playersMap = new Map();
let deckMap = new Map();

async function loadTournament() {
    const urlParams = new URLSearchParams(window.location.search);
    currentTournamentId = urlParams.get('id');
    if (!currentTournamentId) {
        notifyError('ID do torneio não informado.');
        return;
    }

    try {
        const tournament = await apiFetch(`${API_BASE_URL}/tournament/${currentTournamentId}`).then(r => r.json());
        document.getElementById('tournamentTitle').innerText = tournament.name;
        document.title = `${tournament.name} — Bracket`;

        const matches = await apiFetch(`${API_BASE_URL}/tournament/${currentTournamentId}/matches`).then(r => r.json());

        const participants = await apiFetch(`${API_BASE_URL}/tournament/${currentTournamentId}/participants`).then(r => r.json());
        deckMap = new Map(participants.map(p => [p.playerId, p.deck]));

        const players = await apiFetch(`${API_BASE_URL}/player`).then(r => r.json());
        playersMap = new Map(players.map(p => [p.id, p]));

        const upperByRound = new Map();
        const lowerByRound = new Map();
        let grandFinalMatch = null;

        matches.forEach(match => {
            if (match.matchType === 0) {
                if (!upperByRound.has(match.round)) upperByRound.set(match.round, []);
                upperByRound.get(match.round).push(match);
            } else if (match.matchType === 1) {
                if (!lowerByRound.has(match.round)) lowerByRound.set(match.round, []);
                lowerByRound.get(match.round).push(match);
            } else if (match.matchType === 2) {
                grandFinalMatch = match;
            }
        });

        renderSection('upperBracketRoot', upperByRound);
        renderSection('lowerBracketRoot', lowerByRound);
        renderGrandFinal('grandFinalRoot', grandFinalMatch);
        attachResultListeners();
    } catch (error) {
        notifyError('Erro ao carregar torneio: ' + error.message);
    }
}

function renderPlayerLine(playerId, isWinner) {
    if (!playerId) return `<div class="player waiting"><span class="player-name">Aguardando</span></div>`;
    const p = playersMap.get(playerId);
    const name = p ? p.name : 'Desconhecido';
    const deck = deckMap.get(playerId);
    return `
        <div class="player ${isWinner ? 'winner' : ''}">
            <span class="player-name">${escapeHtml(name)}</span>
            ${deck ? `<span class="deck-tag" title="${escapeHtml(deck)}"><i class="bi bi-layers"></i> ${escapeHtml(deck)}</span>` : ''}
        </div>`;
}

function renderMatchCard(match) {
    const w1 = match.winnerId === match.player1Id && match.isPlayed;
    const w2 = match.winnerId === match.player2Id && match.isPlayed;
    return `
        <div class="match">
            ${renderPlayerLine(match.player1Id, w1)}
            ${renderPlayerLine(match.player2Id, w2)}
            ${!match.isPlayed && match.player1Id && match.player2Id ? `
                <button class="btn btn-primary btn-sm result-btn" data-match-id="${match.id}">
                    <i class="bi bi-flag"></i> Registrar resultado
                </button>` : ''}
            ${match.isPlayed ? `<div class="match-status"><i class="bi bi-check2-circle"></i> Finalizada</div>` : ''}
        </div>`;
}

function renderSection(containerId, matchesByRound) {
    const container = document.getElementById(containerId);
    if (!container) return;
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

function renderGrandFinal(containerId, match) {
    const container = document.getElementById(containerId);
    if (!container) return;
    if (!match) {
        container.innerHTML = `<div class="empty-state" style="padding:1.5rem;"><div class="icon"><i class="bi bi-hourglass"></i></div><div class="title">Aguardando</div></div>`;
        return;
    }
    container.innerHTML = renderMatchCard(match);
}

function attachResultListeners() {
    document.querySelectorAll('.result-btn').forEach(btn => {
        btn.addEventListener('click', handleResultClick);
    });
}

let currentMatchId = null;

async function handleResultClick(e) {
    currentMatchId = parseInt(e.currentTarget.dataset.matchId);
    try {
        const match = await apiFetch(`${API_BASE_URL}/tournamentmatch/${currentMatchId}`).then(r => r.json());
        const winnerSelect = document.getElementById('winnerSelect');
        winnerSelect.innerHTML = '<option value="">Selecione o vencedor…</option>';
        [match.player1Id, match.player2Id].forEach(id => {
            if (!id) return;
            const p = playersMap.get(id);
            if (p) winnerSelect.innerHTML += `<option value="${id}">${escapeHtml(p.name)}</option>`;
        });
        new bootstrap.Modal(document.getElementById('resultModal')).show();
    } catch (err) {
        notifyError('Erro ao carregar partida: ' + err.message);
    }
}

document.getElementById('saveResultBtn').addEventListener('click', async () => {
    const winnerId = document.getElementById('winnerSelect').value;
    if (!winnerId) {
        notifyWarning('Selecione o vencedor antes de salvar.');
        return;
    }
    try {
        await apiFetch(`${API_BASE_URL}/tournamentmatch/${currentMatchId}/result`, {
            method: 'POST',
            body: JSON.stringify({ winnerId: parseInt(winnerId) }),
        });
        notifySuccess('Resultado registrado!');
        bootstrap.Modal.getInstance(document.getElementById('resultModal')).hide();
        setTimeout(() => loadTournament(), 600);
    } catch (error) {
        notifyError('Erro ao registrar resultado: ' + error.message);
    }
});

loadTournament();
