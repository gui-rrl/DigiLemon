/* ========== Configurar Torneio (lobby) ========== */

const urlParams = new URLSearchParams(window.location.search);
const tournamentId = urlParams.get('id');
let currentTournament = null;

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
        if (currentTournament.format === 1) {
            btn.innerHTML = '<i class="bi bi-play-circle"></i> Iniciar Swiss';
        }

        // Se já em andamento, redirecionar para a página correta
        if (currentTournament.status === 1) {
            const target = currentTournament.format === 1
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
                            <span class="avatar">${getInitials(p.playerName)}</span>
                            <div style="min-width:0; flex:1;">
                                <div style="font-weight:600;" class="text-truncate">
                                    ${p.isGuest || !p.playerId
                                        ? `<span style="color: var(--text-1);">${escapeHtml(p.playerName)}</span>
                                           <span class="badge bg-secondary ms-1" style="font-size:0.7rem;">Convidado</span>`
                                        : `<a href="/player.html?id=${p.playerId}" style="color: var(--text-1); text-decoration: none;">${escapeHtml(p.playerName)}</a>`
                                    }
                                </div>
                                <div class="text-muted-2 text-truncate" style="font-size:0.82rem;">
                                    <i class="bi bi-layers"></i> ${escapeHtml(p.deck)}
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
            // ── Swiss + Top Cut ────────────────────────────────────────────
            const rounds  = currentTournament.swissRounds;
            const topCut  = currentTournament.topCutSize;
            const confirm = await confirmAction({
                title: 'Iniciar Swiss?',
                text: `${count} jogadores · ${rounds} rodadas Swiss · Top ${topCut} (double elimination). Após iniciar, novos jogadores não poderão ingressar.`,
                confirmText: 'Iniciar agora', cancelText: 'Cancelar', icon: 'question',
            });
            if (!confirm.isConfirmed) return;

            const resp = await apiFetch(`${API_BASE_URL}/tournament/${tournamentId}/swiss/start`, { method: 'POST' });
            const json = await resp.json();
            await Swal.fire({ icon: 'success', title: 'Swiss iniciado!', text: json.message, timer: 1600, showConfirmButton: false });
            window.location.href = `/tournament-swiss.html?id=${tournamentId}`;
        }
    } catch (error) {
        notifyError('Erro ao iniciar torneio: ' + error.message);
    }
});

loadTournamentInfo();
