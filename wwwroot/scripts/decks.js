/* ========== Deck Builder ========== */

const params = new URLSearchParams(window.location.search);
const playerId = parseInt(params.get('playerId'));

let currentDeckId = null;
let isReadOnly = false;
let deckName = '';
/** Map key: `${cardNumber}|${isDigiEgg}` -> { cardNumber, quantity, isDigiEgg, card } */
let deckCards = new Map();

let searchPage = 1;
let searchHasMore = false;
let restrictionsMap = new Map(); // cardNumber -> maxCopies (0 = banida)
let bannedPairs = [];

let currentSearchResults = []; // cartas da busca atual, por índice (permite atualizar a arte escolhida sem re-buscar)
let artsCache = new Map(); // cardNumber -> lista de variantes de arte (lazy, só busca quando o jogador clica)

function cardKey(cardNumber, isDigiEgg) {
    return `${cardNumber}|${isDigiEgg ? 1 : 0}`;
}

function colorClass(color) {
    return color ? `color-${color}` : '';
}

function colorClasses(card) {
    const c1 = colorClass(card.color);
    const c2 = card.color2 ? `color2-${card.color2}` : '';
    return `${c1} ${c2}`.trim();
}

const COLOR_OPTIONS = [
    { value: 'Red', label: 'Vermelho' },
    { value: 'Blue', label: 'Azul' },
    { value: 'Yellow', label: 'Amarelo' },
    { value: 'Green', label: 'Verde' },
    { value: 'Black', label: 'Preto' },
    { value: 'Purple', label: 'Roxo' },
    { value: 'White', label: 'Branco' },
    { value: 'Colorless', label: 'Incolor' },
];

// Monta N seletores de cor (um por "slot") quando o jogador escolhe quantas cores a carta deve ter.
// Cada slot filtra de forma independente — a carta precisa ter TODAS as cores escolhidas (não é OU).
function renderColorSlots(count) {
    const container = document.getElementById('colorSlotFilters');
    const options = COLOR_OPTIONS.map(c => `<option value="${c.value}">${c.label}</option>`).join('');
    container.innerHTML = Array.from({ length: count }, (_, i) => `
        <select class="form-select form-select-sm color-slot-select" style="width:auto;" data-slot="${i}">
            <option value="">Cor ${i + 1}</option>
            ${options}
        </select>`).join('');
}

async function loadRestrictions() {
    try {
        const response = await apiFetch(`${API_BASE_URL}/card/restrictions`);
        const data = await response.json();
        restrictionsMap = new Map(data.restrictions.map(r => [r.cardNumber, r.maxCopies]));
        bannedPairs = data.pairs;
    } catch (error) {
        console.error(error);
    }
}

async function loadFilterOptions() {
    try {
        const response = await apiFetch(`${API_BASE_URL}/card/filter-options`);
        const data = await response.json();

        const setSelect = document.getElementById('cardSetFilter');
        data.sets.forEach(code => {
            setSelect.innerHTML += `<option value="${escapeHtml(code)}">${escapeHtml(code)}</option>`;
        });

        const costSelect = document.getElementById('cardCostFilter');
        data.costs.forEach(cost => {
            costSelect.innerHTML += `<option value="${cost}">${cost}</option>`;
        });

        const levelSelect = document.getElementById('cardLevelFilter');
        data.levels.forEach(level => {
            levelSelect.innerHTML += `<option value="${level}">Lv.${level}</option>`;
        });
    } catch (error) {
        console.error(error);
    }
}

/* ---------- Lista de decks ---------- */

