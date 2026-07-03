/* ========== Página de Temporada ========== */

let playersMap = new Map();

function statusBadge(season) {
    if (season.isActive) return '<span class="status-pill live"><i class="bi bi-play-fill"></i> Em andamento</span>';
    return '<span class="status-pill done"><i class="bi bi-flag-fill"></i> Encerrada</span>';
}

async function loadSeasonPicker(selectedId) {
    const picker = document.getElementById('seasonPicker');
    try {
        const response = await apiFetch(`${API_BASE_URL}/season`);
        const seasons = await response.json();
        if (!seasons.length) {
            picker.style.display = 'none';
            return null;
        }
        picker.innerHTML = seasons.map(s => `<option value="${s.id}">${escapeHtml(s.name)}${s.isActive ? ' (atual)' : ''}</option>`).join('');
        const targetId = selectedId || seasons.find(s => s.isActive)?.id || seasons[0].id;
        picker.value = targetId;
        return targetId;
    } catch (error) {
        console.error(error);
        return null;
    }
}

async function loadSeason(id) {
    try {
        const response = await apiFetch(`${API_BASE_URL}/season/${id}`);
        const season = await response.json();

        document.getElementById('seasonEmptyState').style.display = 'none';
        document.getElementById('seasonContent').style.display = '';

        document.getElementById('seasonTitle').textContent = season.name;
        document.getElementById('seasonSubtitle').innerHTML =
            `${formatDate(season.startDate)} a ${formatDate(season.endDate)} &nbsp;•&nbsp; ${statusBadge(season)}`;

        history.replaceState(null, '', `/season.html?id=${id}`);

        await Promise.all([loadStandings(id), loadSeasonMatches(id)]);
    } catch (error) {
        notifyError('Erro ao carregar temporada: ' + error.message);
    }
}

async function loadStandings(id) {
    const tbody = document.getElementById('seasonStandingsTable');
    try {
        const response = await apiFetch(`${API_BASE_URL}/season/${id}/standings`);
        const standings = await response.json();
        playersMap = new Map(standings.map(p => [p.id, p]));

        if (!standings.length) {
            tbody.innerHTML = `<tr class="empty-row"><td colspan="3"><div class="empty-state"><div class="icon"><i class="bi bi-emoji-frown"></i></div><div class="title">Nenhum jogador cadastrado</div></div></td></tr>`;
            return;
        }

        tbody.innerHTML = standings.map((p, i) => {
            const position = i + 1;
            const rankCls = position <= 3 ? `rank-${position}` : '';
            return `
                <tr>
                    <td><span class="rank-badge ${rankCls}">${position}º</span></td>
                    <td>
                        <a href="/player.html?id=${p.id}" class="player-cell" style="text-decoration:none;">
                            ${p.avatarUrl
                                ? `<img src="${escapeHtml(p.avatarUrl)}" class="avatar avatar-img" alt="${escapeHtml(p.name)}">`
                                : `<span class="avatar">${getInitials(p.name)}</span>`}
                            <span style="font-weight:600; color: var(--text-1);">${escapeHtml(p.name)}</span>
                        </a>
                    </td>
                    <td><span class="score-pill"><i class="bi bi-stars"></i> ${p.score} pts</span></td>
                </tr>`;
        }).join('');
    } catch (error) {
        console.error(error);
        tbody.innerHTML = `<tr class="empty-row"><td colspan="3"><div class="empty-state"><div class="icon"><i class="bi bi-exclamation-octagon"></i></div><div class="title">Erro ao carregar classificação</div></div></td></tr>`;
    }
}

async function loadSeasonMatches(id) {
    const tbody = document.getElementById('seasonMatchesTable');
    try {
        const response = await apiFetch(`${API_BASE_URL}/matches?seasonId=${id}`);
        const matches = await response.json();

        if (!matches.length) {
            tbody.innerHTML = `<tr class="empty-row"><td colspan="4"><div class="empty-state"><div class="icon"><i class="bi bi-inbox"></i></div><div class="title">Nenhuma partida nesta temporada</div></div></td></tr>`;
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
                    <td><a href="/player.html?id=${match.player1Id}" style="text-decoration:none; color: var(--text-1); font-weight:600;">${escapeHtml(p1Name)}</a></td>
                    <td><a href="/player.html?id=${match.player2Id}" style="text-decoration:none; color: var(--text-1); font-weight:600;">${escapeHtml(p2Name)}</a></td>
                    <td>${resultHtml}</td>
                    <td><span class="text-muted-2" style="font-size:0.85rem;">${formatDateTime(match.date)}</span></td>
                </tr>`;
        }).join('');
    } catch (error) {
        console.error(error);
        tbody.innerHTML = `<tr class="empty-row"><td colspan="4"><div class="empty-state"><div class="icon"><i class="bi bi-exclamation-octagon"></i></div><div class="title">Erro ao carregar partidas</div></div></td></tr>`;
    }
}

document.addEventListener('DOMContentLoaded', async () => {
    const params = new URLSearchParams(window.location.search);
    const requestedId = params.get('id');

    const targetId = await loadSeasonPicker(requestedId);
    if (!targetId) {
        document.getElementById('seasonEmptyState').style.display = '';
        document.getElementById('seasonContent').style.display = 'none';
        return;
    }

    await loadSeason(targetId);

    document.getElementById('seasonPicker').addEventListener('change', (e) => {
        loadSeason(e.target.value);
    });
});
