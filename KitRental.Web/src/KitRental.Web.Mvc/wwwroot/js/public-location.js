(() => {
    const button = document.getElementById("use-location");
    const status = document.getElementById("location-status");
    const latitude = document.getElementById("Latitude");
    const longitude = document.getElementById("Longitude");
    const addressInput = document.getElementById("ReporterAddress")
        || document.getElementById("ReturnAddress")
        || document.getElementById("AddressLine");
    if (!button || !status || !latitude || !longitude || !addressInput) return;

    function setStatus(message, isError = false) {
        status.textContent = message;
        status.classList.toggle("error", isError);
    }

    async function fillAddressFromLocation(lat, lon) {
        const url = new URL("https://nominatim.openstreetmap.org/reverse");
        url.searchParams.set("format", "jsonv2");
        url.searchParams.set("lat", lat);
        url.searchParams.set("lon", lon);
        url.searchParams.set("zoom", "18");
        url.searchParams.set("addressdetails", "1");
        url.searchParams.set("accept-language", "tr");

        const response = await fetch(url.toString(), { headers: { Accept: "application/json" } });
        if (!response.ok) throw new Error("reverse-geocode-failed");
        const data = await response.json();
        if (data.display_name) addressInput.value = data.display_name;

        const address = data.address || {};
        const city = address.province || address.city || address.town || address.state;
        const district = address.county || address.city_district || address.town || address.suburb;
        window.publicCityDistricts?.setLocation(city, district);
        setStatus("Konum bulundu ve acik adres dolduruldu.");
    }

    button.addEventListener("click", () => {
        if (!navigator.geolocation) {
            setStatus("Tarayiciniz konum paylasimini desteklemiyor.", true);
            return;
        }

        setStatus("Konum aliniyor...");
        navigator.geolocation.getCurrentPosition(async position => {
            latitude.value = position.coords.latitude.toFixed(6);
            longitude.value = position.coords.longitude.toFixed(6);
            setStatus("Konum bulundu, acik adres aliniyor...");

            try {
                await fillAddressFromLocation(latitude.value, longitude.value);
            } catch {
                setStatus("Konum bulundu ama acik adres otomatik doldurulamadi. Il, ilce ve adresi elle girebilirsiniz.", true);
            }
        }, () => {
            setStatus("Konum izni verilmedi. Il, ilce ve acik adres ile devam edebilirsiniz.");
        }, {
            enableHighAccuracy: true,
            timeout: 15000,
            maximumAge: 60000
        });
    });
})();
