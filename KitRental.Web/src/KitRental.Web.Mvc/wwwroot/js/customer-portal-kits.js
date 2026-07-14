(() => {
    const table = document.getElementById('portal-kit-table');
    const pagination = document.getElementById('portal-kit-pagination');
    const summary = document.getElementById('portal-kit-page-summary');
    const buttons = document.getElementById('portal-kit-page-buttons');
    if (!table || !pagination || !summary || !buttons) return;

    const rows = Array.from(table.querySelectorAll('tbody tr'));
    const pageSize = Math.max(1, Number.parseInt(table.dataset.pageSize ?? '5', 10));
    const pageCount = Math.max(1, Math.ceil(rows.length / pageSize));
    let currentPage = 1;

    const createButton = (label, page, options = {}) => {
        const button = document.createElement('button');
        button.type = 'button';
        button.textContent = label;
        button.disabled = options.disabled ?? false;
        if (options.label) button.setAttribute('aria-label', options.label);
        if (options.current) {
            button.classList.add('active');
            button.setAttribute('aria-current', 'page');
        }
        button.addEventListener('click', () => {
            currentPage = page;
            render();
            table.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        });
        return button;
    };

    const render = () => {
        const firstIndex = (currentPage - 1) * pageSize;
        const lastIndex = Math.min(firstIndex + pageSize, rows.length);
        rows.forEach((row, index) => { row.hidden = index < firstIndex || index >= lastIndex; });

        summary.textContent = `${firstIndex + 1}–${lastIndex} / ${rows.length} kit`;
        buttons.replaceChildren();
        buttons.append(createButton('‹', Math.max(1, currentPage - 1), {
            disabled: currentPage === 1,
            label: 'Önceki sayfa'
        }));
        for (let page = 1; page <= pageCount; page++) {
            buttons.append(createButton(String(page), page, {
                current: page === currentPage,
                label: `${page}. sayfa`
            }));
        }
        buttons.append(createButton('›', Math.min(pageCount, currentPage + 1), {
            disabled: currentPage === pageCount,
            label: 'Sonraki sayfa'
        }));
        pagination.hidden = pageCount <= 1;
    };

    render();
})();
