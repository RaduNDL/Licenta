document.addEventListener('DOMContentLoaded', function () {
    var tooltipTriggerList = [].slice.call(document.querySelectorAll('[title]'));
    tooltipTriggerList.map(function (el) {
        if (typeof bootstrap !== 'undefined') {
            return new bootstrap.Tooltip(el);
        }
        return null;
    });

    const counters = document.querySelectorAll('.stat-number[data-target]');
    counters.forEach((el) => {
        const target = parseInt(el.getAttribute('data-target') || '0', 10);
        if (Number.isNaN(target)) return;

        const duration = 650;
        const start = 0;
        const startTime = performance.now();

        function tick(now) {
            const t = Math.min(1, (now - startTime) / duration);
            const eased = 1 - Math.pow(1 - t, 3);
            const val = Math.round(start + (target - start) * eased);
            el.textContent = val.toString();
            if (t < 1) requestAnimationFrame(tick);
        }

        requestAnimationFrame(tick);
    });
});
