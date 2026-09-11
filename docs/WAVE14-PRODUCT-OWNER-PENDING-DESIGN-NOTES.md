# Wave 14 — Product Owner Pending Design Notes

**Recorded:** 2026-09-05 BRT  
**Authority:** Product Owner decisions during C11 closure  
**State:** **DESIGN NOTES / NOT IMPLEMENTED unless explicitly stated**

> These notes preserve Product Owner intent across coordinator handoff. They are not permission to expand C11 or bypass normal package governance.

## 1. Contextual Help / User Manual must be multilingual

The future EliteSCADA contextual Help/User Manual must follow the active product UI language automatically.

Binding behavior:

- Help IDs remain stable and language-neutral;
- if EliteSCADA UI is in Portuguese (`pt-BR`), clicking contextual Help resolves the requested Help ID directly to the Portuguese topic;
- if EliteSCADA UI is in English, the same Help ID resolves to the equivalent English topic;
- future supported locales follow the same contract;
- changing language must preserve semantic topic identity rather than opening an unrelated manual home page;
- section-level and field-level Help use the same locale resolution;
- Driver-specific Help must remain Driver-specific after localization;
- the locally bundled/offline manual must support the same locale contract as the online mirror, if an online mirror exists.

This refines the design in DRAFT PR #274 and supersedes any earlier wording that treated multiple languages as merely optional.

## 2. Runtime session identity

The Runtime session control must keep the current authenticated user's visible name beside the discreet session icon/control.

Current product already presents user identity and supports logout. Future Runtime UX adds a compact system-owned session popup with at least:

- current identity;
- `Trocar usuário`;
- `Sair`.

Runtime-only users must not gain Engineering/development navigation through this control.

This is design-only and remains tracked in DRAFT PR #274.

## 3. Optional application `.escadapkg` protection concept

Product Owner proposed an optional package-protection feature for OEM/developer scenarios, especially serial machinery where the customer should execute Runtime but the developer may want to protect the engineered application.

Initial conceptual intent:

- when no password is selected, Import/Export works exactly as it does today;
- the developer may optionally export an application package requiring a password for protected import/use;
- unprotected package portability must remain unchanged;
- the original discussion considered leaving the application package structure itself non-encrypted while protecting password/authorization information using product-owned cryptographic material.

### Critical hold before implementation

The Product Owner subsequently identified a flaw in this concept and explicitly instructed:

> **When implementation of password-protected application import/export is about to begin, ask the Product Owner about the flaw he identified.**

Therefore the following are binding until that conversation happens:

- feature is **NOT IMPLEMENTED**;
- security/cryptographic design is **NOT LOCKED**;
- do not assume the earlier idea of a compilation-embedded key is secure or final for this feature;
- do not add a password field to production Import/Export yet;
- do not change the existing unprotected `.escadapkg` contract;
- before implementation, explicitly reopen the threat model with the Product Owner and obtain the missing flaw description;
- only then define confidentiality/integrity/authorization goals and choose an implementation.

This hold is intentional. It is the one Product Owner topic in this handoff where the next coordinator **should ask again before implementation**, rather than relying only on the written preliminary idea.

## 4. System Recovery / Backup & Restore

The accepted design direction is preserved in DRAFT PR #273:

- application portability: existing `.escadapkg`;
- Database/Historian recovery: supported native database backup/restore;
- Security Authority: dedicated encrypted Export / Preview / Import protected by user-supplied master password;
- clean installation: Recovery Bootstrap with provisional administrator/workspace;
- collision rule: provisional recovery administrator has priority over an imported matching user/credential;
- recovery finalization requires at least one usable administrator and a valid Active production application.

Do not confuse this Security Authority backup password with the separate, still-unresolved optional application-package protection concept in section 3.

## 5. Licensing trust anchor — already implemented

Unlike the notes above, this item is already **IMPLEMENTED / TESTED / INTEGRATED** through C23.

Accepted integration product commit:

`5962bee401fadd700041e7c61cd430d4b4f28e27`

Production licensing contract:

- public RSA verification key embedded in EliteSCADA;
- production `KeyId`: `elite-prod-2026-01`;
- public-key SHA-256 fingerprint: `62244a1ca23f4a03d581e3df8fb46508264e29cd13d8747992710d3b0b4aac72`;
- private signing PEM remains outside repository/product and is used only by the License Generator owner/operator;
- customer does not import a PEM to validate normal production licenses.

## 6. Implementation sequencing

These post-DEMO items must not derail C11 canonical package closure.

Current priority remains:

1. fix any C11-exposed **generic product gap** on a separate correction branch;
2. integrate/revalidate accepted generic correction;
3. sync into C11;
4. finish canonical EEE package portability/versioning;
5. Product Owner fresh-Codespace homologation;
6. final Wave14 acceptance;
7. only then schedule post-DEMO enhancements/design implementation according to priority.