async function loadDeckList() {
    const grid = document.getElementById('deckListGrid');
    grid.innerHTML = `<div class="col-12 text-center py-4"><div class="spinner-border spinner-border-sm text-secondary"></div></div>`;
    try {
        const response = await apiFetch(`${API_BASE_URL}/deck?playerId=${playerId}`);
        const decks = await response.json();

        if (!decks.length) {
            grid.innerHTML = `<div class="col-12"><div class="empty-state"><div class="icon"><i class="bi bi-layers"></i></div><div class="title">Nenhum deck criado ainda</div><div>Clique em "Criar novo deck" para montar o primeiro.</div></div></div>`;
            return;
        }

        grid.innerHTML = decks.map(d => `
            <div class="col-md-6 col-lg-4">
                <div class="deck-card-card">
                    <div class="d-flex justify-content-between align-items-start">
                        <div class="deck-card-name">${escapeHtml(d.name)}</div>
                        ${d.isLocked ? '<span class="status-pill done" title="Já usado em partida/torneio — não pode mais ser editado"><i class="bi bi-lock-fill"></i> Travado</span>' : ''}
                    </div>
                    <div class="deck-card-meta">${d.cardCount} carta${d.cardCount === 1 ? '' : 's'} · atualizado em ${formatDate(d.updatedAt)}</div>
                    <div class="d-flex gap-2 mt-3">
                        <button class="btn btn-sm btn-secondary flex-grow-1" onclick="openEditDeck(${d.id}, ${d.isLocked})">
                            <i class="bi ${d.isLocked ? 'bi-eye' : 'bi-pencil'}"></i> ${d.isLocked ? 'Ver' : 'Editar'}
                        </button>
                        ${!d.isLocked ? `<button class="btn btn-sm btn-ghost" onclick="deleteDeck(${d.id}, '${escapeHtml(d.name)}')" title="Excluir"><i class="bi bi-trash3"></i></button>` : ''}
                    </div>
                </div>
            </div>`).join('');
    } catch (error) {
        grid.innerHTML = `<div class="col-12"><div class="empty-state"><div class="icon"><i class="bi bi-exclamation-octagon"></i></div><div class="title">Erro ao carregar decks</div><div>${escapeHtml(error.message)}</div></div></div>`;
    }
}

async function deleteDeck(id, name) {
    const result = await confirmAction({
        title: 'Excluir deck?',
        text: `O deck "${name}" será removido permanentemente.`,
        confirmText: 'Sim, excluir',
        cancelText: 'Cancelar',
        icon: 'warning',
    });
    if (!result.isConfirmed) return;
    try {
        await apiFetch(`${API_BASE_URL}/deck/${id}`, { method: 'DELETE' });
        notifySuccess('Deck excluído.');
        loadDeckList();
    } catch (error) {
        notifyError('Erro ao excluir deck: ' + error.message);
    }
}

/* ---------- Alternância de telas ---------- */

function showListView() {
    document.getElementById('deckListView').style.display = '';
    document.getElementById('deckBuilderView').style.display = 'none';
    loadDeckList();
}

function showBuilderView() {
    document.getElementById('deckListView').style.display = 'none';
    document.getElementById('deckBuilderView').style.display = '';
}

function openNewDeck() {
    currentDeckId = null;
    isReadOnly = false;
    deckName = '';
    deckCards = new Map();
    document.getElementById('deckNameInput').value = '';
    document.getElementById('deckNameInput').disabled = false;
    document.getElementById('saveDeckBtn').style.display = '';
    document.getElementById('importListBtn').style.display = '';
    document.getElementById('deckErrors').style.display = 'none';
    setSearchPanelEnabled(true);
    renderDeckZones();
    searchCards(true);
    showBuilderView();
}

