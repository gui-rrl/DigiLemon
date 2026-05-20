/* ========== Dashboard ========== */

const charts = {};

const palette = {
    primary: '#6d6fff',
    primary2: '#8d6bff',
    accent: '#16e0bd',
    accent2: '#00c2ff',
    warning: '#ffb547',
    danger: '#ff5d73',
    success: '#4ade80',
    text: '#b9c0d9',
    grid: 'rgba(255, 255, 255, 0.08)',
};

const baseChartOptions = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
        legend: {
            labels: { color: palette.text, font: { family: 'Inter, sans-serif' } },
        },
        tooltip: {
            backgroundColor: 'rgba(11, 16, 32, 0.95)',
            borderColor: 'rgba(255, 255, 255, 0.15)',
            borderWidth: 1,
            titleFont: { family: 'Sora, sans-serif' },
            bodyFont: { family: 'Inter, sans-serif' },
            padding: 10,
            cornerRadius: 8,
        },
    },
    scales: {
        x: {
            ticks: { color: palette.text },
            grid: { color: palette.grid, drawBorder: false },
        },
        y: {
            beginAtZero: true,
            ticks: { color: palette.text, precision: 0 },
            grid: { color: palette.grid, drawBorder: false },
        },
    },
};

async function loadStats(days) {
    try {
        const response = await apiFetch(`${API_BASE_URL}/dashboard/stats?days=${days}`);
        const data = await response.json();

        document.getElementById('sumPlayers').textContent = data.summary.totalPlayers;
        document.getElementById('sumMatches').textContent = data.summary.totalMatches;
        document.getElementById('sumTournaments').textContent = data.summary.totalTournaments;
        document.getElementById('sumDraws').textContent = data.summary.draws;

        renderTopPlayers(data.topPlayers);
        renderTopDecks(data.deckWins);
        renderMatchesPerDay(data.matchesPerDay);
        renderResults(data.summary);
        renderPlayerWins(data.playerWins);
    } catch (error) {
        notifyError('Não foi possível carregar o dashboard: ' + error.message);
    }
}

function makeGradient(ctx, color1, color2) {
    const grad = ctx.createLinearGradient(0, 0, 0, 280);
    grad.addColorStop(0, color1);
    grad.addColorStop(1, color2);
    return grad;
}

function destroyChart(key) {
    if (charts[key]) {
        charts[key].destroy();
        delete charts[key];
    }
}

function renderTopPlayers(data) {
    destroyChart('topPlayers');
    const canvas = document.getElementById('chartTopPlayers');
    const ctx = canvas.getContext('2d');
    charts.topPlayers = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: data.map(p => p.name),
            datasets: [{
                label: 'Pontos',
                data: data.map(p => p.score),
                backgroundColor: makeGradient(ctx, 'rgba(109,111,255,0.95)', 'rgba(141,107,255,0.45)'),
                borderRadius: 8,
                borderSkipped: false,
            }],
        },
        options: {
            ...baseChartOptions,
            plugins: { ...baseChartOptions.plugins, legend: { display: false } },
        },
    });
}

function renderTopDecks(data) {
    destroyChart('topDecks');
    const canvas = document.getElementById('chartTopDecks');
    const ctx = canvas.getContext('2d');
    charts.topDecks = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: data.map(d => d.deck),
            datasets: [{
                label: 'Vitórias',
                data: data.map(d => d.wins),
                backgroundColor: makeGradient(ctx, 'rgba(22,224,189,0.95)', 'rgba(0,194,255,0.45)'),
                borderRadius: 8,
                borderSkipped: false,
            }],
        },
        options: {
            ...baseChartOptions,
            indexAxis: 'y',
            plugins: { ...baseChartOptions.plugins, legend: { display: false } },
            scales: {
                x: { ...baseChartOptions.scales.x, beginAtZero: true, ticks: { ...baseChartOptions.scales.x.ticks, precision: 0 } },
                y: { ...baseChartOptions.scales.y, beginAtZero: undefined },
            },
        },
    });
}

function renderMatchesPerDay(data) {
    destroyChart('matchesPerDay');
    const canvas = document.getElementById('chartMatchesPerDay');
    const ctx = canvas.getContext('2d');
    const labels = data.map(d => {
        const date = new Date(d.date + 'T00:00:00');
        return date.toLocaleDateString('pt-BR', { day: '2-digit', month: 'short' });
    });
    charts.matchesPerDay = new Chart(ctx, {
        type: 'line',
        data: {
            labels,
            datasets: [{
                label: 'Partidas',
                data: data.map(d => d.count),
                borderColor: palette.accent2,
                backgroundColor: 'rgba(0, 194, 255, 0.18)',
                fill: true,
                tension: 0.35,
                pointRadius: 3,
                pointBackgroundColor: palette.accent2,
                pointBorderColor: '#fff',
                pointBorderWidth: 1.5,
                borderWidth: 2,
            }],
        },
        options: {
            ...baseChartOptions,
            plugins: { ...baseChartOptions.plugins, legend: { display: false } },
        },
    });
}

function renderResults(summary) {
    destroyChart('results');
    const canvas = document.getElementById('chartResults');
    const ctx = canvas.getContext('2d');
    charts.results = new Chart(ctx, {
        type: 'doughnut',
        data: {
            labels: ['Decididas', 'Empates'],
            datasets: [{
                data: [summary.decisive, summary.draws],
                backgroundColor: [palette.primary, palette.warning],
                borderColor: 'rgba(11, 16, 32, 1)',
                borderWidth: 3,
                hoverOffset: 8,
            }],
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            cutout: '65%',
            plugins: {
                legend: {
                    position: 'bottom',
                    labels: { color: palette.text, font: { family: 'Inter, sans-serif' }, padding: 14 },
                },
                tooltip: baseChartOptions.plugins.tooltip,
            },
        },
    });
}

function renderPlayerWins(data) {
    destroyChart('playerWins');
    const canvas = document.getElementById('chartPlayerWins');
    const ctx = canvas.getContext('2d');
    const winRate = data.map(p => p.played > 0 ? Math.round((p.wins / p.played) * 100) : 0);
    charts.playerWins = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: data.map(p => p.name),
            datasets: [
                {
                    label: 'Vitórias',
                    data: data.map(p => p.wins),
                    backgroundColor: 'rgba(22, 224, 189, 0.8)',
                    borderRadius: 8,
                    borderSkipped: false,
                    yAxisID: 'y',
                },
                {
                    label: 'Aproveitamento %',
                    data: winRate,
                    type: 'line',
                    borderColor: palette.warning,
                    backgroundColor: palette.warning,
                    tension: 0.3,
                    yAxisID: 'y1',
                    pointRadius: 4,
                    borderWidth: 2,
                },
            ],
        },
        options: {
            ...baseChartOptions,
            scales: {
                x: baseChartOptions.scales.x,
                y: {
                    ...baseChartOptions.scales.y,
                    position: 'left',
                    title: { display: true, text: 'Vitórias', color: palette.text },
                },
                y1: {
                    position: 'right',
                    beginAtZero: true,
                    max: 100,
                    ticks: { color: palette.text, callback: v => v + '%' },
                    grid: { display: false },
                    title: { display: true, text: 'Aproveitamento', color: palette.text },
                },
            },
        },
    });
}

document.addEventListener('DOMContentLoaded', () => {
    const select = document.getElementById('rangeSelect');
    loadStats(parseInt(select.value));
    select.addEventListener('change', () => loadStats(parseInt(select.value)));
});
