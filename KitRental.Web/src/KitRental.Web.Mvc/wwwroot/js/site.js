(() => {
    if (window.lucide) {
        window.lucide.createIcons({ attrs: { 'aria-hidden': 'true' } });
    }

    const ensureStylesheet = (id, href) => {
        if (document.getElementById(id) || document.querySelector(`link[href="${href}"]`)) return;
        const link = document.createElement('link');
        link.id = id;
        link.rel = 'stylesheet';
        link.href = href;
        document.head.append(link);
    };

    const ensureScript = (id, src, ready, alwaysLoad = false) => {
        if (!alwaysLoad && ready()) return Promise.resolve();
        return new Promise((resolve, reject) => {
            const existing = document.getElementById(id) || document.querySelector(`script[src="${src}"]`);
            if (existing && (alwaysLoad || ready())) {
                resolve();
                return;
            }
            const script = document.createElement('script');
            const completed = () => (alwaysLoad || ready()) ? resolve() : reject(new Error(`${src} yüklendi ancak beklenen API bulunamadı.`));
            script.addEventListener('load', completed, { once: true });
            script.addEventListener('error', () => reject(new Error(`${src} yüklenemedi.`)), { once: true });
            script.id = existing ? `${id}-retry` : id;
            script.src = src;
            document.head.append(script);
        });
    };

    const ensureDataTableAssets = async () => {
        ensureStylesheet('bootstrap-fallback-css', 'https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css');
        ensureStylesheet('datatable-bootstrap-fallback-css', 'https://cdn.datatables.net/2.1.8/css/dataTables.bootstrap5.min.css');
        ensureStylesheet('datatable-responsive-fallback-css', 'https://cdn.datatables.net/responsive/3.0.3/css/responsive.bootstrap5.min.css');
        ensureStylesheet('datatable-buttons-fallback-css', 'https://cdn.datatables.net/buttons/3.1.2/css/buttons.bootstrap5.min.css');
        ensureStylesheet('datatable-select-fallback-css', 'https://cdn.datatables.net/select/2.1.0/css/select.bootstrap5.min.css');

        await ensureScript('jquery-fallback', 'https://code.jquery.com/jquery-3.7.1.min.js', () => Boolean(window.jQuery));
        await ensureScript('datatable-core-fallback', 'https://cdn.datatables.net/2.1.8/js/dataTables.min.js', () => Boolean(window.DataTable));
        await ensureScript('datatable-bootstrap-fallback', 'https://cdn.datatables.net/2.1.8/js/dataTables.bootstrap5.min.js', () => true, true);
        await ensureScript('datatable-responsive-fallback', 'https://cdn.datatables.net/responsive/3.0.3/js/dataTables.responsive.min.js', () => Boolean(window.DataTable?.Responsive));
        await ensureScript('datatable-responsive-bootstrap-fallback', 'https://cdn.datatables.net/responsive/3.0.3/js/responsive.bootstrap5.min.js', () => true, true);
        await ensureScript('datatable-buttons-fallback', 'https://cdn.datatables.net/buttons/3.1.2/js/dataTables.buttons.min.js', () => Boolean(window.DataTable?.Buttons));
        await ensureScript('datatable-buttons-bootstrap-fallback', 'https://cdn.datatables.net/buttons/3.1.2/js/buttons.bootstrap5.min.js', () => true, true);
        await ensureScript('datatable-buttons-colvis-fallback', 'https://cdn.datatables.net/buttons/3.1.2/js/buttons.colVis.min.js', () => Boolean(window.DataTable?.ext?.buttons?.colvis));
        await ensureScript('datatable-select-fallback', 'https://cdn.datatables.net/select/2.1.0/js/dataTables.select.min.js', () => Boolean(window.DataTable?.render?.select));
        await ensureScript('datatable-select-bootstrap-fallback', 'https://cdn.datatables.net/select/2.1.0/js/select.bootstrap5.min.js', () => true, true);
    };

    const setupDataTables = () => {
        const tables = [...document.querySelectorAll('table.js-datatable, main table:not([data-datatable="false"])')];
        if (!tables.length) return;

        if (!window.DataTable) {
            document.body.classList.add('datatable-load-failed');
            console.error('DataTables yüklenemedi. CDN bağlantısını kontrol edin.');
            return;
        }

        const turkish = {
            aria: {
                sortAscending: ': artan sıralamak için etkinleştirin',
                sortDescending: ': azalan sıralamak için etkinleştirin'
            },
            buttons: {
                colvis: 'Sütunlar',
                pageLength: {
                    _: 'Gösterilecek Satır',
                    '-1': 'Gösterilecek Satır'
                }
            },
            emptyTable: 'Gösterilecek kayıt bulunamadı',
            info: '_TOTAL_ kayıttan _START_–_END_ arası gösteriliyor',
            infoEmpty: 'Kayıt yok',
            infoFiltered: '(_MAX_ kayıt içinden filtrelendi)',
            loadingRecords: 'Yükleniyor…',
            processing: 'İşleniyor…',
            search: '',
            searchPlaceholder: 'Tabloda ara…',
            select: {
                aria: {
                    headerCheckbox: 'Bu sayfadaki tüm öğrencileri seç',
                    rowCheckbox: 'Öğrenciyi seç'
                },
                rows: { _: '%d öğrenci seçildi', 0: 'Öğrenci seçilmedi', 1: '1 öğrenci seçildi' }
            },
            zeroRecords: 'Aramanızla eşleşen kayıt bulunamadı',
            paginate: { first: 'İlk', last: 'Son', next: 'Sonraki', previous: 'Önceki' }
        };

        tables.forEach((table, tableIndex) => {
            if (table.dataset.dataTableReady === 'true' || !table.tHead || !table.tBodies.length) return;

            const tableRegion = table.closest('.table-scroll,.table-wrap,.unit-table-wrap,.portal-kit-table-wrap')?.parentElement;
            const serverPaged = table.dataset.datatableServer === 'true' || Boolean(tableRegion?.querySelector(':scope > .pagination-shell'));
            const responsive = table.dataset.datatableResponsive !== 'false';
            const multiSelect = table.dataset.datatableSelect === 'multi' && Boolean(window.DataTable?.render?.select);
            const selectAllPages = multiSelect && !serverPaged && table.dataset.datatableSelectAllPages === 'true';
            const bulkActionIds = (table.dataset.datatableBulkActions || table.dataset.datatableBulkForm || '')
                .split(',')
                .map((id) => id.trim())
                .filter(Boolean);
            const bulkActions = multiSelect ? bulkActionIds
                .map((id, index) => {
                    const form = document.getElementById(id);
                    return form ? {
                        form,
                        index,
                        label: form.dataset.datatableBulkLabel || table.dataset.datatableBulkLabel || 'Seçilenlere Uygula',
                        className: form.dataset.datatableBulkClass || '',
                        selectionAttribute: form.dataset.datatableSelectionAttribute || '',
                        disabledTitle: form.dataset.datatableBulkDisabledTitle || '',
                        windowName: form.dataset.datatableBulkWindow || ''
                    } : null;
                })
                .filter(Boolean) : [];
            const headers = [...table.tHead.rows[0].cells];
            const actionColumns = headers
                .map((header, index) => ({ header, index }))
                .filter(({ header }) => {
                    const text = header.textContent.trim();
                    return /aksiyon|işlem|seç/i.test(text) || !text;
                })
                .map(({ index }) => index);
            const configurableColumns = headers
                .map((header, index) => ({ header, index }))
                .filter(({ index }) => !actionColumns.includes(index))
                .map(({ index }) => index);

            if (!table.id) table.id = `data-table-${tableIndex + 1}`;
            table.classList.add('table', 'table-striped', 'table-hover', 'align-middle', 'w-100');
            table.dataset.dataTableReady = 'true';

            const utilityButtons = window.DataTable.Buttons ? [
                {
                    extend: 'colvis',
                    text: '<span aria-hidden="true">☷</span> Sütunlar',
                    titleAttr: 'Görünecek sütunları seçin',
                    className: 'btn-sm dt-column-visibility',
                    columns: configurableColumns,
                    columnText: (_dataTable, index) => headers[index]?.textContent.trim() || `Sütun ${index + 1}`
                }
            ] : [];
            const selectionButtons = [];
            if (bulkActions.length && window.DataTable.Buttons) {
                selectionButtons.push(...bulkActions.map((bulkAction) => ({
                    text: bulkAction.label,
                    className: `btn-sm dt-bulk-action dt-bulk-action-${bulkAction.index} d-none ${bulkAction.className}`.trim(),
                    enabled: false,
                    action: (_event, dataTable) => {
                        const selectedRows = dataTable.rows({ selected: true }).nodes().toArray();
                        const applicableRows = bulkAction.selectionAttribute
                            ? selectedRows.filter((row) => row.dataset[bulkAction.selectionAttribute] === 'true')
                            : selectedRows;
                        if (bulkAction.selectionAttribute && applicableRows.length !== selectedRows.length) return;
                        const selectedValues = applicableRows.map((row) => row.dataset.selectValue).filter(Boolean);
                        if (!selectedValues.length) return;

                        bulkAction.form.querySelectorAll('[data-datatable-selection]').forEach((input) => input.remove());
                        selectedValues.forEach((value) => {
                            const input = document.createElement('input');
                            input.type = 'hidden';
                            input.name = table.dataset.datatableBulkInput || 'ids';
                            input.value = value;
                            input.dataset.datatableSelection = 'true';
                            bulkAction.form.append(input);
                        });
                        if (bulkAction.windowName) {
                            const printWindow = window.open('', bulkAction.windowName, 'popup,width=1100,height=850');
                            if (!printWindow) return;
                            bulkAction.form.target = bulkAction.windowName;
                        }
                        bulkAction.form.requestSubmit();
                    }
                })));
            }
            if (selectAllPages && window.DataTable.Buttons) {
                selectionButtons.unshift({
                    text: 'Tüm Sayfalardakileri Seç',
                    className: 'btn-sm dt-select-all-pages',
                    action: (_event, dataTable) => {
                        const filteredRows = dataTable.rows({ search: 'applied', page: 'all' });
                        const filteredCount = filteredRows.count();
                        const selectedFilteredCount = dataTable.rows({ search: 'applied', page: 'all', selected: true }).count();
                        if (filteredCount > 0 && selectedFilteredCount === filteredCount)
                            filteredRows.deselect();
                        else
                            filteredRows.select();
                    }
                });
            }
            if (!serverPaged && window.DataTable.Buttons) {
                utilityButtons.unshift({
                    extend: 'pageLength',
                    titleAttr: 'Sayfa başına gösterilecek satır sayısını seçin',
                    className: 'btn-sm dt-page-length'
                });
            }

            const columnDefs = actionColumns.map((targets) => ({ targets, orderable: false, searchable: false }));
            if (multiSelect) {
                columnDefs.unshift({
                    targets: 0,
                    orderable: false,
                    searchable: false,
                    render: window.DataTable.render.select()
                });
            }

            try {
                const dataTable = new DataTable(table, {
                    language: turkish,
                    responsive: responsive ? { details: { type: 'inline', target: 'tr' } } : false,
                    select: multiSelect ? {
                        style: 'multi',
                        selector: 'td:first-child',
                        headerCheckbox: 'select-page'
                    } : false,
                    autoWidth: false,
                    deferRender: true,
                    stateSave: !serverPaged,
                    pageLength: Number(table.dataset.datatablePageLength || 25),
                    lengthMenu: [[10, 25, 50, 100, -1], [10, 25, 50, 100, 'Tümü']],
                    paging: !serverPaged,
                    info: !serverPaged,
                    lengthChange: !serverPaged,
                    order: [],
                    layout: selectionButtons.length ? {
                        top2Start: utilityButtons.length ? { buttons: utilityButtons } : null,
                        top2End: 'search',
                        topStart: { buttons: { name: 'selection', buttons: selectionButtons } },
                        topEnd: null,
                        bottomStart: serverPaged ? null : 'info',
                        bottomEnd: serverPaged ? null : 'paging'
                    } : {
                        topStart: utilityButtons.length ? { buttons: utilityButtons } : null,
                        topEnd: 'search',
                        bottomStart: serverPaged ? null : 'info',
                        bottomEnd: serverPaged ? null : 'paging'
                    },
                    columnDefs
                });
                const tableSearchActions = table.closest('.table-scroll')?.querySelector('[data-kargonomi-search-actions]');
                if (tableSearchActions) {
                    const dataTableContainer = dataTable.table().container();
                    tableSearchActions.classList.add('dt-kargonomi-search-actions');

                    const clearFiltersButton = tableSearchActions.querySelector('[data-kargonomi-clear-button]');
                    if (!clearFiltersButton) return;
                    clearFiltersButton.type = 'button';
                    clearFiltersButton.classList.add('btn', 'btn-sm', 'dt-kargonomi-clear');
                    clearFiltersButton.textContent = 'Filtreyi Temizle';
                    clearFiltersButton.title = 'Bu tablodaki tüm arama ve sütun filtrelerini temizle';
                    if (clearFiltersButton.dataset.kargonomiBound !== 'true') {
                        clearFiltersButton.dataset.kargonomiBound = 'true';
                        clearFiltersButton.addEventListener('click', () => {
                            const globalSearchInput = dataTable.table().container().querySelector('.dt-search input');
                            if (globalSearchInput) globalSearchInput.value = '';

                            dataTable.search('');
                            table.querySelectorAll('thead .column-filter-row [data-column-filter]').forEach((filter) => {
                                if (filter instanceof HTMLSelectElement && filter.multiple) {
                                    [...filter.options].forEach((option) => {
                                        option.selected = option.value === '';
                                    });
                                    filter.dispatchEvent(new Event('change', { bubbles: true }));
                                    return;
                                }

                                filter.value = '';
                                filter.dispatchEvent(new Event('input', { bubbles: true }));
                            });
                            dataTable.columns().search('').draw();
                        });
                    }

                    const moveSearchActions = () => {
                        const searchElement = dataTableContainer.querySelector('.dt-search');
                        const searchCell = searchElement?.closest('.dt-layout-cell')
                            || dataTableContainer.querySelector('.dt-layout-row .dt-layout-cell.dt-layout-end')
                            || dataTableContainer.querySelector('.dt-layout-row .dt-layout-cell:last-child');
                        if (!searchCell) return false;
                        if (tableSearchActions.parentElement !== searchCell) {
                            tableSearchActions.classList.remove('dt-kargonomi-search-actions-fallback');
                            searchCell.append(tableSearchActions);
                        }
                        return true;
                    };

                    if (!moveSearchActions()) {
                        const searchActionsObserver = new MutationObserver(() => {
                            if (moveSearchActions()) searchActionsObserver.disconnect();
                        });
                        searchActionsObserver.observe(dataTableContainer, { childList: true, subtree: true });
                        window.setTimeout(() => searchActionsObserver.disconnect(), 2000);
                    }
                }
                if (multiSelect) {
                    const selectionCount = document.createElement('span');
                    selectionCount.className = 'datatable-selection-count d-none';
                    selectionCount.setAttribute('role', 'status');
                    selectionCount.setAttribute('aria-live', 'polite');
                    const selectionToolbar = dataTable.table().container()
                        .querySelector('.dt-select-all-pages, .dt-bulk-action')
                        ?.closest('.dt-buttons');
                    selectionToolbar?.append(selectionCount);

                    const updateSelectionUi = () => {
                        const selectedRows = dataTable.rows({ selected: true }).nodes().toArray();
                        const selectedCount = selectedRows.length;
                        selectionCount.textContent = `${selectedCount} satır seçildi`;
                        selectionCount.classList.toggle('d-none', selectedCount === 0);

                        if (window.DataTable.Buttons) {
                            bulkActions.forEach((bulkAction) => {
                                const applicableCount = bulkAction.selectionAttribute
                                    ? selectedRows.filter((row) => row.dataset[bulkAction.selectionAttribute] === 'true').length
                                    : selectedCount;
                                const allSelectedRowsAreApplicable = selectedCount > 0 && applicableCount === selectedCount;
                                const button = dataTable.button(`.dt-bulk-action-${bulkAction.index}`);
                                button.enable(allSelectedRowsAreApplicable);
                                const buttonNode = button.node();
                                const buttonElement = buttonNode?.jquery ? buttonNode[0] : buttonNode;
                                buttonElement?.classList?.toggle('d-none', selectedCount === 0);
                                if (buttonElement && bulkAction.disabledTitle) {
                                    const showDisabledTitle = selectedCount > 0 && !allSelectedRowsAreApplicable;
                                    buttonElement.title = showDisabledTitle ? bulkAction.disabledTitle : '';
                                }
                            });

                            if (selectAllPages) {
                                const filteredCount = dataTable.rows({ search: 'applied', page: 'all' }).count();
                                const selectedFilteredCount = dataTable.rows({ search: 'applied', page: 'all', selected: true }).count();
                                const selectAllButton = dataTable.button('.dt-select-all-pages');
                                selectAllButton.enable(filteredCount > 0);
                                selectAllButton.text(filteredCount > 0 && selectedFilteredCount === filteredCount
                                    ? 'Tüm Seçimleri Kaldır'
                                    : 'Tüm Sayfalardakileri Seç');
                            }
                        }
                    };
                    dataTable.on('select deselect draw', updateSelectionUi);
                    updateSelectionUi();
                }
                if (table.dataset.datatableColumnFilters === 'true') {
                    table.querySelectorAll('thead .column-filter-row [data-column-filter]').forEach((filter) => {
                        const columnIndex = filter.closest('th')?.cellIndex;
                        if (columnIndex === undefined) return;

                        const column = dataTable.column(columnIndex);
                        const isMultiSelect = filter instanceof HTMLSelectElement &&
                            filter.multiple &&
                            filter.dataset.columnFilterMulti === 'true';
                        const filterOptions = filter instanceof HTMLSelectElement ? [...filter.options] : [];
                        const multiFilter = isMultiSelect ? filter.closest('[data-multi-filter]') : null;
                        const multiFilterToggle = multiFilter?.querySelector('[data-multi-filter-toggle]');
                        const multiFilterMenu = multiFilter?.querySelector('[data-multi-filter-menu]');
                        const multiFilterOptionInputs = multiFilter
                            ? [...multiFilter.querySelectorAll('[data-multi-filter-option]')]
                            : [];
                        const updateMultiFilterUi = () => {
                            if (!isMultiSelect || !multiFilterToggle) return;
                            const selectedValues = selectedFilterValues();
                            multiFilterOptionInputs.forEach((input) => {
                                input.checked = selectedValues.includes(input.value);
                            });
                            const selectedLabels = multiFilterOptionInputs
                                .filter((input) => input.checked && input.value)
                                .map((input) => input.nextElementSibling?.textContent.trim())
                                .filter(Boolean);
                            multiFilterToggle.textContent = selectedLabels.length === 0
                                ? 'Tümü'
                                : selectedLabels.length === 1 ? selectedLabels[0] : `${selectedLabels.length} seçili`;
                        };
                        const escapeRegex = (value) => value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
                        const selectedFilterValues = () => filter instanceof HTMLSelectElement && filter.multiple
                            ? [...filter.selectedOptions].map((option) => option.value)
                            : filter.value ? [filter.value] : [];
                        const applyFilter = () => {
                            const values = selectedFilterValues().filter(Boolean);
                            if (isMultiSelect) {
                                column.search(values.map(escapeRegex).join('|'), {
                                    regex: true,
                                    smart: false,
                                    caseInsensitive: true
                                }).draw();
                                return;
                            }

                            if (filter.dataset.columnFilterRegex === 'true') {
                                column.search(filter.value, {
                                    regex: true,
                                    smart: false,
                                    caseInsensitive: true
                                }).draw();
                                return;
                            }

                            column.search(filter.value).draw();
                        };
                        const savedSearch = column.search();
                        if (isMultiSelect) {
                            const matchingOptions = filterOptions.filter((option) =>
                                option.value && savedSearch.includes(escapeRegex(option.value)));
                            filterOptions.forEach((option) => {
                                option.selected = matchingOptions.includes(option);
                            });
                            if (!matchingOptions.length) {
                                filterOptions[0].selected = true;
                                if (savedSearch) column.search('').draw();
                            }
                        } else {
                            filter.value = savedSearch;
                            if (filter instanceof HTMLSelectElement && filter.value !== savedSearch) {
                                column.search('', {
                                    regex: true,
                                    smart: false,
                                    caseInsensitive: true
                                }).draw();
                            }
                        }

                        const filterEvent = filter instanceof HTMLSelectElement ? 'change' : 'input';
                        let previousFilterValues = selectedFilterValues();
                        filter.addEventListener(filterEvent, () => {
                            if (isMultiSelect) {
                                const allOption = filterOptions.find((option) => option.value === '');
                                const selectedValues = selectedFilterValues();
                                const allWasSelected = previousFilterValues.includes('');
                                const allIsSelected = allOption?.selected === true;
                                const hasSpecificSelection = selectedValues.some(Boolean);

                                if (allOption && allIsSelected && hasSpecificSelection && !allWasSelected) {
                                    filterOptions.forEach((option) => {
                                        option.selected = option === allOption;
                                    });
                                } else if (allOption && allIsSelected && hasSpecificSelection && allWasSelected) {
                                    allOption.selected = false;
                                } else if (allOption && !allIsSelected && !hasSpecificSelection) {
                                    allOption.selected = true;
                                }

                                const nextFilterValues = selectedFilterValues();
                                updateMultiFilterUi();
                                if (nextFilterValues.join('\u001f') === previousFilterValues.join('\u001f')) return;
                                previousFilterValues = nextFilterValues;
                                applyFilter();
                                return;
                            }

                            if (column.search() === filter.value) return;
                            applyFilter();
                        });

                        if (isMultiSelect && multiFilterToggle && multiFilterMenu) {
                            multiFilterToggle.addEventListener('click', (event) => {
                                event.preventDefault();
                                event.stopPropagation();
                                const isOpen = multiFilterToggle.getAttribute('aria-expanded') === 'true';
                                multiFilterToggle.setAttribute('aria-expanded', String(!isOpen));
                                multiFilterMenu.hidden = isOpen;
                            });
                            multiFilterOptionInputs.forEach((input) => {
                                input.addEventListener('change', () => {
                                    const selectedValues = multiFilterOptionInputs
                                        .filter((option) => option.checked)
                                        .map((option) => option.value);
                                    filterOptions.forEach((option) => {
                                        option.selected = selectedValues.includes(option.value);
                                    });
                                    filter.dispatchEvent(new Event('change', { bubbles: true }));
                                });
                            });
                            multiFilter.addEventListener('click', (event) => event.stopPropagation());
                            document.addEventListener('click', () => {
                                multiFilterToggle.setAttribute('aria-expanded', 'false');
                                multiFilterMenu.hidden = true;
                            });
                            updateMultiFilterUi();
                        }
                    });
                }
            } catch (error) {
                table.dataset.dataTableReady = 'false';
                table.classList.add('datatable-error');
                console.error(`DataTable başlatılamadı: ${table.id}`, error);
            }
        });
    };

    ensureDataTableAssets()
        .then(setupDataTables)
        .catch((error) => {
            document.body.classList.add('datatable-load-failed');
            console.error('DataTables dosyaları yüklenemedi.', error);
        });

    const popupRegion = document.getElementById('popup-notifications');
    const notificationSources = [
        '.success-banner',
        '.error-banner',
        '.alert.success',
        '.alert.error',
        '.validation-summary-errors',
        '.field-validation-error',
        '.login-card .error'
    ].join(',');
    const shownNotificationText = new WeakMap();
    const busySelector = 'button,input[type="submit"],input[type="button"],input[type="reset"],.primary-action,.secondary-action,.table-action,.btn,.row-link,.icon-action,.link-button';
    let pageBusy = false;
    let busyFallbackTimer = null;

    const phoneInvalidMessage = 'Telefon numarası 0xxx xxx xx xx formatında olmalıdır.';
    const phoneInputSelector = [
        'input[data-phone-mask="tr"]',
        'input[data-val-turkishphone]',
        'input[type="tel"][name$="Phone"]',
        'input[type="tel"][id$="Phone"]',
        'input[name$=".Phone"]',
        'input[name$="Phone"]',
        'input[id$="Phone"]'
    ].join(',');

    const normalizeTurkishPhoneDigits = (value) => {
        let digits = String(value || '').replace(/\D/g, '');
        if (digits.startsWith('0090')) digits = digits.slice(4);
        if (digits.startsWith('90')) digits = digits.slice(2);
        if (digits.length === 10) digits = `0${digits}`;
        return digits.slice(0, 11);
    };

    const formatTurkishPhone = (value) => {
        const digits = normalizeTurkishPhoneDigits(value);
        return [
            digits.slice(0, 4),
            digits.slice(4, 7),
            digits.slice(7, 9),
            digits.slice(9, 11)
        ].filter(Boolean).join(' ');
    };

    const validateTurkishPhoneInput = (input) => {
        if (!input.value.trim()) {
            input.setCustomValidity('');
            return true;
        }
        const isValid = /^0\d{10}$/.test(normalizeTurkishPhoneDigits(input.value));
        input.setCustomValidity(isValid ? '' : phoneInvalidMessage);
        return isValid;
    };

    const setupTurkishPhoneInputs = (root = document) => {
        root.querySelectorAll?.(phoneInputSelector).forEach((input) => {
            if (input.dataset.phoneMaskReady === 'true' || input.type === 'hidden') return;
            input.dataset.phoneMaskReady = 'true';
            input.type = 'tel';
            input.inputMode = 'tel';
            input.autocomplete = input.autocomplete || 'tel';
            input.placeholder = input.placeholder || '0xxx xxx xx xx';
            input.maxLength = 14;
            if (input.value) input.value = formatTurkishPhone(input.value);

            input.addEventListener('input', () => {
                input.value = formatTurkishPhone(input.value);
                validateTurkishPhoneInput(input);
            });
            input.addEventListener('blur', () => {
                input.value = formatTurkishPhone(input.value);
                validateTurkishPhoneInput(input);
            });
            input.addEventListener('invalid', () => validateTurkishPhoneInput(input));
        });
    };

    setupTurkishPhoneInputs();

    document.addEventListener('submit', (event) => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement)) return;
        const invalidPhone = Array.from(form.querySelectorAll(phoneInputSelector))
            .filter((input) => input.type !== 'hidden')
            .find((input) => !validateTurkishPhoneInput(input));
        if (!invalidPhone) return;
        event.preventDefault();
        invalidPhone.reportValidity();
    }, true);

    document.querySelectorAll('select[data-all-option]').forEach((select) => {
        const rememberSelection = () => {
            select.dataset.previousSelection = Array.from(select.selectedOptions)
                .map((option) => option.value)
                .join('|');
        };
        rememberSelection();
        select.addEventListener('focus', rememberSelection);
        select.addEventListener('mousedown', rememberSelection);
        select.addEventListener('change', () => {
            const allOption = Array.from(select.options).find((option) => option.value === 'all');
            if (!allOption) return;
            const previousValues = (select.dataset.previousSelection || '').split('|').filter(Boolean);
            const selectedSpecificOptions = Array.from(select.selectedOptions)
                .filter((option) => option.value !== 'all');
            if (allOption.selected && !previousValues.includes('all')) {
                selectedSpecificOptions.forEach((option) => {
                    option.selected = false;
                });
            } else if (allOption.selected && selectedSpecificOptions.length > 0) {
                allOption.selected = false;
            }
            if (select.selectedOptions.length === 0) {
                allOption.selected = true;
            }
            rememberSelection();
        });
    });

    const shouldSkipBusyLink = (link) => {
        if (!link || link.dataset.noBusy === 'true') return true;
        if (link.target && link.target !== '_self') return true;
        if (link.hasAttribute('download')) return true;
        const href = link.getAttribute('href') || '';
        if (!href || href.startsWith('#')) return true;
        if (/^(mailto:|tel:|javascript:)/i.test(href)) return true;
        if (link.hasAttribute('data-dialog-open') || link.hasAttribute('data-dialog-close')) return true;

        const url = new URL(link.href, window.location.href);
        return url.origin !== window.location.origin;
    };

    const isBackendMutationFetch = (input, init) => {
        const request = window.Request && input instanceof Request ? input : null;
        const method = (init?.method || request?.method || 'GET').toUpperCase();
        if (!['POST', 'PUT', 'PATCH', 'DELETE'].includes(method)) return false;
        const urlValue = request?.url || String(input || '');
        if (!urlValue) return false;
        return new URL(urlValue, window.location.href).origin === window.location.origin;
    };

    const showConfirmDialog = async (message) => {
        if (!message) return true;
        if (!window.Swal) {
            console.warn('SweetAlert2 is required for confirmation dialogs.');
            return false;
        }

        const result = await window.Swal.fire({
            title: 'Emin Misiniz?',
            text: message,
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: 'Evet, Onayla',
            cancelButtonText: 'Vazgeç',
            reverseButtons: true,
            focusCancel: true
        });
        return result.isConfirmed;
    };

    const setPageBusy = (trigger, useFallback = false) => {
        if (pageBusy) return false;
        pageBusy = true;
        document.body.classList.add('is-page-busy');
        document.body.setAttribute('aria-busy', 'true');

        document.querySelectorAll(busySelector).forEach((control) => {
            if (control.dataset.busyOriginalDisabled === undefined) {
                control.dataset.busyOriginalDisabled = String(control.disabled === true);
            }
            if (control.tagName === 'A') {
                control.setAttribute('aria-disabled', 'true');
            } else {
                control.disabled = true;
            }
        });

        trigger?.classList.add('is-loading');

        if (useFallback) {
            window.clearTimeout(busyFallbackTimer);
            busyFallbackTimer = window.setTimeout(clearPageBusy, 15000);
        }
        return true;
    };

    function clearPageBusy() {
        pageBusy = false;
        window.clearTimeout(busyFallbackTimer);
        document.body.classList.remove('is-page-busy');
        document.body.removeAttribute('aria-busy');
        document.querySelectorAll(busySelector).forEach((control) => {
            control.classList.remove('is-loading');
            if (control.tagName === 'A') {
                control.removeAttribute('aria-disabled');
                delete control.dataset.busyOriginalDisabled;
                return;
            }
            control.disabled = control.dataset.busyOriginalDisabled === 'true';
            delete control.dataset.busyOriginalDisabled;
        });
    }

    if (window.fetch) {
        const nativeFetch = window.fetch.bind(window);
        window.fetch = async (input, init) => {
            const shouldLock = isBackendMutationFetch(input, init);
            const startedBusy = shouldLock ? setPageBusy(null) : false;
            try {
                return await nativeFetch(input, init);
            } finally {
                if (startedBusy) clearPageBusy();
            }
        };
    }

    const showPopup = (message, type) => {
        if (!popupRegion || !message) return;
        const popup = document.createElement('section');
        popup.className = `popup-notification popup-${type}`;
        popup.setAttribute('role', type === 'error' ? 'alert' : 'status');

        const icon = document.createElement('span');
        icon.className = 'popup-notification-icon';
        icon.innerHTML = type === 'error'
            ? '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 8v5M12 17h.01"/><path d="M10.3 3.7 2.4 17.4A2 2 0 0 0 4.1 20h15.8a2 2 0 0 0 1.7-2.6L13.7 3.7a2 2 0 0 0-3.4 0Z"/></svg>'
            : '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="m5 12 4 4L19 6"/></svg>';

        const copy = document.createElement('div');
        const title = document.createElement('strong');
        title.textContent = type === 'error' ? 'İşlem tamamlanamadı' : 'İşlem başarılı';
        const text = document.createElement('p');
        text.textContent = message;
        copy.append(title, text);

        const close = document.createElement('button');
        close.type = 'button';
        close.className = 'popup-notification-close';
        close.setAttribute('aria-label', 'Bildirimi kapat');
        close.innerHTML = '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="m6 6 12 12M18 6 6 18"/></svg>';

        const dismiss = () => {
            popup.classList.add('is-leaving');
            window.setTimeout(() => popup.remove(), 180);
        };
        close.addEventListener('click', dismiss);
        popup.append(icon, copy, close);
        popupRegion.append(popup);
        window.setTimeout(dismiss, type === 'error' ? 9000 : 6000);
    };

    const collectNotifications = (root = document) => {
        root.querySelectorAll?.(notificationSources).forEach((source) => {
            if (source.closest('#popup-notifications')) return;
            source.classList.add('notification-source');
            const summary = source.closest('form')?.querySelector('.validation-summary-errors');
            if (source.classList.contains('field-validation-error') && summary?.textContent.trim()) return;
            const listItems = [...source.querySelectorAll('li')].map((item) => item.textContent.trim()).filter(Boolean);
            const message = (listItems.length ? listItems.join(' • ') : source.textContent)
                .replace(/\s+/g, ' ').trim();
            if (!message || shownNotificationText.get(source) === message) return;
            shownNotificationText.set(source, message);
            const isSuccess = source.classList.contains('success-banner') ||
                source.classList.contains('success');
            showPopup(message, isSuccess ? 'success' : 'error');
        });
    };

    collectNotifications();
    window.addEventListener('pageshow', clearPageBusy);

    document.addEventListener('click', (event) => {
        const trigger = event.target.closest?.('[data-inline-error]');
        if (!trigger) return;
        event.preventDefault();
        event.stopPropagation();
        showPopup(trigger.dataset.inlineError, 'error');
    });

    document.addEventListener('click', async (event) => {
        const trigger = event.target.closest?.('[data-kargonomi-barcode-url]');
        if (!trigger) return;
        event.preventDefault();
        event.stopPropagation();
        if (trigger.disabled || trigger.classList.contains('is-loading')) return;

        const printWindow = window.open('', '_blank', 'popup,width=1000,height=800');
        if (!printWindow) {
            showPopup('Yazdırma penceresi açılamadı. Tarayıcı açılır pencere iznini kontrol edin.', 'error');
            return;
        }

        trigger.disabled = true;
        trigger.classList.add('is-loading');
        try {
            const response = await fetch(trigger.dataset.kargonomiBarcodeUrl, {
                headers: { Accept: 'application/pdf, application/json' }
            });
            if (!response.ok) {
                const problem = await response.json().catch(() => null);
                throw new Error(problem?.message || 'Henüz kargo etiketi oluşmamış, lütfen tekrar deneyin.');
            }

            const pdf = await response.blob();
            if (!pdf.size) throw new Error('Henüz kargo etiketi oluşmamış, lütfen tekrar deneyin.');
            printWindow.location.href = URL.createObjectURL(pdf);
            printWindow.focus();
        } catch (error) {
            printWindow.close();
            showPopup(error instanceof Error ? error.message : 'Kargo etiketi alınamadı.', 'error');
        } finally {
            trigger.disabled = false;
            trigger.classList.remove('is-loading');
        }
    });

    document.querySelectorAll('form[data-auto-filter="true"]').forEach((form) => {
        let filterTimer = null;
        const submitFilter = (delay = 0) => {
            window.clearTimeout(filterTimer);
            filterTimer = window.setTimeout(() => {
                if (pageBusy || !form.checkValidity()) return;
                const pageInput = form.querySelector('[name="page"]');
                if (pageInput) pageInput.value = '1';
                if (typeof form.requestSubmit === 'function') {
                    form.requestSubmit();
                } else {
                    form.submit();
                }
            }, delay);
        };

        form.querySelectorAll('select,input,textarea').forEach((control) => {
            const eventName = control.matches('input[type="search"],input[type="text"],textarea') ? 'input' : 'change';
            control.addEventListener(eventName, () => {
                submitFilter(eventName === 'input' ? 450 : 0);
            });
        });
    });

    document.addEventListener('click', (event) => {
        if (!pageBusy) return;
        const busyTarget = event.target.closest?.(busySelector);
        if (!busyTarget) return;
        event.preventDefault();
        event.stopPropagation();
    }, true);

    document.addEventListener('submit', (event) => {
        if (pageBusy) {
            event.preventDefault();
            return;
        }

        const form = event.target;
        if (!(form instanceof HTMLFormElement) || form.dataset.noBusy === 'true') return;
        if (event.defaultPrevented || !form.checkValidity()) return;

        const submitter = event.submitter || form.querySelector('[type="submit"],button:not([type])');
        const confirmMessage = submitter?.dataset.confirm || form.dataset.confirm;
        if (confirmMessage && form.dataset.confirmed !== 'true') {
            event.preventDefault();
            showConfirmDialog(confirmMessage).then((confirmed) => {
                if (!confirmed) return;
                form.dataset.confirmed = 'true';
                if (submitter && typeof form.requestSubmit === 'function') {
                    form.requestSubmit(submitter);
                } else {
                    form.submit();
                }
                window.setTimeout(() => delete form.dataset.confirmed, 0);
            });
            return;
        }

        window.setTimeout(() => {
            if (!event.defaultPrevented) setPageBusy(submitter);
        }, 0);
    }, true);

    document.addEventListener('click', (event) => {
        if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
        const link = event.target.closest?.('a[href]');
        if (!link || shouldSkipBusyLink(link)) return;
        setPageBusy(link, true);
    }, true);

    new MutationObserver((mutations) => {
        mutations.forEach((mutation) => {
            const target = mutation.target.nodeType === 1 ? mutation.target : mutation.target.parentElement;
            if (target) collectNotifications(target.closest('form') || target);
        });
    }).observe(document.body, { childList: true, subtree: true, characterData: true });

    document.querySelectorAll(':where(.table-scroll,.table-wrap,.unit-table-wrap,.portal-kit-table-wrap) table')
        .forEach((table) => {
            const headers = [...table.querySelectorAll('thead th')].map((header, index, all) => {
                const text = header.textContent.trim();
                if (text) return text;
                if (header.querySelector('input[type="checkbox"]')) return 'Seçim';
                return index === all.length - 1 ? 'İşlemler' : '';
            });

            table.querySelectorAll('tbody tr').forEach((row) => {
                [...row.children].forEach((cell, index) => {
                    if (cell.tagName !== 'TD' || cell.hasAttribute('data-mobile-label')) return;
                    cell.dataset.mobileLabel = headers[index] || `Bilgi ${index + 1}`;
                });
            });
        });

    const openDialog = (dialog) => {
        if (!dialog) return;
        if (typeof dialog.showModal === 'function') {
            dialog.showModal();
        } else {
            dialog.setAttribute('open', 'open');
        }
        window.lucide?.createIcons({ attrs: { 'aria-hidden': 'true' } });
        dialog.querySelector(':where(input,select,textarea)')?.focus();
    };

    const closeDialog = (dialog) => {
        if (!dialog) return;
        if (typeof dialog.close === 'function') {
            dialog.close();
        } else {
            dialog.removeAttribute('open');
        }
    };

    document.querySelectorAll('[data-dialog-open]').forEach((trigger) => {
        trigger.addEventListener('click', () => openDialog(document.getElementById(trigger.dataset.dialogOpen)));
    });

    document.querySelectorAll('dialog[data-auto-open="true"]:not(#student-edit-dialog)').forEach((dialog) => openDialog(dialog));

    document.querySelectorAll('dialog').forEach((dialog) => {
        dialog.querySelectorAll('[data-dialog-close]').forEach((trigger) => {
            trigger.addEventListener('click', () => closeDialog(dialog));
        });
        dialog.addEventListener('click', (event) => {
            if (event.target === dialog) closeDialog(dialog);
        });
    });

    const ensureTextPreviewDialog = () => {
        let dialog = document.getElementById('text-preview-dialog');
        if (dialog) return dialog;

        dialog = document.createElement('dialog');
        dialog.id = 'text-preview-dialog';
        dialog.className = 'text-preview-dialog';
        dialog.innerHTML = [
            '<section class="text-preview-modal">',
            '<header>',
            '<div><span class="eyebrow">DETAY</span><h2 id="text-preview-title">Detay</h2></div>',
            '<button class="icon-action icon-action-neutral" type="button" data-text-preview-close aria-label="Pencereyi kapat" title="Kapat"><i data-lucide="x"></i></button>',
            '</header>',
            '<pre id="text-preview-body"></pre>',
            '<footer>',
            '<button class="secondary-action" type="button" data-text-preview-copy>Metni Kopyala</button>',
            '<button class="primary-action" type="button" data-text-preview-close>Kapat</button>',
            '</footer>',
            '</section>'
        ].join('');
        document.body.append(dialog);
        dialog.addEventListener('click', (event) => {
            if (event.target === dialog) closeDialog(dialog);
        });
        dialog.querySelectorAll('[data-text-preview-close]').forEach((trigger) => {
            trigger.addEventListener('click', () => closeDialog(dialog));
        });
        dialog.querySelector('[data-text-preview-copy]')?.addEventListener('click', async () => {
            const text = dialog.dataset.previewText || '';
            if (!text) return;
            await navigator.clipboard?.writeText(text);
        });
        return dialog;
    };

    document.addEventListener('click', (event) => {
        const button = event.target.closest?.('[data-fulltext-modal]');
        if (!button) return;
        const dialog = ensureTextPreviewDialog();
        const title = dialog.querySelector('#text-preview-title');
        const body = dialog.querySelector('#text-preview-body');
        const text = button.dataset.text || '';
        dialog.dataset.previewText = text;
        if (title) title.textContent = button.dataset.title || 'Detay';
        if (body) body.textContent = text || '-';
        openDialog(dialog);
    });

    const rentalPeriodDialog = document.getElementById('rental-period-dialog');
    if (rentalPeriodDialog) {
        const form = rentalPeriodDialog.querySelector('form');
        const idInput = form?.querySelector('[name="Id"]');
        const nameInput = form?.querySelector('[name="Name"]');
        const startInput = form?.querySelector('[name="StartDate"]');
        const endInput = form?.querySelector('[name="EndDate"]');
        const eyebrow = rentalPeriodDialog.querySelector('#rental-period-dialog-eyebrow');
        const title = rentalPeriodDialog.querySelector('#rental-period-dialog-title');
        const defaultStartDate = startInput?.value || '';
        const defaultEndDate = endInput?.value || '';
        const setRentalPeriodMode = (trigger) => {
            const isEdit = trigger.hasAttribute('data-rental-period-edit');
            if (idInput) idInput.value = isEdit ? trigger.dataset.rentalPeriodId || '' : '';
            if (nameInput) nameInput.value = isEdit ? trigger.dataset.rentalPeriodName || '' : '';
            if (startInput) startInput.value = isEdit ? trigger.dataset.rentalPeriodStart || '' : defaultStartDate;
            if (endInput) endInput.value = isEdit ? trigger.dataset.rentalPeriodEnd || '' : defaultEndDate;
            if (eyebrow) eyebrow.textContent = isEdit ? 'SİPARİŞİ DÜZENLE' : 'YENİ SİPARİŞ';
            if (title) title.textContent = isEdit ? 'Sipariş dönemini düzenle' : 'Sipariş dönemi oluştur';
        };
        document.querySelectorAll('[data-rental-period-create],[data-rental-period-edit]').forEach((trigger) => {
            trigger.addEventListener('click', () => setRentalPeriodMode(trigger));
        });
    }

    const studentEditDialog = document.getElementById('student-edit-dialog');
    if (studentEditDialog) {
        const editId = studentEditDialog.querySelector('#edit-student-id');
        const editName = studentEditDialog.querySelector('#edit-student-name');
        const editPhone = studentEditDialog.querySelector('#edit-student-phone');
        const editAddress = studentEditDialog.querySelector('#edit-student-address');
        const editProduct = studentEditDialog.querySelector('#edit-student-product');
        const openStudentEditor = (trigger) => {
            editId.value = trigger.dataset.studentId || '';
            editName.value = trigger.dataset.studentName || '';
            editPhone.value = formatTurkishPhone(trigger.dataset.studentPhone || '');
            validateTurkishPhoneInput(editPhone);
            editAddress.value = trigger.dataset.studentAddress || '';
            editProduct.value = trigger.dataset.studentProduct || '';
            if (typeof studentEditDialog.showModal === 'function') {
                studentEditDialog.showModal();
            } else {
                studentEditDialog.setAttribute('open', 'open');
            }
            window.lucide?.createIcons({ attrs: { 'aria-hidden': 'true' } });
            editName.focus();
        };

        document.querySelectorAll('[data-student-edit]').forEach((trigger) => {
            trigger.addEventListener('click', () => openStudentEditor(trigger));
        });

        studentEditDialog.querySelectorAll('[data-student-edit-close]').forEach((trigger) => {
            trigger.addEventListener('click', () => studentEditDialog.close());
        });

        studentEditDialog.addEventListener('click', (event) => {
            if (event.target === studentEditDialog) studentEditDialog.close();
        });

        if (studentEditDialog.dataset.autoOpen === 'true') {
            if (typeof studentEditDialog.showModal === 'function') {
                studentEditDialog.showModal();
            } else {
                studentEditDialog.setAttribute('open', 'open');
            }
            editName.focus();
        }
    }

    const toggle = document.querySelector('.menu-toggle');
    const menu = document.querySelector('.topbar-menu');
    const submenuToggles = [...document.querySelectorAll('.submenu-toggle')];
    const sidebarToggle = document.querySelector('.sidebar-toggle');

    if (sidebarToggle) {
        const setSidebarCollapsed = (collapsed) => {
            document.body.classList.toggle('sidebar-collapsed', collapsed);
            sidebarToggle.setAttribute('aria-expanded', String(!collapsed));
            sidebarToggle.setAttribute('aria-label', collapsed ? 'Sol menüyü aç' : 'Sol menüyü daralt');
        };

        setSidebarCollapsed(window.localStorage.getItem('kit-rental-sidebar-collapsed') === 'true');
        sidebarToggle.addEventListener('click', () => {
            const collapsed = !document.body.classList.contains('sidebar-collapsed');
            setSidebarCollapsed(collapsed);
            window.localStorage.setItem('kit-rental-sidebar-collapsed', String(collapsed));
        });
    }

    if (!toggle || !menu) return;

    const closeSubmenus = (except = null) => {
        submenuToggles.forEach((submenuToggle) => {
            if (submenuToggle === except) return;
            submenuToggle.setAttribute('aria-expanded', 'false');
            submenuToggle.closest('.nav-group')?.classList.remove('is-open');
        });
    };

    const setOpen = (open) => {
        toggle.setAttribute('aria-expanded', String(open));
        toggle.setAttribute('aria-label', open ? 'Menüyü kapat' : 'Menüyü aç');
        menu.classList.toggle('is-open', open);
        document.body.classList.toggle('menu-open', open);
        if (!open) closeSubmenus();
    };

    submenuToggles.forEach((submenuToggle) => {
        submenuToggle.addEventListener('click', () => {
            const willOpen = submenuToggle.getAttribute('aria-expanded') !== 'true';
            closeSubmenus(submenuToggle);
            submenuToggle.setAttribute('aria-expanded', String(willOpen));
            submenuToggle.closest('.nav-group')?.classList.toggle('is-open', willOpen);
        });
    });

    toggle.addEventListener('click', () => {
        setOpen(toggle.getAttribute('aria-expanded') !== 'true');
    });

    menu.addEventListener('click', (event) => {
        if (event.target.closest('a')) setOpen(false);
    });

    document.addEventListener('click', (event) => {
        if (!menu.contains(event.target) && !toggle.contains(event.target)) {
            setOpen(false);
        }
    });

    document.addEventListener('keydown', (event) => {
        if (event.key !== 'Escape') return;

        const openSubmenu = submenuToggles.find((submenuToggle) => submenuToggle.getAttribute('aria-expanded') === 'true');
        if (openSubmenu) {
            closeSubmenus();
            openSubmenu.focus();
            return;
        }

        if (toggle.getAttribute('aria-expanded') === 'true') {
            setOpen(false);
            toggle.focus();
        }
    });

    window.addEventListener('resize', () => {
        if (window.innerWidth > 960) setOpen(false);
    });
})();
