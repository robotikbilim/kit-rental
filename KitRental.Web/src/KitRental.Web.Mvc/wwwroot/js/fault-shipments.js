document.querySelectorAll('[data-fault-shipment-form]').forEach((form) => {
    form.addEventListener('submit', (event) => {
        if (form.dataset.submitting === 'true') { event.preventDefault(); return; }
        const button = form.querySelector('button[type="submit"]');
        const startNewInput = form.querySelector('[data-start-new-shipment]');
        if (button.dataset.existingShipment === 'true') {
            const status = button.dataset.shipmentStatus || 'Bilinmiyor';
            if (!window.confirm(`Bu arıza kaydı için [${status}] durumunda zaten bir gönderi başlatılmış. Yeni gönderi başlatıp gönderiyi güncellemek ister misiniz?`)) {
                event.preventDefault();
                return;
            }
            startNewInput.value = 'true';
        }
        form.dataset.submitting = 'true';
        button.disabled = true;
        button.textContent = 'Kargo oluşturuluyor…';
        form.setAttribute('aria-busy', 'true');
    });
});

document.querySelectorAll('[data-fault-received-form]').forEach((form) => {
    form.addEventListener('submit', (event) => {
        if (form.dataset.submitting === 'true') { event.preventDefault(); return; }
        form.dataset.submitting = 'true';
        const button = form.querySelector('button[type="submit"]');
        button.disabled = true;
        button.textContent = 'Teslim alındı olarak kaydediliyor…';
        form.setAttribute('aria-busy', 'true');
    });
});

document.querySelectorAll('[data-fault-status-form]').forEach((form) => {
    const panel = form.closest('[data-fault-status-editor]');
    const save = form.querySelector('[data-fault-status-save]');
    const preview = form.querySelector('[data-fault-status-preview]');
    const syncSelection = () => {
        const selected = form.querySelector('input[name="status"]:checked');
        const note = form.querySelector('textarea[name="note"]').value.trim();
        save.disabled = !selected && !note;
        preview.textContent = selected
            ? `Seçilen işlem: ${selected.dataset.actionLabel}`
            : note ? 'Yalnızca not eklenecek; mevcut durum korunacak.' : 'Durum seçin veya not girin.';
    };
    form.addEventListener('change', syncSelection);
    form.querySelector('textarea[name="note"]').addEventListener('input', syncSelection);
    form.querySelector('[data-fault-status-cancel]').addEventListener('click', () => {
        form.reset();
        syncSelection();
        panel.open = false;
        panel.querySelector('summary').focus();
    });
    form.addEventListener('submit', (event) => {
        const selected = form.querySelector('input[name="status"]:checked');
        const note = form.querySelector('textarea[name="note"]').value.trim();
        if (!selected && !note) { event.preventDefault(); syncSelection(); return; }
        if (form.dataset.submitting === 'true') { event.preventDefault(); return; }
        form.dataset.submitting = 'true';
        save.disabled = true;
        save.textContent = 'Kaydediliyor…';
        form.setAttribute('aria-busy', 'true');
    });
    syncSelection();
});
