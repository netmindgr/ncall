# vcpkg overlay ports

`openssl`: the vcpkg port files (MIT, microsoft/vcpkg), with the version set to the
OpenSSL 3.5 LTS release used by nCall. vcpkg itself only offers 3.5 up to 3.5.4.
To update: change `version` in `openssl/vcpkg.json` and the `SHA512` of
`https://github.com/openssl/openssl/archive/openssl-<version>.tar.gz` in `portfile.cmake`.
