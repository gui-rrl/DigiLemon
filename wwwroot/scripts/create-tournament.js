/* ========== Criar Torneio ========== */

let allPlayers = [];

// ── Seletor de formato ──────────────────────────────────────────────────────
function selectFormat(fmt) {
    document.getElementById('format').value = fmt;
    document.getElementById('fmtDoubleElim').classList.toggle('selected', fmt === 0);
    document.getElementById('fmtSwiss').classList.toggle('selected', fmt === 1);
    document.getElementById('fmtSwissPure').classList.toggle('selected', fmt === 2);
    document.getElementById('fmtRoundRobin').classList.toggle('selected', fmt === 3);

    const swissOpts = document.getElementById('swissOptions');
    swissOpts.classList.toggle('d-none', fmt === 0);

    // Top Cut existe no Swiss+TopCut (1) e no todos contra todos (3)
    const topCutRow = document.getElementById('topCutSizeRow');
    if (topCutRow) topCutRow.classList.toggle('d-none', fmt !== 1 && fmt !== 3);

    if (fmt >= 1) updateSwissRoundsInfo();

    // Atualizar opções de vagas (Swiss permite ímpar)
    updateMaxPlayersOptions(fmt);
}

function updateSwissRoundsInfo() {
    const n = parseInt(document.getElementById('maxPlayers').value, 10);
    const fmt = parseInt(document.getElementById('format').value, 10);
    const info = document.getElementById('swissRoundsInfo');

    // Indefinido (n=0): a contagem final só existe quando o torneio iniciar, então nenhuma
    // conta de rodadas/partidas faz sentido aqui — mesmo cálculo adiado que o backend já faz
    // em SwissService.StartAsync/GenerateAllPairingsAsync a partir dos inscritos reais.
    if (n === 0) {
        const topCut = document.getElementById('topCutSize').value;
        if (fmt === 3) {
            info.textContent = `Vagas indefinidas: o número de partidas (todos contra todos, sem rodadas e sem bye) será calculado a partir de quantos jogadores estiverem inscritos ao iniciar → Top ${topCut} (double elimination).`;
        } else if (fmt === 2) {
            info.textContent = `Vagas indefinidas: o número de rodadas Swiss será calculado a partir de quantos jogadores estiverem inscritos ao iniciar. Classificação final por pontos, Top 4 destacado.`;
        } else {
            info.textContent = `Vagas indefinidas: o número de rodadas Swiss será calculado a partir de quantos jogadores estiverem inscritos ao iniciar → Top ${topCut} (double elimination).`;
        }
        return;
    }

    const rounds = n <= 2 ? 1 : n <= 4 ? 2 : n <= 8 ? 3 : n <= 16 ? 4 : n <= 32 ? 5 : 6;
    if (fmt === 3) {
        // Todos contra todos: cada um joga N-1 partidas; o total é N×(N-1)/2
        const topCut = document.getElementById('topCutSize').value;
        info.textContent = `Com ${n} jogadores: ${n * (n - 1) / 2} partidas no total (${n - 1} por jogador), sem rodadas e sem bye → Top ${topCut} (double elimination).`;
    } else if (fmt === 2) {
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

    // 0 = "Indefinido" (vagas abertas) — vale para qualquer formato.
    const unlimitedOpt = document.createElement('option');
    unlimitedOpt.value = '0';
    unlimitedOpt.textContent = 'Indefinido (inscrições abertas)';
    sel.appendChild(unlimitedOpt);

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
    // Preserva "Indefinido" ao trocar de formato; senão cai no padrão de 8.
    if (current === '0') sel.value = '0';
    else if (!sel.value || sel.value === '0') sel.value = '8';
    if (fmt >= 1) updateSwissRoundsInfo();
    updateUnlimitedUi();
}

// Alterna a UI entre vagas fixas e "Indefinido": esconde a data de início (preenchida com
// hoje no submit), torna a data de término opcional e mostra o aviso de auto-início.
function updateUnlimitedUi() {
    const isUnlimited = getMaxPlayers() === 0;
    document.getElementById('startDateGroup').classList.toggle('d-none', isUnlimited);
    document.getElementById('endDateOptionalHint').style.display = isUnlimited ? '' : 'none';
    document.getElementById('endDateHelp').classList.toggle('d-none', isUnlimited);
    document.getElementById('unlimitedHint').classList.toggle('d-none', !isUnlimited);
    document.getElementById('startDate').required = !isUnlimited;
}

// ── Modo de deck (Sorteio entre decks próprios / Death Random) ───────────────
function selectDeckMode(mode) {
    document.getElementById('deckMode').value = mode;
    document.getElementById('deckModeNormal').classList.toggle('selected', mode === 0);
    document.getElementById('deckModeSelfRandom').classList.toggle('selected', mode === 1);
    document.getElementById('deckModeDeathRandom').classList.toggle('selected', mode === 2);
    document.getElementById('deckPoolOptions').classList.toggle('d-none', mode === 0);

    // Death Random não é compatível com Swiss Pontos Corridos (Format 2) — desabilita
    // visualmente o cartão e troca de formato automaticamente se estava selecionado.
    const swissPureCard = document.getElementById('fmtSwissPure');
    swissPureCard.classList.toggle('disabled-option', mode === 2);
    if (mode === 2 && parseInt(document.getElementById('format').value, 10) === 2) {
        notifyWarning('Death Random não é compatível com Swiss Pontos Corridos. Formato ajustado para Dupla Eliminação.');
        selectFormat(0);
    }

    // Participantes pré-cadastrados na criação não são suportados com sorteio de deck — a
    // inscrição precisa passar pelo pool (TournamentPlayerDeckOption), só disponível via
    // convite ou tela de configuração.
    document.getElementById('playersSectionCard').classList.toggle('d-none', mode !== 0);
    document.getElementById('deckModeParticipantsHint').classList.toggle('d-none', mode === 0);
}
document.getElementById('deckModeNormal').addEventListener('click', () => selectDeckMode(0));
document.getElementById('deckModeSelfRandom').addEventListener('click', () => selectDeckMode(1));
document.getElementById('deckModeDeathRandom').addEventListener('click', () => selectDeckMode(2));

// Registrar listeners e inicializar após DOM pronto
document.getElementById('fmtDoubleElim').addEventListener('click', () => selectFormat(0));
document.getElementById('fmtSwiss').addEventListener('click', () => selectFormat(1));
document.getElementById('fmtSwissPure').addEventListener('click', () => selectFormat(2));
document.getElementById('fmtRoundRobin').addEventListener('click', () => selectFormat(3));
document.getElementById('topCutSize').addEventListener('change', updateSwissRoundsInfo);

// Inicializar seleção padrão
selectFormat(0);
selectDeckMode(0);

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
    const addBtn = document.getElementById('addPlayerBtn');
    const rowCount = document.querySelectorAll('.player-row').length;

    if (max === 0) {
        // Indefinido: sem teto, mesmo texto usado em tournament-setup.js pra consistência.
        pill.textContent = `${filledCount} jogador${filledCount === 1 ? '' : 'es'}`;
        pill.className = 'status-pill prep';
        addBtn.disabled = false;
        return;
    }

    pill.textContent = `${filledCount} / ${max} jogadores`;
    pill.className = filledCount === max ? 'status-pill live' : 'status-pill prep';

    // Bloqueia botão de adicionar quando atingir o limite
    addBtn.disabled = rowCount >= max;
}

