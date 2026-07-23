/* ========== Página de Ranking ========== */

let allPlayersData = [];
let sortConfig = { key: 'score', direction: 'desc' };
let searchTerm = '';
let viewMode = 'season'; // 'season' (Score, reseta a cada temporada) | 'career' (CareerScore, geral)
let matchMode = '0'; // '0' = Presencial | '1' = Online (simulador DCGO) — dimensão independente do viewMode
let currentSeason = null;

function scoreOf(player) {
    const online = matchMode === '1';
    if (viewMode === 'career') return (online ? player.careerScoreOnline : player.careerScore) || 0;
    return (online ? player.scoreOnline : player.score) || 0;
}

async function loadRanking() {
    const tbody = document.getElementById('rankingTable');
    try {
        const response = await apiFetch(`${API_BASE_URL}/player`);
        allPlayersData = await response.json();
        updateStats(allPlayersData);
        renderTable();
    } catch (error) {
        console.error(error);
        tbody.innerHTML = `<tr class="empty-row"><td colspan="4"><div class="empty-state"><div class="icon"><i class="bi bi-exclamation-octagon"></i></div><div class="title">Erro ao carregar dados</div><div>${escapeHtml(error.message)}</div></div></td></tr>`;
    }
}

async function loadSeasonInfo() {
    try {
        const response = await apiFetch(`${API_BASE_URL}/season/current`);
        currentSeason = response.status === 204 ? null : await response.json();
    } catch (error) {
        console.error(error);
        currentSeason = null;
    }
    renderSeasonBar();
}

function renderSeasonBar() {
    const nameEl = document.getElementById('seasonName');
    const actionsEl = document.getElementById('seasonAdminActions');
    const startBtn = document.getElementById('startSeasonBtn');
    const endBtn = document.getElementById('endSeasonBtn');
    const isAdmin = typeof authIsAdmin === 'function' && authIsAdmin();

    nameEl.innerHTML = currentSeason
        ? `<a href="/season.html?id=${currentSeason.id}" style="color:inherit; text-decoration:none;">${escapeHtml(currentSeason.name)} — ${formatDate(currentSeason.startDate)} a ${formatDate(currentSeason.endDate)}</a>`
        : 'Nenhuma temporada ativa &nbsp;·&nbsp; <a href="/season.html">Ver temporadas anteriores</a>';

    if (isAdmin) {
        actionsEl.style.display = '';
        startBtn.style.display = currentSeason ? 'none' : '';
        endBtn.style.display = currentSeason ? '' : 'none';
    } else {
        actionsEl.style.display = 'none';
    }
}

async function startSeason() {
    const today = new Date().toISOString().slice(0, 10);
    const result = await Swal.fire({
        title: 'Iniciar nova temporada',
        html: `
            <div class="text-start">
                <label class="form-label d-block mt-2 mb-1">Nome da temporada</label>
                <input id="swalSeasonName" class="swal2-input" placeholder="Ex.: Temporada 2026-2" style="margin:0;">
                <label class="form-label d-block mt-3 mb-1">Data de início</label>
                <input id="swalSeasonStart" type="date" class="swal2-input" value="${today}" style="margin:0;">
                <label class="form-label d-block mt-3 mb-1">Data de término (define a duração)</label>
                <input id="swalSeasonEnd" type="date" class="swal2-input" style="margin:0;">
            </div>`,
        showCancelButton: true,
        confirmButtonText: 'Iniciar',
        cancelButtonText: 'Cancelar',
        reverseButtons: true,
        focusConfirm: false,
        preConfirm: () => {
            const name = document.getElementById('swalSeasonName').value.trim();
            const startDate = document.getElementById('swalSeasonStart').value;
            const endDate = document.getElementById('swalSeasonEnd').value;
            if (!name) { Swal.showValidationMessage('Informe o nome da temporada.'); return false; }
            if (!startDate || !endDate) { Swal.showValidationMessage('Informe as duas datas.'); return false; }
            if (endDate <= startDate) { Swal.showValidationMessage('A data de término precisa ser depois da data de início.'); return false; }
            return { name, startDate, endDate };
        },
    });
    if (!result.isConfirmed) return;

    try {
        await apiFetch(`${API_BASE_URL}/season`, { method: 'POST', body: JSON.stringify(result.value) });
        notifySuccess('Temporada iniciada!');
        loadSeasonInfo();
    } catch (error) {
        notifyError('Erro ao iniciar temporada: ' + error.message);
    }
}

