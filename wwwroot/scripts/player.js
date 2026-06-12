/* ========== Perfil do Jogador ========== */

const params = new URLSearchParams(window.location.search);
const playerId = params.get('id');
const charts = {};

const palette = {
    primary: '#6d6fff',
    accent: '#16e0bd',
    accent2: '#00c2ff',
    warning: '#ffb547',
    danger: '#ff5d73',
    success: '#4ade80',
    text: '#b9c0d9',
    grid: 'rgba(255, 255, 255, 0.08)',
};

const baseChartOptions = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
        legend: { labels: { color: palette.text, font: { family: 'Inter, sans-serif' } } },
        tooltip: {
            backgroundColor: 'rgba(11, 16, 32, 0.95)',
            borderColor: 'rgba(255, 255, 255, 0.15)',
            borderWidth: 1, padding: 10, cornerRadius: 8,
        },
    },
    scales: {
        x: { ticks: { color: palette.text }, grid: { color: palette.grid, drawBorder: false } },
        y: { beginAtZero: true, ticks: { color: palette.text, precision: 0 }, grid: { color: palette.grid, drawBorder: false } },
    },
};

function resultPillHtml(result) {
    if (result === 'win') return '<span class="match-pill win"><i class="bi bi-trophy"></i> Vitória</span>';
    if (result === 'loss') return '<span class="match-pill loss"><i class="bi bi-x-circle"></i> Derrota</span>';
    return '<span class="match-pill draw"><i class="bi bi-handshake"></i> Empate</span>';
}

async function loadProfile() {
    if (!playerId) {
        renderError('ID do jogador não informado na URL.');
        return;
    }
    try {
        const response = await apiFetch(`${API_BASE_URL}/player/${playerId}/profile`);
        const data = await response.json();
        renderProfile(data);
    } catch (error) {
        renderError(error.message);
    }
}

function renderError(message) {
    document.getElementById('profileContent').innerHTML = `
        <div class="card-app">
            <div class="card-body text-center py-5">
                <i class="bi bi-person-x" style="font-size:3.2rem; color: var(--danger);"></i>
                <h2 class="mt-3" style="font-size:1.4rem;">Não foi possível abrir o perfil</h2>
                <p class="text-muted-2">${escapeHtml(message)}</p>
                <a href="/Index.html" class="btn btn-secondary mt-2"><i class="bi bi-arrow-left"></i> Voltar ao ranking</a>
            </div>
        </div>`;
}

