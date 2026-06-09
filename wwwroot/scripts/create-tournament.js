/* ========== Criar Torneio ========== */

let allPlayers = [];

async function loadPlayers() {
    try {
        const response = await apiFetch(`${API_BASE_URL}/player`);
        allPlayers = await response.json();
        updatePlayerCount();
    } catch (error) {
        notifyError('Não foi possível carregar a lista de jogadores: ' + error.message);
    }
}

function getMaxPlayers() {
    return parseInt(document.getElementById('maxPlayers').value, 10);
}

function buildPlayerOptions(selectedId) {
    const usedIds = getSelectedPlayerIds().filter(id => id !== selectedId);
    const options = ['<option value="">Selecione o jogador</option>'];
    allPlayers.forEach(p => {
        const disabled = usedIds.includes(String(p.id)) ? 'disabled' : '';
        const selected = String(p.id) === String(selectedId) ? 'selected' : '';
        options.push(`<option value="${p.id}" ${disabled} ${selected}>${escapeHtml(p.name)}</option>`);
    });
    return options.join('');
}

function getSelectedPlayerIds() {
    return Array.from(document.querySelectorAll('.player-select')).map(s => s.value).filter(Boolean);
}

function refreshAllSelects() {
    document.querySelectorAll('.player-select').forEach(select => {
        const current = select.value;
        select.innerHTML = buildPlayerOptions(current);
        select.value = current;
    });
    updatePlayerCount();
}

function updatePlayerCount() {
    const filledCount = Array.from(document.querySelectorAll('.player-select'))
        .filter(s => s.value).length;
    const max = getMaxPlayers();
    const pill = document.getElementById('playerCountPill');
    pill.textContent = `${filledCount} / ${max} jogadores`;

    const addBtn = document.getElementById('addPlayerBtn');
    const rowCount = document.querySelectorAll('.player-row').length;

    if (filledCount === max) {
        pill.className = 'status-pill live';
    } else {
        pill.className = 'status-pill prep';
    }

    // Bloqueia botão de adicionar quando atingir o limite
    addBtn.disabled = rowCount >= max;
}

function addPlayerRow() {
    const max = getMaxPlayers();
    const rowCount = document.querySelectorAll('.player-row').length;
    if (rowCount >= max) return;

    const container = document.getElementById('playersContainer');
    const row = document.createElement('div');
    row.className = 'player-row';
    row.innerHTML = `
        <div class="row g-2 align-items-end">
            <div class="col-md-6">
                <label class="form-label">Jogador</label>
                <select class="form-select player-select" required>
                    ${buildPlayerOptions('')}
                </select>
            </div>
            <div class="col-md-5">
                <label class="form-label">Deck</label>
                <input type="text" class="form-control deck-input" placeholder="Nome do Deck" required>
            </div>
            <div class="col-md-1 d-grid">
                <button type="button" class="btn btn-danger remove-row" title="Remover">
                    <i class="bi bi-x-lg"></i>
                </button>
            </div>
        </div>`;
    container.appendChild(row);

    row.querySelector('.player-select').addEventListener('change', refreshAllSelects);
    row.querySelector('.remove-row').addEventListener('click', () => {
        row.remove();
        refreshAllSelects();
    });
    updatePlayerCount();
}

document.getElementById('addPlayerBtn').addEventListener('click', addPlayerRow);

document.getElementById('maxPlayers').addEventListener('change', () => {
    // Remove linhas excedentes ao reduzir o número de vagas
    const max = getMaxPlayers();
    const rows = Array.from(document.querySelectorAll('.player-row'));
    if (rows.length > max) {
        rows.slice(max).forEach(r => r.remove());
        refreshAllSelects();
    }
    updatePlayerCount();
});

document.getElementById('createTournamentForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    const name = document.getElementById('name').value.trim();
    const startDate = document.getElementById('startDate').value;
    const maxPlayers = getMaxPlayers();

    if (!name) {
        notifyWarning('Informe o nome do torneio.');
        return;
    }
    if (!startDate) {
        notifyWarning('Informe a data de início.');
        return;
    }

    const rows = document.querySelectorAll('.player-row');
    const players = [];
    const seenIds = new Set();
    let hasIncomplete = false;

    for (const row of rows) {
        const playerId = row.querySelector('.player-select').value;
        const deck = row.querySelector('.deck-input').value.trim();
        if (!playerId && !deck) continue;
        if (!playerId || !deck) { hasIncomplete = true; continue; }
        if (seenIds.has(playerId)) {
            const dupName = allPlayers.find(p => String(p.id) === playerId)?.name || 'Jogador';
            notifyError(`${dupName} foi selecionado mais de uma vez. Cada jogador pode participar apenas uma vez por torneio.`, 'Jogador duplicado');
            return;
        }
        seenIds.add(playerId);
        players.push({ playerId: parseInt(playerId), deck });
    }

    if (hasIncomplete) {
        notifyWarning('Preencha jogador e deck em todas as linhas (ou remova as linhas vazias).');
        return;
    }

    if (players.length > maxPlayers) {
        notifyWarning(`Você adicionou ${players.length} jogadores, mas o limite é ${maxPlayers}.`);
        return;
    }

    try {
        const response = await apiFetch(`${API_BASE_URL}/tournament`, {
            method: 'POST',
            body: JSON.stringify({ name, startDate, maxPlayers, players }),
        });
        const result = await response.json();
        await Swal.fire({
            icon: 'success',
            title: 'Torneio criado!',
            text: players.length === 0
                ? `Torneio criado com ${maxPlayers} vagas. Use o link de convite para inscrever os participantes.`
                : 'Vamos para a tela de configuração.',
            timer: 1800,
            showConfirmButton: false,
        });
        window.location.href = `/tournament-setup.html?id=${result.id}`;
    } catch (error) {
        notifyError(`Erro ao criar torneio: ${error.message}`);
    }
});

loadPlayers();
