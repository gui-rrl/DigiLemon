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
                    <td colspan="5">
                        <div class="empty-state">
                            <div class="icon"><i class="bi bi-trophy"></i></div>
                            <div class="title">Nenhum torneio criado</div>
                            <div>Clique em "Criar novo torneio" para começar.</div>
                        </div>
                    </td>
                </tr>`;
            return;
        }

        tbody.innerHTML = tournaments.map(t => {
            const info = tournamentStatusInfo(t.status);
            const canInvite = t.status === 0 && t.inviteCode;
            return `
                <tr>
                    <td><span class="text-muted-2">#${t.id}</span></td>
                    <td><strong>${escapeHtml(t.name)}</strong></td>
                    <td>${formatDate(t.startDate)}</td>
                    <td><span class="status-pill ${info.cls}">${info.label}</span></td>
                    <td style="text-align:right;">
                        <div class="d-inline-flex gap-2 flex-wrap justify-content-end">
                            ${canInvite ? `<button class="btn btn-sm btn-ghost" onclick="copyInvite('${escapeHtml(t.inviteCode)}')" title="Copiar link de convite"><i class="bi bi-link-45deg"></i> Convite</button>` : ''}
                            <a href="/tournament-setup.html?id=${t.id}" class="btn btn-sm btn-secondary" title="Configurar">
                                <i class="bi bi-gear"></i> Configurar
                            </a>
                            <a href="/tournament-double-bracket.html?id=${t.id}" class="btn btn-sm btn-info" title="Visualizar bracket">
                                <i class="bi bi-diagram-3"></i> Bracket
                            </a>
                            <button class="btn btn-sm btn-danger" onclick="deleteTournament(${t.id}, '${escapeHtml(t.name)}')" title="Excluir">
                                <i class="bi bi-trash3"></i>
                            </button>
                        </div>
                    </td>
                </tr>`;
        }).join('');
    } catch (error) {
        console.error(error);
        tbody.innerHTML = `<tr class="empty-row"><td colspan="5"><div class="empty-state"><div class="icon"><i class="bi bi-exclamation-octagon"></i></div><div class="title">Erro ao carregar torneios</div><div>${escapeHtml(error.message)}</div></div></td></tr>`;
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
    loadTournaments();
    window.deleteTournament = deleteTournament;
    window.copyInvite = copyInvite;
});
