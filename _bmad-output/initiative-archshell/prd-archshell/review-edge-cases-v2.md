---
title: ArchShell PRD - Edge-Case Review v2
reviewed: prd-archshell.md (updated 2026-11-03) + addendum.md
lens: edge-case-hunter (bmad-review)
focus: FR-43 to FR-52; edited FR-1, FR-15a, FR-24, FR-33, UJ-1, UJ-2
created: 2026-11-03
---

# ArchShell PRD - Edge-Case Review v2

**Method:** path tracing over the new feature-plan, contract, and backlog requirements. Only unhandled paths are listed. Findings resolved since `review-edge-cases.md` (EC-01 to EC-36) are not repeated. Open Question 1 (interrupted sessions, concurrent branches) is cited only where the new requirements add states it does not name.

**Verdict:** not ready for architecture handoff on the feature workflow. The closed sets introduced by FR-43 (intents) and FR-47 (types) have no "does not fit" branch, and several paths between the feature plan, contract freeze, and charter growth deadlock against FR-46 and FR-14.

## Summary

| Severity | Count | IDs |
|---|---|---|
| Critical | 2 | EC2-01, EC2-02 |
| High | 6 | EC2-03 to EC2-08 |
| Medium | 8 | EC2-09 to EC2-16 |
| Low | 3 | EC2-17 to EC2-19 |

## Critical

### EC2-01 - A spec item whose intent fits none of the closed set has no path
- **FR:** FR-43, FR-44, FR-33, FR-45, Glossary (Intent)
- **Unhandled scenario:** a spec item asks for something no intent covers: delete or archive, rename, a scheduled or background job, a data migration or seed, a file export, a configuration change. The closed set itself is only given by example ("for example ..."), so its membership is undefined. Schema validation (FR-43) only rejects labels outside the set; it cannot detect a valid label forced onto an item it does not describe. Consensus (FR-44) can then agree on a wrong intent three times.
- **Guard:** enumerate the closed intent set normatively; add an explicit `no_fit` outcome that stops and asks the developer (reshape the item, or record it as a misfit per FR-28); never let the model pick the "closest" intent.
- **Consequence:** wrong building blocks are scaffolded deterministically and then frozen and protected (FR-46, FR-48), so the error is locked in rather than caught.

### EC2-02 - Contracts that need a type outside the closed type set cannot be expressed
- **FR:** FR-47, FR-48, FR-49, UJ-2
- **Unhandled scenario:** the closed set is primitives, entity identifiers, money, enums, and existing contracts or data types. It does not state whether collections, optional values, dates/times/durations, files or streams, pagination, or a **new** composite data type (a new DTO) are allowed. Ordinary features need these: "list a patient's appointments" returns a collection; UJ-2's fee rule depends on the appointment time; the addendum example uses `string?` and `money?` (optionality) without the PRD naming it. A proposal using such a type is "rejected by schema validation and retried within bounds" and then has no next step.
- **Guard:** define the type set normatively, including type constructors (optional, list), date/time, and how a feature declares a new data type; define what happens when retries are exhausted (stop and ask, like FR-27).
- **Consequence:** common features cannot get past contract proposal, or the model is pushed to encode dates and lists as strings, which the frozen contract then makes permanent.

## High

### EC2-03 - Name collisions after token normalization are undefined
- **FR:** FR-43, FR-45, FR-47, FR-29
- **Unhandled scenario:** two spec items normalize to the same verb and noun (S1 "cancel appointment", S4 "cancel appointment by the clinic"); two intents on one item map to the same building block; a normalized name equals a module name (`Appointments.Appointments`), a C# keyword or BCL type (`Event`, `Task`, `Type`), or an existing type of a **different** building-block kind. Singularization of irregular or compound nouns ("no-show fee", "series") and Spanish-origin tokens is not specified (FR-12 needs English identifiers).
- **Guard:** define collision detection over the plan and the project inventory, deduplication of identical building blocks, a disambiguation question when two items collide, and the reserved-name list per language pack.
- **Consequence:** two spec items silently share one handler, or a "modification" is applied to an unrelated file that happens to have the same name.

### EC2-04 - "Already exists becomes a modification" conflicts with protection, other modules, and determinism
- **FR:** FR-45, FR-24, FR-46, FR-48, FR-14, UJ-2 step 1
- **Unhandled scenario:** the existing building block is (a) generated contract code, which FR-48 puts in the protected set; (b) owned by another module, so modifying it crosses module boundaries; (c) a file the developer created or edited by hand; (d) the same name but a different building-block kind. FR-24's "byte-identical" guarantee is defined for scaffolding a plan, but a modification merges template output into an existing file whose bodies the agent already wrote; what is generated for a modification is not defined.
- **Guard:** define what a modification produces (for example, added signatures only), restrict modifications to the feature's own module, route changes to protected or generated files through FR-49/FR-50 or growth, and scope the byte-identical guarantee to new files.
- **Consequence:** the plan either cannot be scaffolded (FR-14 blocks it) or scaffolding overwrites implemented code.

