# ADR-019 — Naming, reconciled against the expanded scope

Status: Accepted — **the founder decided: keep SeniorConnect** (2026-09)
Date: 2026-08 (decided 2026-09)

> **Decision (2026-09):** keep **SeniorConnect**. The actual codebase (backend
> namespaces, module names, database, Docker network, Flutter package id) was
> already built under this name across nine implementation phases by the
> time this ADR was revisited. Renaming now would mean touching every
> namespace, the database name, the Flutter bundle id, and every stored
> build artifact for a naming preference that was never load-bearing on the
> product's actual audience or trust architecture. Per this ADR's own
> analysis below, "Mitanand"/"Miteinander" remains the better brand fit on
> paper — that reasoning is preserved as-is for a future rebrand exercise,
> but it does not override the cost of renaming a working, tested codebase
> today. See `PHASE-AUDIT-2026-08.md` step 1, which first flagged this as
> unresolved and urgent.

## Context

Two separate naming inputs now exist:

1. `docs/design/brand.md` recommended **Mitanand** — the Austrian dialect
   form of *miteinander* ("with one another") — reasoning that it reads as
   warm, local, and does not label the user by age or need.
2. A fresh brainstorm (this document's trigger) proposes 19 candidates for
   the same reason — the product outgrew "SeniorConnect" — and independently
   arrives at **Miteinander** as its own #1 pick, with **Dazugehören**,
   **NahDabei**, **WirDa**, **Brücken**, **Gemeinsam** and **LebensBrücke**
   as runners-up.

Separately, `docs/plans/PHASE-AUDIT-2026-08.md` found that the actual
codebase was built under **SeniorConnect** — the exact working title
`brand.md` said to avoid — which is a third, unresolved thread that exists
regardless of which of the above wins.

## The two lists agree more than they disagree

**Mitanand *is* Miteinander.** It is the same word, in the dialect that
reads as Austrian rather than as standard-German-import. The new
brainstorm's #1 pick and the existing recommendation are the same concept
wearing two different registers. This is worth stating plainly because it
means the scope expansion does not actually argue against the original
naming direction — if anything, it confirms it landed on the right concept
independently.

## Evaluation of the new candidates against the expanded audience

| Name | Works for seniors | Works for a newcomer | Doesn't imply care/frailty | Note |
| --- | --- | --- | --- | --- |
| **Mitanand** | ✓ | ✓ | ✓ | Dialect warmth; slightly less legible to a non-Austrian newcomer's first German lesson than standard `Miteinander` |
| **Miteinander** | ✓ | ✓ | ✓ | Standard German — universally readable, less distinctively Austrian; likely harder to trademark (very common word, checked nowhere yet by either of us) |
| **Dazugehören** | ✓ | ✓✓ | ✓ | *Belonging* — arguably the single best concept match for a newcomer's actual emotional need. Four syllables, harder to say on the phone than the others. |
| **NahDabei** | ✓ | ✓ | ✓ | Neutral, geographic ("nearby and in the loop"), a touch generic/corporate-sounding next to the others |
| **WirDa** | ✓ | ✓ | ✓ | Short, modern, a little slang-forward — test it with actual seniors before betting on it (P0-03) |
| **Brücken** | ✓ | ✓✓ | ✓ | Strong *metaphor* for the platform's literal function (senior↔volunteer, migrant↔community); plural noun as a product name reads slightly abstract |
| **Gemeinsam** | ✓ | ✓ | ✓ | Very common word (adverb "together"), likely the hardest of the set to trademark distinctively |

None of these fail the "does not label the user" test that ruled out
SeniorConnect. That test is passed by the whole shortlist now — the earlier
naming work already did the hard filtering.

## Recommendation

**Keep Mitanand**, for three reasons specific to the expanded scope, not just
the original one:

1. It already tested well against every one of `brand.md`'s original
   criteria, and the expanded audience doesn't change those criteria — it
   confirms the same concept (*miteinander*) is still the right one.
2. Of the whole combined list, **Dazugehören** is the only genuinely
   stronger conceptual fit *specifically for the newcomer audience* — but it
   is four syllables and harder to say clearly on a phone call to a 78-year-old
   or dictate to a Gemeindeamt clerk. Mitanand is three, unambiguous, and
   already the working name in every document in this repository.
3. Re-deciding the name a third time (SeniorConnect → Mitanand →
   something-else) costs real calendar time for zero product value, and the
   audit already flagged one unresolved rename debt. Compounding it is worse
   than picking.

**If you want the newcomer-belonging concept represented anywhere, use it in
the tagline, not the name**: `Mitanand — Niemand bleibt allein.` borrows
`Dazugehören`'s emotional core (*"nobody stays alone"*) without paying its
pronounceability cost.

## What this ADR does NOT resolve

- The **SeniorConnect → Mitanand rename in the actual codebase** is a
  separate, already-flagged action item (`PHASE-AUDIT-2026-08.md` §5). This
  ADR does not make that any more or less urgent — do it once, in one pass,
  regardless of which name wins.
- Domain (`mitanand.at`), trademark (Österreichisches Patentamt, Klasse 9/42/45),
  and App Store / Play Store collision checks are still **not done** by
  anyone, for any candidate name, including Mitanand. This remains a
  before-Phase-7-legal-gate action item, not an engineering one.

## Alternatives considered

Every name in the brainstorm's table was screened against the three
`brand.md` criteria (age-neutral, Austrian-legible, non-clinical). All pass.
The choice among the survivors is a judgment call on pronounceability vs.
conceptual purity, not a right-or-wrong engineering decision — hence
"Proposed," not "Accepted." Overrule this ADR by editing it directly if you
land somewhere else; don't let the name drift silently a third time.
