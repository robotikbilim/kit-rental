(() => {
    const toggle = document.querySelector('.menu-toggle');
    const menu = document.querySelector('.topbar-menu');
    const submenuToggles = [...document.querySelectorAll('.submenu-toggle')];

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
