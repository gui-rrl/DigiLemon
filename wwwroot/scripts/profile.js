/* ========== profile.js — redireciona para player.html ou exibe conta ========== */

if (!authGuard()) throw new Error('not authenticated');

(async function () {
    const user = authUser();

    // Se o usuário tem jogador vinculado, vai direto para o perfil completo
    if (user?.playerId) {
        window.location.replace(`/player.html?id=${user.playerId}`);
        return;
    }

    // Sem jogador vinculado (ex: admin puro) — exibe só a seção de conta
    const main = document.getElementById('mainContent');
    const isAdmin = user?.role === 'Admin';

    main.innerHTML = `
        <header class="d-flex justify-content-between align-items-center gap-3 mb-4">
            <h1 class="page-title">
                <span class="icon-badge"><i class="bi bi-person-circle"></i></span>
                Meu Perfil
            </h1>
            <a href="javascript:history.back()" class="btn btn-secondary btn-sm">
                <i class="bi bi-arrow-left"></i> Voltar
            </a>
        </header>

        <section class="card-app mb-4">
            <div class="card-header"><i class="bi bi-person-gear"></i> Minha conta</div>
            <div class="card-body">
                <div class="d-flex align-items-center gap-3 mb-4">
                    <div style="
                        width:52px;height:52px;border-radius:50%;flex-shrink:0;
                        background:linear-gradient(135deg,var(--accent),var(--primary));
                        display:flex;align-items:center;justify-content:center;
                        font-size:1.1rem;font-weight:700;color:#fff;">
                        ${getInitials(user?.username ?? '?')}
                    </div>
                    <div>
                        <div style="font-weight:700;font-size:1rem;color:var(--text-1);">${escapeHtml(user?.username ?? '—')}</div>
                        <span class="badge ${isAdmin ? 'bg-primary' : 'bg-secondary'} mt-1">${isAdmin ? 'Administrador' : 'Jogador'}</span>
                    </div>
                </div>
            </div>
        </section>

        <section class="card-app">
            <div class="card-header"><i class="bi bi-key-fill"></i> Alterar senha</div>
            <div class="card-body">
                <form id="changePasswordForm" novalidate>
                    <div class="mb-3">
                        <label class="form-label">Senha atual</label>
                        <div class="input-group">
                            <input type="password" id="currentPassword" class="form-control" placeholder="Senha atual" autocomplete="current-password">
                            <button type="button" class="btn btn-outline-secondary toggle-pw" data-target="currentPassword" tabindex="-1"><i class="bi bi-eye"></i></button>
                        </div>
                    </div>
                    <div class="mb-3">
                        <label class="form-label">Nova senha</label>
                        <div class="input-group">
                            <input type="password" id="newPassword" class="form-control" placeholder="Mínimo 4 caracteres" autocomplete="new-password">
                            <button type="button" class="btn btn-outline-secondary toggle-pw" data-target="newPassword" tabindex="-1"><i class="bi bi-eye"></i></button>
                        </div>
                    </div>
                    <div class="mb-4">
                        <label class="form-label">Confirmar nova senha</label>
                        <div class="input-group">
                            <input type="password" id="confirmPassword" class="form-control" placeholder="Repita a nova senha" autocomplete="new-password">
                            <button type="button" class="btn btn-outline-secondary toggle-pw" data-target="confirmPassword" tabindex="-1"><i class="bi bi-eye"></i></button>
                        </div>
                    </div>
                    <button type="submit" class="btn btn-primary w-100" id="savePasswordBtn">
                        <i class="bi bi-check2-circle"></i> Salvar nova senha
                    </button>
                </form>
            </div>
        </section>`;

    // Bind form
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

    document.querySelectorAll('.toggle-pw').forEach(btn => {
        btn.addEventListener('click', () => {
            const input = document.getElementById(btn.dataset.target);
            const icon  = btn.querySelector('i');
            input.type  = input.type === 'password' ? 'text' : 'password';
            icon.className = input.type === 'password' ? 'bi bi-eye' : 'bi bi-eye-slash';
        });
    });
})();