async function openEditDeck(deckId, locked) {
    try {
        const response = await apiFetch(`${API_BASE_URL}/deck/${deckId}`);
        const data = await response.json();

        currentDeckId = deckId;
        isReadOnly = !!locked;
        deckCards = new Map();
        [...data.mainDeck, ...data.digiEggDeck].forEach(dc => {
            deckCards.set(cardKey(dc.cardNumber, dc.isDigiEgg), {
                cardNumber: dc.cardNumber,
                quantity: dc.quantity,
                isDigiEgg: dc.isDigiEgg,
                card: dc.card,
            });
        });

        document.getElementById('deckNameInput').value = data.name;
        document.getElementById('deckNameInput').disabled = isReadOnly;
        document.getElementById('saveDeckBtn').style.display = isReadOnly ? 'none' : '';
        document.getElementById('importListBtn').style.display = isReadOnly ? 'none' : '';
        document.getElementById('deckErrors').style.display = 'none';
        setSearchPanelEnabled(!isReadOnly);

        renderDeckZones();
        if (!isReadOnly) searchCards(true);
        else document.getElementById('cardResults').innerHTML = '<div class="text-muted-2 text-center py-3">Este deck já foi usado e não pode mais ser editado.</div>';
        showBuilderView();
    } catch (error) {
        notifyError('Erro ao abrir deck: ' + error.message);
    }
}

function setSearchPanelEnabled(enabled) {
    document.getElementById('cardSearchInput').disabled = !enabled;
    document.getElementById('cardColorCountFilter').disabled = !enabled;
    document.getElementById('cardTypeFilter').disabled = !enabled;
    document.getElementById('cardSetFilter').disabled = !enabled;
    document.getElementById('cardCostFilter').disabled = !enabled;
    document.getElementById('cardLevelFilter').disabled = !enabled;
    document.getElementById('clearFiltersBtn').disabled = !enabled;
    document.querySelectorAll('.color-slot-select').forEach(sel => { sel.disabled = !enabled; });
}

/* ---------- Busca de cartas ---------- */

async function searchCards(reset) {
    if (reset) {
        searchPage = 1;
        document.getElementById('cardResults').innerHTML = '';
        currentSearchResults = [];
    }
    const name = document.getElementById('cardSearchInput').value.trim();
    const colorCount = document.getElementById('cardColorCountFilter').value;
    const colors = [...document.querySelectorAll('.color-slot-select')].map(s => s.value).filter(Boolean);
    const type = document.getElementById('cardTypeFilter').value;
    const set = document.getElementById('cardSetFilter').value;
    const cost = document.getElementById('cardCostFilter').value;
    const level = document.getElementById('cardLevelFilter').value;

    const qs = new URLSearchParams();
    if (name) qs.append('name', name);
    if (colorCount) qs.append('colorCount', colorCount);
    colors.forEach(c => qs.append('colors', c));
    if (type) qs.append('type', type);
    if (set) qs.append('set', set);
    if (cost) qs.append('cost', cost);
    if (level) qs.append('level', level);
    qs.append('page', searchPage);
    qs.append('pageSize', 40);

    try {
        const response = await apiFetch(`${API_BASE_URL}/card?${qs.toString()}`);
        const data = await response.json();
        renderCardResults(data.items, !reset, data.artCounts || {});
        searchHasMore = (searchPage * 40) < data.total;
        document.getElementById('loadMoreBtn').style.display = searchHasMore ? '' : 'none';
    } catch (error) {
        console.error(error);
    }
}

function cardMetaLine(card) {
    const parts = [];
    if (card.level) parts.push(`Lv.${card.level}`);
    if (card.playCost !== null && card.playCost !== undefined) parts.push(`Custo ${card.playCost}`);
    if (card.dp) parts.push(`${card.dp} DP`);
    if (card.color) parts.push(card.color + (card.color2 ? `/${card.color2}` : ''));
    return parts.join(' · ');
}

function cardThumbHtml(card, artCount = 0) {
    const hasImg = !!card.imageUrl;
    const cyclable = artCount > 1;
    return `
        <div class="card-thumb${cyclable ? ' cyclable' : ''}" ${cyclable ? `data-card-number="${escapeHtml(card.cardNumber)}" title="${artCount} artes disponíveis — clique para trocar"` : ''}>
            ${hasImg ? `<img src="${escapeHtml(card.imageUrl)}" alt="" loading="lazy" onerror="this.style.display='none'; this.nextElementSibling.style.display='flex';">` : ''}
            <div class="card-thumb-placeholder" style="${hasImg ? 'display:none;' : ''}"><i class="bi bi-image"></i></div>
            ${cyclable ? `<span class="art-badge">${artCount}</span>` : ''}
        </div>`;
}

