/* ==========================================================================
   RankingDigi — gerenciamento de autenticação JWT
   ========================================================================== */

const AUTH_KEY      = 'rd_token';
const AUTH_USER_KEY = 'rd_user';

/** Salva token e dados do usuário no localStorage */
function authSave(token, user) {
    localStorage.setItem(AUTH_KEY, token);
    localStorage.setItem(AUTH_USER_KEY, JSON.stringify(user));
}

/** Remove sessão local */
function authClear() {
    localStorage.removeItem(AUTH_KEY);
    localStorage.removeItem(AUTH_USER_KEY);
}

/** Retorna o token JWT armazenado ou null */
function authToken() {
    return localStorage.getItem(AUTH_KEY);
}

/** Retorna o objeto de usuário { username, role, playerId, playerName } ou null */
function authUser() {
    const raw = localStorage.getItem(AUTH_USER_KEY);
    if (!raw) return null;
    try { return JSON.parse(raw); } catch (_) { return null; }
}

/** Verdadeiro se há token salvo */
function authIsLoggedIn() {
    return !!authToken();
}

/** Verdadeiro se o usuário logado é Admin */
function authIsAdmin() {
    const u = authUser();
    return u?.role === 'Admin';
}

/**
 * Guarda de autenticação para páginas protegidas.
 * Redireciona para login.html se não houver token.
 * @param {boolean} adminOnly - se true, redireciona Players também
 */
function authGuard(adminOnly = false) {
    if (!authIsLoggedIn()) {
        window.location.href = `/login.html?next=${encodeURIComponent(window.location.pathname + window.location.search)}`;
        return false;
    }
    if (adminOnly && !authIsAdmin()) {
        window.location.href = '/Index.html';
        return false;
    }
    return true;
}

/** Logout: limpa sessão e vai para login */
function authLogout() {
    authClear();
    window.location.href = '/login.html';
}
