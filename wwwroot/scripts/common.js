/* ==========================================================================
   RankingDigi — utilitários compartilhados
   ========================================================================== */

const API_BASE_URL = '/api';

/** Faz fetch para a API com JWT Bearer token (ou sem auth para rotas públicas) */
async function apiFetch(url, options = {}) {
    const token = authToken?.() ?? null;  // authToken() vem de auth.js
    const headers = {
        'Content-Type': 'application/json',
        'ngrok-skip-browser-warning': '1',
        ...(token ? { 'Authorization': `Bearer ${token}` } : {}),
        ...(options.headers || {}),
    };
    const merged = { ...options, headers };
    const response = await fetch(url, merged);

    if (response.status === 401) {
        // Token expirado ou inválido — redireciona para login (evita loop se já estiver em login.html)
        if (typeof authClear === 'function') authClear();
        if (!window.location.pathname.endsWith('login.html')) {
            window.location.href = `/login.html?next=${encodeURIComponent(window.location.pathname + window.location.search)}`;
        }
        throw new Error('Sessão expirada. Faça login novamente.');
    }

    if (!response.ok) {
        let message = `Erro HTTP: ${response.status}`;
        try {
            const text = await response.text();
            if (text) {
                try {
                    const json = JSON.parse(text);
                    message = json.error || json.message || text;
                } catch (_) { message = text; }
            }
        } catch (_) { /* noop */ }
        throw new Error(message);
    }
    return response;
}

/* ---------- Notificações estilizadas ---------- */

const Toast = (window.Swal && Swal.mixin({
    toast: true,
    position: 'top-end',
    showConfirmButton: false,
    timer: 2600,
    timerProgressBar: true,
})) || null;

function notifySuccess(message, title = 'Pronto!') {
    if (Toast) return Toast.fire({ icon: 'success', title, text: message });
}

function notifyError(message, title = 'Algo deu errado') {
    return Swal.fire({ icon: 'error', title, text: message, confirmButtonText: 'Entendi' });
}

function notifyWarning(message, title = 'Atenção') {
    return Swal.fire({ icon: 'warning', title, text: message, confirmButtonText: 'Ok' });
}

function notifyInfo(message, title = 'Aviso') {
    if (Toast) return Toast.fire({ icon: 'info', title, text: message });
}

function confirmAction({ title = 'Tem certeza?', text = 'Esta ação não pode ser desfeita.', confirmText = 'Confirmar', cancelText = 'Cancelar', icon = 'warning' } = {}) {
    return Swal.fire({
        title, text, icon,
        showCancelButton: true,
        confirmButtonText: confirmText,
        cancelButtonText: cancelText,
        reverseButtons: true,
        focusCancel: true,
    });
}

/* ---------- Helpers visuais ---------- */