async function endSeason() {
    if (!currentSeason) return;
    const result = await confirmAction({
        title: 'Encerrar temporada?',
        text: `A pontuação atual de todos os jogadores será arquivada e o ranking da temporada "${currentSeason.name}" será zerado. Essa ação não pode ser desfeita.`,
        confirmText: 'Sim, encerrar',
        cancelText: 'Cancelar',
        icon: 'warning',
    });
    if (!result.isConfirmed) return;

    try {
        await apiFetch(`${API_BASE_URL}/season/${currentSeason.id}/end`, { method: 'POST' });
        notifySuccess('Temporada encerrada e ranking reiniciado.');
        await loadSeasonInfo();
        await loadRanking();
    } catch (error) {
        notifyError('Erro ao encerrar temporada: ' + error.message);
    }
}

function setupViewModeToggle() {
    document.querySelectorAll('#viewModeGroup button').forEach(btn => {
        btn.addEventListener('click', () => {
            viewMode = btn.dataset.mode;
            document.querySelectorAll('#viewModeGroup button').forEach(b => b.classList.toggle('active', b === btn));
            updateStats(allPlayersData);
            renderTable();
        });
    });
}

function setupMatchModeToggle() {
    document.querySelectorAll('#matchModeGroup button').forEach(btn => {
        btn.addEventListener('click', () => {
            matchMode = btn.dataset.matchMode;
            document.querySelectorAll('#matchModeGroup button').forEach(b => b.classList.toggle('active', b === btn));
            updateStats(allPlayersData);
            renderTable();
        });
    });
}

function renderTable() {
    const tbody = document.getElementById('rankingTable');
    let list = [...allPlayersData];
    if (searchTerm) {
        const lower = searchTerm.toLowerCase();
        list = list.filter(p => p.name.toLowerCase().includes(lower));
    }

    list.sort((a, b) => {
        const key = sortConfig.key;
        let va = key === 'score' ? scoreOf(a) : a[key];
        let vb = key === 'score' ? scoreOf(b) : b[key];
        if (typeof va === 'string') va = va.toLowerCase();
        if (typeof vb === 'string') vb = vb.toLowerCase();
        if (va < vb) return sortConfig.direction === 'asc' ? -1 : 1;
        if (va > vb) return sortConfig.direction === 'asc' ? 1 : -1;
        return 0;
    });

    if (!list.length) {
        const msg = searchTerm ? 'Nenhum jogador encontrado para a busca.' : 'Nenhum jogador cadastrado';
        tbody.innerHTML = `
            <tr class="empty-row">
                <td colspan="4">
                    <div class="empty-state">
                        <div class="icon"><i class="bi bi-emoji-frown"></i></div>
                        <div class="title">${msg}</div>
                        ${searchTerm ? '' : '<div>Adicione o primeiro jogador no formulário acima.</div>'}
                    </div>
                </td>
            </tr>`;
        return;
    }

    // Posição usa o ranking ORIGINAL (por pontuação desc do modo atual), não a ordem da tabela
    const rankByScore = [...allPlayersData].sort((a, b) => scoreOf(b) - scoreOf(a));
    const positionMap = new Map(rankByScore.map((p, i) => [p.id, i + 1]));

    const isAdmin = typeof authIsAdmin === 'function' && authIsAdmin();
    tbody.innerHTML = list.map(player => {
        const position = positionMap.get(player.id);
        const rankCls = position <= 3 ? `rank-${position}` : '';
        return `
            <tr>
                <td><span class="rank-badge ${rankCls}">${position}º</span></td>
                <td>
                    <a href="/player.html?id=${player.id}" class="player-cell" style="text-decoration:none;">
                        ${player.avatarUrl
                            ? `<img src="${escapeHtml(player.avatarUrl)}" class="avatar avatar-img" alt="${escapeHtml(player.name)}">`
                            : `<span class="avatar">${getInitials(player.name)}</span>`}
                        <div>
                            <div style="font-weight:600; color: var(--text-1);">${escapeHtml(player.name)}</div>
                        </div>
                    </a>
                </td>
                <td><span class="score-pill"><i class="bi bi-stars"></i> ${scoreOf(player)} pts</span></td>
                ${isAdmin ? `
                <td style="text-align:right;">
                    <div class="d-inline-flex gap-1">
                        <button class="btn btn-sm btn-ghost" onclick="editPlayer(${player.id}, '${escapeHtml(player.name)}')" title="Editar nome">
                            <i class="bi bi-pencil"></i>
                        </button>
                        <button class="btn btn-sm btn-ghost" onclick="deletePlayer(${player.id}, '${escapeHtml(player.name)}')" title="Excluir jogador">
                            <i class="bi bi-trash3"></i>
                        </button>
                    </div>
                </td>` : ''}
            </tr>`;
    }).join('');
}

