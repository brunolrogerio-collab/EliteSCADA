# Help learning-guide audit — 24 October 2026

Owner scope: [#424](https://github.com/brunolrogerio-collab/EliteSCADA/issues/424#issuecomment-6392233542). Existing delivery: PR #772. This is development evidence, not installed user copy.

## Acceptance direction

The owner found the Help shallow, disorganized and dependent on prior knowledge. A topic must let a beginner perform the task: introduce the situation, explain each relevant field, show a concrete example and expected effect, and provide a useful next diagnostic action. Mentioning a feature or repeating its summary is insufficient. Use areas and subtopics as needed. Illustrations must show actual product UI; authored diagrams must be distinguished from captures.

## Source and branch discipline

- Product base: `wave15/corrections-integration@d41c2f84ba179f5eae01dc1df96c9c1a28830dc7`.
- Continue `work/w15-help-foundation-tag-20261023` / PR #772. Its previous head was `4db075e67997f61072dfeb22d2acf4e14ad9ecda`; normal merge `e0706e162fb1a5557d067102118a26aa59e7e8f1` brings in the product base without rewriting the branch.
- Corrected prior review: #772 already adds the Intelligence overview and route. #774 supersedes #773 and permits explicit global or restricted Intelligence grants. Do not restore the superseded SystemAdmin substitution.

## Findings and implementation

| Finding | Change in this batch | Source/limit |
| --- | --- | --- |
| One flat list mixed tasks, API references and driver topics; internal topic IDs were always visible | Eleven collapsible areas, visual subareas, accent-insensitive multiword search over prose/code/fields, section outline, beginner-first ordering; IDs removed from ordinary navigation | Stable topic IDs and existing `/api/help` remain the same installed Help system |
| Numbered procedures appeared as a single paragraph; summary text was repeated as multiple sections | Ordered steps and paragraphs render separately; repeated summary/body content is suppressed in presentation | Plain text/code rendering, no HTML content execution |
| Distinct Screen/Popup objects lacked a property reference | Thirty-one built-in object guides cover all 96 distinct editable scalar property keys with authored pt-BR/en/es purpose/example text | `BuiltinVisualObjectSchemas.All` and `VisualPropertyDefinition` supply each type's actual fields, defaults, limits, units and restrictions; generated read-only Intelligence references are not presented as manual-entry fields |
| General object advice omitted structural editors | Separate pen/axis/scale and alarm/event-browser filter guides; object-specific notes for polygon points, media and generated Intelligence references | `TrendPenEditor.tsx`, `BrowserConfigurationEditor.tsx`, visual schemas |
| A beginner had to infer the sequence from TAG to screen | First-screen walkthrough, object-choice guide, Popup workflow and rewritten Engineering orientation | Existing editor/Preview/Apply/project lifecycle; no new editor behavior |
| Server and Client examples were easy to mix | Scope selection, Server syntax explained line by line, entry-point/dependency guide, calculation/hysteresis/quality recipes, command/result guide, Client function reference and local interaction recipes | Actual Server interpreter and Client worker/capability providers; no general-Python promises for Server |
| Existing Client examples indexed bridge objects as Python dictionaries | Help/manual examples use `sample.quality`, `sample.value`, `event.visualObjectId` and `event.eventKey`; access differences are explicitly explained | The old form reproduced `TypeError` in the actual Client Python worker. Exact shipped examples are embedded `.py` resources and executed by the browser test; Server dictionaries retain their own bracket syntax |
| Intelligence overview did not explain enough fields/relationships | Profile/signal/Equipment-assignment table; scope/activation explanation; context predicates and exact-source-time requirements; ordered stages and generated-screen workflow | Current workspace, engineering DTO/validation and generator; metadata authoring does not run analytics, and no field accuracy/diagnosis is claimed |
| Generic Engineering/Troubleshooting articles survived prior passes | End-to-end data path and symptom/action diagnostic guide replace the two remaining `Basic` templates in the served catalog | Preserves process feedback, quality, permission and revision distinctions |

The resulting catalog contains 123 topics in each locale. Forty-seven additional topics complement the earlier batch; two existing generic topics are rewritten. Counts are inventory evidence, not a measure of writing quality or final acceptance.

## Illustration provenance

`web/scada-web/public/help/eee-runtime-screen.jpg` is copied byte-for-byte from the existing real public demonstration capture at `website/scadivo/site/assets/eee-runtime-screen.jpg`, `main@3b520aff3c249d0c7e0b8c2957344226de911ed7`. The image shows the EEE application dated 22/10/2026, in Portuguese, with simulated data. Its installed caption identifies it as a customizable demonstration composition, not a mandatory layout or current final-build acceptance capture. It illustrates the relationship between labels, values, process drawing, buttons and trend.

The current browser-control inventory reported `Local browser use is disabled in Work settings`. No fabricated editor or Script screenshot was substituted. Browser test captures verify the real updated Help component using the exact serialized catalog, with synthetic shell identity only. They are QA artifacts and are not represented as editor/product-operation captures. Final manual images still follow the #357 interface audit and #425 sequence.

## Validation

The final local checks are recorded in `LAST CHANGE.md` and the PR. Backend coverage verifies every editable scalar property against the registered object schemas, locale/field/code parity and serialization. Existing Server examples plus the new Server recipes run in the actual isolated interpreter. Browser coverage uses the exact embedded Client examples in the actual worker, including normal/below-reference/bad-quality behavior, visual override/clear and Client Memory; it also covers search, real-catalog rendering, language changes, local illustration loading, narrow layout and contextual routing.

A test guard initially matched the implementation-brand token `Vite` inside the ordinary Portuguese word `evite`. The guard now checks word boundaries while retaining the actual brand/jargon exclusions; natural writing is not distorted to satisfy a substring false positive. The browser check caught repeated links across subareas; the renderer was corrected to filter each subarea's own topics. The mobile table pass showed clipped descriptions; rows now stack into readable field cards on narrow screens.

## Acceptance still required

- Owner review of teaching depth and discoverability on the updated installed build; no automatic claim that #424/#425 are complete.
- Final #379 terminology/semantic sweep, including legacy/descriptor-derived wording beyond the new authoring guides.
- Real Editor/Script field-location captures after browser access and the accepted UI build are available; final manual illustrations and worked examples remain #425 work.
- Any product defect discovered while following instructions must be recorded separately rather than hidden inside a documentation change. HA, drivers, licensing implementation and frozen Intelligence analytics are unchanged by this batch.