function renderCardResults(items, append, artCounts = {}) {
    const container = document.getElementById('cardResults');
    const startIndex = append ? currentSearchResults.length : 0;
    currentSearchResults.push(...items);

    const html = items.map((card, i) => {
        const index = startIndex + i;
        return `
        <div class="card-row ${colorClasses(card)}" data-index="${index}" ${card.imageUrlLarge ? `data-image-large="${escapeHtml(card.imageUrlLarge)}"` : ''}>
            ${cardThumbHtml(card, artCounts[card.cardNumber] || 0)}
            <div class="card-main">
                <div class="card-name-line">
                    <span>${escapeHtml(card.name)}</span>
                    <span class="card-number">${escapeHtml(card.cardNumber)}</span>
                </div>
                <div class="card-meta" title="${escapeHtml(card.mainEffect || '')}">${escapeHtml(cardMetaLine(card))}</div>
            </div>
            <div class="card-actions">
                <button class="btn btn-sm btn-ghost" onclick="addCardToDeckAt(${index})" title="Adicionar ao deck">
                    <i class="bi bi-plus-circle"></i>
                </button>
            </div>
        </div>`;
    }).join('');

    container.innerHTML = append ? container.innerHTML + html : html;
}

// Busca (uma vez, cacheada) as variantes de arte conhecidas de uma carta e cicla pra próxima
// no resultado de busca de índice `index` — a arte escolhida é o que vai pro deck ao clicar "+".
async function cycleCardArt(index) {
    const card = currentSearchResults[index];
    if (!card) return;

    let arts = artsCache.get(card.cardNumber);
    if (!arts) {
        try {
            const response = await apiFetch(`${API_BASE_URL}/card/${encodeURIComponent(card.cardNumber)}/arts`);
            arts = await response.json();
        } catch (_) {
            arts = [];
        }
        artsCache.set(card.cardNumber, arts);
    }
    if (arts.length < 2) return;

    const currentIdx = arts.findIndex(a => a.tcgplayerId === card.tcgplayerId);
    const next = arts[(currentIdx + 1) % arts.length];

    card.tcgplayerId = next.tcgplayerId;
    card.imageUrl = next.imageUrl;
    card.imageUrlLarge = next.imageUrlLarge;

    const row = document.querySelector(`#cardResults .card-row[data-index="${index}"]`);
    if (!row) return;
    row.querySelector('.card-thumb').outerHTML = cardThumbHtml(card, arts.length);
    if (card.imageUrlLarge) row.dataset.imageLarge = card.imageUrlLarge;
}

function addCardToDeckAt(index) {
    const card = currentSearchResults[index];
    if (card) addCardToDeck(card);
}

/* ---------- Montagem do deck ---------- */

function addCardToDeck(card) {
    if (isReadOnly) return;
    const isDigiEgg = card.type === 'Digi-Egg';
    const key = cardKey(card.cardNumber, isDigiEgg);
    const existing = deckCards.get(key);
    if (existing) {
        existing.quantity += 1;
    } else {
        deckCards.set(key, { cardNumber: card.cardNumber, quantity: 1, isDigiEgg, card });
    }
    renderDeckZones();
}

function changeQuantity(cardNumber, isDigiEgg, delta) {
    if (isReadOnly) return;
    const key = cardKey(cardNumber, isDigiEgg);
    const entry = deckCards.get(key);
    if (!entry) return;
    entry.quantity += delta;
    if (entry.quantity <= 0) deckCards.delete(key);
    renderDeckZones();
}

