(() => {
    const citySelect = document.getElementById("City");
    const districtSelect = document.getElementById("District");
    const districtData = window.KIT_RENTAL_TURKEY_DISTRICTS || {};
    if (!citySelect || !districtSelect) return;

    const selectedCity = citySelect.dataset.selected || citySelect.value;
    const selectedDistrict = districtSelect.dataset.selected || districtSelect.value;
    const addressInput = document.getElementById("AddressLine");

    const createOption = (value, text) => {
        const option = document.createElement("option");
        option.value = value;
        option.textContent = text;
        return option;
    };

    const normalizeLocationName = value => (value || "")
        .toLocaleLowerCase("tr")
        .normalize("NFD")
        .replace(/[\u0300-\u036f]/g, "")
        .replace(/\b(il|ili|ilcesi|ilçe|ilçesi|merkez ilçe)\b/g, "")
        .replace(/[^a-z0-9ığüşöçİĞÜŞÖÇ]+/gi, "")
        .replace(/ı/g, "i")
        .replace(/ğ/g, "g")
        .replace(/ü/g, "u")
        .replace(/ş/g, "s")
        .replace(/ö/g, "o")
        .replace(/ç/g, "c");

    const findKnownValue = (values, candidates) => {
        const candidateKeys = candidates
            .filter(Boolean)
            .map(normalizeLocationName)
            .filter(Boolean);

        return values.find(value => candidateKeys.includes(normalizeLocationName(value))) || "";
    };

    const fillDistricts = city => {
        districtSelect.replaceChildren(createOption("", city ? "İlçe seçin" : "Önce il seçin"));
        const districts = districtData[city] || [];
        districts.forEach(district => districtSelect.appendChild(createOption(district, district)));
        districtSelect.disabled = districts.length === 0;
    };

    Object.keys(districtData)
        .sort((left, right) => left.localeCompare(right, "tr"))
        .forEach(city => citySelect.appendChild(createOption(city, city)));

    if (selectedCity && districtData[selectedCity]) {
        citySelect.value = selectedCity;
        fillDistricts(selectedCity);
        if (selectedDistrict && districtData[selectedCity].includes(selectedDistrict)) {
            districtSelect.value = selectedDistrict;
        }
    }

    citySelect.addEventListener("change", () => {
        fillDistricts(citySelect.value);
    });

    addressInput?.addEventListener("public-location:address-resolved", event => {
        const address = event.detail?.address || {};
        const city = findKnownValue(Object.keys(districtData), [
            address.province,
            address.state,
            address.city,
            address.town
        ]);

        if (!city) return;

        citySelect.value = city;
        fillDistricts(city);
        citySelect.dispatchEvent(new Event("change", { bubbles: true }));

        const district = findKnownValue(districtData[city] || [], [
            address.town,
            address.county,
            address.city_district,
            address.district,
            address.municipality,
            address.suburb
        ]);

        if (district) {
            districtSelect.value = district;
        }

        districtSelect.dispatchEvent(new Event("change", { bubbles: true }));
    });
})();
