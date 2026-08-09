(() => {
    const mapElement = document.getElementById("turkey-kit-map");
    if (!mapElement || !window.L) return;

    const cityCoordinates = {
        adana: [37.0, 35.3213], adiyaman: [37.7648, 38.2786], afyonkarahisar: [38.7569, 30.5387],
        agri: [39.7191, 43.0503], aksaray: [38.3687, 34.037], amasya: [40.6499, 35.8353],
        ankara: [39.9334, 32.8597], antalya: [36.8969, 30.7133], ardahan: [41.1105, 42.7022],
        artvin: [41.1828, 41.8183], aydin: [37.845, 27.8396], balikesir: [39.6484, 27.8826],
        bartin: [41.5811, 32.461], batman: [37.8812, 41.1351], bayburt: [40.2603, 40.228],
        bilecik: [40.1426, 29.9793], bingol: [38.8847, 40.4939], bitlis: [38.3938, 42.1232],
        bolu: [40.7395, 31.6116], burdur: [37.7203, 30.2908], bursa: [40.1828, 29.0663],
        canakkale: [40.1553, 26.4142], cankiri: [40.6013, 33.6134], corum: [40.5506, 34.9556],
        denizli: [37.7765, 29.0864], diyarbakir: [37.9144, 40.2306], duzce: [40.8438, 31.1565],
        edirne: [41.6771, 26.5557], elazig: [38.6748, 39.2225], erzincan: [39.75, 39.5],
        erzurum: [39.9043, 41.2679], eskisehir: [39.7767, 30.5206], gaziantep: [37.0662, 37.3833],
        giresun: [40.9128, 38.3895], gumushane: [40.4386, 39.5086], hakkari: [37.5833, 43.7333],
        hatay: [36.2023, 36.1613], igdir: [39.9167, 44.0444], isparta: [37.7648, 30.5566],
        istanbul: [41.0082, 28.9784], izmir: [38.4237, 27.1428], kahramanmaras: [37.5753, 36.9228],
        karabuk: [41.2061, 32.6204], karaman: [37.1811, 33.215], kars: [40.6013, 43.0975],
        kastamonu: [41.3887, 33.7827], kayseri: [38.7205, 35.4826], kilis: [36.7184, 37.1212],
        kirikkale: [39.8468, 33.5153], kirklareli: [41.7351, 27.2252], kirsehir: [39.1458, 34.1606],
        kocaeli: [40.8533, 29.8815], konya: [37.8746, 32.4932], kutahya: [39.4167, 29.9833],
        malatya: [38.3552, 38.3095], manisa: [38.6191, 27.4289], mardin: [37.3122, 40.735],
        mersin: [36.8121, 34.6415], mugla: [37.2153, 28.3636], mus: [38.7432, 41.5065],
        nevsehir: [38.6244, 34.7239], nigde: [37.9667, 34.6833], ordu: [40.9862, 37.8797],
        osmaniye: [37.0742, 36.2478], rize: [41.0255, 40.5177], sakarya: [40.7569, 30.3781],
        samsun: [41.2867, 36.33], sanliurfa: [37.1674, 38.7955], siirt: [37.9333, 41.95],
        sinop: [42.0264, 35.1551], sirnak: [37.5164, 42.4611], sivas: [39.7477, 37.0179],
        tekirdag: [40.978, 27.511], tokat: [40.3167, 36.55], trabzon: [41.0027, 39.7168],
        tunceli: [39.3074, 39.4388], usak: [38.6823, 29.4082], van: [38.5012, 43.372],
        yalova: [40.65, 29.2667], yozgat: [39.8181, 34.8147], zonguldak: [41.4564, 31.7987]
    };

    const normalize = value => (value || "").toLocaleLowerCase("tr-TR")
        .normalize("NFD").replace(/[\u0300-\u036f]/g, "")
        .replaceAll("ı", "i").replaceAll("ğ", "g").replaceAll("ü", "u")
        .replaceAll("ş", "s").replaceAll("ö", "o").replaceAll("ç", "c")
        .replace(/\s+/g, "");
    const offsetFor = value => {
        let hash = 0;
        for (const char of value) hash = ((hash << 5) - hash) + char.charCodeAt(0);
        return [((hash % 17) - 8) / 80, ((((hash / 17) | 0) % 17) - 8) / 80];
    };

    const locations = JSON.parse(mapElement.dataset.locations || "[]");
    const map = L.map(mapElement, { scrollWheelZoom: true }).setView([39.0, 35.0], 5);
    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
        maxZoom: 18,
        attribution: "&copy; OpenStreetMap"
    }).addTo(map);

    const clusters = L.markerClusterGroup({
        showCoverageOnHover: false,
        maxClusterRadius: zoom => zoom < 7 ? 72 : 36
    });

    locations.forEach(item => {
        const base = cityCoordinates[normalize(item.city)];
        if (!base) return;
        const [latOffset, lngOffset] = offsetFor(`${item.city}-${item.district}`);
        const marker = L.marker([base[0] + latOffset, base[1] + lngOffset]);
        marker.bindPopup(`<strong>${item.kitName}</strong><br>${item.serialNumber}<br>${item.recipientName}<br>${item.district} / ${item.city}`);
        clusters.addLayer(marker);
    });
    map.addLayer(clusters);
    if (locations.length > 0) map.fitBounds(clusters.getBounds().pad(0.25), { maxZoom: 8 });
})();
