(function(){
    var iframe=document.getElementById("signout-frame");
    var url=iframe.getAttribute("data-completion-url");
    var safeUrl=getSafeUrl(url);
    var done=false;

    function getSafeUrl(value){
        if(!value){return null;}
        var isAbsoluteHttpUrl=/^https?:\/\//i.test(value);
        var isRootRelativeUrl=value[0]==="/"&&value[1]!=="/";
        if(!isAbsoluteHttpUrl&&!isRootRelativeUrl){return null;}

        var parsedUrl;
        try{
            parsedUrl=new URL(value,window.location.origin);
        }catch(error){
            return null;
        }

        if(parsedUrl.protocol!=="http:"&&parsedUrl.protocol!=="https:"){return null;}
        return parsedUrl.href;
    }

    function complete(){if(!done&&safeUrl){done=true;window.location.href=safeUrl;}}
    window.addEventListener("message",function(e){
        if(e.origin===window.location.origin&&e.source===iframe.contentWindow&&e.data==="logout-iframes-complete")complete();
    });
    setTimeout(complete,5000);
})();