(() => {
    if (window.lucide) {
        window.lucide.createIcons({ attrs: { 'aria-hidden': 'true' } });
    }

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
        const editCity = studentEditDialog.querySelector('#edit-student-city');
        const editDistrict = studentEditDialog.querySelector('#edit-student-district');
        const editProduct = studentEditDialog.querySelector('#edit-student-product');
        const openStudentEditor = (trigger) => {
            editId.value = trigger.dataset.studentId || '';
            editName.value = trigger.dataset.studentName || '';
            editPhone.value = trigger.dataset.studentPhone || '';
            editAddress.value = trigger.dataset.studentAddress || '';
            editCity.value = trigger.dataset.studentCityId || '';
            editDistrict.value = trigger.dataset.studentDistrictId || '';
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
