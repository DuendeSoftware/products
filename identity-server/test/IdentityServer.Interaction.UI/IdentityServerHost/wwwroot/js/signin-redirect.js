(function () {
    var meta = document.querySelector("meta[http-equiv=refresh]");
    var url = meta && meta.getAttribute("data-url");

    function getSafeUrl(value) {
        if (!value) {
            return null;
        }

        var isAbsoluteHttpUrl = /^https?:\/\//i.test(value);
        var isRootRelativeUrl = value[0] === "/" && value[1] !== "/";
        if (!isAbsoluteHttpUrl && !isRootRelativeUrl) {
            return null;
        }

        var parsedUrl;
        try {
            parsedUrl = new URL(value, window.location.origin);
        } catch (error) {
            return null;
        }

        if (parsedUrl.protocol !== "http:" && parsedUrl.protocol !== "https:") {
            return null;
        }

        return parsedUrl.href;
    }

    var safeUrl = getSafeUrl(url);
    if (safeUrl) {
        window.location.href = safeUrl;
    }
})();