// Ordena por nível/custo crescente (curva de mana visual), depois por número da carta.
function sortDeckEntries(a, b) {
    const levelA = a.card?.level ?? 0, levelB = b.card?.level ?? 0;
    if (levelA !== levelB) return levelA - levelB;
    const costA = a.card?.playCost ?? 0, costB = b.card?.playCost ?? 0;
    if (costA !== costB) return costA - costB;
    return a.cardNumber.localeCompare(b.cardNumber);
}

function renderDeckZones() {
    const mainEntries = [...deckCards.values()].filter(e => !e.isDigiEgg).sort(sortDeckEntries);
    const digiEggEntries = [...deckCards.values()].filter(e => e.isDigiEgg).sort(sortDeckEntries);

    document.getElementById('mainZoneList').innerHTML = mainEntries.map(renderDeckCardTiles).join('');
    document.getElementById('digiEggZoneList').innerHTML = digiEggEntries.map(renderDeckCardTiles).join('');

    const mainTotal = mainEntries.reduce((sum, e) => sum + e.quantity, 0);
    const digiEggTotal = digiEggEntries.reduce((sum, e) => sum + e.quantity, 0);

    document.getElementById('mainZoneCount').textContent = `(${mainTotal}/50)`;
    document.getElementById('digiEggZoneCount').textContent = `(${digiEggTotal}/5)`;

    const mainCounterEl = document.getElementById('mainCounter');
    mainCounterEl.textContent = `Principal: ${mainTotal}/50`;
    mainCounterEl.className = 'deck-counter ' + (mainTotal === 50 ? 'valid' : 'invalid');

    const digiEggCounterEl = document.getElementById('digiEggCounter');
    digiEggCounterEl.textContent = `Digi-Egg: ${digiEggTotal}/5`;
    digiEggCounterEl.className = 'deck-counter ' + (digiEggTotal <= 5 ? 'valid' : 'invalid');
}

// Cada cópia da carta vira um "quadradinho" próprio no grid (em vez de 1 linha com contador) —
// clique esquerdo adiciona +1 cópia, clique direito remove -1.
function renderDeckCardTiles(entry) {
    const card = entry.card || {};
    const maxAllowed = restrictionsMap.has(entry.cardNumber) ? restrictionsMap.get(entry.cardNumber) : 4;
    const overLimit = entry.quantity > maxAllowed;
    const title = overLimit
        ? `${card.name || entry.cardNumber} — ${entry.quantity}/${maxAllowed} (acima do limite)`
        : `${card.name || entry.cardNumber}${!isReadOnly ? ' — clique esquerdo: +1 · clique direito: -1' : ''}`;
    const tile = `
        <div class="card-row deck-grid-tile ${colorClasses(card)}${overLimit ? ' over-limit' : ''}${isReadOnly ? ' read-only' : ''}"
             ${!isReadOnly ? `onclick="changeQuantity('${entry.cardNumber}', ${entry.isDigiEgg}, 1)" oncontextmenu="event.preventDefault(); changeQuantity('${entry.cardNumber}', ${entry.isDigiEgg}, -1)"` : ''}
             title="${escapeHtml(title)}"
             ${card.imageUrlLarge ? `data-image-large="${escapeHtml(card.imageUrlLarge)}"` : ''}>
            ${cardThumbHtml(card)}
        </div>`;
    return tile.repeat(entry.quantity);
}

/* ---------- Preview ampliado ao passar o mouse ---------- */

let hoverPreviewEl = null;

