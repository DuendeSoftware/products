# Upgrade guide

## From v2.x => v3.x

If you rely on the default extension methods for wiring up the BFF, then V3 should be a drop-in replacement. 

### Migrating from custom implementations of IHttpMessageInvokerFactory 

In Duende.BFF V2, there was an interface called IHttpMessageInvokerFactory. This class was responsible for creating 
and wiring up yarp's HttpMessageInvoker. This interface has been removed in favor yarp's IForwarderHttpClientFactory.

One common scenario for creating a custom implementation of this class was for mocking the http client
during unit testing. 

If you wish to inject a http handler for unit testing, you should now inject a custom IForwarderHttpClientFactory. For example:

``` c#
   // A Forwarder factory that forwards the messages to a message handler (which can be easily retrieved from a testhost)
    public class BackChannelHttpMessageInvokerFactory(HttpMessageHandler backChannel) 
        : IForwarderHttpClientFactory
    {
        public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context) => 
            new HttpMessageInvoker(backChannel);
    }

   // Wire up the forwarder in your application's test host:
    services.AddSingleton<IForwarderHttpClientFactory>(
         new BackChannelHttpMessageInvokerFactory(_apiHost.Server.CreateHandler()));


```

### Migrating from custom implementations IHttpTransformerFactory
The *IHttpTransformerFactory* was a way to globally configure the YARP tranform pipeline. In V3, the way that 
the default *endpoints.MapRemoteBffApiEndpoint()* method builds up the YARP transform has been simplified
significantly. Most of the logic has been pushed down to the *AccessTokenRequestTransform*. 

Here are common scenario's for implementing your own *IHttpTransformerFactory* and how to upgrade:

**Replacing defaults**

If you used a custom implementation of IHttpTransformerFactory to change the default behavior of *MapRemoteBffApiEndpoint()*, 
for example to add additional transforms, then you can now inject a custom delegate into the di container:

```
services.AddSingleton<BffYarpTransformBuilder>(CustomDefaultYarpTransforms);

//...

// This is an example of how to add a response header to ALL invocations of MapRemoteBffApiEndpoint()
private void CustomDefaultBffTransformBuilder(string localpath, TransformBuilderContext context)
{
    context.AddResponseHeader("added-by-custom-default-transform", "some-value");
    DefaultBffYarpTransformerBuilders.DirectProxyWithAccessToken(localpath, context);
}
```

Another way of doing this is to create a custom extensionmethod *MyCustomMapRemoteBffApiEndpoint()* that wraps
the MapRemoteBffApiEndpoint() and use that everywhere in your application. This is a great way to add other defaults
that should apply to all endpoints, such as requiring a specific type of access token. 

**Configuring transforms for a single route**
Another common usecase for overriding the IHttpTransformerFactory was to have a custom transform for a single route, by
applying a switch statement and testing for specific routes. 

Now, there is an overload on the *endpoints.MapRemoteBffApiEndpoint()* that allows you to configure the pipeline directly:

``` c#

            endpoints.MapRemoteBffApiEndpoint(
                "/local-path",
                _apiHost.Url(),
                context =>
                {
                    // do something custom: IE: copy request headers
                    context.CopyRequestHeaders = true;

                    // wire up the default transformer logic
                    DefaultTransformers.DirectProxyWithAccessToken("/local-path", context);
                })
                // Continue with normal BFF configuration, for example, allowing optional user access tokens
                .WithOptionalUserAccessToken();

```

### Removed method RemoteApiEndpoint.Map(localpath, apiAddress). 
The Map method was no longer needed as most of the logic had been moved to either the MapRemoteBffApiEndpoint and the DefaultTransformers. The map method also wasn't very explicit about what it did and a number of test scenario's tried to verify if it wasn't called wrongly. You are now expected to call the method MapRemoteBffApiEndpoint. This method now has a nullable parameter that allows you to inject your own transformers. 

### AccessTokenRetrievalContext properties are now typed
The LocalPath and ApiAddress properties are now typed. They used to be strings. If you rely on these, for example for implementing 
a custom IAccessTokenRetriever, then you should adjust their usage accordingly. 

    /// <summary>
    /// The locally requested path.
    /// </summary>
    public required PathString LocalPath { get; set; }

    /// <summary>
    /// The remote address of the API.
    /// </summary>
    public required Uri ApiAddress { get; set; }

### AddAddEntityFrameworkServerSideSessionsServices has been renamed to AddEntityFrameworkServerSideSessionsServices

If you used the method AddAddEntityFrameworkServerSideSessionsServices() in your code, please replace it with the corrected AddEntityFrameworkServerSideSessionsServices()