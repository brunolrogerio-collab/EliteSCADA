# Coordinator Priority Override — 2026-08-31

Development Lead decision:

> Close Preview/Demo licensing, including an executable offline License Generator, before continuing the established Driver mainline/L3 development cycle.

Operational consequence:

- PR #175 remains Draft/Open and is not merged to `main` yet.
- Driver convergence 7/7 and L2 7/7 remain accepted; they are not reopened.
- `product/preview-demo-licensing` is the active development line.
- PR #185 is the active product implementation PR stacked on `coordination/driver-convergence-v3`.
- Issues #183 and #184 own the licensing product contract and priority gate.
- Issue #180 remains blocked until licensing is accepted, PR #175 reaches `main`, and exact post-main CI is green.
- Wave 11 remains blocked until L3 passes.

Revised stage order:

`Preview/Demo licensing + License Generator -> licensing CI/acceptance -> integrate into coordinator line -> PR #175 main merge -> post-main CI -> L3 #180 -> Wave 11`