function initCardHoverPreview() {
    hoverPreviewEl = document.createElement('div');
    hoverPreviewEl.className = 'card-hover-preview';
    hoverPreviewEl.innerHTML = '<img alt="">';
    document.body.appendChild(hoverPreviewEl);

    document.addEventListener('mouseover', (e) => {
        const row = e.target.closest('.card-row');
        const imageUrl = row?.dataset.imageLarge;
        if (!imageUrl) return;
        hoverPreviewEl.querySelector('img').src = imageUrl;
        hoverPreviewEl.style.display = 'block';
        positionCardHoverPreview(e.clientX, e.clientY);
    });

    document.addEventListener('mousemove', (e) => {
        if (hoverPreviewEl.style.display === 'block') positionCardHoverPreview(e.clientX, e.clientY);
    });

    document.addEventListener('mouseout', (e) => {
        const row = e.target.closest('.card-row');
        if (!row || row.contains(e.relatedTarget)) return;
        hoverPreviewEl.style.display = 'none';
    });
}

function positionCardHoverPreview(x, y) {
    const margin = 16;
    const width = 300;
    const height = 420; // aprox. proporção 5:7 das cartas do Digimon Card Game
    let left = x + margin;
    let top = y - height / 2;
    if (left + width > window.innerWidth) left = x - margin - width;
    top = Math.max(margin, Math.min(top, window.innerHeight - height - margin));
    hoverPreviewEl.style.left = `${left}px`;
    hoverPreviewEl.style.top = `${top}px`;
}

/* ---------- Importar lista pronta ---------- */

// Lê o formato de decklist do simulador DCGO: "<quantidade> <nome>   <número da carta>" por linha.
// A imagem/arte escolhida no DCGO vem sufixada no número (ex.: "BT11-062_P1" = 1ª variante paralela);
// isso não corresponde 1:1 às nossas variantes de arte, então só usamos o número base pra achar a carta.
function parseDecklistText(text) {
    const entries = [];
    const unparsed = [];
    text.split(/\r?\n/).forEach(rawLine => {
        const line = rawLine.trim();
        if (!line || line.startsWith('//') || line.startsWith('#')) return;
        const match = line.match(/^(\d+)\s+(.+?)\s+([A-Za-z0-9]+-[A-Za-z0-9]+(?:_P\d+)?)$/i);
        if (!match) { unparsed.push(rawLine); return; }
        const [, qtyStr, name, rawCode] = match;
        const quantity = parseInt(qtyStr, 10);
        if (!quantity) { unparsed.push(rawLine); return; }
        entries.push({ quantity, name: name.trim(), cardNumber: rawCode.replace(/_P\d+$/i, '').toUpperCase() });
    });
    return { entries, unparsed };
}

async function importDeckList() {
    if (isReadOnly) return;

    if (deckCards.size > 0) {
        const result = await confirmAction({
            title: 'Substituir cartas do deck?',
            text: 'A lista importada vai substituir todas as cartas já adicionadas neste deck.',
            confirmText: 'Sim, substituir',
            cancelText: 'Cancelar',
            icon: 'warning',
        });
        if (!result.isConfirmed) return;
    }

    const { value: text, isConfirmed } = await Swal.fire({
        title: 'Importar lista de deck',
        html: '<p class="text-muted-2 mb-2" style="font-size:0.85rem;">Cole a lista no formato do simulador DCGO (quantidade, nome e número da carta em cada linha).</p>',
        input: 'textarea',
        inputPlaceholder: '4 Koromon   EX10-002_P1\n4 Agumon   BT14-007\n...',
        inputAttributes: { style: 'height: 260px; font-family: monospace; font-size: 0.8rem;' },
        showCancelButton: true,
        confirmButtonText: 'Importar',
        cancelButtonText: 'Cancelar',
        reverseButtons: true,
    });
    if (!isConfirmed || !text || !text.trim()) return;

    const { entries, unparsed } = parseDecklistText(text);
    if (!entries.length) {
        notifyWarning('Nenhuma carta reconhecida na lista colada.');
        return;
    }

    const qtyByNumber = new Map();
    entries.forEach(e => qtyByNumber.set(e.cardNumber, (qtyByNumber.get(e.cardNumber) || 0) + e.quantity));

    let cardsFound;
    try {
        const response = await apiFetch(`${API_BASE_URL}/card/lookup`, {
            method: 'POST',
            body: JSON.stringify({ cardNumbers: [...qtyByNumber.keys()] }),
        });
        cardsFound = await response.json();
    } catch (error) {
        notifyError('Erro ao buscar cartas: ' + error.message);
        return;
    }

    const cardByNumber = new Map(cardsFound.map(c => [c.cardNumber, c]));
    const notFound = [...qtyByNumber.keys()].filter(cn => !cardByNumber.has(cn));

    deckCards = new Map();
    qtyByNumber.forEach((quantity, cardNumber) => {
        const card = cardByNumber.get(cardNumber);
        if (!card) return;
        const isDigiEgg = card.type === 'Digi-Egg';
        deckCards.set(cardKey(cardNumber, isDigiEgg), { cardNumber, quantity, isDigiEgg, card });
    });
    renderDeckZones();

    const warnings = [];
    if (unparsed.length) warnings.push(`${unparsed.length} linha(s) não reconhecida(s): ${unparsed.slice(0, 5).join(' | ')}${unparsed.length > 5 ? '…' : ''}`);
    if (notFound.length) warnings.push(`Carta(s) não encontrada(s) na base: ${notFound.join(', ')}.`);

    if (warnings.length) {
        notifyWarning(warnings.join(' '), 'Lista importada com ressalvas');
    } else {
        notifySuccess(`${entries.length} carta(s) importada(s).`);
    }
}

