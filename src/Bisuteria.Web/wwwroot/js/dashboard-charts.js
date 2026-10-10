// Gráfico local sin dependencias externas. Las cifras también se exponen como texto accesible.
function drawSalesChart(id, points) {
    const canvas = document.getElementById(id);
    if (!canvas) return;
    canvas.setAttribute('role', 'img');
    canvas.setAttribute('aria-label', points.map(p => `${p.Label}: $${p.Value}`).join('; '));
    const draw = () => {
        canvas.width = canvas.parentElement.clientWidth;
        canvas.height = 240;
        const ctx = canvas.getContext('2d');
        const max = Math.max(1, ...points.map(p => p.Value));
        const width = canvas.width / Math.max(1, points.length);
        ctx.font = '12px sans-serif';
        points.forEach((p, i) => {
            const h = Math.max(0, p.Value) / max * 175;
            ctx.fillStyle = '#8b6914';
            ctx.fillRect(i * width + 5, 205 - h, Math.max(1, width - 10), h);
            ctx.fillStyle = '#303030';
            ctx.fillText(p.Label, i * width + 5, 225);
        });
    };
    draw();
    window.addEventListener('resize', draw);
}
