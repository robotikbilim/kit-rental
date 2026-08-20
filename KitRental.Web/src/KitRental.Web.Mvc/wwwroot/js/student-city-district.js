(function () {
    const cities = window.TurkeyCityDistricts || [];
    const citySelects = document.querySelectorAll('[data-location-city]');
    const districtSelects = document.querySelectorAll('[data-location-district]');
    if (!citySelects.length || !districtSelects.length) return;

    const cityById = (id) => cities.find(city => Number(city.code) === Number(id));
    const fillCities = (select) => {
        cities.forEach(city => {
            const option = document.createElement('option');
            option.value = Number(city.code);
            option.textContent = city.name;
            select.appendChild(option);
        });
    };
    const fillDistricts = (select, cityId) => {
        select.innerHTML = '<option value="">İlçe seçin</option>';
        const city = cityById(cityId);
        (city?.districts || []).forEach((district, index) => {
            const option = document.createElement('option');
            option.value = Number(city.code) * 1000 + index + 1;
            option.textContent = district;
            select.appendChild(option);
        });
    };
    const syncNames = (citySelect, districtSelect) => {
        const city = cityById(citySelect.value);
        const district = [...districtSelect.options].find(option => option.value === districtSelect.value);
        citySelect.form?.querySelector('[data-location-city-name]')?.setAttribute('value', city?.name || '');
        districtSelect.form?.querySelector('[data-location-district-name]')?.setAttribute('value', district?.textContent || '');
    };
    citySelects.forEach((citySelect, index) => {
        const districtSelect = districtSelects[index] || districtSelects[0];
        fillCities(citySelect);
        citySelect.addEventListener('change', () => { fillDistricts(districtSelect, citySelect.value); syncNames(citySelect, districtSelect); });
        districtSelect.addEventListener('change', () => syncNames(citySelect, districtSelect));
        if (citySelect.value) fillDistricts(districtSelect, citySelect.value);
    });

    document.querySelectorAll('[data-student-edit]').forEach(trigger => {
        trigger.addEventListener('click', () => {
            const form = document.querySelector('#student-edit-dialog form');
            const city = form?.querySelector('[data-location-city]');
            const district = form?.querySelector('[data-location-district]');
            if (!city || !district) return;
            city.value = trigger.dataset.studentCityId || '';
            fillDistricts(district, city.value);
            district.value = trigger.dataset.studentDistrictId || '';
            syncNames(city, district);
        });
    });
})();