function getInitials(name) {
    if (!name) return '?';
    const parts = name.trim().split(/\s+/);
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

function escapeHtml(value) {
    if (value === null || value === undefined) return '';
    return String(value)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
}

function formatDate(value) {
    if (!value) return '-';
    try { return new Date(value).toLocaleDateString('pt-BR'); } catch (_) { return value; }
}

function formatDateTime(value) {
    if (!value) return '-';
    try { return new Date(value).toLocaleString('pt-BR'); } catch (_) { return value; }
}

function tournamentStatusInfo(status) {
    switch (status) {
        case 0: return { label: 'Preparação', cls: 'prep' };
        case 1: return { label: 'Em andamento', cls: 'live' };
        case 2: return { label: 'Finalizado', cls: 'done' };
        default: return { label: 'Desconhecido', cls: 'done' };
    }
}

/* ---------- Exportação CSV ---------- */

function downloadCsv(filename, rows) {
    if (!rows || !rows.length) { notifyWarning('Não há dados para exportar.'); return; }
    const headers = Object.keys(rows[0]);
    const escape = (val) => {
        if (val === null || val === undefined) return '';
        const s = String(val).replace(/"/g, '""');
        return /[",;\n\r]/.test(s) ? `"${s}"` : s;
    };
    const csv = [headers.join(';'), ...rows.map(r => headers.map(h => escape(r[h])).join(';'))].join('\r\n');
    const blob = new Blob(['﻿' + csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url; link.download = filename;
    document.body.appendChild(link); link.click();
    document.body.removeChild(link); URL.revokeObjectURL(url);
}

/* ---------- Prompts customizados (SweetAlert) ---------- */

async function promptText({ title, label, defaultValue = '', placeholder = '', confirmText = 'Salvar', cancelText = 'Cancelar' }) {
    return Swal.fire({
        title, input: 'text', inputLabel: label, inputValue: defaultValue,
        inputPlaceholder: placeholder, showCancelButton: true,
        confirmButtonText: confirmText, cancelButtonText: cancelText,
        reverseButtons: true,
        inputValidator: (value) => !value || !value.trim() ? 'Campo obrigatório' : null,
    });
}

/* ---------- Navbar ---------- */

function activateNavLink(name) {
    document.querySelectorAll('.nav-pills-app .nav-link').forEach(link => {
        if (link.dataset.nav === name) link.classList.add('active');
        else link.classList.remove('active');
    });
}

function renderNavbar(activeName) {
    const user    = typeof authUser === 'function' ? authUser() : null;
    const isAdmin = user?.role === 'Admin';

    // Links visíveis apenas para Admin
    const adminLinks = isAdmin ? `
      <li class="nav-item">
        <a class="nav-link" data-nav="matches" href="/match.html">
          <i class="bi bi-controller"></i><span class="d-none d-sm-inline">Partidas</span>
        </a>
      </li>
      <li class="nav-item">
        <a class="nav-link" data-nav="users" href="/users.html">
          <i class="bi bi-people-fill"></i><span class="d-none d-sm-inline">Usuários</span>
        </a>
      </li>` : '';

    const sharedLinks = user ? `
      <li class="nav-item">
        <a class="nav-link" data-nav="dashboard" href="/dashboard.html">
          <i class="bi bi-graph-up"></i><span class="d-none d-sm-inline">Dashboard</span>
        </a>
      </li>` : '';

    // Área do usuário (canto direito)
    const userArea = user ? `
        <span class="badge ${isAdmin ? 'bg-primary' : 'bg-secondary'} d-none d-sm-inline">
          ${isAdmin ? 'Admin' : 'Jogador'}
        </span>
        <a href="${user.playerId ? `/player.html?id=${user.playerId}` : '/profile.html'}" class="btn btn-ghost btn-sm d-flex align-items-center gap-1" title="Meu perfil" style="padding:0.25rem 0.5rem;">
          <i class="bi bi-person-circle" style="font-size:1rem;"></i>
          <span class="d-none d-md-inline" style="font-size:0.82rem;font-weight:500;">${escapeHtml(user.username)}</span>
        </a>
        <button class="btn btn-ghost btn-sm" onclick="authLogout()" title="Sair" style="padding:0.25rem 0.5rem;">
          <i class="bi bi-box-arrow-right"></i>
        </button>` : `
      <a href="/login.html" class="btn btn-primary btn-sm">
        <i class="bi bi-box-arrow-in-right"></i> Login
      </a>`;

    const themeToggle = `
        <button type="button" class="theme-toggle-btn" onclick="toggleTheme()" aria-label="Alternar tema claro/escuro">
          <i class="bi theme-toggle-icon"></i>
        </button>`;

    const html = `
    <nav class="navbar app-navbar">
      <div class="container-fluid px-4">
        <a class="navbar-brand" href="/Index.html">
          <span class="logo"><i class="bi bi-trophy-fill"></i></span>
          <span>RankingDigi</span>
        </a>
        <ul class="nav nav-pills nav-pills-app">
          <li class="nav-item">
            <a class="nav-link" data-nav="ranking" href="/Index.html">
              <i class="bi bi-bar-chart-fill"></i><span class="d-none d-sm-inline">Ranking</span>
            </a>
          </li>
          <li class="nav-item">
            <a class="nav-link" data-nav="tournaments" href="/tournaments.html">
              <i class="bi bi-trophy"></i><span class="d-none d-sm-inline">Torneios</span>
            </a>
          </li>
          ${adminLinks}
          ${sharedLinks}
        </ul>
        <div class="d-flex align-items-center gap-2 ms-3">
          ${themeToggle}
          ${userArea}
        </div>
      </div>
    </nav>`;

    document.body.insertAdjacentHTML('afterbegin', html);
    activateNavLink(activeName);
    updateThemeToggleIcon(document.documentElement.getAttribute('data-theme') === 'light' ? 'light' : 'dark');
}

document.addEventListener('DOMContentLoaded', () => {
    const active = document.body.dataset.page;
    if (active) renderNavbar(active);
});