### EC2-05 - Charter growth discovered after the plan is confirmed breaks plan derivation order
- **FR:** FR-45, FR-29, FR-46, FR-50, UJ-2 steps 1-2
- **Unhandled scenario:** in UJ-2 the plan (with a notification port) and contracts are confirmed and frozen in step 1, but the `Notifications` module only exists after growth in step 2. The mapping table and naming rules need the owning module to produce paths, so the plan in step 1 references an undeclared module. The PRD does not say whether growth must be detected during plan derivation, or whether the plan and frozen contracts are re-derived after growth, and whether the RED test (FR-25/FR-50) is rewritten.
- **Guard:** require growth needs to be detected while deriving the plan, before confirmation and freeze; or define that growth reopens the plan and contracts and re-runs FR-49/FR-50 and FR-25.
- **Consequence:** plan paths point to a module that does not exist, or the frozen contracts and RED test no longer match the grown charter.

### EC2-06 - A plan extension lets the model choose structure, bypassing FR-43/FR-45
- **FR:** FR-46, FR-43, FR-45, FR-26, FR-50
- **Unhandled scenario:** when the agent needs structure outside the plan, "it stops and proposes a plan extension". That proposal is the model selecting building blocks and names directly, which FR-43 forbids. Also undefined: whether the extension is derived by re-classifying a new or revised spec item, whether it can add contracts (re-opening the freeze), whether it resets the retry count (FR-26), and what happens when the developer declines it.
- **Guard:** make extensions go through the same path (new or revised S# -> FR-43/FR-44 -> FR-45 mapping), and define decline (treat as misfit, FR-28) and its effect on FR-26 counting.
- **Consequence:** the deterministic-plan guarantee holds only until the first extension; a junior developer approves structure they cannot judge.

### EC2-07 - A breaking change to a public contract consumed by another module in the same solution deadlocks
- **FR:** FR-51, FR-46, FR-21, FR-26, FR-29
- **Unhandled scenario:** feature A changes `Billing.Contracts`; `Appointments` already calls it. The developer confirms the breaking change (FR-51), but the build now fails in `Appointments`, whose files are not in feature A's plan, so FR-46 blocks the agent from fixing them. The full gates (FR-21) fail and each attempt counts against FR-26 until exhaustion.
- **Guard:** have FR-51 list in-solution consumers of the changed contract and add their call sites to the plan (as modifications) when the change is confirmed, or forbid confirming a breaking change that breaks in-solution consumers without that.
- **Consequence:** a confirmed, recorded breaking change can never pass the gates.

### EC2-08 - Renaming or removing an existing entity or contract has no path
- **FR:** FR-45, FR-47, FR-51, FR-29, FR-14
- **Unhandled scenario:** the developer asks to rename `Patient` to `Client`, or remove an operation. FR-45 derives names from tokens, so a new token creates a **new** file instead of a modification and the old type stays; no intent covers rename or delete (see EC2-01); FR-29 forbids growth that duplicates or conflicts with existing names; module renames are not covered by "grow only".
- **Guard:** state that renames and removals are out of scope for v1 (with the message shown), or define a rename/remove intent whose plan lists old and new names, all call sites as modifications, and the breaking-change record (FR-51).
- **Consequence:** duplicate concepts accumulate, or the agent renames by hand and is blocked by FR-46 and FR-14.

## Medium

### EC2-09 - Partial agreement and token disagreement in consensus are undefined
- **FR:** FR-44, FR-43, FR-45
- **Unhandled scenario:** an item takes "one or more" intents, so runs can partly agree (`{state_change, side_effect}` vs `{state_change}`). The three runs also produce verb and noun tokens; FR-44 covers only intents, so token disagreement (`cancel` vs `void`) silently feeds nondeterministic names into FR-45, whose determinism is only "given the same tokens". The developer question for a disagreement must be phrased so a low-architecture-knowledge user can answer it (Target User), which is not required.
- **Guard:** define agreement as exact set equality over intents **and** tokens; any difference becomes a plain-language question per FR-3 style; when consensus is off, state that tokens are taken from the single run.
- **Consequence:** two runs of the same feature produce different type names despite NFR-4.

### EC2-10 - The developer's answer to a disagreement can also be unusable
- **FR:** FR-44, FR-23
- **Unhandled scenario:** the developer answers "both", "I don't know", or describes an intent outside the set; repeated disagreement on many items has no question budget (FR-3 caps selection at 5 questions; feature questions have no cap).
- **Guard:** offer closed choices (the disagreeing intents plus "neither, reshape this item"), and define a per-feature question budget or fallback.
- **Consequence:** an unresolvable question loop, or a free-text answer mapped by the model, which FR-44 forbids.

### EC2-11 - Mapping table gaps for combinations, per-module patterns, and custom cards
- **FR:** FR-33, FR-15, FR-17, FR-38, FR-45
- **Unhandled scenario:** FR-33 requires complete mapping tables for "every shipped project template", but not for team-added custom cards (FR-38) that do have a template, nor for Modular Monolith + Vertical Slice "per module" where different modules may resolve to different building blocks. An intent that is meaningless in a pattern (for example `query` in a write-only slice) must still "map to at least one building block".
- **Guard:** require a complete mapping table wherever a template exists, including custom extensions, and enforce it in the conformance suite (FR-37); allow a pattern to declare an intent as not applicable, which turns it into an EC2-01 stop.
- **Consequence:** the feature workflow fails at plan derivation on projects whose bootstrap succeeded.

### EC2-12 - "Previous version" for breaking-change comparison is undefined
- **FR:** FR-51, FR-41
- **Unhandled scenario:** the baseline could be the last commit, the target branch, or the last release. Also: a contract introduced and changed within the same unmerged feature; a contract moved from internal to public (or public to internal, which removes it for consumers); several features on branches changing the same contract.
- **Guard:** define the baseline per context (in-session vs `archshell verify` on a PR: the merge base), and treat a kind change as a public-contract change.
- **Consequence:** CI and in-session checks disagree, or a public removal passes as an "internal" change.

### EC2-13 - A frozen contract needed by two features at once
- **FR:** FR-50, FR-51, FR-46, Open Question 1
- **Unhandled scenario:** feature B (or a second team branch) needs to change a contract that feature A froze and has not finished. FR-50 only covers the agent of the same feature asking for a revision. Open Question 1 covers concurrent growth, not concurrent contract freezes.
- **Guard:** state whether a freeze is per feature or per contract, and what B does (wait, or confirm a revision that reopens A's RED test).
- **Consequence:** A's RED test and implementation break when B's revision lands.

### EC2-14 - A revised frozen contract invalidates the RED test with no defined step
- **FR:** FR-50, FR-25, FR-26
- **Unhandled scenario:** after the developer confirms a revised contract, the existing RED test was written against the old one. Who rewrites it, whether it must be observed failing again, and whether the attempt count resets are not stated.
- **Guard:** a confirmed revision regenerates the contract, requires a new RED observation, and resets FR-26 for affected S#.
- **Consequence:** the agent edits the test to match its own code, defeating test-first.

### EC2-15 - Backlog items that misfit, span modules, need growth, or are hand-edited
- **FR:** FR-52, FR-1, FR-7, FR-29, FR-28
- **Unhandled scenario:** the backlog is editable and unprotected. Undefined: an item whose module was removed during FR-7 editing ("nothing silently dropped" vs. no module to hold it); an item spanning two modules; an item that needs charter growth or does not fit the pattern; an item edited into something vague or into several features; a malformed hand-edited backlog file; an item deleted while its feature is in progress; marking done when the item produced several features or only part of one.
- **Guard:** treat a backlog item only as a request (FR-23 runs normally, including growth FR-29 and misfit FR-28); define done for multi-feature items; tolerate parse failures by asking instead of skipping items.
- **Consequence:** capabilities are silently lost, or items stay "not done" forever.

### EC2-16 - Interrupted sessions mid-plan leave new intermediate states
- **FR:** FR-45, FR-24, FR-46, FR-48, FR-50, FR-25, Open Question 1
- **Unhandled scenario:** the session stops after plan confirmation but before scaffolding; after scaffolding but before contract freeze; after freeze but before the RED test is observed; or while `plan.yaml` is half written. The plan is protected (FR-46) once confirmed, so a partial or stale plan cannot be fixed by hand. Open Question 1 names resume in general but not these states or what is protected while incomplete.
- **Guard:** make plan, scaffold, contract generation, and freeze one resumable unit with recorded phase, or roll back to the last confirmed step (as FR-15a does for bootstrap).
- **Consequence:** a protected, partially applied plan that neither the agent nor the developer may change.

## Low

### EC2-17 - Helper types and test doubles are blocked by plan conformance
- **FR:** FR-46, FR-25, FR-24
- **Unhandled scenario:** FR-46 blocks any type not in the plan, including private nested types, extension methods, test fakes for ports, and fixtures needed for the RED test.
- **Guard:** define which type kinds are allowed inside planned files (private or nested types, test-only types in planned test files).
- **Consequence:** frequent plan-extension stops for trivial helpers, inflating developer interruptions.

### EC2-18 - Showing generated code before confirmation contradicts "nothing scaffolded before confirmation"
- **FR:** FR-49, FR-45, FR-48
- **Unhandled scenario:** the developer can ask to see generated contract code before confirming, but FR-45 says nothing is scaffolded before confirmation.
- **Guard:** state the code is rendered as a preview only and not written to the project.
- **Consequence:** preview files left in the tree trigger FR-46 or FR-14 blocks.

### EC2-19 - Requests with no extractable capabilities produce an empty backlog
- **FR:** FR-1, FR-52, UJ-1 step 5
- **Unhandled scenario:** a request like "an API for a clinic" names modules but no capabilities, or capabilities that match no confirmed module.
- **Guard:** ask for at least one capability per module, or allow an empty backlog and skip the "start first item" offer.
- **Consequence:** UJ-1's "offer to start the first backlog item" has nothing to offer, or items are attached to the wrong module.
