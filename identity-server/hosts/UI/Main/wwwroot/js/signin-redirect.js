(function () {
    var meta = document.querySelector("meta[http-equiv=refresh]");
    var url = meta && meta.getAttribute("data-url");
    if (url && (/^https?:\/\//i.test(url) || (url[0] === "/" && url[1] !== "/"))) {
        window.location.href = url;
    }
})();
