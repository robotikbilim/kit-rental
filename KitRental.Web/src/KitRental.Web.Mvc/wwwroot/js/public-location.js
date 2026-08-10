(() => {
    const button = document.getElementById("use-location");
    const status = document.getElementById("location-status");
    const latitude = document.getElementById("Latitude");
    const longitude = document.getElementById("Longitude");
    const addressInput = document.getElementById("ReporterAddress")
        || document.getElementById("ReturnAddress")
        || document.getElementById("AddressLine");
    const cityInput = document.getElementById("City");
    const districtInput = document.getElementById("District");
    if (!button || !status || !latitude || !longitude) return;
    let submittingAfterGeocode = false;

    function setStatus(message, isError = false) {
        status.textContent = message;
        status.classList.toggle("error", isError);
    }

    function hasCoordinates() {
        return latitude.value.trim().length > 0 && longitude.value.trim().length > 0;
    }

    function buildAddressQuery() {
        const parts = [
            addressInput?.value,
            districtInput?.value,
            cityInput?.value,
            "Türkiye"
        ].filter(value => value && value.trim().length > 0);
        return parts.join(", ");
    }

    async function fillLocationFromAddress() {
        const query = buildAddressQuery();
        if (!query || query === "Türkiye") return false;
        const url = new URL("https://nominatim.openstreetmap.org/search");
        url.searchParams.set("format", "jsonv2");
        url.searchParams.set("q", query);
        url.searchParams.set("limit", "1");
        url.searchParams.set("countrycodes", "tr");
        url.searchParams.set("addressdetails", "1");
        url.searchParams.set("accept-language", "tr");
        const response = await fetch(url.toString(), { headers: { "Accept": "application/json" } });
        if (!response.ok) throw new Error("geocode-failed");
        const results = await response.json();
        const first = Array.isArray(results) ? results[0] : null;
        if (!first?.lat || !first?.lon) return false;
        latitude.value = Number(first.lat).toFixed(6);
        longitude.value = Number(first.lon).toFixed(6);
        const address = first.address || {};
        const city = address.province || address.city || address.town || address.state;
        const district = address.county || address.city_district || address.town || address.suburb;
        if (cityInput && city) cityInput.value = city;
        if (districtInput && district) districtInput.value = district;
        return true;
    }

    async function fillAddressFromLocation(lat, lon) {
        if (!addressInput) return;
        try {
            const url = new URL("https://nominatim.openstreetmap.org/reverse");
            url.searchParams.set("format", "jsonv2");
            url.searchParams.set("lat", lat);
            url.searchParams.set("lon", lon);
            url.searchParams.set("zoom", "18");
            url.searchParams.set("addressdetails", "1");
            url.searchParams.set("accept-language", "tr");
            const response = await fetch(url.toString(), { headers: { "Accept": "application/json" } });
            if (!response.ok) throw new Error("reverse-geocode-failed");
            const data = await response.json();
            if (data.display_name) addressInput.value = data.display_name;

            const address = data.address || {};
            const city = address.province || address.city || address.town || address.state;
            const district = address.county || address.city_district || address.town || address.suburb;
            if (cityInput && city) cityInput.value = city;
            if (districtInput && district) districtInput.value = district;
            setStatus("Konum ve acik adres dolduruldu.");
        } catch {
            setStatus(`Konum isaretlendi: ${latitude.value}, ${longitude.value}. Adres otomatik doldurulamadi.`, true);
        }
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
            setStatus("Konum isaretlendi, adres aliniyor...");
            fillAddressFromLocation(latitude.value, longitude.value);
        }, () => setStatus("Konum izni alinmadi. Adresle devam edebilirsiniz.", true), {
            enableHighAccuracy: true,
            timeout: 15000,
            maximumAge: 60000
        });
    });

    document.querySelectorAll("form.public-fault-form").forEach(form => {
        form.addEventListener("submit", async event => {
            if (submittingAfterGeocode || hasCoordinates()) return;
            event.preventDefault();
            const submitter = event.submitter;
            if (submitter) submitter.disabled = true;
            setStatus("Adres konuma çevriliyor...");
            try {
                const found = await fillLocationFromAddress();
                setStatus(found
                    ? "Adres koordinatlari eklendi."
                    : "Adres icin koordinat bulunamadi; adres bilgisiyle devam ediliyor.",
                    !found);
            } catch {
                setStatus("Adres koordinata cevrilemedi; adres bilgisiyle devam ediliyor.", true);
            } finally {
                submittingAfterGeocode = true;
                if (submitter) submitter.disabled = false;
                form.requestSubmit(submitter || undefined);
            }
        });
    });
})();
