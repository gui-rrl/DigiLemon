/* ========== Configurar Torneio (lobby) ========== */

const urlParams = new URLSearchParams(window.location.search);
const tournamentId = urlParams.get('id');
let currentTournament = null;
let allPlayers = [];            // todos os jogadores cadastrados (para o select de inscrição manual)
let registeredPlayerIds = [];   // já inscritos neste torneio — ficam desabilitados no select

function isPowerOfTwo(n) {
    return n >= 1 && (n & (n - 1)) === 0;
}

function buildInviteUrl(code) {
    return `${window.location.origin}/join-tournament.html?code=${encodeURIComponent(code)}`;
}

async function loadTournamentInfo() {
    if (!tournamentId) {
        notifyError('ID do torneio não informado.');
        return;
    }

    try {
        const response = await apiFetch(`${API_BASE_URL}/tournament/${tournamentId}`);
        currentTournament = await response.json();
        document.getElementById('tournamentName').innerText = currentTournament.name;
        document.title = `${currentTournament.name} — Configurar`;

        // Adaptar botão ao formato
        const btn = document.getElementById('generateBracketBtn');
        if (currentTournament.format >= 1) {
            btn.innerHTML = '<i class="bi bi-play-circle"></i> Iniciar Swiss';
        }

        // Se já em andamento, redirecionar para a página correta
        if (currentTournament.status === 1) {
            const target = currentTournament.format >= 1
                ? `/tournament-swiss.html?id=${tournamentId}`
                : `/tournament-double-bracket.html?id=${tournamentId}`;
            btn.textContent = 'Ver torneio';
            btn.onclick = () => window.location.href = target;
        }

        const inviteUrl = currentTournament.inviteCode ? buildInviteUrl(currentTournament.inviteCode) : '';
        document.getElementById('inviteUrlInput').value = inviteUrl;

        await loadParticipants();
    } catch (error) {
        notifyError('Erro ao carregar torneio: ' + error.message);
    }
}

async function loadParticipants() {
    try {
        const partsResponse = await apiFetch(`${API_BASE_URL}/tournament/${tournamentId}/participants`);
        const participants = await partsResponse.json();
        const container = document.getElementById('participantsList');
        const pill = document.getElementById('participantsCountPill');

        // Mantém o select de inscrição manual em sincronia com quem já está inscrito
        registeredPlayerIds = participants.map(p => p.playerId).filter(Boolean);
        refreshAddPlayerSelect();

        if (!participants.length) {
            container.innerHTML = `
                <div class="empty-state">
                    <div class="icon"><i class="bi bi-person-plus"></i></div>
                    <div class="title">Ninguém inscrito ainda</div>
                    <div>Compartilhe o link de convite acima para que os participantes se inscrevam.</div>
                </div>`;
            pill.textContent = '0 jogadores';
            pill.className = 'status-pill prep';
            return;
        }

        container.innerHTML = `
            <div class="row g-3">
                ${participants.map(p => `
                    <div class="col-md-6 col-lg-4">
                        <div class="card-stat hover-lift" style="border-radius: 14px; position: relative;">
                            ${p.avatarUrl
                                ? `<img src="${escapeHtml(p.avatarUrl)}" class="avatar avatar-img" alt="${escapeHtml(p.playerName)}">`
                                : `<span class="avatar">${getInitials(p.playerName)}</span>`
                            }
                            <div style="min-width:0; flex:1;">
                                <div style="font-weight:600;" class="text-truncate">
                                    ${p.isGuest || !p.playerId
                                        ? `<span style="color: var(--text-1);">${escapeHtml(p.playerName)}</span>
                                           <span class="badge bg-secondary ms-1" style="font-size:0.7rem;">Convidado</span>`
                                        : `<a href="/player.html?id=${p.playerId}" style="color: var(--text-1); text-decoration: none;">${escapeHtml(p.playerName)}</a>`
                                    }
                                </div>
                                <div class="text-muted-2" style="font-size:0.82rem; display:flex; align-items:center; gap:0.35rem;">
                                    <button type="button" class="btn btn-ghost btn-sm btn-toggle-deck" data-deck-id="deck-${p.id}" title="Mostrar deck" style="padding:0.05rem 0.4rem; font-size:0.72rem; line-height:1.4;">
                                        <i class="bi bi-eye"></i> Deck
                                    </button>
                                    <span class="text-truncate" id="deck-${p.id}" style="display:none;">
                                        <i class="bi bi-layers"></i> ${escapeHtml(p.deck)}
                                    </span>
                                </div>
                            </div>
                            <button class="btn btn-ghost btn-sm" data-remove-id="${p.id}" data-remove-name="${escapeHtml(p.playerName)}" title="Remover participante" style="position:absolute; top:0.4rem; right:0.4rem;">
                                <i class="bi bi-x-lg"></i>
                            </button>
                        </div>
                    </div>
                `).join('')}
            </div>`;

        const max = currentTournament?.maxPlayers || 0;
        pill.textContent = max > 0 ? `${participants.length} / ${max} jogadores` : `${participants.length} jogador${participants.length === 1 ? '' : 'es'}`;
        const isFull = max > 0 ? participants.length >= max : (participants.length >= 2 && participants.length % 2 === 0);
        pill.className = 'status-pill ' + (isFull ? 'live' : 'prep');

        container.querySelectorAll('[data-remove-id]').forEach(btn => {
            btn.addEventListener('click', () => removeParticipant(
                parseInt(btn.dataset.removeId),
                btn.dataset.removeName,
            ));
        });

        container.querySelectorAll('.btn-toggle-deck').forEach(btn => {
            btn.addEventListener('click', () => {
                const deckSpan = document.getElementById(btn.dataset.deckId);
                const showing = deckSpan.style.display !== 'none';
                deckSpan.style.display = showing ? 'none' : '';
                btn.innerHTML = showing
                    ? '<i class="bi bi-eye"></i> Deck'
                    : '<i class="bi bi-eye-slash"></i> Deck';
                btn.title = showing ? 'Mostrar deck' : 'Ocultar deck';
            });
        });
    } catch (error) {
        notifyError('Erro ao carregar participantes: ' + error.message);
    }
}

