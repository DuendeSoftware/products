var mgr = new Oidc.UserManager({ loadUserInfo: true, filterProtocolClaims: true, response_mode:"query" });
mgr.signinRedirectCallback().then(function (user) {
    console.log(user);
    window.history.replaceState({},
        window.document.title,
        window.location.origin + window.location.pathname);
    window.location = "index.html";
});
