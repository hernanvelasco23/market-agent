# Score Calibration and Normalization

## Problem

MarketAgent scores are compressed near the top of the range. Many strong momentum signals, especially `MomentumContinuation`, reach `95-100`, which makes it harder to separate the best candidates from merely good candidates.

Score attribution diagnostics showed:

- many additive positive factors
- few penalties
- weak separation between top candidates
- score saturation/clamping

Examples:

- `NVDA uncappedScore 108.29 -> finalScore 100`
- `V uncappedScore 99.24 -> finalScore 99.24`

The current score still conveys strength, but once signals saturate near `100`, ranking and alert thresholds become less informative.

## Goal

Improve score separation quality without rewriting the signal engine.

V1 should add a calibration/normalization layer after existing raw score calculation:

- reduce score saturation near `100`
- improve dispersion between top candidates
- preserve relative ranking quality
- preserve explainability and score attribution
- avoid large scoring regressions
- keep the change reversible

## V1 Scope

- Backend only.
- No frontend changes required.
- Keep existing scoring factors.
- Do not remove current score contributions.
- Add normalization/calibration after raw score calculation.
- Preserve raw and calibrated values for diagnostics.
- Keep score attribution compatible.

## Suggested Model Additions

Persist or expose:

- `RawScore`
- `CalibratedScore`
- `CalibrationReason`
- `WasNormalized`

Diagnostics:

- score distribution before/after calibration
- capped score count
- average calibrated score
- top-score dispersion

## Calibration Approaches to Evaluate

### Soft Cap Normalization

Compress scores above a threshold, such as `85`, without changing lower scores much.

Example:

```text
if rawScore <= 85: calibrated = rawScore
if rawScore > 85: calibrated = 85 + (rawScore - 85) * 0.55
```

Pros:

- Simple and explainable.
- Reduces score saturation.
- Low risk for lower-quality signals.
- Easy rollback.

Cons:

- Still manually tuned.
- Does not address why factors stack.

### Logistic Compression

Use an S-shaped transform to compress extremes.

Pros:

- Smooth.
- Strongly reduces top-end compression.

Cons:

- Harder to explain.
- May surprise users because score changes are less intuitive.

### Dynamic Scaling

Normalize scores within a run based on observed distribution.

Pros:

- Improves separation within the current watchlist/run.
- Adapts to market regimes.

Cons:

- Same signal could get different calibrated scores depending on peer group.
- More difficult for alerts and historical comparisons.

### Diminishing Returns for Stacked Positive Factors

Keep all factors, but reduce the marginal value of additional positive points after a threshold.

Pros:

- Addresses score inflation source.
- Keeps explainability.

Cons:

- More invasive than a final calibration layer.
- Requires careful tests to avoid changing factor semantics.

### Momentum Overheating Penalties

Add or increase penalties when momentum is too extended, RSI is elevated, or price is far from EMA.

Pros:

- Domain-specific.
- Can improve signal quality.

Cons:

- Changes scoring behavior more directly.
- May suppress legitimate breakout signals.

## V1 Recommendation

Start with soft cap normalization after existing raw score calculation.

Reasons:

- Minimal and additive.
- Easy to explain.
- Keeps current score factors.
- Reduces top-end saturation without rewriting analyzer logic.
- Can preserve raw score for attribution, diagnostics, and rollback.

## Success Criteria

- Raw score is still available.
- Calibrated score is available.
- Score attribution can show both raw/uncapped and calibrated/final score.
- Fewer signals are compressed at exactly `100`.
- Top candidates have better score dispersion.
- Existing alert logic does not break unexpectedly.
- Existing tests keep passing.

## Out of Scope

- No frontend changes.
- No alert threshold change in V1 unless explicitly approved later.
- No removal of existing scoring factors.
- No complete analyzer rewrite.
- No ML calibration.
- No dynamic per-user scoring profile.

## Risks

- Alert thresholds based on score may become too strict if they use calibrated score.
- Users may compare new calibrated scores against old raw scores.
- Soft cap parameters may need tuning after seeing real distributions.
- Preserving both raw and calibrated scores may introduce naming confusion.

## Rollback Plan

- Disable calibration and return current final score behavior.
- Keep raw score persistence if useful, or remove calibration fields in a migration rollback.
- Remove calibration service/helper and tests.
- Leave existing score factors, attribution, outcomes, alerts, and dashboard untouched.