/* ---------- Exportar lista ---------- */

// Gera a lista no mesmo formato aceito pela importação (DCGO): "<quantidade> <nome>   <número da carta>".
function buildDecklistText() {
    const digiEggEntries = [...deckCards.values()].filter(e => e.isDigiEgg).sort(sortDeckEntries);
    const mainEntries = [...deckCards.values()].filter(e => !e.isDigiEgg).sort(sortDeckEntries);
    return [...digiEggEntries, ...mainEntries]
        .map(e => `${e.quantity} ${e.card?.name || e.cardNumber}   ${e.cardNumber}`)
        .join('\n');
}

async function exportDeckList() {
    if (deckCards.size === 0) {
        notifyWarning('Não há cartas no deck para exportar.');
        return;
    }

    const text = buildDecklistText();
    const { isConfirmed } = await Swal.fire({
        title: 'Exportar lista de deck',
        html: '<p class="text-muted-2 mb-2" style="font-size:0.85rem;">Lista no mesmo formato aceito por "Importar lista" — copie e cole onde precisar.</p>',
        input: 'textarea',
        inputValue: text,
        inputAttributes: { readonly: true, style: 'height: 260px; font-family: monospace; font-size: 0.8rem;' },
        showCancelButton: true,
        confirmButtonText: 'Copiar',
        cancelButtonText: 'Fechar',
        reverseButtons: true,
        didOpen: () => Swal.getInput()?.select(),
    });
    if (!isConfirmed) return;

    try {
        await navigator.clipboard.writeText(text);
        notifySuccess('Lista copiada para a área de transferência.');
    } catch (error) {
        notifyError('Não foi possível copiar automaticamente. Selecione o texto e copie manualmente.');
    }
}

/* ---------- Salvar ---------- */

