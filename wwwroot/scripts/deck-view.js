/* ========== Visualização somente-leitura da lista de deck de um participante ==========
   Aberta pela tela de resumo do torneio. Não tem nenhuma ação de edição: nem busca de
   cartas, nem alterar quantidade, nem salvar. O backend só devolve a lista depois que o
   torneio é finalizado, então nada aqui expõe deck de torneio em andamento. */

const params = new URLSearchParams(window.location.search);
const tournamentId = params.get('tournamentId');
const tpId = params.get('tpId');

function colorClass(color) {
    return color ? `color-${color}` : '';
}

function colorClasses(card) {
    const c1 = colorClass(card.color);
    const c2 = card.color2 ? `color2-${card.color2}` : '';
    return `${c1} ${c2}`.trim();
}

// Mesma ordenação do construtor: nível/custo crescente (curva visual), depois número da carta.
function sortEntries(a, b) {
    const levelA = a.card?.level ?? 0, levelB = b.card?.level ?? 0;
    if (levelA !== levelB) return levelA - levelB;
    const costA = a.card?.playCost ?? 0, costB = b.card?.playCost ?? 0;
    if (costA !== costB) return costA - costB;
    return a.cardNumber.localeCompare(b.cardNumber);
}

// Cada cópia da carta vira um quadradinho, igual ao construtor — mas sem handler de clique.
function renderTiles(entry) {
    const card = entry.card || {};
    const title = `${card.name || entry.cardNumber} (${entry.cardNumber})`;
    const tile = `
        <div class="card-row deck-grid-tile read-only ${colorClasses(card)}"
             title="${escapeHtml(title)}"
             ${card.imageUrlLarge ? `data-image-large="${escapeHtml(card.imageUrlLarge)}"` : ''}>
            <div class="card-thumb">
                ${card.imageUrl
                    ? `<img src="${escapeHtml(card.imageUrl)}" alt="" loading="lazy" onerror="this.style.display='none'; this.nextElementSibling.style.display='flex';">`
                    : ''}
                <div class="card-thumb-placeholder" style="${card.imageUrl ? 'display:none;' : ''}"><i class="bi bi-image"></i></div>
            </div>
        </div>`;
    return tile.repeat(entry.quantity);
}

function showError(message) {
    document.getElementById('pageSubtitle').textContent = 'Lista indisponível.';
    document.getElementById('errorText').textContent = message;
    document.getElementById('errorBlock').style.display = '';
    document.getElementById('deckBlock').style.display = 'none';
}

async function loadDeck() {
    const backLink = document.getElementById('backLink');
    backLink.href = tournamentId ? `/tournament-recap.html?id=${tournamentId}` : '/tournaments.html';

    if (!tournamentId || !tpId) {
        showError('Link incompleto: faltou o torneio ou o participante.');
        return;
    }

    try {
        const response = await apiFetch(`${API_BASE_URL}/tournament/${tournamentId}/participants/${tpId}/deck`);
        const data = await response.json();

        document.title = `${data.playerName} — ${data.deckName || 'Deck'}`;
        document.getElementById('deckTitle').textContent = data.deckName || 'Lista do deck';
        document.getElementById('pageSubtitle').textContent =
            `Lista usada por ${data.playerName} em ${data.tournamentName}. Somente visualização.`;
        document.getElementById('playerName').textContent = data.playerName;
        document.getElementById('deckName').innerHTML = `<i class="bi bi-layers"></i> ${escapeHtml(data.deckName || '—')}`;
        document.getElementById('playerAvatar').innerHTML = data.avatarUrl
            ? `<img src="${escapeHtml(data.avatarUrl)}" class="avatar avatar-img" alt="${escapeHtml(data.playerName)}">`
            : `<span class="avatar">${getInitials(data.playerName || '')}</span>`;

        const main = (data.mainDeck || []).slice().sort(sortEntries);
        const eggs = (data.digiEggDeck || []).slice().sort(sortEntries);

        document.getElementById('mainZoneList').innerHTML = main.map(renderTiles).join('');
        document.getElementById('digiEggZoneList').innerHTML = eggs.map(renderTiles).join('');

        const mainTotal = main.reduce((sum, e) => sum + e.quantity, 0);
        const eggTotal = eggs.reduce((sum, e) => sum + e.quantity, 0);
        document.getElementById('mainZoneCount').textContent = `(${mainTotal})`;
        document.getElementById('digiEggZoneCount').textContent = `(${eggTotal})`;
        document.getElementById('mainCounter').textContent = `Principal: ${mainTotal}`;
        document.getElementById('digiEggCounter').textContent = `Digi-Egg: ${eggTotal}`;

        document.getElementById('deckBlock').style.display = '';
        // Vem do common.js; se o navegador tiver uma cópia antiga em cache, a lista continua
        // sendo exibida normalmente — só o zoom fica indisponível.
        if (typeof initCardHoverPreview === 'function') initCardHoverPreview();
    } catch (error) {
        showError(error.message || 'Erro ao carregar a lista do deck.');
    }
}

loadDeck();
