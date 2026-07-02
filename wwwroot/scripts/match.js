/* ========== Página de Partidas ========== */

let currentFilters = { playerId: '', startDate: '', endDate: '', deck: '' };
let playersMap = new Map();
let lastLoadedMatches = [];

async function loadPlayersForFilter() {
    try {
        const response = await apiFetch(`${API_BASE_URL}/player`);
        const players = await response.json();
        const filterSelect = document.getElementById('filterPlayer');
        filterSelect.innerHTML = '<option value="">Todos</option>';
        players.forEach(p => {
            filterSelect.innerHTML += `<option value="${p.id}">${escapeHtml(p.name)}</option>`;
        });
    } catch (error) {
        console.error(error);
    }
}

async function loadPlayers() {
    try {
        const response = await apiFetch(`${API_BASE_URL}/player`);
        const players = await response.json();
        playersMap = new Map(players.map(p => [p.id, p]));
        const player1Select = document.getElementById('player1');
        const player2Select = document.getElementById('player2');
        player1Select.innerHTML = '<option value="">Selecione…</option>';
        player2Select.innerHTML = '<option value="">Selecione…</option>';
        players.forEach(p => {
            player1Select.innerHTML += `<option value="${p.id}">${escapeHtml(p.name)}</option>`;
            player2Select.innerHTML += `<option value="${p.id}">${escapeHtml(p.name)}</option>`;
        });
    } catch (error) {
        console.error(error);
    }
}

async function loadMatches() {
    const tbody = document.getElementById('matchesTable');
    try {
        const params = new URLSearchParams();
        if (currentFilters.playerId) params.append('playerId', currentFilters.playerId);
        if (currentFilters.startDate) params.append('startDate', currentFilters.startDate);
        if (currentFilters.endDate) params.append('endDate', currentFilters.endDate);
        if (currentFilters.deck) params.append('deck', currentFilters.deck);

        const url = `${API_BASE_URL}/matches${params.toString() ? '?' + params.toString() : ''}`;
        const response = await apiFetch(url);
        const matches = await response.json();
        lastLoadedMatches = matches;

        if (!matches.length) {
            tbody.innerHTML = `
                <tr class="empty-row">
                    <td colspan="6">
                        <div class="empty-state">
                            <div class="icon"><i class="bi bi-inbox"></i></div>
                            <div class="title">Nenhuma partida encontrada</div>
                            <div>Ajuste os filtros ou registre uma nova partida.</div>
                        </div>
                    </td>
                </tr>`;
            return;
        }

        tbody.innerHTML = matches.map(match => {
            const p1 = playersMap.get(match.player1Id);
            const p2 = playersMap.get(match.player2Id);
            const p1Name = p1 ? p1.name : `#${match.player1Id}`;
            const p2Name = p2 ? p2.name : `#${match.player2Id}`;

            let resultHtml = '';
            if (match.winnerId === 0) {
                resultHtml = `<span class="status-pill prep"><i class="bi bi-handshake"></i> Empate</span>`;
            } else if (match.winnerId === match.player1Id) {
                resultHtml = `<span class="status-pill live"><i class="bi bi-trophy"></i> ${escapeHtml(p1Name)}</span>`;
            } else {
                resultHtml = `<span class="status-pill live"><i class="bi bi-trophy"></i> ${escapeHtml(p2Name)}</span>`;
            }

            return `
                <tr>
                    <td>
                        <a href="/player.html?id=${match.player1Id}" class="player-cell" style="text-decoration:none;">
                            ${p1?.avatarUrl ? `<img src="${escapeHtml(p1.avatarUrl)}" class="avatar avatar-img" alt="${escapeHtml(p1Name)}">` : `<span class="avatar">${getInitials(p1Name)}</span>`}
                            <span style="font-weight:600; color: var(--text-1);">${escapeHtml(p1Name)}</span>
                        </a>
                    </td>
                    <td>${match.deck1 ? `<span style="font-size:0.78rem; padding:0.25rem 0.55rem; background: rgba(var(--surface-rgb),0.06); border-radius:6px;"><i class="bi bi-layers"></i> ${escapeHtml(match.deck1)}</span>` : '<span class="text-muted-2">-</span>'}</td>
                    <td>
                        <a href="/player.html?id=${match.player2Id}" class="player-cell" style="text-decoration:none;">
                            ${p2?.avatarUrl ? `<img src="${escapeHtml(p2.avatarUrl)}" class="avatar avatar-img" alt="${escapeHtml(p2Name)}">` : `<span class="avatar">${getInitials(p2Name)}</span>`}
                            <span style="font-weight:600; color: var(--text-1);">${escapeHtml(p2Name)}</span>
                        </a>
                    </td>
                    <td>${match.deck2 ? `<span style="font-size:0.78rem; padding:0.25rem 0.55rem; background: rgba(var(--surface-rgb),0.06); border-radius:6px;"><i class="bi bi-layers"></i> ${escapeHtml(match.deck2)}</span>` : '<span class="text-muted-2">-</span>'}</td>
                    <td>${resultHtml}</td>
                    <td><span class="text-muted-2" style="font-size:0.85rem;">${formatDateTime(match.date)}</span></td>
                </tr>`;
        }).join('');
    } catch (error) {
        console.error(error);
        tbody.innerHTML = `<tr class="empty-row"><td colspan="6"><div class="empty-state"><div class="icon"><i class="bi bi-exclamation-octagon"></i></div><div class="title">Erro ao carregar histórico</div><div>${escapeHtml(error.message)}</div></div></td></tr>`;
    }
}

