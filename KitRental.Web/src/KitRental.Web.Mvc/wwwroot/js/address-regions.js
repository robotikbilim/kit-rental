(() => {
    const requests = new Map();
    const readRegions = url => {
        if (!requests.has(url)) {
            const request = fetch(url, { headers: { Accept: "application/json" } })
                .then(response => {
                    if (!response.ok) throw new Error("regions-unavailable");
                    return response.json();
                })
                .then(values => {
                    if (!Array.isArray(values)) throw new Error("regions-invalid");
                    return values;
                })
                .catch(error => { requests.delete(url); throw error; });
            requests.set(url, request);
        }
        return requests.get(url);
    };
    const normalize = value => (value || "").toLocaleLowerCase("tr").normalize("NFD")
        .replace(/[\u0300-\u036f]/g, "").replace(/ı/g, "i")
        .replace(/\b(ili|ilcesi|ilçesi|ilçe|il)\b/g, "").replace(/[^a-z0-9]/g, "");
    const findRegion = (values, id, names) => values.find(value => id && String(value.id) === String(id))
        || values.find(value => names.some(name => normalize(name) && normalize(name) === normalize(value.name)));
    const option = (value, name) => {
        const element = document.createElement("option");
        element.value = value;
        element.textContent = name;
        return element;
    };
    document.querySelectorAll("[data-address-regions]").forEach(container => {
        const city = container.querySelector("[data-region-city]");
        const district = container.querySelector("[data-region-district]");
        const cityName = container.querySelector("[data-region-city-name]");
        const districtName = container.querySelector("[data-region-district-name]");
        const error = container.querySelector("[data-region-error]");
        const street = document.getElementById(container.dataset.streetId);
        let districtRequest = 0;
        let selectionRequest = 0;
        let cities = [];
        let ready = false;
        const showError = message => { error.textContent = message; error.hidden = !message; };
        const updateNames = () => {
            cityName.value = city.value ? city.selectedOptions[0]?.textContent || "" : "";
            districtName.value = district.value ? district.selectedOptions[0]?.textContent || "" : "";
        };
        const loadDistricts = async (id, names = []) => {
            const request = ++districtRequest;
            const selectedCity = city.value;
            district.replaceChildren(option("", selectedCity ? "İlçeler yükleniyor…" : "Önce il seçin"));
            district.disabled = true;
            districtName.value = "";
            ready = false;
            if (!selectedCity) { ready = true; return; }
            try {
                const values = await readRegions(`/address-regions/districts?cityId=${encodeURIComponent(selectedCity)}`);
                if (request !== districtRequest || city.value !== selectedCity) return;
                district.replaceChildren(option("", "İlçe seçin"), ...values.map(value => option(value.id, value.name)));
                district.disabled = false;
                const selected = findRegion(values, id, names);
                if (selected) district.value = String(selected.id);
                updateNames();
                ready = true;
                showError("");
            } catch {
                if (request !== districtRequest) return;
                district.replaceChildren(option("", "İlçeler yüklenemedi"));
                showError("İlçe listesi yüklenemedi. İli tekrar seçerek yeniden deneyin.");
            }
        };
        city.addEventListener("change", () => { ++selectionRequest; updateNames(); loadDistricts(); });
        district.addEventListener("change", updateNames);
        city.disabled = true;
        const initialized = readRegions("/address-regions/cities").then(async values => {
            cities = values;
            city.replaceChildren(option("", "İl seçin"), ...values.map(value => option(value.id, value.name)));
            city.disabled = false;
            const selected = findRegion(values, container.dataset.cityId, [container.dataset.cityName]);
            if (selected) city.value = String(selected.id);
            updateNames();
            await loadDistricts(container.dataset.districtId, [container.dataset.districtName]);
        }).catch(() => {
            city.replaceChildren(option("", "İller yüklenemedi"));
            showError("İl listesi yüklenemedi. Sayfayı yenileyerek yeniden deneyin.");
        });
        street?.addEventListener("public-location:address-resolved", async event => {
            const selection = ++selectionRequest;
            await initialized;
            if (selection !== selectionRequest) return;
            const address = event.detail?.address || {};
            const selected = findRegion(cities, null, [address.province, address.state, address.city, address.town]);
            if (!selected) return;
            city.value = String(selected.id);
            updateNames();
            await loadDistricts(null, [address.town, address.county, address.city_district, address.district,
                address.municipality, address.suburb]);
        });
        container.closest("form")?.addEventListener("submit", event => {
            if (container.closest("[data-return-pickup-field]")?.hidden) return;
            if (!ready) {
                event.preventDefault();
                showError("İl ve ilçe listesinin yüklenmesini bekleyin; yükleme başarısızsa yeniden deneyin.");
            }
        }, { capture: true });
    });
})();
