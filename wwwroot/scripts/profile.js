/* ========== Perfil do usuário ========== */

if (!authGuard()) throw new Error('not authenticated');

async function loadProfile() {
    try {
        const res  = await apiFetch(`${API_BASE_URL}/auth/me`);
        const user = await res.json();

        document.getElementById('userAvatar').textContent    = getInitials(user.username);
        document.getElementById('displayUsername').textContent = user.username;
        document.getElementById('displayPlayerName').textContent = user.playerName ?? '—';

        const isAdmin = user.role === 'Admin';
        document.getElementById('displayRole').innerHTML =
            `<span class="badge ${isAdmin ? 'bg-primary' : 'bg-secondary'}">${isAdmin ? 'Administrador' : 'Jogador'}</span>`;
    } catch (err) {
        notifyError('Erro ao carregar perfil: ' + err.message);
    }
}

document.getElementById('changePasswordForm').addEventListener('submit', async (e) => {
    e.preventDefault();

    const currentPassword = document.getElementById('currentPassword').value;
    const newPassword     = document.getElementById('newPassword').value;
    const confirmPassword = document.getElementById('confirmPassword').value;

    if (!currentPassword) { notifyWarning('Informe a senha atual.'); return; }
    if (!newPassword || newPassword.length < 4) { notifyWarning('A nova senha deve ter ao menos 4 caracteres.'); return; }
    if (newPassword !== confirmPassword) { notifyWarning('As senhas não coincidem.'); return; }

    const btn = document.getElementById('savePasswordBtn');
    btn.disabled = true;
    btn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Salvando…';

    try {
        await apiFetch(`${API_BASE_URL}/auth/change-password`, {
            method: 'POST',
            body: JSON.stringify({ currentPassword, newPassword, confirmPassword }),
        });
        notifySuccess('Senha alterada com sucesso!');
        document.getElementById('changePasswordForm').reset();
    } catch (err) {
        notifyError(err.message);
    } finally {
        btn.disabled = false;
        btn.innerHTML = '<i class="bi bi-check2-circle"></i> Salvar nova senha';
    }
});

// Toggle show/hide senha
document.querySelectorAll('.toggle-pw').forEach(btn => {
    btn.addEventListener('click', () => {
        const input = document.getElementById(btn.dataset.target);
        const icon  = btn.querySelector('i');
        if (input.type === 'password') {
            input.type = 'text';
            icon.className = 'bi bi-eye-slash';
        } else {
            input.type = 'password';
            icon.className = 'bi bi-eye';
        }
    });
});

loadProfile();
