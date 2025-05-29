This client requires a client certificate.

Create one with mkcert:

```sh
mkcert -client -pkcs12 localhost
```

This will generate localhost-client.p12. That file is excluded from git, since it will only be trusted on the machine where
mkcert is run.
