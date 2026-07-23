/* ========== Criar Torneio ========== */

let allPlayers = [];

// ── Seletor de formato ──────────────────────────────────────────────────────
function selectFormat(fmt) {
    document.getElementById('format').value = fmt;
    document.getElementById('fmtDoubleElim').classList.toggle('selected', fmt === 0);
    document.getElementById('fmtSwiss').classList.toggle('selected', fmt === 1);
    document.getElementById('fmtSwissPure').classList.toggle('selected', fmt === 2);

    const swissOpts = document.getElementById('swissOptions');
    swissOpts.classList.toggle('d-none', fmt === 0);

    // Top Cut só faz sentido no formato 1
    const topCutRow = document.getElementById('topCutSizeRow');
    if (topCutRow) topCutRow.classList.toggle('d-none', fmt !== 1);

    if (fmt >= 1) updateSwissRoundsInfo();

    // Atualizar opções de vagas (Swiss permite ímpar)
    updateMaxPlayersOptions(fmt);
}

function updateSwissRoundsInfo() {
    const n = parseInt(document.getElementById('maxPlayers').value, 10);
    const rounds = n <= 2 ? 1 : n <= 4 ? 2 : n <= 8 ? 3 : n <= 16 ? 4 : n <= 32 ? 5 : 6;
    const fmt = parseInt(document.getElementById('format').value, 10);
    const info = document.getElementById('swissRoundsInfo');
    if (fmt === 2) {
        info.textContent = `Com ${n} jogadores: ${rounds} rodadas Swiss. Classificação final por pontos, Top 4 destacado.`;
    } else {
        const topCut = document.getElementById('topCutSize').value;
        info.textContent = `Com ${n} jogadores: ${rounds} rodadas Swiss → Top ${topCut} (double elimination).`;
    }
}

function updateMaxPlayersOptions(fmt) {
    const sel = document.getElementById('maxPlayers');
    const current = sel.value;
    sel.innerHTML = '';
    const options = fmt >= 1
        ? [4,5,6,7,8,9,10,11,12,14,16,18,20,24,32]
        : [6,8,10,12,14,16,18,20,24,32];
    options.forEach(n => {
        const opt = document.createElement('option');
        opt.value = n;
        opt.textContent = `${n} jogadores`;
        if (String(n) === current) opt.selected = true;
        sel.appendChild(opt);
    });
    if (!sel.value) sel.value = '8';
    if (fmt >= 1) updateSwissRoundsInfo();
}

// Registrar listeners e inicializar após DOM pronto
document.getElementById('fmtDoubleElim').addEventListener('click', () => selectFormat(0));
document.getElementById('fmtSwiss').addEventListener('click', () => selectFormat(1));
document.getElementById('fmtSwissPure').addEventListener('click', () => selectFormat(2));
document.getElementById('topCutSize').addEventListener('change', updateSwissRoundsInfo);

// Inicializar seleção padrão
selectFormat(0);

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
                <select class="form-select deck-saved-select mb-2">
                    <option value="">Digitar manualmente…</option>
                </select>
                <input type="text" class="form-control deck-input" placeholder="Nome do Deck" required>
            </div>
            <div class="col-md-1 d-grid">
                <button type="button" class="btn btn-danger remove-row" title="Remover">
                    <i class="bi bi-x-lg"></i>
                </button>
            </div>
        </div>`;
    container.appendChild(row);

    row.querySelector('.player-select').addEventListener('change', () => {
        refreshAllSelects();
        loadSavedDecksForRow(row);
    });
    row.querySelector('.deck-saved-select').addEventListener('change', () => {
        const savedSelect = row.querySelector('.deck-saved-select');
        const input = row.querySelector('.deck-input');
        const selectedOption = savedSelect.options[savedSelect.selectedIndex];
        if (savedSelect.value) {
            input.value = selectedOption.dataset.name;
            input.disabled = true;
        } else {
            input.value = '';
            input.disabled = false;
        }
    });
    row.querySelector('.remove-row').addEventListener('click', () => {
        row.remove();
        refreshAllSelects();
    });
    updatePlayerCount();
}

async function loadSavedDecksForRow(row) {
    const playerId = row.querySelector('.player-select').value;
    const savedSelect = row.querySelector('.deck-saved-select');
    const input = row.querySelector('.deck-input');
    savedSelect.innerHTML = '<option value="">Digitar manualmente…</option>';
    input.disabled = false;
    if (!playerId) return;
    try {
        const response = await apiFetch(`${API_BASE_URL}/deck?playerId=${playerId}`);
        const decks = await response.json();
        decks.forEach(d => {
            savedSelect.innerHTML += `<option value="${d.id}" data-name="${escapeHtml(d.name)}">${escapeHtml(d.name)} (${d.cardCount} cartas)</option>`;
        });
    } catch (_) { /* ignora silenciosamente */ }
}

document.getElementById('addPlayerBtn').addEventListener('click', addPlayerRow);

document.getElementById('maxPlayers').addEventListener('change', () => {
    const max = getMaxPlayers();
    const rows = Array.from(document.querySelectorAll('.player-row'));
    if (rows.length > max) {
        rows.slice(max).forEach(r => r.remove());
        refreshAllSelects();
    }
    updatePlayerCount();
    const fmt = parseInt(document.getElementById('format').value, 10);
    if (fmt >= 1) updateSwissRoundsInfo();
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
    const today = new Date(); today.setHours(0, 0, 0, 0);
    if (new Date(startDate + 'T00:00:00') < today) {
        notifyWarning('Não é permitido criar torneios com data no passado.');
        return;
    }

    const rows = document.querySelectorAll('.player-row');
    const players = [];
    const seenIds = new Set();
    let hasIncomplete = false;

    for (const row of rows) {
        const playerId = row.querySelector('.player-select').value;
        const deck = row.querySelector('.deck-input').value.trim();
        const deckIdValue = row.querySelector('.deck-saved-select').value;
        if (!playerId && !deck) continue;
        if (!playerId || !deck) { hasIncomplete = true; continue; }
        if (seenIds.has(playerId)) {
            const dupName = allPlayers.find(p => String(p.id) === playerId)?.name || 'Jogador';
            notifyError(`${dupName} foi selecionado mais de uma vez. Cada jogador pode participar apenas uma vez por torneio.`, 'Jogador duplicado');
            return;
        }
        seenIds.add(playerId);
        players.push({ playerId: parseInt(playerId), deck, deckId: deckIdValue ? parseInt(deckIdValue) : null });
    }

    if (hasIncomplete) {
        notifyWarning('Preencha jogador e deck em todas as linhas (ou remova as linhas vazias).');
        return;
    }

    if (players.length > maxPlayers) {
        notifyWarning(`Você adicionou ${players.length} jogadores, mas o limite é ${maxPlayers}.`);
        return;
    }

    const format     = parseInt(document.getElementById('format').value, 10);
    const topCutSize = parseInt(document.getElementById('topCutSize').value, 10);
    const mode       = parseInt(document.getElementById('tournamentMode').value, 10);

    try {
        const response = await apiFetch(`${API_BASE_URL}/tournament`, {
            method: 'POST',
            body: JSON.stringify({ name, startDate, maxPlayers, players, format, topCutSize, mode }),
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

// Bloqueia seleção de datas passadas no date picker
const startDateInput = document.getElementById('startDate');
if (startDateInput) {
    const todayStr = new Date().toISOString().slice(0, 10);
    startDateInput.min = todayStr;
}

loadPlayers();
