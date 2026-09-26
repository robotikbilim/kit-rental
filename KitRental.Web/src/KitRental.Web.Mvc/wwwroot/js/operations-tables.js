// Opt-in enhancements for the three admin operation tables. Keep the original
// row nodes: selection attributes, forms and their event handlers stay intact.
(() => {
    const escapeRegex = (value) => value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const make = (tag, className, text) => {
        const element = document.createElement(tag);
        if (className) element.className = className;
        if (text) element.textContent = text;
        return element;
    };

    window.KitRentalOperationsTables = {
        prepare(table) {
            const headers = [...table.tHead.rows[0].cells].map((cell) => cell.textContent.trim());
            const fields = [...table.querySelectorAll('[data-column-filter]')].map((input) => ({
                input, index: input.closest('th').cellIndex
            }));
            // Capture indices before DataTables can remove hidden columns.
            [...table.tBodies[0].rows].forEach((row) => {
                [...row.cells].forEach((cell, index) => { cell.dataset.mobileLabel = headers[index]; });
            });
            return { headers, fields };
        },

        setup(api, table, { headers, fields }) {
            const container = api.table().container();
            container.classList.add('ops-datatable');
            const serverPaged = table.dataset.datatableServer === 'true';
            if (serverPaged) return; // The fault form searches the entire result set on the server.

            let timer;
            const schedule = (action, immediate = false) => {
                window.clearTimeout(timer);
                if (immediate) action();
                else timer = window.setTimeout(action, 250);
            };
            const tools = make('div', 'ops-table-tools');
            const searchLabel = make('label', 'ops-table-search', 'Kayıtlarda Ara');
            const search = make('input');
            search.type = 'search';
            search.placeholder = 'Öğrenci, telefon, kit veya takip numarası…';
            search.autocomplete = 'off';
            search.setAttribute('aria-controls', table.id);
            searchLabel.append(search);
            tools.append(searchLabel);

            const clear = make('button', 'secondary-action', 'Filtreleri Temizle');
            clear.type = 'button';
            tools.append(clear);
            container.prepend(tools);

            const panel = make('details', 'ops-column-filters');
            const summary = make('summary', '', 'Sütun Filtreleri');
            const grid = make('div', 'ops-column-filter-grid');
            panel.append(summary, grid);
            tools.after(panel);
            const refreshers = [];

            const applyFilters = () => {
                api.search(search.value);
                fields.forEach(({ input, index }) => {
                    const column = api.column(index);
                    if (input.multiple) {
                        const values = [...input.selectedOptions].map((option) => option.value).filter(Boolean);
                        column.search(values.length ? `^(?:${values.map(escapeRegex).join('|')})$` : '', {
                            regex: true, smart: false, caseInsensitive: true
                        });
                    } else {
                        column.search(input.value, {
                            regex: input.dataset.columnFilterRegex === 'true',
                            smart: false, caseInsensitive: true
                        });
                    }
                });
                refreshers.forEach((refresh) => refresh());
                // Apply all fields together and draw once, including a full reset.
                api.draw();
            };
            const bindSearch = (input) => {
                input.addEventListener('input', (event) => {
                    if (!event.isComposing) schedule(applyFilters);
                });
                input.addEventListener('compositionend', () => schedule(applyFilters));
                input.addEventListener('keydown', (event) => {
                    if (event.key === 'Enter' && !event.isComposing) {
                        event.preventDefault();
                        schedule(applyFilters, true);
                    }
                });
            };
            bindSearch(search);

            fields.forEach(({ input, index }) => {
                const field = make('div', 'ops-column-field');
                const label = make('label', '', headers[index]);
                const multi = input.closest('[data-multi-filter]');
                const control = multi || input;
                const toggle = multi?.querySelector('[data-multi-filter-toggle]');
                const labelledInput = toggle || input;
                labelledInput.id = `${table.id}-filter-${index}`;
                label.htmlFor = labelledInput.id;
                field.append(label, control);
                grid.append(field);

                if (!multi) {
                    if (input.tagName === 'SELECT') input.addEventListener('change', () => schedule(applyFilters, true));
                    else bindSearch(input);
                    return;
                }

                const menu = multi.querySelector('[data-multi-filter-menu]');
                const checkboxes = [...menu.querySelectorAll('[data-multi-filter-option]')];
                menu.id = `${labelledInput.id}-options`;
                toggle.setAttribute('aria-controls', menu.id);
                toggle.removeAttribute('aria-haspopup'); // A group of native checkboxes, not a listbox.
                const close = () => { menu.hidden = true; toggle.setAttribute('aria-expanded', 'false'); };
                const refresh = () => {
                    const selected = [...input.selectedOptions].map((option) => option.value).filter(Boolean);
                    checkboxes.forEach((checkbox) => {
                        checkbox.checked = checkbox.value ? selected.includes(checkbox.value) : selected.length === 0;
                    });
                    toggle.textContent = selected.length > 1 ? `${selected.length} Durum Seçili` : selected[0] || 'Tümü';
                };
                refreshers.push(refresh);
                refresh();
                toggle.addEventListener('click', () => {
                    const opening = menu.hidden;
                    menu.hidden = !opening;
                    toggle.setAttribute('aria-expanded', String(opening));
                    if (opening) checkboxes[0]?.focus();
                });
                checkboxes.forEach((checkbox) => checkbox.addEventListener('change', () => {
                    if (!checkbox.value) checkboxes.forEach((item) => { item.checked = false; });
                    const values = checkboxes.filter((item) => item.checked && item.value).map((item) => item.value);
                    [...input.options].forEach((option) => { option.selected = values.includes(option.value); });
                    schedule(applyFilters, true);
                }));
                multi.addEventListener('keydown', (event) => {
                    if (event.key === 'Escape') { close(); toggle.focus(); event.stopPropagation(); }
                });
                multi.addEventListener('focusout', (event) => {
                    // A label click can briefly clear focus before its checkbox is
                    // activated. Outside clicks are handled below; only close here
                    // when focus actually moves to another control (e.g. Tab).
                    if (event.relatedTarget && !multi.contains(event.relatedTarget)) close();
                });
                document.addEventListener('click', (event) => { if (!multi.contains(event.target)) close(); });
            });
            table.tHead.querySelector('.column-filter-row')?.remove();

            const sortLabel = make('label', 'ops-mobile-sort', 'Sıralama');
            const sort = make('select');
            sort.setAttribute('aria-controls', table.id);
            sort.add(new Option('Varsayılan Sıra', ''));
            headers.forEach((header, index) => {
                if (!header || /^(Seç|İşlem|Aksiyon)$/i.test(header)) return;
                sort.add(new Option(`${header} · Artan`, `${index}:asc`));
                sort.add(new Option(`${header} · Azalan`, `${index}:desc`));
            });
            sortLabel.append(sort);
            tools.append(sortLabel);
            sort.addEventListener('change', () => {
                const [index, direction] = sort.value.split(':');
                api.order(sort.value ? [[Number(index), direction]] : []).draw();
            });
            api.on('order', () => {
                const order = api.order()[0];
                sort.value = order ? `${order[0]}:${order[1]}` : '';
            });

            clear.addEventListener('click', () => {
                search.value = '';
                fields.forEach(({ input }) => {
                    if (input.multiple) [...input.options].forEach((option) => { option.selected = false; });
                    else input.value = '';
                });
                schedule(applyFilters, true);
            });
            const updateFilterSummary = () => {
                const count = fields.filter(({ input }) => input.multiple
                    ? [...input.selectedOptions].some((option) => option.value) : Boolean(input.value)).length;
                summary.textContent = count ? `Sütun Filtreleri · ${count} Aktif` : 'Sütun Filtreleri';
                clear.disabled = !count && !search.value;
            };
            api.on('draw', updateFilterSummary);
            updateFilterSummary();

            if (table.dataset.datatableSelect === 'multi') {
                const deselect = make('button', 'secondary-action ops-clear-selection', 'Seçimleri Kaldır');
                deselect.type = 'button';
                deselect.hidden = true;
                deselect.addEventListener('click', () => api.rows({ selected: true }).deselect());
                container.querySelector('.datatable-selection-count')?.after(deselect);
                api.on('select deselect draw', () => { deselect.hidden = api.rows({ selected: true }).count() === 0; });
            }
        }
    };
})();
