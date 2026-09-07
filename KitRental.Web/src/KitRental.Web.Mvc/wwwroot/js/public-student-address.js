(() => {
    const citySelect = document.getElementById("City");
    const districtSelect = document.getElementById("District");
    const districtData = window.KIT_RENTAL_TURKEY_DISTRICTS || {};
    if (!citySelect || !districtSelect) return;

    const selectedCity = citySelect.dataset.selected || citySelect.value;
    const selectedDistrict = districtSelect.dataset.selected || districtSelect.value;

    const createOption = (value, text) => {
        const option = document.createElement("option");
        option.value = value;
        option.textContent = text;
        return option;
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
})();