async function suggestDeckForPlayer(playerSelectId, deckInputId) {
    const playerId = document.getElementById(playerSelectId).value;
    const input = document.getElementById(deckInputId);
    if (!playerId) {
        input.placeholder = 'Ex.: Aggro Burn';
        return;
    }
    // Não sobrescreve se já tem texto digitado
    if (input.value.trim()) return;
    try {
        const response = await apiFetch(`${API_BASE_URL}/player/${playerId}/last-deck`);
        const data = await response.json();
        if (data.deck) {
            input.placeholder = `Último deck: ${data.deck}`;
            input.dataset.suggested = data.deck;
        } else {
            input.placeholder = 'Ex.: Aggro Burn';
            delete input.dataset.suggested;
        }
    } catch (_) { /* ignora silenciosamente */ }
}

function applyDeckSuggestionIfEmpty(deckInputId) {
    const input = document.getElementById(deckInputId);
    if (!input.value.trim() && input.dataset.suggested) {
        input.value = input.dataset.suggested;
    }
}

async function registerMatch(winnerCode) {
    const player1Id = document.getElementById('player1').value;
    const player2Id = document.getElementById('player2').value;
    // Aplica sugestão de deck se o campo estiver vazio
    applyDeckSuggestionIfEmpty('deck1');
    applyDeckSuggestionIfEmpty('deck2');
    const deck1 = document.getElementById('deck1').value.trim();
    const deck2 = document.getElementById('deck2').value.trim();

    if (!player1Id || !player2Id) {
        notifyWarning('Selecione os dois jogadores.');
        return;
    }
    if (player1Id === player2Id) {
        notifyError('Os jogadores precisam ser diferentes.', 'Seleção inválida');
        return;
    }
    if (!deck1 || !deck2) {
        notifyWarning('Informe os decks de ambos os jogadores.');
        return;
    }

    let winnerId;
    if (winnerCode === 0) winnerId = 0;
    else if (winnerCode === 1) winnerId = parseInt(player1Id);
    else winnerId = parseInt(player2Id);

    const matchData = {
        player1Id: parseInt(player1Id),
        player2Id: parseInt(player2Id),
        winnerId,
        deck1,
        deck2,
        date: new Date().toISOString(),
    };

    try {
        await apiFetch(`${API_BASE_URL}/matches`, {
            method: 'POST',
            body: JSON.stringify(matchData),
        });
        notifySuccess('Partida registrada!');
        document.getElementById('deck1').value = '';
        document.getElementById('deck2').value = '';
        loadMatches();
        loadPlayers();
    } catch (error) {
        notifyError('Erro ao registrar partida: ' + error.message);
    }
}

function applyFilters() {
    currentFilters.playerId = document.getElementById('filterPlayer').value;
    currentFilters.startDate = document.getElementById('filterStartDate').value;
    currentFilters.endDate = document.getElementById('filterEndDate').value;
    currentFilters.deck = document.getElementById('filterDeck').value.trim();
    loadMatches();
}

function clearFilters() {
    document.getElementById('filterPlayer').value = '';
    document.getElementById('filterStartDate').value = '';
    document.getElementById('filterEndDate').value = '';
    document.getElementById('filterDeck').value = '';
    currentFilters = { playerId: '', startDate: '', endDate: '', deck: '' };
    loadMatches();
}

function exportMatches() {
    if (!lastLoadedMatches.length) {
        notifyWarning('Nenhuma partida para exportar. Aplique os filtros desejados e tente novamente.');
        return;
    }
    const rows = lastLoadedMatches.map(m => {
        const p1Name = playersMap.get(m.player1Id)?.name || `#${m.player1Id}`;
        const p2Name = playersMap.get(m.player2Id)?.name || `#${m.player2Id}`;
        let resultado;
        if (m.winnerId === 0) resultado = 'Empate';
        else resultado = m.winnerId === m.player1Id ? p1Name : p2Name;
        return {
            Data: formatDateTime(m.date),
            Jogador1: p1Name,
            Deck1: m.deck1 || '',
            Jogador2: p2Name,
            Deck2: m.deck2 || '',
            Vencedor: resultado,
        };
    });
    const today = new Date().toISOString().slice(0, 10);
    downloadCsv(`partidas-${today}.csv`, rows);
    notifySuccess('Histórico exportado!');
}

document.addEventListener('DOMContentLoaded', async () => {
    await Promise.all([loadPlayers(), loadPlayersForFilter()]);
    await loadMatches();
    document.getElementById('player1').addEventListener('change', () => suggestDeckForPlayer('player1', 'deck1'));
    document.getElementById('player2').addEventListener('change', () => suggestDeckForPlayer('player2', 'deck2'));
    document.getElementById('exportMatchesBtn').addEventListener('click', exportMatches);
    window.registerMatch = registerMatch;
    window.applyFilters = applyFilters;
    window.clearFilters = clearFilters;
});
