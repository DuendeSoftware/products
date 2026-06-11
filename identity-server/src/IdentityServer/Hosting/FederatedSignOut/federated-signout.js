(function(){
    var iframe=document.getElementById("signout-frame");
    var url=iframe.getAttribute("data-completion-url");
    var done=false;
    function isSafeUrl(u){return u&&(/^https?:\/\//i.test(u)||(u[0]==="/"&&u[1]!=="/"));}
    function complete(){if(!done&&isSafeUrl(url)){done=true;window.location.href=url;}}
    window.addEventListener("message",function(e){
        if(e.origin===window.location.origin&&e.source===iframe.contentWindow&&e.data==="logout-iframes-complete")complete();
    });
    setTimeout(complete,5000);
})();