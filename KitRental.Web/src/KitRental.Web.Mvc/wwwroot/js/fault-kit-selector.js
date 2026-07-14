(() => {
    const combobox = document.getElementById('fault-kit-combobox');
    const search = document.getElementById('fault-kit-search');
    const toggleButton = document.getElementById('fault-kit-toggle');
    const listbox = document.getElementById('fault-kit-options');
    const select = document.getElementById('AssignmentId');
    const searchStatus = document.getElementById('fault-kit-search-status');
    const startButton = document.getElementById('fault-scan-qr');
    const stopButton = document.getElementById('fault-stop-scanner');
    const scanner = document.getElementById('fault-qr-scanner');
    const video = document.getElementById('fault-scanner-video');
    const scannerStatus = document.getElementById('fault-scanner-status');
    let stream;
    let scanning = false;
    let filteredOptions = [];
    let activeIndex = -1;
    let suppressFocusOpen = false;

    if (!combobox || !search || !toggleButton || !listbox || !select || !searchStatus ||
        !startButton || !scanner || !video || !scannerStatus) return;

    const options = Array.from(select.options);
    const normalize = value => value.trim().toUpperCase().replaceAll('İ', 'I');
    const optionLabel = option => `${option.dataset.name ?? ''} — ${option.dataset.serial ?? ''}`;
    const optionText = option => normalize(`${option.dataset.name ?? ''} ${option.dataset.serial ?? ''} ${option.dataset.qr ?? ''}`);

    const closeOptions = () => {
        listbox.hidden = true;
        search.setAttribute('aria-expanded', 'false');
        search.removeAttribute('aria-activedescendant');
        activeIndex = -1;
    };

    const setActiveOption = index => {
        const items = Array.from(listbox.querySelectorAll('[role="option"]'));
        if (items.length === 0) return;
        activeIndex = (index + items.length) % items.length;
        for (const [itemIndex, item] of items.entries()) item.classList.toggle('is-active', itemIndex === activeIndex);
        const activeItem = items[activeIndex];
        search.setAttribute('aria-activedescendant', activeItem.id);
        activeItem.scrollIntoView({ block: 'nearest' });
    };

    const chooseOption = (option, message = true) => {
        select.value = option.value;
        search.value = optionLabel(option);
        select.dispatchEvent(new Event('change', { bubbles: true }));
        closeOptions();
        if (message) searchStatus.textContent = `${option.dataset.serial} seri numaralı kit seçildi.`;
    };

    const focusSearchWithoutOpening = () => {
        suppressFocusOpen = true;
        search.focus();
        suppressFocusOpen = false;
    };

    const renderOptions = (open = true, queryValue = search.value) => {
        const query = normalize(queryValue);
        filteredOptions = options.filter(option => query.length === 0 || optionText(option).includes(query));
        listbox.replaceChildren();

        if (filteredOptions.length === 0) {
            const empty = document.createElement('div');
            empty.className = 'fault-kit-no-result';
            empty.textContent = 'Eşleşen aktif kiralık kit bulunamadı.';
            listbox.append(empty);
        } else {
            for (const [index, option] of filteredOptions.entries()) {
                const item = document.createElement('button');
                item.type = 'button';
                item.id = `fault-kit-option-${index}`;
                item.className = 'fault-kit-option';
                item.setAttribute('role', 'option');
                item.setAttribute('aria-selected', String(option.value === select.value));

                const name = document.createElement('strong');
                name.textContent = option.dataset.name ?? '';
                const serial = document.createElement('span');
                serial.textContent = option.dataset.serial ?? '';
                item.append(name, serial);
                item.addEventListener('click', () => {
                    chooseOption(option);
                    focusSearchWithoutOpening();
                });
                listbox.append(item);
            }
        }

        searchStatus.textContent = query.length === 0
            ? `${filteredOptions.length} aktif kit listelendi.`
            : filteredOptions.length === 0 ? 'Bu aramayla eşleşen aktif kiralık kit bulunamadı.' : `${filteredOptions.length} kit eşleşti.`;
        activeIndex = -1;
        if (open) {
            listbox.hidden = false;
            search.setAttribute('aria-expanded', 'true');
        }
    };

    const selectedOption = options.find(option => option.selected) ?? options[0];
    if (selectedOption) chooseOption(selectedOption, false);
    select.hidden = true;
    combobox.hidden = false;

    search.addEventListener('focus', () => {
        if (suppressFocusOpen) return;
        const displaysSelection = options.some(option => option.value === select.value && optionLabel(option) === search.value);
        if (displaysSelection) search.select();
        renderOptions(true, displaysSelection ? '' : search.value);
    });
    search.addEventListener('input', () => {
        select.selectedIndex = -1;
        renderOptions();
    });
    search.addEventListener('keydown', event => {
        if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
            event.preventDefault();
            if (listbox.hidden) renderOptions();
            setActiveOption(activeIndex + (event.key === 'ArrowDown' ? 1 : -1));
        } else if (event.key === 'Enter' && activeIndex >= 0) {
            event.preventDefault();
            chooseOption(filteredOptions[activeIndex]);
        } else if (event.key === 'Escape') {
            closeOptions();
        }
    });
    toggleButton.addEventListener('click', () => {
        if (listbox.hidden) {
            search.focus();
            renderOptions(true, '');
        } else closeOptions();
    });
    document.addEventListener('pointerdown', event => {
        if (!combobox.contains(event.target)) closeOptions();
    });

    const stop = (focusButton = true) => {
        scanning = false;
        stream?.getTracks().forEach(track => track.stop());
        stream = undefined;
        video.srcObject = null;
        scanner.hidden = true;
        if (focusButton) startButton.focus();
    };

    const selectScannedKit = value => {
        const normalizedValue = normalize(value);
        const match = options.find(option =>
            normalize(option.dataset.qr ?? '') === normalizedValue || normalize(option.dataset.serial ?? '') === normalizedValue);
        if (!match) {
            scannerStatus.textContent = 'Bu QR kod hesabınızdaki aktif kiralık kitlerle eşleşmedi. Başka bir kod deneyin.';
            return false;
        }

        chooseOption(match);
        stop(false);
        focusSearchWithoutOpening();
        return true;
    };

    const scanFrame = async detector => {
        if (!scanning) return;
        try {
            const codes = await detector.detect(video);
            const value = codes.find(code => code.rawValue)?.rawValue;
            if (value && selectScannedKit(value)) return;
        } catch { /* Video is not ready for every frame. */ }
        window.setTimeout(() => scanFrame(detector), 160);
    };

    startButton.addEventListener('click', async () => {
        closeOptions();
        if (!('BarcodeDetector' in window) || !navigator.mediaDevices?.getUserMedia) {
            scanner.hidden = false;
            scannerStatus.textContent = 'Bu tarayıcı doğrudan QR okumayı desteklemiyor. Seri numarasıyla arama yapabilirsiniz.';
            return;
        }

        scanner.hidden = false;
        scannerStatus.textContent = 'Kamera izni bekleniyor…';
        try {
            stream = await navigator.mediaDevices.getUserMedia({
                video: { facingMode: { ideal: 'environment' }, width: { ideal: 1280 }, height: { ideal: 720 } },
                audio: false
            });
            video.srcObject = stream;
            await video.play();
            const detector = new BarcodeDetector({ formats: ['qr_code'] });
            scanning = true;
            scannerStatus.textContent = 'QR kod aranıyor…';
            scanFrame(detector);
        } catch {
            scannerStatus.textContent = 'Kamera açılamadı. Kamera iznini kontrol edin veya seri numarasıyla arama yapın.';
            stream?.getTracks().forEach(track => track.stop());
        }
    });

    stopButton?.addEventListener('click', () => stop());
    window.addEventListener('pagehide', () => stream?.getTracks().forEach(track => track.stop()));
})();