function addPlayerRow() {
    const max = getMaxPlayers();
    const rowCount = document.querySelectorAll('.player-row').length;
    if (max > 0 && rowCount >= max) return;

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
                <select class="form-select deck-saved-select" required>
                    <option value="">Selecione o jogador primeiro</option>
                </select>
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
    row.querySelector('.remove-row').addEventListener('click', () => {
        row.remove();
        refreshAllSelects();
    });
    updatePlayerCount();
}

// Só decks já montados no sistema: não existe mais digitar o nome do deck à mão.
async function loadSavedDecksForRow(row) {
    const playerId = row.querySelector('.player-select').value;
    const savedSelect = row.querySelector('.deck-saved-select');
    savedSelect.innerHTML = '<option value="">Selecione o jogador primeiro</option>';
    if (!playerId) return;

    savedSelect.innerHTML = '<option value="">Carregando decks…</option>';
    try {
        const decks = await apiFetch(`${API_BASE_URL}/deck?playerId=${playerId}`).then(r => r.json());
        if (!decks.length) {
            // Deixa explícito por que o select ficou vazio, em vez de parecer defeito
            savedSelect.innerHTML = '<option value="">Este jogador não tem deck salvo</option>';
            return;
        }
        savedSelect.innerHTML = '<option value="">Selecione o deck</option>' +
            decks.map(d => `<option value="${d.id}" data-name="${escapeHtml(d.name)}">${escapeHtml(d.name)} (${d.cardCount} cartas)</option>`).join('');
    } catch (_) {
        savedSelect.innerHTML = '<option value="">Não foi possível carregar os decks</option>';
    }
}

