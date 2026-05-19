# Technical Design

## Overview

This feature extends the existing Market Signal Engine with execution-aware risk calculations.

The system should still behave as a decision-support engine, not an automated trading engine.

## New Concepts

### Relative Strength vs SPY

Calculate:

AssetReturn - SPYReturn

This helps identify assets outperforming or underperforming the broader market.

### ATR-Based Stops

Use ATR14 to calculate dynamic stops.

Suggested rules:

- StrongBullish: Entry - ATR14 * 0.8
- BullishContinuation: Entry - ATR14 * 1.0
- Pullback: Entry - ATR14 * 1.2

If ATR14 is missing, fallback to the existing stop calculation.

### Dynamic Take Profits

Calculate:

- TP1 = Entry + ATR14 * 1.5
- TP2 = Entry + ATR14 * 2.5
- TP3 = Entry + ATR14 * 3.5

Also calculate risk/reward against each target.

### Position Sizing

Optional configuration:

- AccountSize
- RiskPercentPerTrade
- RegimeSizingMultiplier

Formula:

maxRiskAmount = AccountSize * RiskPercentPerTrade * RegimeSizingMultiplier

riskPerShare = Entry - Stop

suggestedPositionSize = maxRiskAmount / riskPerShare

If account size is not configured, position sizing values should be null.

### Regime Sizing Multipliers

- Risk-On: 1.0
- Neutral: 0.5
- Risk-Off: 0.25

If VIX data exists and VIX > 25, multiply final sizing by 0.5.

## Score Breakdown

Each signal should include a scoreBreakdown collection.

Example:

- Above EMA20: +10
- Above EMA50: +15
- Closed near session low: -12
- Relative strength vs SPY: +8

This improves explainability and makes the signal auditable.

## Architecture

Keep Clean Architecture boundaries:

- Domain: models/enums only
- Application: interfaces and use cases
- Infrastructure: calculations/providers/repositories
- API: endpoint wiring only

No business logic should be placed in the API layer.
