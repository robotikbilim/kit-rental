(() => {
    const data = Array.isArray(window.TurkeyCityDistricts) ? window.TurkeyCityDistricts : [];
    const citySelect = document.getElementById("City");
    const districtSelect = document.getElementById("District");
    if (!citySelect || !districtSelect || data.length === 0) return;

    const cityMap = new Map(data.map(item => [item.name, item.districts]));
    const normalize = value => (value || "")
        .toLocaleLowerCase("tr-TR")
        .replace(/\s+/g, " ")
        .trim();

    function findExactCityName(value) {
        const normalized = normalize(value);
        return data.find(item => normalize(item.name) === normalized)?.name ?? null;
    }

    function findExactDistrictName(cityName, value) {
        const districts = cityMap.get(cityName) || [];
        const normalized = normalize(value);
        return districts.find(item => normalize(item) === normalized) ?? null;
    }

    function populateCities() {
        const selectedCity = citySelect.dataset.selected || citySelect.value;
        citySelect.innerHTML = '<option value="">İl seçin</option>';
        for (const city of data) {
            const option = document.createElement("option");
            option.value = city.name;
            option.textContent = city.name;
            citySelect.appendChild(option);
        }

        const matchedCity = findExactCityName(selectedCity);
        if (matchedCity) citySelect.value = matchedCity;
    }

    function populateDistricts(selectedDistrict) {
        const matchedCity = findExactCityName(citySelect.value);
        districtSelect.innerHTML = '<option value="">İlçe seçin</option>';
        districtSelect.disabled = !matchedCity;
        if (!matchedCity) return;

        const districts = cityMap.get(matchedCity) || [];
        for (const district of districts) {
            const option = document.createElement("option");
            option.value = district;
            option.textContent = district;
            districtSelect.appendChild(option);
        }

        const matchedDistrict = findExactDistrictName(matchedCity, selectedDistrict);
        if (matchedDistrict) districtSelect.value = matchedDistrict;
    }

    populateCities();
    populateDistricts(districtSelect.dataset.selected || districtSelect.value);

    citySelect.addEventListener("change", () => {
        populateDistricts("");
    });

    window.publicCityDistricts = {
        setLocation(city, district) {
            const matchedCity = findExactCityName(city);
            if (!matchedCity) return;
            citySelect.value = matchedCity;
            populateDistricts(district);
        }
    };
})();
