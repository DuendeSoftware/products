const loginUrl = "./bff/login";
const silentLoginUrl = "./bff/silent-login";
const userUrl = "./bff/user";
const localApiUrl = "./local";
const remoteApiUrl = "./api";
const yarpApiUrl = "./yarp"
let logoutUrl = "./bff/logout";

async function onLoad() {
    var req = new Request(userUrl, {
        headers: new Headers({
            'X-CSRF': '1'
        })
    })

    try {
        var resp = await fetch(req);
        if (resp.ok) {

            let claims = await resp.json();
            showUser(claims);

            if (claims) {
                log("user logged in");

                let logoutUrlClaim = claims.find(claim => claim.type === 'bff:logout_url');
                if (logoutUrlClaim) {
                    logoutUrl = logoutUrlClaim.value;
                }
            }
            else {
                log("user not logged in");
            }
        } else if (resp.status === 401) {
            log("user not logged in");

            // if we've detected that the user is no already logged in, we can attempt a silent login
            // this will trigger a normal OIDC request in an iframe using prompt=none.
            // if the user is already logged into IdentityServer, then the result will establish a session in the BFF.
            // this whole process avoids redirecting the top window without knowing if the user is logged in or not.
            var silentLoginResult = await silentLogin();

            // the result is a boolean letting us know if the user has been logged in silently
            log("silent login result: " + silentLoginResult);

            if (silentLoginResult) {
                // if we now have a user logged in silently, then reload this window
                window.location.reload();
            }
        }
    }
    catch (e) {
        log("error checking user status");
    }
}

onLoad();

function login() {
    window.location = loginUrl;
}

function logout() {
    window.location = logoutUrl;
}

async function callLocalApi(path) {
    return await callApi(localApiUrl + path);
}

async function callRemoteApi(path) {
    return await callApi(remoteApiUrl + path);
}

async function callYarpApi(path) {
    return await callApi(yarpApiUrl + path);
}

async function callApi(path) {
    var req = new Request(path, {
        headers: new Headers({
            'X-CSRF': '1'
        })
    })
    var resp = await fetch(req);

    log("API Result: " + resp.status);
    if (resp.ok) {
        showApi(await resp.json());
    } else {
        showApi(resp.statusText)
    }
}

async function callLocalSelfContainedApi() {
    await callLocalApi("/self-contained")
}

async function callLocalApiThatInvokesExternalApi() {
    await callLocalApi("/invokes-external-api")
}

async function callUserTokenApi() {
    await callRemoteApi("/user-token");
}

async function callClientTokenApi() {
    await callRemoteApi("/client-token");
}

async function callUserOrClientTokenApi() {
    await callRemoteApi("/user-or-client-token");
}

async function callAnonymousApi() {
    await callRemoteApi("/anonymous");
}

async function callOptionalUserTokenApi() {
    await callRemoteApi("/optional-user-token");
}

async function callYarpAnonymousApi() {
    await callYarpApi("/anonymous");
}

async function callYarpUserTokenApi() {
    await callYarpApi("/user-token");
}

async function callYarpClientTokenApi() {
    await callYarpApi("/client-token");
}

async function callYarpUserOrClientTokenApi() {
    await callYarpApi("/user-or-client-token");
}


// User Management
document.querySelector(".login").addEventListener("click", login, false);
document.querySelector(".logout").addEventListener("click", logout, false);

// Local APIs
document.querySelector(".call_local_self_contained").addEventListener("click", callLocalSelfContainedApi, false);
document.querySelector(".call_local_external").addEventListener("click", callLocalApiThatInvokesExternalApi, false);

// Remote APIs
document.querySelector(".call_anonymous").addEventListener("click", callAnonymousApi, false);
document.querySelector(".call_user").addEventListener("click", callUserTokenApi, false);
document.querySelector(".call_optional_user").addEventListener("click", callOptionalUserTokenApi, false);
document.querySelector(".call_client").addEventListener("click", callClientTokenApi, false);
document.querySelector(".call_user_or_client").addEventListener("click", callUserOrClientTokenApi, false);

// Yarp APIs
document.querySelector(".call_yarp_anonymous").addEventListener("click", callYarpAnonymousApi, false);
document.querySelector(".call_yarp_user").addEventListener("click", callYarpUserTokenApi, false);
document.querySelector(".call_yarp_client").addEventListener("click", callYarpClientTokenApi, false);
document.querySelector(".call_yarp_user_or_client").addEventListener("click", callYarpUserOrClientTokenApi, false);

function showApi() {
    document.getElementById('api-result').innerText = '';

    Array.prototype.forEach.call(arguments, function (msg) {
        if (msg instanceof Error) {
            msg = "Error: " + msg.message;
        } else if (typeof msg !== 'string') {
            msg = JSON.stringify(msg, null, 2);
        }
        document.getElementById('api-result').innerText += msg + '\r\n';
    });
}

function showUser() {
    document.getElementById('user').innerText = '';

    Array.prototype.forEach.call(arguments, function (msg) {
        if (msg instanceof Error) {
            msg = "Error: " + msg.message;
        } else if (typeof msg !== 'string') {
            msg = JSON.stringify(msg, null, 2);
        }
        document.getElementById('user').innerText += msg + '\r\n';
    });
}

function log() {
    document.getElementById('response').innerText = '';

    Array.prototype.forEach.call(arguments, function (msg) {
        if (msg instanceof Error) {
            msg = "Error: " + msg.message;
        } else if (typeof msg !== 'string') {
            msg = JSON.stringify(msg, null, 2);
        }
        var msgWithTimestamp = `${(new Date()).toLocaleTimeString()} - ${msg}`;
        document.getElementById('response').innerText += msgWithTimestamp;
    });
}


// this will trigger the silent login and return a promise that resolves to true or false.
function silentLogin(iframeSelector) {
    iframeSelector = iframeSelector || "#bff-silent-login";
    const timeout = 5000;

    return new Promise((resolve, reject) => {
        function onMessage(e) {
            // look for messages sent from the BFF iframe
            if (e.data && e.data['source'] === 'bff-silent-login') {
                window.removeEventListener("message", onMessage);
                // send along the boolean result
                resolve(e.data.isLoggedIn);
            }
        };

        // listen for the iframe response to notify its parent (i.e. this window).
        window.addEventListener("message", onMessage);

        // we're setting up a time to handle scenarios when the iframe doesn't return immediaetly (despite prompt=none).
        // this likely means the iframe is showing the error page at IdentityServer (typically due to client misconfiguration).
        window.setTimeout(() => {
            window.removeEventListener("message", onMessage);

            // we can either just treat this like a "not logged in"
            resolve(false);
            // or we can trigger an error, so someone can look into the reason why
            // reject(new Error("timed_out")); 
        }, timeout);

        // send the iframe to the silent login endpoint to kick off the workflow
        document.querySelector(iframeSelector).src = silentLoginUrl;
    });
}
