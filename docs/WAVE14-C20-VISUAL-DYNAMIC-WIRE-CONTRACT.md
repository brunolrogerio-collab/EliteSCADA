# Wave 14 C20 — Visual Dynamic Wire Contract

Status: **IMPLEMENTATION ACTIVE**

Base authority: `wave14/corrections-integration` @ `e91aa12972aa35806cd5ca61cd27fe8c9afdbf10`

Discovered by: C11 canonical EEE DEMO construction.

## Problem

EliteSCADA Engineering intentionally serializes public enums with `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)`. Therefore a visual dynamic definition authored in the frontend using the TypeScript authoring vocabulary is persisted and returned through the Active Runtime wire boundary using camelCase enum strings.

Examples:

| Meaning | Authoring representation | Persisted wire representation |
| --- | --- | --- |
| TAG value source | `Tag` | `tag` |
| Client Memory source | `ClientMemory` | `clientMemory` |
| Expression source | `Expression` | `expression` |
| Boolean result | `Boolean` | `boolean` |
| Numeric result | `Number` | `number` |
| Direct condition | `Direct` | `direct` |
| Numeric interval | `NumericInterval` | `numericInterval` |
| Inside interval | `Inside` | `inside` |
| Outside interval | `Outside` | `outside` |
| Bottom-to-top fill | `BottomToTop` | `bottomToTop` |
| Top-to-bottom fill | `TopToBottom` | `topToBottom` |
| Left-to-right fill | `LeftToRight` | `leftToRight` |
| Right-to-left fill | `RightToLeft` | `rightToLeft` |

`visualDynamicRuntime.ts` currently compares several of those values only against the PascalCase authoring representation. As a result, a dynamic object may work in Working/authoring state and fail or be misinterpreted after the normal `Save -> Publish -> Activate -> Active Runtime` round trip.

This is a generic product defect. C11 MUST NOT compensate for it with DEMO-only JSON, private runtime behavior or frontend-fabricated state.

## Scope

C20 shall fix only the generic visual dynamic wire seam for:

1. `VisualValueSourceKind`;
2. `VisualExpressionValueType`;
3. `VisualExpressionDependencyKind`;
4. `VisualBooleanConditionKind`;
5. `VisualNumericIntervalMode`;
6. `VisualAnalogFillDirection`.

The normalizer shall accept both:

- the current frontend authoring representation; and
- the current persisted camelCase wire representation.

It shall project both forms to one internal canonical representation before evaluation/rendering.

## Fail-closed rule

An explicit unknown enum value MUST NOT silently fall through to another supported value.

In particular:

- an unknown expression value type must not become Number merely because it is not Boolean;
- an unknown dependency kind must not become Tag merely because it is not ClientMemory;
- an unknown interval mode must not become Inside;
- an unknown Analog Fill direction must not be replaced by a default.

Existing defaults may continue to apply only when the corresponding optional field is absent/null and the public contract already defines that default.

Invalid explicit values must produce a deterministic visual dynamic diagnostic or an equivalent fail-closed result.

## Non-goals / hard boundaries

C20 MUST NOT:

- change the backend global JSON enum serialization policy;
- introduce a schema fork for C11;
- introduce EEE-specific behavior;
- weaken import validation;
- change authentication, authorization, licensing or lifecycle semantics;
- fabricate visual state in the frontend;
- bypass the Active revision authority;
- alter Alarm / Operational Event / Audit semantics.

## Required proof

C20 is not accepted from source inspection alone.

Required evidence:

1. focused frontend tests proving every affected enum accepts both supported representations and rejects unknown explicit values;
2. a dynamic resolution test using persisted camelCase wire values;
3. a Wave11 Active Runtime regression that exercises the normal lifecycle and proves a persisted dynamic object still works after `Save -> Publish -> Activate`;
4. Analog Fill must be included in that lifecycle proof because C11 depends on it for the animated well level;
5. no regression in the existing Wave11 visual/runtime lane.

## Integration rule

C20 shall be integrated into `wave14/corrections-integration` only after its own proof is green.

After integration, C11 shall consume the exact integrated generic fix and resume from its frozen DEMO head. C11 must not carry a divergent local copy of the C20 implementation.

PR #212 remains DRAFT and is not authorized for merge to `main`.
