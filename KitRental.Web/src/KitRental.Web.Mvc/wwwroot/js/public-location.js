(() => {
    const mapElement = document.getElementById("public-location-map");
    const locateButton = document.getElementById("use-location");
    const status = document.getElementById("location-status");
    const latitude = document.getElementById("Latitude");
    const longitude = document.getElementById("Longitude");
    const citySelect = document.getElementById("City");
    const districtSelect = document.getElementById("District");
    const addressInput = document.getElementById("ReporterAddress")
        || document.getElementById("ReturnAddress")
        || document.getElementById("AddressLine");
    if (!mapElement || !status || !latitude || !longitude || !addressInput || !window.L) return;

    const formatCoordinate = value => Number(value).toFixed(6);
    let marker = null;
    let activeRequestId = 0;

    const setStatus = (message, isError = false) => {
        status.textContent = message;
        status.classList.toggle("error", isError);
    };

    const map = L.map(mapElement, { scrollWheelZoom: true }).setView([39.0, 35.0], 6);
    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
        maxZoom: 19,
        attribution: "&copy; OpenStreetMap"
    }).addTo(map);

    const initialLatitude = Number(latitude.value);
    const initialLongitude = Number(longitude.value);
    const hasInitialCoordinates = Number.isFinite(initialLatitude) && Number.isFinite(initialLongitude);

    const applyCoordinates = (lat, lon) => {
        latitude.value = formatCoordinate(lat);
        longitude.value = formatCoordinate(lon);
    };

    const reverseGeocode = async (lat, lon) => {
        const requestId = ++activeRequestId;
        setStatus("Konuma gore adres bilgisi aliniyor...");

        try {
            const url = new URL("https://nominatim.openstreetmap.org/reverse");
            url.searchParams.set("format", "jsonv2");
            url.searchParams.set("lat", formatCoordinate(lat));
            url.searchParams.set("lon", formatCoordinate(lon));
            url.searchParams.set("zoom", "18");
            url.searchParams.set("addressdetails", "1");
            url.searchParams.set("accept-language", "tr");

            const response = await fetch(url.toString(), {
                headers: { Accept: "application/json" }
            });
            if (!response.ok) throw new Error("reverse-geocode-failed");

            const data = await response.json();
            if (requestId !== activeRequestId) return;

            const address = data.address || {};
            const city = address.province || address.city || address.town || address.state || "";
            const district = address.county || address.city_district || address.town || address.suburb || "";
            const displayName = typeof data.display_name === "string" ? data.display_name.trim() : "";

            if (displayName) {
                addressInput.value = displayName;
            }

            if (city || district) {
                window.publicCityDistricts?.setLocation(city, district);
            }

            setStatus(`Secilen konum kaydedildi: ${formatCoordinate(lat)}, ${formatCoordinate(lon)}`);
        } catch {
            if (requestId !== activeRequestId) return;
            setStatus("Konum secildi ama acik adres otomatik doldurulamadi. Gerekirse adresi elle duzeltebilirsiniz.", true);
        }
    };

    const setMarker = (lat, lon, options = {}) => {
        const { centerMap = false, resolveAddress = true } = options;
        applyCoordinates(lat, lon);

        if (marker) {
            marker.setLatLng([lat, lon]);
        } else {
            marker = L.marker([lat, lon], { draggable: true }).addTo(map);
            marker.on("dragend", event => {
                const position = event.target.getLatLng();
                setMarker(position.lat, position.lng, { centerMap: false, resolveAddress: true });
            });
        }

        if (centerMap) {
            map.setView([lat, lon], Math.max(map.getZoom(), 16));
        }

        if (resolveAddress) {
            reverseGeocode(lat, lon);
        } else {
            setStatus(`Secilen konum hazir: ${formatCoordinate(lat)}, ${formatCoordinate(lon)}`);
        }
    };

    if (hasInitialCoordinates) {
        map.setView([initialLatitude, initialLongitude], 16);
        setMarker(initialLatitude, initialLongitude, { centerMap: false, resolveAddress: false });
    } else {
        setStatus("Konumumu bul ile yakindaki noktayi secin veya haritadan pimi elle yerlestirin.");
    }

    map.on("click", event => {
        const { lat, lng } = event.latlng;
        setMarker(lat, lng, { centerMap: false, resolveAddress: true });
    });

    locateButton?.addEventListener("click", () => {
        if (!navigator.geolocation) {
            setStatus("Tarayiciniz GPS konumunu desteklemiyor. Haritadan elle secim yapabilirsiniz.", true);
            return;
        }

        setStatus("GPS konumu aliniyor...");
        navigator.geolocation.getCurrentPosition(position => {
            const lat = position.coords.latitude;
            const lon = position.coords.longitude;
            setMarker(lat, lon, { centerMap: true, resolveAddress: true });
        }, () => {
            setStatus("GPS konumu alinamadi. Haritadan elle secim yapabilirsiniz.", true);
        }, {
            enableHighAccuracy: true,
            timeout: 15000,
            maximumAge: 60000
        });
    });
})();
