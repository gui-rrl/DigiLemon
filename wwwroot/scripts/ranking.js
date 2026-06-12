/* ========== Página de Ranking ========== */

let allPlayersData = [];
let sortConfig = { key: 'score', direction: 'desc' };
let searchTerm = '';

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

function renderTable() {
    const tbody = document.getElementById('rankingTable');
    let list = [...allPlayersData];
    if (searchTerm) {
        const lower = searchTerm.toLowerCase();
        list = list.filter(p => p.name.toLowerCase().includes(lower));
    }

    list.sort((a, b) => {
        const key = sortConfig.key;
        let va = a[key], vb = b[key];
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

    // Posição usa o ranking ORIGINAL (por score desc), não a ordem da tabela
    const rankByScore = [...allPlayersData].sort((a, b) => b.score - a.score);
    const positionMap = new Map(rankByScore.map((p, i) => [p.id, i + 1]));

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
                            <div class="text-muted-2" style="font-size:0.78rem;">ID #${player.id} · ver perfil</div>
                        </div>
                    </a>
                </td>
                <td><span class="score-pill"><i class="bi bi-stars"></i> ${player.score} pts</span></td>
                <td style="text-align:right;">
                    ${typeof authIsAdmin === 'function' && authIsAdmin() ? `
                    <div class="d-inline-flex gap-1">
                        <button class="btn btn-sm btn-ghost" onclick="editPlayer(${player.id}, '${escapeHtml(player.name)}')" title="Editar nome">
                            <i class="bi bi-pencil"></i>
                        </button>
                        <button class="btn btn-sm btn-ghost" onclick="deletePlayer(${player.id}, '${escapeHtml(player.name)}')" title="Excluir jogador">
                            <i class="bi bi-trash3"></i>
                        </button>
                    </div>` : ''}
                </td>
            </tr>`;
    }).join('');
}

function updateStats(players) {
    document.getElementById('statPlayers').textContent = players.length;
    const total = players.reduce((acc, p) => acc + (p.score || 0), 0);
    document.getElementById('statTotalScore').textContent = total;
    const sorted = [...players].sort((a, b) => b.score - a.score);
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
    const sorted = [...allPlayersData].sort((a, b) => b.score - a.score);
    const rows = sorted.map((p, i) => ({
        Posicao: i + 1,
        ID: p.id,
        Nome: p.name,
        Pontuacao: p.score,
    }));
    const today = new Date().toISOString().slice(0, 10);
    downloadCsv(`ranking-${today}.csv`, rows);
    notifySuccess('Ranking exportado!');
}

document.addEventListener('DOMContentLoaded', () => {
    loadRanking();
    document.getElementById('addPlayerForm').addEventListener('submit', addPlayer);
    document.getElementById('exportCsvBtn').addEventListener('click', exportRanking);
    document.getElementById('rankingSearch').addEventListener('input', (e) => {
        searchTerm = e.target.value.trim();
        renderTable();
    });
    setupSort();
    window.deletePlayer = deletePlayer;
    window.editPlayer = editPlayer;
});
