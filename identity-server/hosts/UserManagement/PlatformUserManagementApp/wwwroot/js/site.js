// Vanilla JS replacements for Bootstrap JS and jQuery Validation
(function () {
    "use strict";

    // ── Collapse toggle ──
    // Supports data-toggle="collapse" data-target="#id"
    document.addEventListener("click", function (e) {
        var toggler = e.target.closest("[data-toggle='collapse']");
        if (!toggler) return;

        var targetSelector = toggler.getAttribute("data-target");
        if (!targetSelector) return;

        var target = document.querySelector(targetSelector);
        if (!target) return;

        var isShown = target.classList.contains("show");
        target.classList.toggle("show", !isShown);
        toggler.setAttribute("aria-expanded", String(!isShown));
    });

    // ── Alert dismiss ──
    // Supports btn-close inside .alert-dismissible
    document.addEventListener("click", function (e) {
        var closeBtn = e.target.closest(".btn-close");
        if (!closeBtn) return;

        var alert = closeBtn.closest(".alert-dismissible");
        if (!alert) return;

        alert.style.transition = "opacity 0.15s linear";
        alert.style.opacity = "0";
        setTimeout(function () { alert.remove(); }, 150);
    });

    // ── Minimal form validation ──
    // Replaces jQuery Validation Unobtrusive for server-rendered data-val attributes
    document.addEventListener("submit", function (e) {
        var form = e.target;
        if (!form.matches("form")) return;

        var isValid = true;

        // Clear previous errors
        form.querySelectorAll(".field-validation-error").forEach(function (el) {
            el.textContent = "";
            el.classList.remove("field-validation-error");
            el.classList.add("field-validation-valid");
        });

        // Validate required fields (data-val-required)
        form.querySelectorAll("[data-val='true']").forEach(function (input) {
            var requiredMsg = input.getAttribute("data-val-required");
            if (requiredMsg && !input.value.trim()) {
                showFieldError(input, requiredMsg);
                isValid = false;
                return;
            }

            // Min length
            var minLength = input.getAttribute("data-val-minlength-min");
            var minLengthMsg = input.getAttribute("data-val-minlength");
            if (minLength && input.value.length < parseInt(minLength, 10)) {
                showFieldError(input, minLengthMsg || "Too short.");
                isValid = false;
                return;
            }

            // Max length
            var maxLength = input.getAttribute("data-val-maxlength-max");
            var maxLengthMsg = input.getAttribute("data-val-maxlength");
            if (maxLength && input.value.length > parseInt(maxLength, 10)) {
                showFieldError(input, maxLengthMsg || "Too long.");
                isValid = false;
                return;
            }

            // Regex
            var pattern = input.getAttribute("data-val-regex-pattern");
            var regexMsg = input.getAttribute("data-val-regex");
            if (pattern && input.value && !new RegExp(pattern).test(input.value)) {
                showFieldError(input, regexMsg || "Invalid format.");
                isValid = false;
            }
        });

        if (!isValid) {
            e.preventDefault();

            // Update validation summary if present
            var summary = form.querySelector("[data-valmsg-summary='true']");
            if (summary) {
                var errors = form.querySelectorAll(".field-validation-error");
                if (errors.length > 0) {
                    var ul = summary.querySelector("ul") || document.createElement("ul");
                    ul.innerHTML = "";
                    errors.forEach(function (err) {
                        var li = document.createElement("li");
                        li.textContent = err.textContent;
                        ul.appendChild(li);
                    });
                    if (!summary.querySelector("ul")) summary.appendChild(ul);
                    summary.classList.remove("validation-summary-valid");
                    summary.classList.add("validation-summary-errors");
                }
            }
        }
    });

    function showFieldError(input, message) {
        var name = input.getAttribute("name");
        if (!name) return;

        var span = input.form.querySelector("[data-valmsg-for='" + name + "']");
        if (span) {
            span.textContent = message;
            span.classList.remove("field-validation-valid");
            span.classList.add("field-validation-error");
        }
    }

    // Clear field errors on input
    document.addEventListener("input", function (e) {
        var input = e.target;
        if (!input.matches("[data-val='true']")) return;

        var name = input.getAttribute("name");
        if (!name) return;

        var form = input.form;
        if (!form) return;

        var span = form.querySelector("[data-valmsg-for='" + name + "']");
        if (span && span.classList.contains("field-validation-error")) {
            span.textContent = "";
            span.classList.remove("field-validation-error");
            span.classList.add("field-validation-valid");
        }
    });
})();
