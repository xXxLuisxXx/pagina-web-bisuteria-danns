/* ============================================================
   Bisuterias Dann's — Interacciones del sitio público
   ============================================================ */

(function () {
    'use strict';

    document.querySelectorAll('.main-navbar .nav-link').forEach(function (link) {
        var current = location.pathname.toLowerCase().replace(/\/$/, '') || '/';
        var target = new URL(link.href).pathname.toLowerCase().replace(/\/$/, '') || '/';
        if (current === target || (target === '/catalog' && current.startsWith('/catalog/'))) {
            link.classList.add('active'); link.setAttribute('aria-current', 'page');
        }
    });
    var menu = document.getElementById('adminMenuToggle');
    var sidebar = document.getElementById('adminSidebar');
    function setMenu(open) {
        if (!menu || !sidebar) return;
        sidebar.classList.toggle('open', open);
        menu.setAttribute('aria-expanded', String(open));
        sidebar.inert = !open && window.innerWidth < 992;
    }
    if (menu) {
        setMenu(false);
        menu.addEventListener('click', function () { setMenu(menu.getAttribute('aria-expanded') !== 'true'); });
        document.addEventListener('keydown', function (event) { if (event.key === 'Escape') { setMenu(false); menu.focus(); } });
        window.addEventListener('resize', function () { setMenu(false); });
    }
    document.addEventListener('click', function (event) {
        var button = event.target.closest('.btn-qty[data-dir]');
        if (!button) return;
        var input = button.closest('.qty-stepper').querySelector('input');
        input.value = Math.max(Number(input.min) || 1, Math.min(Number(input.max) || 50, (Number(input.value) || 1) + Number(button.dataset.dir)));
        input.dispatchEvent(new Event('change', { bubbles: true }));
    });
    // ---------- Loader ----------
    window.addEventListener('load', function () {
        var loader = document.getElementById('appLoader');
        if (loader) {
            setTimeout(function () {
                loader.classList.add('hidden');
            }, 350);
        }
    });

    // Fallback: ocultar loader tras 3s por si el evento load no dispara.
    setTimeout(function () {
        var loader = document.getElementById('appLoader');
        if (loader) {
            loader.classList.add('hidden');
        }
    }, 3000);

    // ---------- Tema oscuro / claro ----------
    function applyTheme(theme) {
        document.documentElement.setAttribute('data-bs-theme', theme);
        var icon = document.querySelector('#themeToggle i');
        if (icon) {
            icon.className = theme === 'dark' ? 'bi bi-sun' : 'bi bi-moon-stars';
        }
    }

    (function initTheme() {
        var saved = localStorage.getItem('bisuteria-theme');
        if (saved === 'dark') {
            applyTheme('dark');
        } else if (saved === 'light') {
            applyTheme('light');
        } else if (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches) {
            applyTheme('dark');
        }
    })();

    document.addEventListener('click', function (e) {
        var toggle = e.target.closest('#themeToggle');
        if (!toggle) return;
        var current = document.documentElement.getAttribute('data-bs-theme') || 'light';
        var next = current === 'dark' ? 'light' : 'dark';
        localStorage.setItem('bisuteria-theme', next);
        applyTheme(next);
    });

    // ---------- Tooltips (botón de WhatsApp, etc.) ----------
    function initTooltips(root) {
        var scope = root || document;
        scope.querySelectorAll('[data-bs-toggle="tooltip"]').forEach(function (el) {
            if (!bootstrap.Tooltip.getInstance(el)) {
                new bootstrap.Tooltip(el);
            }
        });
    }

    initTooltips(document);
    document.addEventListener('shown.bs.modal', function (e) { initTooltips(e.target); });

    // ---------- Navbar scroll ----------
    var navbar = document.querySelector('.main-navbar');
    window.addEventListener('scroll', function () {
        if (!navbar) return;
        if (window.scrollY > 40) {
            navbar.classList.add('scrolled');
        } else {
            navbar.classList.remove('scrolled');
        }
    }, { passive: true });

    // ---------- Volver arriba ----------
    var backToTop = document.getElementById('backToTop');
    window.addEventListener('scroll', function () {
        if (!backToTop) return;
        if (window.scrollY > 500) {
            backToTop.classList.add('visible');
        } else {
            backToTop.classList.remove('visible');
        }
    }, { passive: true });

    if (backToTop) {
        backToTop.addEventListener('click', function () {
            window.scrollTo({ top: 0, behavior: 'smooth' });
        });
    }

    // ---------- Animación al hacer scroll ----------
    var observer = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry) {
            if (entry.isIntersecting) {
                entry.target.classList.add('visible');
                observer.unobserve(entry.target);
            }
        });
    }, { threshold: 0.12 });

    document.querySelectorAll('.animate-on-scroll').forEach(function (el) {
        observer.observe(el);
    });

    // ---------- Toasts ----------
    window.bisuteriaToast = function (message, type) {
        var container = document.getElementById('toastContainer');
        if (!container) return;
        type = type || 'info';
        var icons = {
            success: 'bi-check-circle-fill',
            error: 'bi-x-circle-fill',
            warning: 'bi-exclamation-triangle-fill',
            info: 'bi-info-circle-fill'
        };
        var el = document.createElement('div');
        el.className = 'toast align-items-center text-bg-' + (type === 'success' ? 'success' : type === 'error' ? 'danger' : type === 'warning' ? 'warning' : 'dark') + ' border-0 shadow';
        el.setAttribute('role', 'alert');
        el.innerHTML =
            '<div class="d-flex">' +
            '<div class="toast-body"><i class="bi ' + (icons[type] || icons.info) + ' me-2"></i>' + '<span class="toast-message"></span></div>' +
            '<button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Cerrar"></button>' +
            '</div>';
        el.querySelector('.toast-message').textContent = message;
        container.appendChild(el);
        var toast = new bootstrap.Toast(el, { delay: 3500 });
        toast.show();
        el.addEventListener('hidden.bs.toast', function () { el.remove(); });
    };

    // ---------- Galería de thumbs en detalle ----------
    document.addEventListener('click', function (e) {
        var thumb = e.target.closest('.product-thumb');
        if (!thumb) return;
        var url = thumb.getAttribute('data-image');
        var main = document.querySelector('.product-detail-img img');
        if (url && main) {
            main.src = url;
            document.querySelectorAll('.product-thumb').forEach(function (t) {
                t.classList.toggle('active', t === thumb);
            });
        }
    });

    // ---------- Favoritos (AJAX) ----------
    document.addEventListener('click', function (e) {
        var btn = e.target.closest('.fav-btn[data-product-id]');
        if (!btn) return;
        e.preventDefault();
        var id = btn.getAttribute('data-product-id');
        var token = document.querySelector('input[name="__RequestVerificationToken"]') || btn.getAttribute('data-antiforgery');

        fetch('/Catalog/ToggleFavorite', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token ? (typeof token === 'string' ? token : token.value) : ''
            },
            body: JSON.stringify({ productId: parseInt(id, 10) })
        })
            .then(function (res) { return res.json(); })
            .then(function (data) {
                if (data.success) {
                    btn.classList.toggle('active', data.isFavorite);
                    window.bisuteriaToast(data.isFavorite ? 'Agregado a tus favoritos ♥' : 'Eliminado de favoritos', 'success');
                } else {
                    if (data.requiresLogin) {
                        window.location.href = '/Account/Login?returnUrl=' + encodeURIComponent(window.location.pathname);
                    } else {
                        window.bisuteriaToast(data.message || 'No se pudo actualizar', 'error');
                    }
                }
            })
            .catch(function () {
                window.bisuteriaToast('Error de conexión', 'error');
            });
    });

    // ---------- Contador de carrito ----------
    window.bisuteriaUpdateCartCount = function () {
        fetch('/Cart/Count')
            .then(function (res) { return res.json(); })
            .then(function (data) {
                var badge = document.getElementById('cartCount');
                if (badge) badge.textContent = data.count;
            })
            .catch(function () { });
    };

    // ---------- Peticiones con antiforgery (utilidad) ----------
    window.bisuteriaFetch = function (url, options) {
        var token = document.querySelector('input[name="__RequestVerificationToken"]');
        options = options || {};
        options.headers = options.headers || {};
        if (token) {
            options.headers['RequestVerificationToken'] = token.value;
        }
        return fetch(url, options);
    };
})();
// Eventos fuera del marcado para mantener una CSP sin unsafe-inline.
document.addEventListener('submit', function (event) {
    const message = event.target.dataset.confirm;
    if (message && !window.confirm(message)) event.preventDefault();
});
document.addEventListener('error', function (event) {
    if (event.target instanceof HTMLImageElement && event.target.dataset.imageFallback) {
        delete event.target.dataset.imageFallback;
        event.target.src = '/img/placeholder.svg';
    }
}, true);