async function saveDeck() {
    const name = document.getElementById('deckNameInput').value.trim();
    if (!name) {
        notifyWarning('Informe o nome do deck.');
        return;
    }

    const cards = [...deckCards.values()].map(e => ({
        cardNumber: e.cardNumber,
        quantity: e.quantity,
        isDigiEgg: e.isDigiEgg,
        tcgplayerId: e.card?.tcgplayerId ?? null,
    }));

    const payload = { playerId, name, cards };
    const errorsBox = document.getElementById('deckErrors');
    errorsBox.style.display = 'none';

    const btn = document.getElementById('saveDeckBtn');
    btn.disabled = true;
    try {
        if (currentDeckId) {
            await apiFetch(`${API_BASE_URL}/deck/${currentDeckId}`, { method: 'PUT', body: JSON.stringify(payload) });
        } else {
            await apiFetch(`${API_BASE_URL}/deck`, { method: 'POST', body: JSON.stringify(payload) });
        }
        notifySuccess('Deck salvo!');
        showListView();
    } catch (error) {
        const messages = error.data?.errors?.length ? error.data.errors : [error.message];
        errorsBox.innerHTML = `<div class="alert alert-danger mb-0"><strong>Não foi possível salvar:</strong><ul class="mb-0 mt-1">${messages.map(m => `<li>${escapeHtml(m)}</li>`).join('')}</ul></div>`;
        errorsBox.style.display = '';
    } finally {
        btn.disabled = false;
    }
}

/* ---------- Inicialização ---------- */

document.addEventListener('DOMContentLoaded', async () => {
    if (!playerId) {
        document.getElementById('pageSubtitle').textContent = 'Jogador inválido.';
        return;
    }

    try {
        const response = await apiFetch(`${API_BASE_URL}/player/${playerId}`);
        const player = await response.json();
        document.getElementById('pageSubtitle').textContent = `Gerencie os decks de ${player.name}`;
        document.getElementById('backLink').href = `/player.html?id=${playerId}`;
    } catch (_) {
        document.getElementById('backLink').href = '/Index.html';
    }

    await Promise.all([loadRestrictions(), loadFilterOptions()]);
    loadDeckList();
    initCardHoverPreview();

    document.getElementById('newDeckBtn').addEventListener('click', openNewDeck);
    document.getElementById('cancelBuilderBtn').addEventListener('click', showListView);
    document.getElementById('saveDeckBtn').addEventListener('click', saveDeck);
    document.getElementById('importListBtn').addEventListener('click', importDeckList);
    document.getElementById('exportListBtn').addEventListener('click', exportDeckList);
    document.getElementById('loadMoreBtn').addEventListener('click', () => { searchPage++; searchCards(false); });

    let searchDebounce;
    document.getElementById('cardSearchInput').addEventListener('input', () => {
        clearTimeout(searchDebounce);
        searchDebounce = setTimeout(() => searchCards(true), 350);
    });
    document.getElementById('cardColorCountFilter').addEventListener('change', (e) => {
        renderColorSlots(e.target.value ? parseInt(e.target.value, 10) : 0);
        searchCards(true);
    });
    document.getElementById('cardTypeFilter').addEventListener('change', () => searchCards(true));
    document.getElementById('cardSetFilter').addEventListener('change', () => searchCards(true));
    document.getElementById('cardCostFilter').addEventListener('change', () => searchCards(true));
    document.getElementById('cardLevelFilter').addEventListener('change', () => searchCards(true));
    document.getElementById('colorSlotFilters').addEventListener('change', (e) => {
        if (e.target.classList.contains('color-slot-select')) searchCards(true);
    });
    document.getElementById('clearFiltersBtn').addEventListener('click', () => {
        document.getElementById('cardSearchInput').value = '';
        document.getElementById('cardColorCountFilter').value = '';
        document.getElementById('cardTypeFilter').value = '';
        document.getElementById('cardSetFilter').value = '';
        document.getElementById('cardCostFilter').value = '';
        document.getElementById('cardLevelFilter').value = '';
        renderColorSlots(0);
        searchCards(true);
    });

    document.getElementById('cardResults').addEventListener('click', (e) => {
        const thumb = e.target.closest('.card-thumb[data-card-number]');
        const row = thumb?.closest('.card-row');
        if (!row) return;
        cycleCardArt(parseInt(row.dataset.index, 10));
    });

    window.openEditDeck = openEditDeck;
    window.deleteDeck = deleteDeck;
    window.addCardToDeck = addCardToDeck;
    window.addCardToDeckAt = addCardToDeckAt;
    window.changeQuantity = changeQuantity;
});
