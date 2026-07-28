/* ========== Resumo pós-torneio ========== */

const params = new URLSearchParams(window.location.search);
const tournamentId = params.get('id');

// Quem está no pódio já tem o link da decklist ali embaixo; na lista de participantes
// o link é omitido pra não aparecer duas vezes.
let podiumTpIds = [];

const FORMAT_LABELS = { 0: 'Dupla Eliminação', 1: 'Swiss + Top Cut', 2: 'Swiss Pontos Corridos', 3: 'Todos contra todos + Top Cut' };

function show(id) {
    ['loadingBlock', 'errorBlock', 'recapContent'].forEach(b => {
        document.getElementById(b).style.display = b === id ? '' : 'none';
    });
}

function cssVar(name) {
    return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
}

async function load() {
    if (!tournamentId) {
        document.getElementById('errorText').textContent = 'Torneio não informado.';
        show('errorBlock');
        return;
    }
    try {
        const data = await apiFetch(`${API_BASE_URL}/tournament/${tournamentId}/recap`).then(r => r.json());
        renderHero(data.tournament, data.stats);
        renderStats(data.stats);
        podiumTpIds = (data.podium || []).map(p => p.participantId).filter(Boolean);
        renderPodium(data.podium);
        renderStandings(data);
        renderParticipants(data.participants);
        renderPieChart(data.deckNameBreakdown);
        renderTopCards(data.topCards, data.topCardsMode, data.stats.totalParticipants);
        show('recapContent');
    } catch (error) {
        document.getElementById('errorText').textContent = error.message || 'Não foi possível carregar o resumo.';
        show('errorBlock');
    }
}

function renderHero(t, stats) {
    document.title = `${t.name} — Resumo`;
    document.getElementById('recapTourName').textContent = t.name;

    const bracketLink = document.getElementById('backToBracketLink');
    bracketLink.href = t.format === 0
        ? `/tournament-double-bracket.html?id=${t.id}`
        : `/tournament-swiss.html?id=${t.id}`;
    bracketLink.style.display = '';

    const modeLabel = t.mode === 1
        ? '<i class="bi bi-controller"></i> Online'
        : '<i class="bi bi-people-fill"></i> Presencial';
    const formatLabel = FORMAT_LABELS[t.format] || 'Torneio';
    const finished = t.finishedAt ? formatDate(t.finishedAt) : formatDate(t.endDate);

    document.getElementById('recapTourMeta').innerHTML =
        `${modeLabel} <span class="ms-2 ps-2" style="border-left:1px solid var(--border);">${escapeHtml(formatLabel)}</span>` +
        `<span class="ms-2 ps-2" style="border-left:1px solid var(--border);"><i class="bi bi-calendar-check"></i> Encerrado em ${finished}</span>`;
}

function renderStats(stats) {
    const tiles = [
        { value: stats.totalParticipants, label: 'Participantes', icon: 'bi-people-fill' },
        { value: stats.totalMatches, label: 'Partidas', icon: 'bi-controller' },
        { value: stats.totalDraws, label: 'Empates', icon: 'bi-arrow-left-right' },
    ];
    if (stats.totalRounds) tiles.push({ value: stats.totalRounds, label: 'Rodadas Swiss', icon: 'bi-collection' });

    document.getElementById('statsRow').innerHTML = tiles.map(t => `
        <div class="col-6 col-md-3">
            <div class="stat-tile">
                <div class="stat-value">${t.value}</div>
                <div class="stat-label"><i class="bi ${t.icon} me-1"></i>${t.label}</div>
            </div>
        </div>`).join('');
}

function renderPodium(podium) {
    const container = document.getElementById('podiumContainer');
    if (!podium.length) {
        container.innerHTML = '<div class="text-muted-2">Sem colocação registrada.</div>';
        return;
    }
    const byPos = {};
    podium.forEach(p => { byPos[p.position] = p; });

    container.innerHTML = [2, 1, 3].map(pos => {
        const p = byPos[pos];
        if (!p) return '';
        const avatar = p.avatarUrl
            ? `<img src="${escapeHtml(p.avatarUrl)}" class="podium-avatar" alt="${escapeHtml(p.playerName)}">`
            : `<span class="podium-avatar-fallback">${getInitials(p.playerName || '')}</span>`;
        const deckListLink = p.hasDeckList
            ? `<a href="/deck-view.html?tournamentId=${tournamentId}&tpId=${p.participantId}"
                  class="btn btn-sm btn-ghost podium-decklist" title="Ver a lista completa do deck de ${escapeHtml(p.playerName || '')}">
                   <i class="bi bi-list-ul"></i> Ver lista
               </a>`
            : '';
        return `
            <div class="podium-place place-${pos}">
                ${avatar}
                <div class="podium-name">${escapeHtml(p.playerName || 'Desconhecido')}</div>
                <div class="podium-deck text-muted-2">${escapeHtml(p.deck || '')}</div>
                <div class="podium-block"><span>${pos}º</span></div>
                <div class="podium-bonus">+${p.bonus} pts</div>
                ${deckListLink}
            </div>`;
    }).join('');
}