async function removeParticipant(playerId, playerName) {
    const result = await confirmAction({
        title: 'Remover participante?',
        text: `"${playerName}" será removido(a) deste torneio.`,
        confirmText: 'Remover',
        cancelText: 'Cancelar',
        icon: 'warning',
    });
    if (!result.isConfirmed) return;
    try {
        await apiFetch(`${API_BASE_URL}/tournament/${tournamentId}/participants/${playerId}`, { method: 'DELETE' });
        notifySuccess(`${playerName} removido(a).`);
        loadParticipants();
    } catch (error) {
        notifyError('Erro ao remover: ' + error.message);
    }
}

/* ---------- Inscrição manual pelo admin ---------- */

async function loadAllPlayers() {
    try {
        const response = await apiFetch(`${API_BASE_URL}/player`);
        allPlayers = await response.json();
        refreshAddPlayerSelect();
    } catch (error) {
        notifyError('Não foi possível carregar a lista de jogadores: ' + error.message);
    }
}

function refreshAddPlayerSelect() {
    const select = document.getElementById('addPlayerSelect');
    if (!select) return;
    const current = select.value;
    select.innerHTML = '<option value="">Selecione o jogador</option>' +
        allPlayers.map(p => {
            const already = registeredPlayerIds.includes(p.id);
            return `<option value="${p.id}"${already ? ' disabled' : ''}>${escapeHtml(p.name)}${already ? ' (já inscrito)' : ''}</option>`;
        }).join('');
    // Se o jogador selecionado acabou de ser inscrito, limpa a seleção
    select.value = registeredPlayerIds.includes(parseInt(current, 10)) ? '' : current;
}

