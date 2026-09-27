(() => {
    const latin = /^\p{Script=Latin}$/u;
    const letterOrMark = /^[\p{L}\p{M}]$/u;
    const decimalDigit = /^\p{Decimal_Number}$/u;
    const punctuation = /^\p{Punctuation}$/u;
    const fields = document.querySelectorAll("[data-kargonomi-address]");

    fields.forEach(input => {
        const warning = input.parentElement?.querySelector("[data-kargonomi-address-warning]");
        if (!warning) return;

        const updateWarning = () => {
            const hasUnsupportedCharacter = Array.from((input.value || "").normalize("NFC")).some(character => {
                if (/\s/u.test(character)) return false;
                if (letterOrMark.test(character)) return !latin.test(character);
                if (decimalDigit.test(character)) return !/^[0-9]$/.test(character);

                const codePoint = character.codePointAt(0);
                return codePoint < 0x21 || codePoint > 0x7E || !punctuation.test(character);
            });

            warning.hidden = !hasUnsupportedCharacter;
        };

        input.addEventListener("input", updateWarning);
        updateWarning();
    });
})();
