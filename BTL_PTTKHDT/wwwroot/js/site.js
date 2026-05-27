// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

(() => {
    const forms = document.querySelectorAll('form[data-auto-filter]');

    forms.forEach((form) => {
        const delay = Number.parseInt(form.dataset.autoFilterDelay || '350', 10);
        let timerId;
        let lastQuery = new URLSearchParams(new FormData(form)).toString();

        const submitFilter = () => {
            const currentQuery = new URLSearchParams(new FormData(form)).toString();
            if (currentQuery === lastQuery) {
                return;
            }

            lastQuery = currentQuery;

            if (typeof form.requestSubmit === 'function') {
                form.requestSubmit();
            } else {
                form.submit();
            }
        };

        form.querySelectorAll('input[type="search"], input[type="text"], input:not([type])')
            .forEach((input) => {
                input.addEventListener('input', () => {
                    window.clearTimeout(timerId);
                    timerId = window.setTimeout(submitFilter, delay);
                });
            });

        form.querySelectorAll('select')
            .forEach((select) => {
                select.addEventListener('change', () => {
                    window.clearTimeout(timerId);
                    submitFilter();
                });
            });
    });
})();

(() => {
    if (!window.bootstrap?.Toast) {
        return;
    }

    document.querySelectorAll('.app-toast')
        .forEach((toastElement) => {
            const toast = bootstrap.Toast.getOrCreateInstance(toastElement);
            toast.show();
        });
})();

(() => {
    const moneyNamePattern = /(^|\.)(SoTien|GiaTri|ThuNhap|DoanhThu|LoiNhuan|HanMuc)|^(thuNhapHangThang|hanMucToiDa)$/i;
    const moneyInputs = Array.from(document.querySelectorAll('input'))
        .filter((input) => {
            if (input.type === 'hidden' || input.disabled || input.readOnly) return false;
            const name = input.getAttribute('name') || input.id || '';
            if (!moneyNamePattern.test(name)) return false;

            return input.inputMode === 'numeric'
                || input.getAttribute('inputmode') === 'numeric'
                || input.dataset.moneyInput === 'true';
        });

    if (moneyInputs.length === 0) {
        return;
    }

    const onlyDigits = (value) => (value || '').replace(/[^\d]/g, '');
    const formatMoney = (value) => {
        const digits = onlyDigits(value).replace(/^0+(?=\d)/, '');
        return digits.replace(/\B(?=(\d{3})+(?!\d))/g, '.');
    };

    const formatInput = (input) => {
        const before = input.value;
        const selectionFromEnd = before.length - (input.selectionStart ?? before.length);
        input.value = formatMoney(before);

        const nextPosition = Math.max(0, input.value.length - selectionFromEnd);
        try {
            input.setSelectionRange(nextPosition, nextPosition);
        } catch {
            // Some browser/input combinations do not allow programmatic selection.
        }
    };

    moneyInputs.forEach((input) => {
        input.autocomplete = input.autocomplete || 'off';
        input.value = formatMoney(input.value);
        input.addEventListener('input', () => formatInput(input));
        input.addEventListener('blur', () => {
            input.value = formatMoney(input.value);
        });
    });

    document.querySelectorAll('form').forEach((form) => {
        form.addEventListener('submit', () => {
            form.querySelectorAll('input').forEach((input) => {
                if (moneyInputs.includes(input)) {
                    input.value = onlyDigits(input.value);
                }
            });
        }, true);
    });
})();