function renderProfile(data) {
    const { player, stats, scoreHistory, decks, recentMatches, tournaments } = data;
    document.title = `${player.name} — RankingDigi`;

    const currentUser = typeof authUser === 'function' ? authUser() : null;
    const isOwnProfile = currentUser && (currentUser.playerId === player.id || currentUser.role === 'Admin');

    const totalChampionships = stats.championships || 0;
    const rankBadge = player.position <= 3
        ? `<span class="match-pill win"><i class="bi bi-award-fill"></i> ${player.position}º no ranking</span>`
        : `<span class="match-pill draw"><i class="bi bi-bar-chart"></i> ${player.position}º no ranking</span>`;

    const avatarHtml = player.avatarUrl
        ? `<img src="${escapeHtml(player.avatarUrl)}" class="avatar-xl-img" alt="Foto de perfil">`
        : `<span class="avatar-xl">${escapeHtml(player.initials)}</span>`;

    const editButtons = isOwnProfile ? `
        <div class="d-flex gap-2 mt-2 flex-wrap">
            <button class="btn btn-sm btn-secondary" id="btnEditName">
                <i class="bi bi-pencil"></i> Editar nome
            </button>
            <label class="btn btn-sm btn-secondary mb-0" style="cursor:pointer;" title="Alterar foto de perfil">
                <i class="bi bi-camera-fill"></i> Alterar foto
                <input type="file" id="avatarInput" accept="image/jpeg,image/png,image/webp,image/gif" style="display:none;">
            </label>
        </div>` : '';

    document.getElementById('profileContent').innerHTML = `
        <div class="d-flex justify-content-between align-items-center flex-wrap gap-2 mb-3">
            <a href="/Index.html" class="btn btn-ghost btn-sm"><i class="bi bi-arrow-left"></i> Voltar</a>
        </div>

        <section class="profile-hero">
            <div class="avatar-xl-wrap" style="position:relative;display:inline-block;">
                ${avatarHtml}
            </div>
            <div class="profile-info">
                <h1 id="profileName">${escapeHtml(player.name)}</h1>
                <div class="player-meta">
                    <i class="bi bi-hash"></i> ID ${player.id}
                    <span class="ms-2 ps-2" style="border-left:1px solid var(--border);"><i class="bi bi-stars"></i> ${player.score} pontos</span>
                </div>
                <div class="player-badges">
                    ${rankBadge}
                    ${totalChampionships > 0
                        ? `<span class="match-pill win"><i class="bi bi-trophy-fill"></i> ${totalChampionships} título${totalChampionships === 1 ? '' : 's'}</span>`
                        : ''}
                </div>
                ${editButtons}
            </div>
        </section>

        <section class="row g-3 mb-4">
            <div class="col-md-3 col-6">
                <div class="card-stat hover-lift">
                    <span class="stat-icon"><i class="bi bi-controller"></i></span>
                    <div>
                        <div class="stat-label">Partidas</div>
                        <div class="stat-value">${stats.played}</div>
                    </div>
                </div>
            </div>
            <div class="col-md-3 col-6">
                <div class="card-stat hover-lift" style="border-color: rgba(22,224,189,0.3);">
                    <span class="stat-icon" style="background: linear-gradient(135deg,#16e0bd,#4ade80);"><i class="bi bi-trophy-fill"></i></span>
                    <div>
                        <div class="stat-label">Vitórias</div>
                        <div class="stat-value">${stats.wins}</div>
                    </div>
                </div>
            </div>
            <div class="col-md-3 col-6">
                <div class="card-stat hover-lift" style="border-color: rgba(255,93,115,0.25);">
                    <span class="stat-icon" style="background: linear-gradient(135deg,#ff5d73,#ff8a5b);"><i class="bi bi-x-octagon-fill"></i></span>
                    <div>
                        <div class="stat-label">Derrotas</div>
                        <div class="stat-value">${stats.losses}</div>
                    </div>
                </div>
            </div>
            <div class="col-md-3 col-6">
                <div class="card-stat hover-lift" style="border-color: rgba(255,181,71,0.3);">
                    <span class="stat-icon" style="background: linear-gradient(135deg,#ffb547,#ff8a5b);"><i class="bi bi-percent"></i></span>
                    <div>
                        <div class="stat-label">Aproveitamento</div>
                        <div class="stat-value">${stats.winRate}%</div>
                    </div>
                </div>
            </div>
        </section>

        <section class="row g-3 mb-4">
            <div class="col-lg-8">
                <div class="chart-card">
                    <h3><i class="bi bi-graph-up-arrow"></i> Evolução da pontuação</h3>
                    <div class="chart-wrapper"><canvas id="chartScoreHistory"></canvas></div>
                </div>
            </div>
            <div class="col-lg-4">
                <div class="chart-card">
                    <h3><i class="bi bi-pie-chart-fill"></i> Resultados</h3>
                    <div class="chart-wrapper"><canvas id="chartResults"></canvas></div>
                </div>
            </div>
        </section>

        <section class="row g-3 mb-4">
            <div class="col-lg-7">
                <div class="card-app h-100">
                    <div class="card-header"><i class="bi bi-layers"></i> Decks utilizados</div>
                    <div class="card-body">
                        ${renderDecksSection(decks)}
                    </div>
                </div>
            </div>
            <div class="col-lg-5">
                <div class="card-app h-100">
                    <div class="card-header"><i class="bi bi-clock-history"></i> Últimas partidas</div>
                    <div class="card-body">
                        ${renderRecentMatches(recentMatches)}
                    </div>
                </div>
            </div>
        </section>

        <section class="card-app">
            <div class="card-header"><i class="bi bi-trophy"></i> Torneios disputados</div>
            <div class="card-body">
                ${renderTournaments(tournaments)}
            </div>
        </section>
    `;

    renderScoreHistoryChart(scoreHistory, player.score);
    renderResultsChart(stats);

    if (decks.length) renderDecksChart(decks);

    if (isOwnProfile) {
        document.getElementById('btnEditName')?.addEventListener('click', async () => {
            const result = await promptText({
                title: 'Editar nome',
                label: 'Novo nome',
                defaultValue: player.name,
                placeholder: 'Digite o novo nome',
            });
            if (!result.isConfirmed) return;
            const newName = result.value.trim();
            if (newName === player.name) { notifyInfo('O nome não foi alterado.'); return; }
            try {
                await apiFetch(`${API_BASE_URL}/player/${player.id}/name`, {
                    method: 'PUT',
                    body: JSON.stringify({ name: newName }),
                });
                notifySuccess(`Nome atualizado para "${newName}".`);
                loadProfile();
            } catch (err) {
                notifyError(err.message, 'Não foi possível salvar');
            }
        });

        document.getElementById('avatarInput')?.addEventListener('change', async (e) => {
            const file = e.target.files?.[0];
            if (!file) return;
            if (file.size > 2 * 1024 * 1024) { notifyError('A imagem deve ter no máximo 2 MB.'); return; }
            const form = new FormData();
            form.append('file', file);
            try {
                const token = typeof authToken === 'function' ? authToken() : null;
                const resp = await fetch(`${API_BASE_URL}/player/${player.id}/avatar`, {
                    method: 'POST',
                    headers: {
                        'ngrok-skip-browser-warning': '1',
                        ...(token ? { 'Authorization': `Bearer ${token}` } : {}),
                    },
                    body: form,
                });
                if (!resp.ok) {
                    const j = await resp.json().catch(() => ({}));
                    throw new Error(j.error || `Erro ${resp.status}`);
                }
                notifySuccess('Foto de perfil atualizada!');
                loadProfile();
            } catch (err) {
                notifyError(err.message, 'Não foi possível enviar a foto');
            }
        });
    }
}