function renderStandings(data) {
    const section = document.getElementById('standingsSection');
    if (!data.isFullyRanked || !data.standings || !data.standings.length) {
        section.style.display = 'none';
        return;
    }
    section.style.display = '';
    document.getElementById('standingsTable').innerHTML = data.standings.map(s => `
        <tr>
            <td><span class="text-muted-2">${s.position}º</span></td>
            <td>
                <div style="font-weight:600;">${escapeHtml(s.playerName || 'Desconhecido')}</div>
                <div class="text-muted-2" style="font-size:0.78rem;">${escapeHtml(s.deck || '')}</div>
            </td>
            <td style="text-align:center;font-weight:700;color:var(--accent);">${s.points}</td>
            <td style="text-align:center;color:var(--text-2);">${s.wins}-${s.losses}-${s.draws}</td>
            <td style="text-align:right;color:var(--text-3);font-size:0.85rem;">${s.omw}%</td>
        </tr>`).join('');
}

function renderParticipants(participants) {
    // O top 3 já aparece no pódio (com nome, deck e link da lista), então aqui ficam
    // só os demais participantes — sem repetir ninguém.
    const outros = participants.filter(p => !podiumTpIds.includes(p.id));
    const container = document.getElementById('participantsList');

    if (!outros.length) {
        container.innerHTML = '<div class="text-muted-2">Todos os participantes estão no pódio.</div>';
        return;
    }

    container.innerHTML = outros.map(p => {
        const cover = p.deckCoverImageUrl
            ? `<img src="${escapeHtml(p.deckCoverImageUrl)}" class="participant-cover" alt="">`
            : `<span class="participant-cover d-flex align-items-center justify-content-center text-muted-2"><i class="bi bi-layers"></i></span>`;
        const avatar = p.avatarUrl
            ? `<img src="${escapeHtml(p.avatarUrl)}" class="participant-avatar" alt="">`
            : `<span class="avatar participant-avatar" style="font-size:0.7rem;">${getInitials(p.playerName || '')}</span>`;
        // Só quem registrou um deck salvo tem lista pra abrir
        const deckListLink = p.hasDeckList
            ? `<a href="/deck-view.html?tournamentId=${tournamentId}&tpId=${p.id}" class="btn btn-sm btn-ghost" title="Ver a lista completa do deck">
                   <i class="bi bi-list-ul"></i> Ver lista
               </a>`
            : '';
        return `
            <div class="participant-row">
                ${cover}
                <div class="flex-grow-1">
                    <div style="display:flex;align-items:center;gap:0.5rem;">
                        ${avatar}
                        <span style="font-weight:600;">${escapeHtml(p.playerName || 'Desconhecido')}</span>
                    </div>
                    <div class="text-muted-2" style="font-size:0.8rem;margin-left:2.4rem;">${escapeHtml(p.deck || 'Sem deck informado')}</div>
                </div>
                ${deckListLink}
            </div>`;
    }).join('');
}

function renderPieChart(breakdown) {
    const canvas = document.getElementById('chartDecks');
    if (!breakdown.length) {
        canvas.parentElement.innerHTML = '<div class="text-center text-muted-2 py-4">Sem dados de decks.</div>';
        return;
    }
    const colors = [
        cssVar('--primary') || '#6d6fff', cssVar('--accent') || '#16e0bd', cssVar('--warning') || '#ffb547',
        cssVar('--danger') || '#ff5d73', cssVar('--accent-2') || '#00c2ff', cssVar('--primary-2') || '#8d6bff',
        cssVar('--success') || '#4ade80',
    ];
    new Chart(canvas.getContext('2d'), {
        type: 'pie',
        data: {
            labels: breakdown.map(b => b.deck),
            datasets: [{
                data: breakdown.map(b => b.count),
                backgroundColor: breakdown.map((_, i) => colors[i % colors.length]),
                borderColor: cssVar('--bg-1') || '#0b1020',
                borderWidth: 2,
                hoverOffset: 8,
            }],
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { position: 'bottom', labels: { color: cssVar('--text-2') || '#b9c0d9', font: { family: 'Inter, sans-serif' }, boxWidth: 12 } },
                tooltip: {
                    backgroundColor: cssVar('--tooltip-bg') || 'rgba(11,16,32,0.95)',
                    borderColor: cssVar('--border') || 'rgba(255,255,255,0.1)',
                    borderWidth: 1, padding: 10, cornerRadius: 8,
                },
            },
        },
    });
}

function renderTopCards(topCards, mode, totalDecks) {
    const section = document.getElementById('topCardsSection');
    if (!topCards.length) { section.style.display = 'none'; return; }
    section.style.display = '';

    section.querySelector('.card-header').innerHTML = mode === 'shared'
        ? '<i class="bi bi-star-fill"></i> Cartas mais jogadas no torneio'
        : '<i class="bi bi-star-fill"></i> Carta de destaque de cada deck';

    document.getElementById('topCardsGrid').innerHTML = topCards.map(c => {
        const caption = mode === 'shared'
            ? `${c.deckCount}/${totalDecks} decks`
            : escapeHtml(c.playerName || '');
        return `
        <div class="col-6 col-md-3 col-lg-2">
            <div class="top-card-tile">
                <img src="${c.imageUrl ? escapeHtml(c.imageUrl) : ''}" alt="${escapeHtml(c.name || c.cardNumber)}" loading="lazy"
                     onerror="this.style.visibility='hidden'">
                <div style="font-size:0.8rem;font-weight:600;margin-top:0.3rem;">${escapeHtml(c.name || c.cardNumber)}</div>
                <div class="tc-rate">${caption}</div>
            </div>
        </div>`;
    }).join('');
}

load();
