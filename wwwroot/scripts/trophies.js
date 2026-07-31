/* ========== Galeria de Troféus ========== */

async function loadTrophies() {
    const gallery = document.getElementById('trophyGallery');
    try {
        const response = await apiFetch(`${API_BASE_URL}/tournament/trophies`);
        const trophies = await response.json();

        if (!trophies.length) {
            gallery.innerHTML = `
                <div class="empty-state">
                    <div class="icon"><i class="bi bi-award"></i></div>
                    <div class="title">Nenhum troféu cadastrado ainda</div>
                    <div>Os troféus aparecem aqui assim que forem vinculados a um torneio com campeão definido.</div>
                </div>`;
            return;
        }

        gallery.innerHTML = trophies.map(t => {
            const winnerAvatar = t.winnerAvatarUrl
                ? `<img src="${escapeHtml(t.winnerAvatarUrl)}" class="avatar avatar-img" style="width:24px;height:24px;" alt="${escapeHtml(t.winnerName)}">`
                : `<span class="avatar" style="width:24px;height:24px;font-size:0.68rem;">${getInitials(t.winnerName || '')}</span>`;
            return `
                <a class="trophy-card" href="/tournament-recap.html?id=${t.tournamentId}">
                    <div class="trophy-card-img">
                        <img src="${escapeHtml(t.trophyImageUrl)}" alt="Troféu — ${escapeHtml(t.tournamentName)}" loading="lazy">
                    </div>
                    <div class="trophy-card-name">${escapeHtml(t.tournamentName)}</div>
                    <div class="trophy-card-winner">${winnerAvatar}<span>${escapeHtml(t.winnerName)}</span></div>
                </a>`;
        }).join('');
    } catch (error) {
        gallery.innerHTML = `<div class="empty-state"><div class="icon"><i class="bi bi-exclamation-octagon"></i></div><div class="title">Erro ao carregar troféus</div><div>${escapeHtml(error.message)}</div></div>`;
    }
}

document.addEventListener('DOMContentLoaded', loadTrophies);