document.getElementById('addPlayerBtn').addEventListener('click', addPlayerRow);

document.getElementById('maxPlayers').addEventListener('change', () => {
    const max = getMaxPlayers();
    const rows = Array.from(document.querySelectorAll('.player-row'));
    if (max > 0 && rows.length > max) {
        rows.slice(max).forEach(r => r.remove());
        refreshAllSelects();
    }
    updatePlayerCount();
    updateUnlimitedUi();
    const fmt = parseInt(document.getElementById('format').value, 10);
    if (fmt >= 1) updateSwissRoundsInfo();
});

document.getElementById('createTournamentForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    const name = document.getElementById('name').value.trim();
    const isUnlimited = getMaxPlayers() === 0;
    // Indefinido: a data de início não é escolhida pelo admin (campo fica oculto) — usa hoje.
    // Formato YYYY-MM-DD local, mesmo cálculo já usado mais abaixo pro min dos date pickers.
    const todayStr = (() => {
        const now = new Date();
        return new Date(now.getTime() - now.getTimezoneOffset() * 60000).toISOString().slice(0, 10);
    })();
    const startDate = isUnlimited ? todayStr : document.getElementById('startDate').value;
    const endDate = document.getElementById('endDate').value;
    const registrationDeadline = document.getElementById('registrationDeadline').value;
    const maxPlayers = getMaxPlayers();

    if (!name) {
        notifyWarning('Informe o nome do torneio.');
        return;
    }
    if (!isUnlimited && !startDate) {
        notifyWarning('Informe a data de início.');
        return;
    }
    // Data de término é opcional em modo Indefinido — o torneio pode não ter fim previsto.
    if (!isUnlimited && !endDate) {
        notifyWarning('Informe a data de término.');
        return;
    }
    const today = new Date(); today.setHours(0, 0, 0, 0);
    if (!isUnlimited && new Date(startDate + 'T00:00:00') < today) {
        notifyWarning('Não é permitido criar torneios com data no passado.');
        return;
    }
    if (endDate && new Date(endDate + 'T00:00:00') < new Date(startDate + 'T00:00:00')) {
        notifyWarning('A data de término não pode ser anterior à data de início.');
        return;
    }
    if (registrationDeadline) {
        if (new Date(registrationDeadline + 'T00:00:00') < today) {
            notifyWarning('O fim das inscrições não pode ser uma data no passado.');
            return;
        }
        if (endDate && new Date(registrationDeadline + 'T00:00:00') > new Date(endDate + 'T00:00:00')) {
            notifyWarning('O fim das inscrições não pode ser depois da data de término do torneio.');
            return;
        }
    }

    const deckMode = parseInt(document.getElementById('deckMode').value, 10);

    const players = [];
    // Com sorteio de deck ligado, participantes não são pré-cadastrados aqui (ver
    // selectDeckMode) — o back-end rejeita players.length > 0 nesse caso de qualquer forma.
    if (deckMode === 0) {
        const rows = document.querySelectorAll('.player-row');
        const seenIds = new Set();
        let hasIncomplete = false;

        for (const row of rows) {
            const playerId = row.querySelector('.player-select').value;
            const deckSelect = row.querySelector('.deck-saved-select');
            const deckIdValue = deckSelect.value;
            // O nome do deck vem do próprio deck escolhido — não há mais digitação manual
            const deck = deckIdValue ? (deckSelect.options[deckSelect.selectedIndex].dataset.name || '') : '';
            if (!playerId && !deckIdValue) continue;
            if (!playerId || !deckIdValue) { hasIncomplete = true; continue; }
            if (seenIds.has(playerId)) {
                const dupName = allPlayers.find(p => String(p.id) === playerId)?.name || 'Jogador';
                notifyError(`${dupName} foi selecionado mais de uma vez. Cada jogador pode participar apenas uma vez por torneio.`, 'Jogador duplicado');
                return;
            }
            seenIds.add(playerId);
            players.push({ playerId: parseInt(playerId), deck, deckId: deckIdValue ? parseInt(deckIdValue) : null });
        }

        if (hasIncomplete) {
            notifyWarning('Selecione o jogador e um deck salvo em todas as linhas (ou remova as linhas vazias).');
            return;
        }

        if (!isUnlimited && players.length > maxPlayers) {
            notifyWarning(`Você adicionou ${players.length} jogadores, mas o limite é ${maxPlayers}.`);
            return;
        }
    }

    const format       = parseInt(document.getElementById('format').value, 10);
    const topCutSize   = parseInt(document.getElementById('topCutSize').value, 10);
    const mode         = parseInt(document.getElementById('tournamentMode').value, 10);
    const deckPoolSize = parseInt(document.getElementById('deckPoolSize').value, 10);

    try {
        const response = await apiFetch(`${API_BASE_URL}/tournament`, {
            method: 'POST',
            body: JSON.stringify({ name, startDate, endDate: endDate || null, registrationDeadline: registrationDeadline || null, maxPlayers, players, format, topCutSize, mode, deckMode, deckPoolSize }),
        });
        const result = await response.json();
        await Swal.fire({
            icon: 'success',
            title: 'Torneio criado!',
            text: players.length === 0
                ? (isUnlimited
                    ? 'Torneio criado com vagas indefinidas. Use o link de convite para inscrever os participantes.'
                    : `Torneio criado com ${maxPlayers} vagas. Use o link de convite para inscrever os participantes.`)
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
const endDateInput = document.getElementById('endDate');
const deadlineInput = document.getElementById('registrationDeadline');
if (startDateInput) {
    // Data local (não UTC): depois das 21h no Brasil o toISOString() já aponta pro dia seguinte
    // e bloquearia escolher o dia de hoje no date picker.
    const now = new Date();
    const todayStr = new Date(now.getTime() - now.getTimezoneOffset() * 60000).toISOString().slice(0, 10);
    startDateInput.min = todayStr;
    if (deadlineInput) deadlineInput.min = todayStr;
    startDateInput.addEventListener('change', () => {
        if (endDateInput) {
            endDateInput.min = startDateInput.value;
            if (endDateInput.value && endDateInput.value < startDateInput.value) {
                endDateInput.value = startDateInput.value;
            }
        }
    });
}
// O prazo de inscrição nunca pode passar do término do torneio
if (endDateInput && deadlineInput) {
    endDateInput.addEventListener('change', () => {
        deadlineInput.max = endDateInput.value;
        if (deadlineInput.value && endDateInput.value && deadlineInput.value > endDateInput.value) {
            deadlineInput.value = endDateInput.value;
        }
    });
}

loadPlayers();
