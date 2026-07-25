(() => {
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
