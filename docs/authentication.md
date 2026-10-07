# Phase 3 authentication design

This records the authentication foundation. Phase 4 now adds business/service management using these handlers; see [business management](business-management.md). Authentication storage and token behavior remain unchanged.

## Flow

Registration form → API client → AuthController → AuthService → Identity UserManager → EF Core/PostgreSQL. Registration accepts only Customer or BusinessOwner. Identity normalizes email and hashes passwords. Account creation and role assignment share a database transaction. A 201 response asks the user to log in separately, keeping registration and session creation explicit.

Login → AuthService → SignInManager.CheckPasswordSignInAsync → JwtTokenService → token/user response → AuthContext. Unknown email, wrong password, inactive account and lockout share a generic 401. Five failed attempts trigger a 15-minute lockout. Application code never logs passwords or signing keys.

Protected API calls explicitly pass the token to the shared client, which attaches the Bearer header. JWT middleware validates it before controller authorization. /me reads sub, checks the current active user, and returns a DTO. Client-selected user IDs are ignored. The role demonstration endpoint uses Authorize(Roles = BusinessOwner): missing authentication gives 401; wrong role gives 403.

## Identity and roles

ApplicationUser already inherits IdentityUser<Guid>; the context already includes Identity storage. Phase 3 registers managers/handlers without changing mappings or migration history. Identity requires unique email. Existing unique normalized username storage also prevents concurrent duplicates because registration sets username to email.

The explicit --seed-roles command creates missing Customer, BusinessOwner and Admin roles, then exits. It grants no memberships and creates no accounts. Public Admin registration is forbidden. Current accounts have one application role; role management, secure admin provisioning and ownership authorization are later work.

## Tokens and configuration

HS256 tokens include sub (user ID), email, role, jti (token ID), issuer, audience, validity start and expiration. No passwords/hashes. JWTs are signed, not encrypted: claims are readable.

Jwt:Key must be Base64 representing at least 32 cryptographically random bytes, supplied through user secrets/environment. No fallback key exists. Defaults: issuer ServiceHub.Api, audience ServiceHub.Web, expiration 30 minutes (range 1–60). Validation requires signature, HS256, exact issuer/audience and expiration with zero clock skew. Host clocks must stay synchronized. README documents setup commands.

Key changes invalidate issued tokens. Role changes/deactivation do not generally revoke tokens: /me checks current active status, while role authorization uses issued claims until expiry. General revocation and production secret management are deferred.

## Browser session and security

AuthContext keeps token, expiry and user in React memory only. Logout and the expiry timer clear state. Refreshing/closing a page or opening a new tab requires login. This avoids persistent browser token storage without introducing refresh-token infrastructure. Malicious JavaScript could still access memory or invoke requests; this does not eliminate XSS risk.

ProtectedRoute controls navigation, not API security. The API remains authoritative. The owner page calls a protected permission demonstration; no business management exists.

No API logout endpoint exists because there is no server session/refresh token to revoke. Copied access tokens remain usable until expiry after browser logout. Public hosting requires HTTPS; Development uses local HTTP. CORS accepts configured origins and bearer headers.

## Verification and scope

Real PostgreSQL HTTP tests cover approved registration, forbidden roles, duplicate email, password hashing/normalization, valid/invalid login, lockout, JWT identity, /me, role permissions, invalid signatures/issuers/audiences/expiration and idempotent role seeding. All earlier persistence tests remain. Test hosts supply random keys without weakening production requirements.

Browser checks exercised both registration types, login, /me, authenticated navigation, owner authorization, unauthenticated account redirect and logout. No schema migration is needed. Business management, booking, OAuth, refresh tokens, password reset, email verification, MFA and admin pages are deferred.
