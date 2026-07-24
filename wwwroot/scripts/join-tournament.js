/* ========== Página pública de convite (login + deck próprio obrigatórios) ========== */

const params = new URLSearchParams(window.location.search);
const code = params.get('code');
const currentUrl = window.location.pathname + window.location.search;

let myPlayerId = null;
let myPlayerName = null;

function show(id) {
    ['loadingBlock', 'errorBlock', 'inviteBlock', 'closedBlock', 'successBlock'].forEach(b => {
        document.getElementById(b).style.display = b === id ? '' : 'none';
    });
}

function setJoinStep(step) {
    ['stepNeedLogin', 'stepNeedPlayer', 'stepNeedDeck', 'stepPickDeck'].forEach(b => {
        document.getElementById(b).style.display = b === step ? '' : 'none';
    });
}

async function loadInvite() {
    if (!code) {
        document.getElementById('errorText').textContent = 'Link sem código de convite.';
        show('errorBlock');
        return;
    }
    try {
        const res = await apiFetch(`${API_BASE_URL}/tournament/invite/${encodeURIComponent(code)}`);
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
        await renderJoinStep();
    } catch (error) {
        document.getElementById('errorText').textContent = error.message || 'Não foi possível conectar ao servidor.';
        show('errorBlock');
    }
}

async function renderJoinStep() {
    document.getElementById('loginLink').href = `/login.html?next=${encodeURIComponent(currentUrl)}`;
    document.getElementById('registerLink').href = `/register.html?next=${encodeURIComponent(currentUrl)}`;

    if (!authIsLoggedIn()) {
        document.getElementById('loggedInAsBlock').style.display = 'none';
        setJoinStep('stepNeedLogin');
        return;
    }

    let me;
    try {
        const res = await apiFetch(`${API_BASE_URL}/auth/me`);
        me = await res.json();
    } catch (_) {
        return; // apiFetch já redireciona para o login em caso de sessão expirada
    }
    authUpdateUser({ playerId: me.playerId, playerName: me.playerName });

    document.getElementById('loggedInAsName').textContent = me.username;
    document.getElementById('loggedInAsBlock').style.display = '';

    if (!me.playerId) {
        setJoinStep('stepNeedPlayer');
        return;
    }

    myPlayerId = me.playerId;
    myPlayerName = me.playerName;

    const decksRes = await apiFetch(`${API_BASE_URL}/deck?playerId=${myPlayerId}`);
    const decks = await decksRes.json();

    if (!decks.length) {
        document.getElementById('createDeckLink').href =
            `/decks.html?playerId=${myPlayerId}&next=${encodeURIComponent(currentUrl)}`;
        setJoinStep('stepNeedDeck');
        return;
    }

    document.getElementById('pickDeckPlayerName').textContent = myPlayerName;
    document.getElementById('joinDeckSelect').innerHTML = decks
        .map(d => `<option value="${d.id}">${escapeHtml(d.name)} (${d.cardCount} cartas)</option>`)
        .join('');
    setJoinStep('stepPickDeck');
}

document.getElementById('switchAccountLink').addEventListener('click', (e) => {
    e.preventDefault();
    authClear();
    window.location.href = `/login.html?next=${encodeURIComponent(currentUrl)}`;
});

document.getElementById('linkPlayerForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    const playerName = document.getElementById('linkPlayerName').value.trim();
    if (!playerName) {
        Swal.fire({ icon: 'warning', title: 'Atenção', text: 'Informe o nome do jogador.' });
        return;
    }
    const submitBtn = e.target.querySelector('button[type="submit"]');
    submitBtn.disabled = true;
    try {
        const res = await apiFetch(`${API_BASE_URL}/auth/link-player`, {
            method: 'POST',
            body: JSON.stringify({ playerName }),
        });
        const data = await res.json();
        authUpdateUser({ playerId: data.playerId, playerName: data.playerName });
        notifySuccess('Perfil de jogador criado!');
        await renderJoinStep();
    } catch (error) {
        Swal.fire({ icon: 'error', title: 'Erro', text: error.message });
    } finally {
        submitBtn.disabled = false;
    }
});

document.getElementById('joinForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    const deckId = parseInt(document.getElementById('joinDeckSelect').value, 10);
    if (!deckId) {
        Swal.fire({ icon: 'warning', title: 'Atenção', text: 'Escolha um deck.' });
        return;
    }
    const submitBtn = e.target.querySelector('button[type="submit"]');
    submitBtn.disabled = true;
    submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span> Enviando…';

    try {
        const res = await apiFetch(`${API_BASE_URL}/tournament/invite/${encodeURIComponent(code)}/join`, {
            method: 'POST',
            body: JSON.stringify({ deckId }),
        });
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
