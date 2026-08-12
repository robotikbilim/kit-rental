(() => {
    const mapElement = document.getElementById("turkey-kit-map");
    if (!mapElement || !window.L) return;

    const locations = JSON.parse(mapElement.dataset.locations || "[]");
    const detailUrlTemplate = mapElement.dataset.detailUrlTemplate || "";
    const filterInputs = [...document.querySelectorAll(".kit-map-filter-input")];
    const modelFilterInputs = [...document.querySelectorAll(".kit-map-model-filter-input")];
    const serialFilterInput = document.querySelector(".kit-map-serial-filter");
    const map = L.map(mapElement, { scrollWheelZoom: true }).setView([39.0, 35.0], 5);
    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
        maxZoom: 18,
        attribution: "&copy; OpenStreetMap"
    }).addTo(map);

    const clusters = L.markerClusterGroup({
        showCoverageOnHover: false,
        maxClusterRadius: zoom => zoom < 7 ? 72 : 36
    });
    const escapeHtml = value => String(value ?? "")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll("\"", "&quot;")
        .replaceAll("'", "&#39;");

    const markerLocations = locations.filter(item =>
        Number.isFinite(Number(item.latitude)) && Number.isFinite(Number(item.longitude)));

    const statusKey = status => {
        switch (Number(status)) {
            case 5: return "active";
            case 6: return "returning";
            case 7:
            case 8:
            case 9:
            case 10:
            case 11:
            case 12:
                return "faulty";
            default:
                return "other";
        }
    };

    const categoryKey = item => item.locationCategory || statusKey(item.status);

    const markerColor = item => {
        switch (categoryKey(item)) {
            case "faulty":
                return "#c0392b";
            case "returning":
                return "#d97706";
            case "active":
                return "#227347";
            default:
                return "#5b6b64";
        }
    };

    const createColoredIcon = item => L.divIcon({
        className: "kit-status-marker",
        html: `<span class="kit-status-marker-dot" style="background:${markerColor(item)}"></span>`,
        iconSize: [22, 22],
        iconAnchor: [11, 11],
        popupAnchor: [0, -10]
    });

    const markers = markerLocations.map(item => {
        const marker = L.marker([Number(item.latitude), Number(item.longitude)], {
            title: `${item.kitName} ${item.serialNumber}`,
            icon: createColoredIcon(item)
        });
        const detailUrl = detailUrlTemplate.replace("__id__", encodeURIComponent(item.productUnitId));
        marker.bindTooltip(`${item.kitSku || item.kitName} - ${item.serialNumber}`);
        marker.bindPopup(`
            <strong>${escapeHtml(item.kitName)}</strong><br>
            ${escapeHtml(item.kitSku)}<br>
            ${escapeHtml(item.serialNumber)}<br>
            ${escapeHtml(item.recipientName)}<br>
            ${escapeHtml(item.addressLine)}<br>
            ${escapeHtml(item.district)} / ${escapeHtml(item.city)}<br>
            <a href="${escapeHtml(detailUrl)}">Kit gecmisine git</a>
        `);
        return { item, marker, status: categoryKey(item), productModelId: String(item.productModelId || "") };
    });

    const syncMarkers = () => {
        clusters.clearLayers();
        const activeFilters = new Set(filterInputs.filter(input => input.checked).map(input => input.value));
        const activeModels = new Set(modelFilterInputs.filter(input => input.checked).map(input => input.value));
        const serialQuery = (serialFilterInput?.value || "").trim().toLocaleLowerCase("tr-TR");
        const visibleMarkers = markers.filter(entry =>
            activeFilters.has(entry.status) &&
            (modelFilterInputs.length === 0 || activeModels.has(entry.productModelId)) &&
            (!serialQuery || String(entry.item.serialNumber || "").toLocaleLowerCase("tr-TR").includes(serialQuery)));
        visibleMarkers.forEach(entry => clusters.addLayer(entry.marker));
        if (!map.hasLayer(clusters)) map.addLayer(clusters);
        if (visibleMarkers.length > 0) {
            map.fitBounds(clusters.getBounds().pad(0.25), { maxZoom: 13 });
        }
    };

    filterInputs.forEach(input => input.addEventListener("change", syncMarkers));
    modelFilterInputs.forEach(input => input.addEventListener("change", syncMarkers));
    serialFilterInput?.addEventListener("input", syncMarkers);
    syncMarkers();
})();
