/* ========== Usuários ========== */

let changePasswordUserId = null;

async function loadUsers() {
    try {
        const users = await apiFetch(`${API_BASE_URL}/auth/users`).then(r => r.json());
        const tbody = document.getElementById('usersTable');

        if (!users.length) {
            tbody.innerHTML = '<tr class="empty-row"><td colspan="5">Nenhum usuário cadastrado.</td></tr>';
            return;
        }

        tbody.innerHTML = users.map(u => `
            <tr>
                <td><span class="text-muted-2">#${u.id}</span></td>
                <td><strong>${escapeHtml(u.username)}</strong></td>
                <td>
                    <span class="status-pill ${u.role === 'Admin' ? 'live' : 'prep'}" style="font-size:0.75rem;">
                        <i class="bi bi-${u.role === 'Admin' ? 'shield-fill' : 'person-fill'}"></i>
                        ${u.role}
                    </span>
                </td>
                <td>${u.playerName ? escapeHtml(u.playerName) : '<span class="text-muted-2">—</span>'}</td>
                <td style="text-align:right;">
                    <div class="d-inline-flex gap-2">
                        <button class="btn btn-sm btn-secondary btn-change-pw" data-id="${u.id}" data-username="${escapeHtml(u.username)}" title="Trocar senha">
                            <i class="bi bi-key"></i> Senha
                        </button>
                        <button class="btn btn-sm btn-danger btn-delete" data-id="${u.id}" data-username="${escapeHtml(u.username)}" title="Remover usuário">
                            <i class="bi bi-trash3"></i>
                        </button>
                    </div>
                </td>
            </tr>`).join('');

        tbody.querySelectorAll('.btn-change-pw').forEach(btn => {
            btn.addEventListener('click', () => openChangePassword(parseInt(btn.dataset.id), btn.dataset.username));
        });
        tbody.querySelectorAll('.btn-delete').forEach(btn => {
            btn.addEventListener('click', () => deleteUser(parseInt(btn.dataset.id), btn.dataset.username));
        });

    } catch (err) {
        notifyError('Erro ao carregar usuários: ' + err.message);
    }
}

async function loadPlayers() {
    try {
        const players = await apiFetch(`${API_BASE_URL}/player`).then(r => r.json());
        const sel = document.getElementById('newPlayerId');
        players.forEach(p => {
            const opt = document.createElement('option');
            opt.value = p.id;
            opt.textContent = p.name;
            sel.appendChild(opt);
        });
    } catch { /* opcional, ignora erro */ }
}

// ── Criar usuário ─────────────────────────────────────────────────────────────

document.getElementById('btnCreateUser').addEventListener('click', async () => {
    const username = document.getElementById('newUsername').value.trim();
    const password = document.getElementById('newPassword').value;
    const role     = document.getElementById('newRole').value;
    const playerId = document.getElementById('newPlayerId').value;

    if (!username) { notifyWarning('Informe o nome de usuário.'); return; }
    if (!password || password.length < 4) { notifyWarning('A senha deve ter ao menos 4 caracteres.'); return; }

    try {
        await apiFetch(`${API_BASE_URL}/auth/users`, {
            method: 'POST',
            body: JSON.stringify({ username, password, role, playerId: playerId ? parseInt(playerId) : null }),
        });
        bootstrap.Modal.getInstance(document.getElementById('modalCreateUser')).hide();
        document.getElementById('newUsername').value = '';
        document.getElementById('newPassword').value = '';
        document.getElementById('newRole').value = 'Player';
        document.getElementById('newPlayerId').value = '';
        notifySuccess('Usuário criado com sucesso!');
        loadUsers();
    } catch (err) {
        notifyError('Erro ao criar usuário: ' + err.message);
    }
});

// ── Trocar senha ──────────────────────────────────────────────────────────────

function openChangePassword(id, username) {
    changePasswordUserId = id;
    document.getElementById('changePasswordUsername').textContent = username;
    document.getElementById('changePasswordValue').value = '';
    new bootstrap.Modal(document.getElementById('modalChangePassword')).show();
}

document.getElementById('btnSavePassword').addEventListener('click', async () => {
    const newPassword = document.getElementById('changePasswordValue').value;
    if (!newPassword || newPassword.length < 4) { notifyWarning('A senha deve ter ao menos 4 caracteres.'); return; }

    try {
        await apiFetch(`${API_BASE_URL}/auth/users/${changePasswordUserId}/password`, {
            method: 'PUT',
            body: JSON.stringify({ newPassword }),
        });
        bootstrap.Modal.getInstance(document.getElementById('modalChangePassword')).hide();
        notifySuccess('Senha alterada com sucesso!');
    } catch (err) {
        notifyError('Erro ao alterar senha: ' + err.message);
    }
});

// ── Deletar usuário ───────────────────────────────────────────────────────────

async function deleteUser(id, username) {
    const confirm = await confirmAction({
        title: 'Remover usuário?',
        text: `O usuário "${username}" será removido permanentemente.`,
        confirmText: 'Remover', cancelText: 'Cancelar', icon: 'warning',
    });
    if (!confirm.isConfirmed) return;

    try {
        await apiFetch(`${API_BASE_URL}/auth/users/${id}`, { method: 'DELETE' });
        notifySuccess('Usuário removido.');
        loadUsers();
    } catch (err) {
        notifyError('Erro ao remover usuário: ' + err.message);
    }
}

// ── Init ──────────────────────────────────────────────────────────────────────

loadUsers();
loadPlayers();
