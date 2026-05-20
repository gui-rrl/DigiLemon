/* ========== Página pública de convite (sem API key) ========== */

const API_BASE_URL = '/api';

const params = new URLSearchParams(window.location.search);
const code = params.get('code');

function show(id) {
    ['loadingBlock', 'errorBlock', 'inviteBlock', 'closedBlock', 'successBlock'].forEach(b => {
        document.getElementById(b).style.display = b === id ? '' : 'none';
    });
}

function escapeHtml(value) {
    if (value === null || value === undefined) return '';
    return String(value)
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;').replace(/'/g, '&#039;');
}

function formatDate(value) {
    if (!value) return '-';
    try { return new Date(value).toLocaleDateString('pt-BR'); } catch (_) { return value; }
}

async function loadInvite() {
    if (!code) {
        document.getElementById('errorText').textContent = 'Link sem código de convite.';
        show('errorBlock');
        return;
    }
    try {
        const res = await fetch(`${API_BASE_URL}/tournament/invite/${encodeURIComponent(code)}`);
        if (!res.ok) {
            let msg = 'Convite inválido ou expirado.';
            try {
                const data = await res.json();
                msg = data.error || msg;
            } catch (_) { /* noop */ }
            document.getElementById('errorText').textContent = msg;
            show('errorBlock');
            return;
        }
        const data = await res.json();
        document.getElementById('tournamentName').textContent = data.name;
        document.getElementById('tournamentDate').textContent = formatDate(data.startDate);
        document.getElementById('participantsCount').textContent = data.participants.length;

        const thumb = document.getElementById('participantsThumb');
        if (data.participants.length) {
            thumb.innerHTML = data.participants
                .slice(0, 12)
                .map(p => `<span class="chip">${escapeHtml(p.playerName)}</span>`)
                .join('') + (data.participants.length > 12
                    ? `<span class="chip">+${data.participants.length - 12}</span>`
                    : '');
        } else {
            thumb.innerHTML = '<span class="chip" style="opacity:0.7;">Seja o primeiro a se inscrever!</span>';
        }

        if (!data.isOpenForJoin) {
            show('closedBlock');
            return;
        }
        show('inviteBlock');
    } catch (error) {
        document.getElementById('errorText').textContent = 'Não foi possível conectar ao servidor.';
        show('errorBlock');
    }
}

document.getElementById('joinForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    const name = document.getElementById('joinName').value.trim();
    const deck = document.getElementById('joinDeck').value.trim();
    if (!name || !deck) {
        Swal.fire({ icon: 'warning', title: 'Atenção', text: 'Preencha seu nome e o deck.' });
        return;
    }
    const submitBtn = e.target.querySelector('button[type="submit"]');
    submitBtn.disabled = true;
    submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span> Enviando…';

    try {
        const res = await fetch(`${API_BASE_URL}/tournament/invite/${encodeURIComponent(code)}/join`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ name, deck }),
        });
        if (!res.ok) {
            let msg = 'Não foi possível confirmar a inscrição.';
            try {
                const data = await res.json();
                msg = data.error || msg;
            } catch (_) { /* noop */ }
            throw new Error(msg);
        }
        const data = await res.json();
        document.getElementById('successText').textContent = data.message;
        show('successBlock');
    } catch (error) {
        Swal.fire({ icon: 'error', title: 'Erro', text: error.message });
    } finally {
        submitBtn.disabled = false;
        submitBtn.innerHTML = '<i class="bi bi-check-circle"></i> Confirmar inscrição';
    }
});

loadInvite();
