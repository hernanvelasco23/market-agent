# Dynamic Risk & Position Engine

## Goal

Improve MarketAgent signals by adding execution-aware risk management.

The current signal engine detects opportunities and risks, but stop loss, take profit and position sizing are still too static.

This feature adds:
- ATR-based dynamic stops
- dynamic take-profit levels
- risk/reward calculation
- position sizing based on account risk
- real relative strength vs SPY
- score breakdown explainability

## Business Value

This makes MarketAgent more useful as a decision-support engine.

It helps answer:
- How risky is this setup?
- Where could the stop be placed?
- What are realistic profit targets?
- How much capital should be allocated?
- Why did the signal receive this score?

## Why It Matters

Different assets have different volatility.

A fixed stop or target does not make sense for:
- MSFT
- TSLA
- NVDA
- BTC
- MELI

Using ATR makes risk management more adaptive and realistic.

## Expected Outcome

The briefing should become more actionable and explainable, without becoming an automated trading bot.
