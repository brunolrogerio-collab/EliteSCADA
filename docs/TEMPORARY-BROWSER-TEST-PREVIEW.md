# EliteSCADA — Temporary C11 Browser Test Preview

## Purpose

Provide a reproducible GitHub Codespaces environment for Product Owner homologation of the frozen Wave 14 C11 canonical EEE Demo without changing the accepted C11 product bytes or creating a route to `main`.

The Preview successor is based on exact C11 freeze `a724ece64a292aa1d1dedd886a72fb28ff8d90fe` and consumes the committed self-contained package `preview/fixtures/EliteSCADA-EEE-Demo.escadapkg`.

## Architecture

- one Codespaces app container with exact .NET 10.0.400 compatibility and Node 24;
- one private TimescaleDB service;
- disposable per-Codespace `/etc/machine-id` for the existing fail-closed licensing implementation;
- protected `ELITESCADA_PREVIEW_ADMIN_PASSWORD` Codespaces secret;
- API bound to internal `127.0.0.1:5080`;
- Web bound to `0.0.0.0:5173`, the only intentionally forwarded port;
- real Local Identity authentication;
- real Engineering package/lifecycle APIs;
- persisted Active Revision as Runtime authority.

## Frozen package contract

The harness verifies:

- project key `eee-demo`;
- SHA-256 `4be1ca2338094799a8bf3c989322e5488a2381ce65d92cac3871188639e215c6`;
- provenance from accepted post-C25 integration product `ff185ffd67fe4abc597af9184c21f86376ba6e17`;
- self-contained package with no `.escadalib` dependency;
- canonical application shape: six screens, two popups, eight built-in Dynamos and one application Dynamo;
- Overview startup screen `c1170000-0000-4000-8000-000000000001`.

## Non-goals

This branch does not:

- alter product behavior to make Preview easier;
- bypass authentication, authorization, Engineering Lock or licensing;
- replace Save -> Publish -> Activate;
- make working Engineering state Runtime authority;
- merge C11 or #212 into `main`;
- resume Wave 13 signing;
- reintroduce the old Wave11 Demo fixture or the historical Script Engineering CSS correction from PR #210.

The historical #210 remains useful evidence for the Codespaces infrastructure pattern, but its product snapshot is not current authority.
