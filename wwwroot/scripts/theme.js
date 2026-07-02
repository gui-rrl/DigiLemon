/* ==========================================================================
   RankingDigi — alternância de tema claro/escuro
   ========================================================================== */

const THEME_STORAGE_KEY = 'rankingdigi-theme';

function getPreferredTheme() {
    const stored = localStorage.getItem(THEME_STORAGE_KEY);
    if (stored === 'light' || stored === 'dark') return stored;
    return window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark';
}

function updateThemeToggleIcon(theme) {
    document.querySelectorAll('.theme-toggle-icon').forEach(icon => {
        icon.className = 'bi theme-toggle-icon ' + (theme === 'light' ? 'bi-sun-fill' : 'bi-moon-stars-fill');
    });
    document.querySelectorAll('.theme-toggle-btn').forEach(btn => {
        btn.title = theme === 'light' ? 'Ativar modo escuro' : 'Ativar modo claro';
    });
}

function applyTheme(theme) {
    document.documentElement.setAttribute('data-theme', theme);
    updateThemeToggleIcon(theme);
}

function setTheme(theme) {
    localStorage.setItem(THEME_STORAGE_KEY, theme);
    applyTheme(theme);
    window.dispatchEvent(new CustomEvent('themechange', { detail: { theme } }));
}

function toggleTheme() {
    const current = document.documentElement.getAttribute('data-theme') === 'light' ? 'light' : 'dark';
    setTheme(current === 'dark' ? 'light' : 'dark');
}

// Garante consistência mesmo se o script inline anti-flash do <head> não rodar
applyTheme(getPreferredTheme());

// Segue a preferência do sistema enquanto o usuário não escolher manualmente
window.matchMedia('(prefers-color-scheme: light)').addEventListener('change', (e) => {
    if (!localStorage.getItem(THEME_STORAGE_KEY)) applyTheme(e.matches ? 'light' : 'dark');
});

document.addEventListener('DOMContentLoaded', () => {
    updateThemeToggleIcon(document.documentElement.getAttribute('data-theme') === 'light' ? 'light' : 'dark');

    // Em páginas sem navbar (login, registro, etc.) exibe um botão flutuante
    if (!document.querySelector('.app-navbar')) {
        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'theme-toggle-btn theme-toggle-floating';
        btn.setAttribute('aria-label', 'Alternar tema claro/escuro');
        btn.onclick = toggleTheme;
        btn.innerHTML = '<i class="bi theme-toggle-icon"></i>';
        document.body.appendChild(btn);
        updateThemeToggleIcon(document.documentElement.getAttribute('data-theme') === 'light' ? 'light' : 'dark');
    }
});
