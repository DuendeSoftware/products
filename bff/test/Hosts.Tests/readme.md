# Hosts.Tests

This project contains the integration tests for the various hosts. This host is built using Aspnet Aspire.

The actual tests have been written in such a way that they only need a HTTP client to work. If you don't do anything,
then the system will start an aspire test host, run the tests, then kill the aspire host again. 

Howeve,if you want faster feedback, you can also start the aspire host yourself. To avoid collisions between the running 
application and the testing framework trying to rebuild all libraries, I recommend running the system in release mode.

```
cd src/samples/Hosts.Tests
dotnet run --configuration Release
```

During startup of the first test, the system checks if the aspire host is already running. If so, it will skip starting 
and simply configure a http client to work against the host. 


## Running under NCrunch

It turns out that aspnet aspire doesn't work well with NCrunch. See this link for more info.
https://forum.ncrunch.net/yaf_postst3541_Aspire.aspx

But as NCrunch is a really fast way to get feedback on the tests, I've tried to make this work differently. 

I've added a new configuration: Debug_Ncrunch and have included conditional compilation. If you have this enabled
(as it is in NCrunch) then it not use any of the aspire initialization and simply proceed to running the tests. 
This is by far the fastest way to develop the tests. You can even have the aspire host running in debug AND debug the tests 
at the same time. 