function renderDecksSection(decks) {
    if (!decks.length) {
        return `<div class="empty-state"><div class="icon"><i class="bi bi-layers"></i></div><div class="title">Nenhum deck registrado ainda</div></div>`;
    }
    return `
        <div class="row g-3">
            <div class="col-md-6">
                <div class="chart-wrapper" style="height: 240px;"><canvas id="chartDecks"></canvas></div>
            </div>
            <div class="col-md-6">
                <div class="d-flex flex-column gap-2">
                    ${decks.slice(0, 6).map(d => `
                        <div class="timeline-row" style="margin-bottom:0;">
                            <div style="flex:1; min-width:0;">
                                <div style="font-weight:600;" class="text-truncate">${escapeHtml(d.deck)}</div>
                                <div class="text-muted-2" style="font-size:0.78rem;">${d.used} uso${d.used === 1 ? '' : 's'} · ${d.wins} vitória${d.wins === 1 ? '' : 's'}</div>
                            </div>
                            <span class="match-pill ${d.winRate >= 50 ? 'win' : d.winRate > 0 ? 'draw' : 'loss'}">${d.winRate}%</span>
                        </div>
                    `).join('')}
                </div>
            </div>
        </div>`;
}

function renderRecentMatches(matches) {
    if (!matches.length) {
        return `<div class="empty-state"><div class="icon"><i class="bi bi-inbox"></i></div><div class="title">Nenhuma partida ainda</div></div>`;
    }
    return matches.map(m => {
        const opponentLink = m.opponentId
            ? `<a href="/player.html?id=${m.opponentId}" style="color: var(--text-1); text-decoration: none;">${escapeHtml(m.opponentName)}</a>`
            : escapeHtml(m.opponentName);
        return `
            <div class="timeline-row">
                <span class="avatar" style="width:34px;height:34px;font-size:0.85rem;">${getInitials(m.opponentName)}</span>
                <div class="opp-info">
                    <div class="opp-name">vs ${opponentLink}</div>
                    <div class="opp-deck">${m.myDeck ? `<i class="bi bi-layers"></i> ${escapeHtml(m.myDeck)}` : ''} ${m.opponentDeck ? `· oponente: ${escapeHtml(m.opponentDeck)}` : ''}</div>
                </div>
                <div class="d-flex flex-column align-items-end gap-1">
                    ${resultPillHtml(m.result)}
                    <span class="match-date">${formatDate(m.date)}</span>
                </div>
            </div>`;
    }).join('');
}

