document.addEventListener('DOMContentLoaded', function () {
    const greetingElement = document.getElementById('dynamic-greeting');
    if (greetingElement) {
        const hour = new Date().getHours();
        let greeting = 'Welcome back';

        if (hour >= 5 && hour < 12) greeting = 'Good Morning';
        else if (hour >= 12 && hour < 18) greeting = 'Good Afternoon';
        else greeting = 'Good Evening';

        greetingElement.innerText = greeting;
    }

    var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    tooltipTriggerList.map(function (el) {
        if (typeof bootstrap !== 'undefined') return new bootstrap.Tooltip(el);
    });

    if (!document.getElementById('patientpanel-animations')) {
        const style = document.createElement('style');
        style.id = 'patientpanel-animations';
        style.innerHTML = `
            @keyframes fadeInUp {
                from { opacity: 0; transform: translateY(20px); }
                to { opacity: 1; transform: translateY(0); }
            }
        `;
        document.head.appendChild(style);
    }

    const items = document.querySelectorAll('.dashboard-card, .stat-widget');
    items.forEach((item, index) => {
        item.style.opacity = '0';
        item.style.animation = `fadeInUp 0.5s ease forwards ${index * 0.08}s`;
    });
});
