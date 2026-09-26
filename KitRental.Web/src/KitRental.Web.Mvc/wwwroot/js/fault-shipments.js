document.querySelectorAll('[data-fault-shipment-form]').forEach((form) => {
    form.addEventListener('submit', (event) => {
        if (form.dataset.submitting === 'true') { event.preventDefault(); return; }
        form.dataset.submitting = 'true';
        const button = form.querySelector('button[type="submit"]');
        button.disabled = true;
        button.textContent = 'Kargo oluşturuluyor…';
        form.setAttribute('aria-busy', 'true');
    });
});

document.querySelectorAll('[data-fault-status-form]').forEach((form) => {
    const panel = form.closest('[data-fault-status-editor]');
    const save = form.querySelector('[data-fault-status-save]');
    const preview = form.querySelector('[data-fault-status-preview]');
    const syncSelection = () => {
        const selected = form.querySelector('input[name="status"]:checked');
        save.disabled = !selected;
        preview.textContent = selected
            ? `Seçilen işlem: ${selected.dataset.actionLabel}`
            : 'Kaydetmeden önce bir işlem seçin.';
    };
    form.addEventListener('change', syncSelection);
    form.querySelector('[data-fault-status-cancel]').addEventListener('click', () => {
        form.reset();
        syncSelection();
        panel.open = false;
        panel.querySelector('summary').focus();
    });
    form.addEventListener('submit', (event) => {
        if (form.dataset.submitting === 'true') { event.preventDefault(); return; }
        form.dataset.submitting = 'true';
        save.disabled = true;
        save.textContent = 'Kaydediliyor…';
        form.setAttribute('aria-busy', 'true');
    });
    syncSelection();
});