function renderTournaments(tournaments) {
    if (!tournaments.length) {
        return `<div class="empty-state"><div class="icon"><i class="bi bi-trophy"></i></div><div class="title">Ainda não participou de torneios</div></div>`;
    }
    return `
        <div class="row g-3">
            ${tournaments.map(t => {
                const isChamp = t.isChampion;
                const isRunnerUp = t.finalPosition === '2º lugar';
                const statusInfo = tournamentStatusInfo(t.status);
                const posLabel = t.finalPosition
                    ? t.finalPosition
                    : (t.status === 2 ? 'Eliminado(a)' : 'Participando');
                const icon = isChamp ? 'bi-trophy-fill' : isRunnerUp ? 'bi-award-fill' : 'bi-flag';
                return `
                    <div class="col-md-6 col-lg-4">
                        <div class="tournament-card ${isChamp ? 'champion' : ''}">
                            <div class="tour-title">${escapeHtml(t.name)}</div>
                            <div class="tour-meta">
                                <i class="bi bi-calendar3"></i> ${formatDate(t.startDate)}
                                <span class="ms-2"><i class="bi bi-layers"></i> ${escapeHtml(t.deck || '-')}</span>
                            </div>
                            <div class="d-flex justify-content-between align-items-center mt-2 gap-2 flex-wrap">
                                <span class="status-pill ${statusInfo.cls}">${statusInfo.label}</span>
                                <span class="pos-badge"><i class="bi ${icon}"></i> ${posLabel}</span>
                            </div>
                        </div>
                    </div>`;
            }).join('')}
        </div>`;
}

function renderScoreHistoryChart(history, currentScore) {
    if (!history.length) {
        // Mostra placeholder
        const canvas = document.getElementById('chartScoreHistory');
        const ctx = canvas.getContext('2d');
        ctx.fillStyle = palette.text;
        ctx.font = '14px Inter';
        ctx.textAlign = 'center';
        ctx.fillText('Sem partidas registradas ainda', canvas.width / 2, canvas.height / 2);
        return;
    }
    const ctx = document.getElementById('chartScoreHistory').getContext('2d');
    if (charts.scoreHistory) charts.scoreHistory.destroy();
    charts.scoreHistory = new Chart(ctx, {
        type: 'line',
        data: {
            labels: history.map(h => {
                const d = new Date(h.date + 'T00:00:00');
                return d.toLocaleDateString('pt-BR', { day: '2-digit', month: 'short' });
            }),
            datasets: [{
                label: 'Pontuação',
                data: history.map(h => h.score),
                borderColor: palette.primary,
                backgroundColor: 'rgba(109, 111, 255, 0.18)',
                fill: true,
                tension: 0.3,
                pointRadius: 3,
                pointBackgroundColor: palette.primary,
                pointBorderColor: '#fff',
                pointBorderWidth: 1.5,
                borderWidth: 2,
            }],
        },
        options: { ...baseChartOptions, plugins: { ...baseChartOptions.plugins, legend: { display: false } } },
    });
}

function renderResultsChart(stats) {
    const ctx = document.getElementById('chartResults').getContext('2d');
    if (charts.results) charts.results.destroy();
    if (stats.played === 0) {
        ctx.fillStyle = palette.text;
        ctx.font = '14px Inter';
        ctx.textAlign = 'center';
        ctx.fillText('Sem partidas', ctx.canvas.width / 2, ctx.canvas.height / 2);
        return;
    }
    charts.results = new Chart(ctx, {
        type: 'doughnut',
        data: {
            labels: ['Vitórias', 'Derrotas', 'Empates'],
            datasets: [{
                data: [stats.wins, stats.losses, stats.draws],
                backgroundColor: [palette.accent, palette.danger, palette.warning],
                borderColor: 'rgba(11, 16, 32, 1)',
                borderWidth: 3,
                hoverOffset: 8,
            }],
        },
        options: {
            responsive: true, maintainAspectRatio: false, cutout: '60%',
            plugins: {
                legend: { position: 'bottom', labels: { color: palette.text, padding: 12 } },
                tooltip: baseChartOptions.plugins.tooltip,
            },
        },
    });
}

function renderDecksChart(decks) {
    const top = decks.slice(0, 6);
    const ctx = document.getElementById('chartDecks').getContext('2d');
    if (charts.decks) charts.decks.destroy();
    charts.decks = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: top.map(d => d.deck),
            datasets: [
                {
                    label: 'Usos',
                    data: top.map(d => d.used),
                    backgroundColor: 'rgba(0, 194, 255, 0.65)',
                    borderRadius: 6,
                },
                {
                    label: 'Vitórias',
                    data: top.map(d => d.wins),
                    backgroundColor: 'rgba(22, 224, 189, 0.85)',
                    borderRadius: 6,
                },
            ],
        },
        options: {
            ...baseChartOptions,
            indexAxis: 'y',
            scales: {
                x: { ...baseChartOptions.scales.x, beginAtZero: true, ticks: { ...baseChartOptions.scales.x.ticks, precision: 0 } },
                y: { ...baseChartOptions.scales.y, beginAtZero: undefined },
            },
        },
    });
}

document.addEventListener('DOMContentLoaded', loadProfile);