// Só decks já montados no sistema: não existe mais digitar o nome do deck à mão.
async function loadSavedDecksForAdd() {
    const playerId    = document.getElementById('addPlayerSelect').value;
    const savedSelect = document.getElementById('addDeckSavedSelect');
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

async function addParticipant() {
    const playerId   = document.getElementById('addPlayerSelect').value;
    const deckSelect = document.getElementById('addDeckSavedSelect');
    const deckId     = deckSelect.value;
    // O nome do deck vem do próprio deck escolhido — não há mais digitação manual
    const deck = deckId ? (deckSelect.options[deckSelect.selectedIndex].dataset.name || '') : '';

    if (!playerId) { notifyWarning('Selecione o jogador que será inscrito.'); return; }
    if (!deckId) { notifyWarning('Selecione um deck salvo do jogador. Se ele não tem deck, precisa montar um antes.'); return; }

    const btn = document.getElementById('addParticipantBtn');
    btn.disabled = true;
    try {
        await apiFetch(`${API_BASE_URL}/tournament/${tournamentId}/participants`, {
            method: 'POST',
            body: JSON.stringify({
                playerId: parseInt(playerId, 10),
                deck: deck || null,
                deckId: deckId ? parseInt(deckId, 10) : null,
            }),
        });
        const playerName = allPlayers.find(p => String(p.id) === playerId)?.name || 'Jogador';
        notifySuccess(`${playerName} inscrito(a) no torneio!`);
        document.getElementById('addPlayerSelect').value = '';
        await loadSavedDecksForAdd();
        await loadParticipants();
    } catch (error) {
        notifyError('Erro ao inscrever participante: ' + error.message);
    } finally {
        btn.disabled = false;
    }
}

document.getElementById('addPlayerSelect').addEventListener('change', loadSavedDecksForAdd);
document.getElementById('addParticipantBtn').addEventListener('click', addParticipant);

async function copyInviteLink() {
    const url = document.getElementById('inviteUrlInput').value;
    if (!url) {
        notifyWarning('Este torneio ainda não tem código de convite.');
        return;
    }
    try {
        await navigator.clipboard.writeText(url);
        notifySuccess('Link copiado!');
    } catch (_) {
        // Fallback
        const input = document.getElementById('inviteUrlInput');
        input.select();
        document.execCommand('copy');
        notifySuccess('Link copiado!');
    }
}

async function regenerateInvite() {
    const result = await confirmAction({
        title: 'Gerar novo código?',
        text: 'O link antigo deixará de funcionar imediatamente. Use esta opção se o link atual foi compartilhado por engano.',
        confirmText: 'Sim, gerar novo',
        cancelText: 'Cancelar',
        icon: 'warning',
    });
    if (!result.isConfirmed) return;
    try {
        const response = await apiFetch(`${API_BASE_URL}/tournament/${tournamentId}/regenerate-invite`, { method: 'POST' });
        const data = await response.json();
        document.getElementById('inviteUrlInput').value = buildInviteUrl(data.inviteCode);
        notifySuccess('Novo código gerado.');
    } catch (error) {
        notifyError('Erro ao gerar novo código: ' + error.message);
    }
}

document.getElementById('copyInviteBtn').addEventListener('click', copyInviteLink);
document.getElementById('regenInviteBtn').addEventListener('click', regenerateInvite);
document.getElementById('openInviteBtn').addEventListener('click', () => {
    const url = document.getElementById('inviteUrlInput').value;
    if (url) window.open(url, '_blank');
});
document.getElementById('refreshParticipantsBtn').addEventListener('click', loadParticipants);

document.getElementById('generateBracketBtn').addEventListener('click', async () => {
    try {
        const participants = await apiFetch(`${API_BASE_URL}/tournament/${tournamentId}/participants`)
            .then(r => r.json());
        const count = participants.length;
        const max   = currentTournament?.maxPlayers || 0;
        const fmt   = currentTournament?.format ?? 0;

        if (count < 2) {
            notifyWarning('São necessários pelo menos 2 participantes inscritos.');
            return;
        }

        if (fmt === 0) {
            // ── Double Elimination ─────────────────────────────────────────
            if (count % 2 !== 0) {
                notifyWarning(`Há ${count} participante(s) inscritos. Para dupla eliminação a quantidade precisa ser par.`);
                return;
            }
            if (max > 0 && count < max) {
                const warn = await confirmAction({
                    title: 'Iniciar com vagas abertas?',
                    text: `O torneio tem ${max} vagas mas apenas ${count} estão preenchidas. Os ${max - count} lugar(es) restantes serão BYEs. Deseja continuar?`,
                    confirmText: 'Sim, iniciar assim', cancelText: 'Cancelar', icon: 'warning',
                });
                if (!warn.isConfirmed) return;
            }
            const confirm = await confirmAction({
                title: 'Iniciar torneio?',
                text: `Será gerado o chaveamento de dupla eliminação com ${count} jogadores.`,
                confirmText: 'Iniciar agora', cancelText: 'Cancelar', icon: 'question',
            });
            if (!confirm.isConfirmed) return;

            const resp = await apiFetch(`${API_BASE_URL}/tournament/${tournamentId}/generate-double-elimination`, { method: 'POST' });
            const json = await resp.json();
            await Swal.fire({ icon: 'success', title: 'Torneio iniciado!', text: json.message, timer: 1600, showConfirmButton: false });
            window.location.href = `/tournament-double-bracket.html?id=${tournamentId}`;

        } else {
            // ── Swiss (com ou sem Top Cut) ─────────────────────────────────
            const rounds = currentTournament.swissRounds;
            const isPure = currentTournament.format === 2;
            const isRoundRobin = currentTournament.format === 3;
            const detailText = isRoundRobin
                ? `${count} jogadores · ${count * (count - 1) / 2} partidas (${count - 1} por jogador), sem rodadas e sem bye · Top ${currentTournament.topCutSize} (double elimination).`
                : isPure
                ? `${count} jogadores · ${rounds} rodadas Swiss · Classificação por pontos corridos.`
                : `${count} jogadores · ${rounds} rodadas Swiss · Top ${currentTournament.topCutSize} (double elimination).`;
            const confirm = await confirmAction({
                title: isRoundRobin ? 'Iniciar todos contra todos?' : 'Iniciar Swiss?',
                text: `${detailText} Após iniciar, novos jogadores não poderão ingressar.`,
                confirmText: 'Iniciar agora', cancelText: 'Cancelar', icon: 'question',
            });
            if (!confirm.isConfirmed) return;

            const resp = await apiFetch(`${API_BASE_URL}/tournament/${tournamentId}/swiss/start`, { method: 'POST' });
            const json = await resp.json();
            await Swal.fire({ icon: 'success', title: isRoundRobin ? 'Torneio iniciado!' : 'Swiss iniciado!', text: json.message, timer: 1600, showConfirmButton: false });
            window.location.href = `/tournament-swiss.html?id=${tournamentId}`;
        }
    } catch (error) {
        notifyError('Erro ao iniciar torneio: ' + error.message);
    }
});

loadAllPlayers();
loadTournamentInfo();
