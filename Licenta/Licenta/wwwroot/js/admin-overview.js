document.addEventListener('DOMContentLoaded', function () {
    const canvas = document.getElementById('rolesChart');
    if (!canvas) return;

    const dataEl = document.getElementById('rolesChartData');
    if (!dataEl) return;

    let usersPerRole = {};
    try {
        usersPerRole = JSON.parse(dataEl.textContent || '{}') || {};
    } catch {
        usersPerRole = {};
    }

    const labels = Object.keys(usersPerRole);
    const data = Object.values(usersPerRole);

    if (!labels.length || !data.length) return;

    const ctx = canvas.getContext('2d');

    const colors = [
        '#4e73df',
        '#1cc88a',
        '#36b9cc',
        '#f6c23e',
        '#e74a3b',
        '#858796'
    ];

    new Chart(ctx, {
        type: 'doughnut',
        data: {
            labels: labels,
            datasets: [{
                data: data,
                backgroundColor: colors,
                hoverBackgroundColor: colors,
                hoverBorderColor: "rgba(234, 236, 244, 1)",
                borderWidth: 5,
                cutout: '75%',
            }],
        },
        options: {
            maintainAspectRatio: false,
            responsive: true,
            plugins: {
                legend: {
                    display: true,
                    position: 'bottom',
                    labels: {
                        usePointStyle: true,
                        padding: 20,
                        font: {
                            size: 12,
                            family: "'Nunito', sans-serif"
                        }
                    }
                },
                tooltip: {
                    backgroundColor: "rgb(255,255,255)",
                    bodyColor: "#858796",
                    borderColor: '#dddfeb',
                    borderWidth: 1,
                    xPadding: 15,
                    yPadding: 15,
                    displayColors: true,
                    caretPadding: 10,
                }
            },
            layout: {
                padding: 10
            }
        },
    });
});
