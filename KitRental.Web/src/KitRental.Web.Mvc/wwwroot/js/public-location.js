(() => {
    const button = document.getElementById("use-location");
    const status = document.getElementById("location-status");
    const latitude = document.getElementById("Latitude");
    const longitude = document.getElementById("Longitude");
    if (!button || !status || !latitude || !longitude) return;

    function setStatus(message, isError = false) {
        status.textContent = message;
        status.classList.toggle("error", isError);
    }

    button.addEventListener("click", () => {
        if (!navigator.geolocation) {
            setStatus("Tarayiciniz konum paylasimini desteklemiyor.", true);
            return;
        }
        setStatus("Konum aliniyor...");
        navigator.geolocation.getCurrentPosition(position => {
            latitude.value = position.coords.latitude.toFixed(6);
            longitude.value = position.coords.longitude.toFixed(6);
            setStatus(`Konum isaretlendi: ${latitude.value}, ${longitude.value}`);
        }, () => setStatus("Konum izni alinmadi. Adresle devam edebilirsiniz.", true), {
            enableHighAccuracy: true,
            timeout: 15000,
            maximumAge: 60000
        });
    });
})();
