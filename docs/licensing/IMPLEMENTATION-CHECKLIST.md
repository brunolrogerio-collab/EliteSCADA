# Licensing Implementation Checklist

Status: **IMPLEMENTED / ACCEPTANCE PENDING FINAL DOCUMENTATION HEAD CI**

- [x] Canonical entitlement contracts
- [x] Machine request code
- [x] Signed license verification
- [x] Run/activation entitlement gate
- [x] Remove transitional mutation-time 200-TAG ceiling
- [x] Demo 300-minute monotonic session supervisor
- [x] Demo expiry lifecycle/status
- [x] License API/UI
- [x] Offline License Generator
- [x] Windows x64 generator publish path
- [x] Focused tests
- [x] Exact-head normal CI on implementation head

Implementation validation checkpoint before this documentation-only acceptance update:

- head: `90727bb8bf94fe7912a3c998cfb8655840410205`
- Preview Licensing CI run: `#32`
- build solution: PASS
- License Generator build: PASS
- Core/licensing tests: PASS
- runtime licensing tests: PASS
- Windows x64 single-file publish: PASS
- artifact upload: PASS

The final documentation head must also be green before PR #185 is merged into the coordinator line.
