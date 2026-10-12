/* netmind voice build configuration, 2026-10-11. */
#pragma once
#define PJMEDIA_HAS_VIDEO 0
#define PJ_HAS_SSL_SOCK 1
#define PJMEDIA_HAS_OPUS_CODEC 1
#define PJMEDIA_HAS_G7221_CODEC 0
/* iLBC (RFC 3951 reference code) is not built: unclear licence, codec no longer in use. */
#define PJMEDIA_HAS_ILBC_CODEC 0
#define PJMEDIA_HAS_SPEEX_AEC 1
#define PJSUA_MAX_CALLS 32

#define PJMEDIA_HAS_BCG729 1
#define PJMEDIA_SRTP_HAS_DTLS 1

/* SIP over TLS: only TLS 1.2 and 1.3. */
#define PJSIP_SSL_DEFAULT_PROTO (PJ_SSL_SOCK_PROTO_TLS1_2 | PJ_SSL_SOCK_PROTO_TLS1_3)
