/* ========== Página de Torneios ========== */

async function deleteTournament(id, name) {
    const result = await confirmAction({
        title: 'Excluir torneio?',
        text: `"${name}" e todos seus participantes, brackets e partidas serão removidos. Esta ação é irreversível.`,
        confirmText: 'Sim, excluir',
        cancelText: 'Cancelar',
        icon: 'warning',
    });
    if (!result.isConfirmed) return;

    try {
        await apiFetch(`${API_BASE_URL}/tournament/${id}`, { method: 'DELETE' });
        notifySuccess('Torneio excluído com sucesso.');
        loadTournaments();
    } catch (error) {
        notifyError(`Erro ao excluir torneio: ${error.message}`);
    }
}

async function loadTournaments() {
    const tbody = document.getElementById('tournamentsTable');
    try {
        const response = await apiFetch(`${API_BASE_URL}/tournament`);
        const tournaments = await response.json();
        if (!tournaments.length) {
            tbody.innerHTML = `
                <tr class="empty-row">
                    <td colspan="9">
                        <div class="empty-state">
                            <div class="icon"><i class="bi bi-trophy"></i></div>
                            <div class="title">Nenhum torneio criado</div>
                            <div>Clique em "Criar novo torneio" para começar.</div>
                        </div>
                    </td>
                </tr>`;
            return;
        }

        const isAdmin = typeof authIsAdmin === 'function' && authIsAdmin();

        tbody.innerHTML = tournaments.map(t => {
            const info = tournamentStatusInfo(t.status);
            const canInvite = t.status === 0 && t.inviteCode;
            const winnerAvatar = t.winnerAvatarUrl
                ? `<img src="${escapeHtml(t.winnerAvatarUrl)}" class="avatar avatar-img" style="width:26px;height:26px;" alt="${escapeHtml(t.winnerName)}">`
                : `<span class="avatar" style="width:26px;height:26px;font-size:0.7rem;">${getInitials(t.winnerName || '')}</span>`;
            const winner = t.winnerName
                ? `<span style="display:inline-flex;align-items:center;gap:0.4rem;">${winnerAvatar}<i class="bi bi-trophy-fill" style="color:var(--warning);font-size:0.78rem;"></i>${escapeHtml(t.winnerName)}</span>`
                : `<span class="text-muted-2">—</span>`;
            return `
                <tr>
                    <td><span class="text-muted-2">#${t.id}</span></td>
                    <td style="white-space:nowrap;"><strong>${escapeHtml(t.name)}</strong></td>
                    <td>${t.mode === 1
                        ? '<span class="status-pill prep"><i class="bi bi-controller"></i> Online</span>'
                        : '<span class="status-pill" style="background:rgba(109,111,255,0.15);color:var(--primary)"><i class="bi bi-people-fill"></i> Presencial</span>'
                    }</td>
                    <td style="white-space:nowrap;">${t.format === 1
                        ? `<span class="status-pill prep" title="${t.swissRounds} rodadas Swiss · Top ${t.topCutSize}"><i class="bi bi-grid-3x3-gap-fill"></i> Swiss + Top Cut</span>`
                        : t.format === 2
                        ? `<span class="status-pill prep" title="${t.swissRounds} rodadas Swiss · Pontos Corridos"><i class="bi bi-bar-chart-steps"></i> Swiss P. Corridos</span>`
                        : `<span class="status-pill" style="background:rgba(109,111,255,0.15);color:var(--primary)"><i class="bi bi-diagram-3"></i> Dupla Elim.</span>`
                    }</td>
                    <td>${formatDate(t.startDate)}</td>
                    <td>${t.endDate ? formatDate(t.endDate) : '<span class="text-muted-2">—</span>'}</td>
                    <td><span class="status-pill ${info.cls}" style="white-space:nowrap;">${info.label}</span></td>
                    <td>${winner}</td>
                    <td style="text-align:right; white-space:nowrap;">
                        <div class="d-inline-flex gap-2 justify-content-end" style="flex-wrap:nowrap;">
                            ${canInvite ? `<button class="btn btn-sm btn-ghost" onclick="copyInvite('${escapeHtml(t.inviteCode)}')" title="Copiar link de convite"><i class="bi bi-link-45deg"></i> Convite</button>` : ''}
                            ${isAdmin && t.status === 0 ? `<a href="/tournament-setup.html?id=${t.id}" class="btn btn-sm btn-secondary" title="Configurar"><i class="bi bi-gear"></i> Configurar</a>` : ''}
                            ${t.status >= 1
                                ? (t.format >= 1
                                    ? `<a href="/tournament-swiss.html?id=${t.id}" class="btn btn-sm btn-info" title="Ver Swiss"><i class="bi bi-grid-3x3-gap-fill"></i> Swiss</a>`
                                    : `<a href="/tournament-double-bracket.html?id=${t.id}" class="btn btn-sm btn-info" title="Visualizar bracket"><i class="bi bi-diagram-3"></i> Bracket</a>`)
                                : ''
                            }
                            ${isAdmin ? `<button class="btn btn-sm btn-danger" onclick="deleteTournament(${t.id}, '${escapeHtml(t.name)}')" title="Excluir"><i class="bi bi-trash3"></i></button>` : ''}
                        </div>
                    </td>
                </tr>`;
        }).join('');
    } catch (error) {
        console.error(error);
        tbody.innerHTML = `<tr class="empty-row"><td colspan="9"><div class="empty-state"><div class="icon"><i class="bi bi-exclamation-octagon"></i></div><div class="title">Erro ao carregar torneios</div><div>${escapeHtml(error.message)}</div></div></td></tr>`;
    }
}

async function copyInvite(code) {
    const url = `${window.location.origin}/join-tournament.html?code=${encodeURIComponent(code)}`;
    try {
        await navigator.clipboard.writeText(url);
        notifySuccess('Link de convite copiado!');
    } catch (_) {
        await Swal.fire({
            icon: 'info',
            title: 'Copie o link abaixo',
            input: 'text',
            inputValue: url,
            confirmButtonText: 'Fechar',
        });
    }
}

document.addEventListener('DOMContentLoaded', () => {
    // Oculta botões de admin para usuários Player
    if (typeof authIsAdmin === 'function' && !authIsAdmin()) {
        const btnCreate = document.querySelector('a[href="/create-tournament.html"]');
        if (btnCreate) btnCreate.style.display = 'none';
    }
    loadTournaments();
    window.deleteTournament = deleteTournament;
    window.copyInvite = copyInvite;
});
