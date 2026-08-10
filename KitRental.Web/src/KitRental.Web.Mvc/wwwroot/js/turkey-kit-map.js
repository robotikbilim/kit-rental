(() => {
    const mapElement = document.getElementById("turkey-kit-map");
    if (!mapElement || !window.L) return;

    const locations = JSON.parse(mapElement.dataset.locations || "[]");
    const detailUrlTemplate = mapElement.dataset.detailUrlTemplate || "";
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

    markerLocations.forEach(item => {
        const marker = L.marker([Number(item.latitude), Number(item.longitude)], {
            title: `${item.kitName} ${item.serialNumber}`
        });
        const detailUrl = detailUrlTemplate.replace("__id__", encodeURIComponent(item.productUnitId));
        marker.bindTooltip(`${item.kitName} - ${item.serialNumber}`);
        marker.bindPopup(`
            <strong>${escapeHtml(item.kitName)}</strong><br>
            ${escapeHtml(item.serialNumber)}<br>
            ${escapeHtml(item.recipientName)}<br>
            ${escapeHtml(item.addressLine)}<br>
            ${escapeHtml(item.district)} / ${escapeHtml(item.city)}<br>
            <a href="${escapeHtml(detailUrl)}">Kit gecmisine git</a>
        `);
        clusters.addLayer(marker);
    });

    map.addLayer(clusters);
    if (markerLocations.length > 0) map.fitBounds(clusters.getBounds().pad(0.25), { maxZoom: 13 });
})();
