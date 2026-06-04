(function () {
    if (typeof signalR === 'undefined') {
        return;
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/payments')
        .withAutomaticReconnect()
        .build();

    connection.on('PaymentUpdated', function (data) {
        const banner = document.getElementById('payment-live-banner');
        if (banner) {
            banner.classList.remove('d-none');
            banner.textContent =
                'Payment updated for flat ' + data.flatNumber +
                ' (' + data.month + ' ' + data.year + '). Refreshing…';
        }
        setTimeout(function () {
            window.location.reload();
        }, 1500);
    });

    connection.start().catch(function (err) {
        console.warn('Payment live updates unavailable:', err);
    });
})();