function updateStats(players) {
    document.getElementById('statPlayers').textContent = players.length;
    const total = players.reduce((acc, p) => acc + scoreOf(p), 0);
    document.getElementById('statTotalScore').textContent = total;
    const sorted = [...players].sort((a, b) => scoreOf(b) - scoreOf(a));
    document.getElementById('statLeader').textContent = sorted.length ? sorted[0].name : '—';
}

async function addPlayer(event) {
    event.preventDefault();
    const input = document.getElementById('playerName');
    const name = input.value.trim();
    if (!name) {
        notifyWarning('Informe o nome do jogador antes de adicionar.');
        return;
    }
    try {
        await apiFetch(`${API_BASE_URL}/player`, {
            method: 'POST',
            body: JSON.stringify({ name, score: 0 }),
        });
        input.value = '';
        notifySuccess(`${name} foi adicionado ao ranking.`);
        loadRanking();
    } catch (error) {
        notifyError(error.message);
    }
}

async function editPlayer(id, currentName) {
    const result = await promptText({
        title: 'Editar jogador',
        label: 'Novo nome',
        defaultValue: currentName,
        placeholder: 'Digite o novo nome',
    });
    if (!result.isConfirmed) return;
    const newName = result.value.trim();
    if (newName === currentName) {
        notifyInfo('O nome não foi alterado.');
        return;
    }
    try {
        await apiFetch(`${API_BASE_URL}/player/${id}`, {
            method: 'PUT',
            body: JSON.stringify({ name: newName, score: 0 }),
        });
        notifySuccess(`Nome atualizado para "${newName}".`);
        loadRanking();
    } catch (error) {
        notifyError(error.message, 'Não foi possível salvar');
    }
}

async function deletePlayer(id, name) {
    const result = await confirmAction({
        title: 'Excluir jogador?',
        text: `O jogador "${name}" será removido permanentemente.`,
        confirmText: 'Sim, excluir',
        cancelText: 'Cancelar',
        icon: 'warning',
    });
    if (!result.isConfirmed) return;

    try {
        await apiFetch(`${API_BASE_URL}/player/${id}`, { method: 'DELETE' });
        notifySuccess('Jogador removido com sucesso.');
        loadRanking();
    } catch (error) {
        let msg = error.message;
        if (msg.toLowerCase().includes('partidas') || msg.toLowerCase().includes('conflict')) {
            msg = 'Não é possível excluir um jogador que já participou de partidas.';
        }
        notifyError(msg);
    }
}

function setupSort() {
    document.querySelectorAll('.table-app th.sortable').forEach(th => {
        th.addEventListener('click', () => {
            const key = th.dataset.sort;
            if (sortConfig.key === key) {
                sortConfig.direction = sortConfig.direction === 'asc' ? 'desc' : 'asc';
            } else {
                sortConfig.key = key;
                sortConfig.direction = key === 'score' ? 'desc' : 'asc';
            }
            updateSortIndicators();
            renderTable();
        });
    });
}

function updateSortIndicators() {
    document.querySelectorAll('.table-app th.sortable').forEach(th => {
        const ind = th.querySelector('.sort-indicator i');
        if (th.dataset.sort === sortConfig.key) {
            th.classList.add('active');
            ind.className = sortConfig.direction === 'asc' ? 'bi bi-sort-up' : 'bi bi-sort-down';
        } else {
            th.classList.remove('active');
            ind.className = 'bi bi-arrow-down-up';
        }
    });
}

function exportRanking() {
    if (!allPlayersData.length) {
        notifyWarning('Não há jogadores para exportar.');
        return;
    }
    const sorted = [...allPlayersData].sort((a, b) => scoreOf(b) - scoreOf(a));
    const rows = sorted.map((p, i) => ({
        Posicao: i + 1,
        ID: p.id,
        Nome: p.name,
        Pontuacao: scoreOf(p),
    }));
    const today = new Date().toISOString().slice(0, 10);
    downloadCsv(`ranking-${today}.csv`, rows);
    notifySuccess('Ranking exportado!');
}

document.addEventListener('DOMContentLoaded', () => {
    if (!(typeof authIsAdmin === 'function' && authIsAdmin())) {
        document.getElementById('thAcoes')?.remove();
    }
    loadRanking();
    loadSeasonInfo();
    document.getElementById('addPlayerForm').addEventListener('submit', addPlayer);
    document.getElementById('exportCsvBtn').addEventListener('click', exportRanking);
    document.getElementById('rankingSearch').addEventListener('input', (e) => {
        searchTerm = e.target.value.trim();
        renderTable();
    });
    document.getElementById('startSeasonBtn').addEventListener('click', startSeason);
    document.getElementById('endSeasonBtn').addEventListener('click', endSeason);
    setupSort();
    setupViewModeToggle();
    setupMatchModeToggle();
    window.deletePlayer = deletePlayer;
    window.editPlayer = editPlayer;
});
