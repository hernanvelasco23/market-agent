# Vision

## Product name

Market Agent

## One-line summary

An AI-powered market monitoring agent that watches selected assets, detects technical signals, and produces grounded daily briefings.

## Problem

Market participants often have access to raw price dashboards but lack a focused monitoring workflow that explains what changed, why it matters, and what deserves attention next.

Most tools show prices, charts, and generic market data, but they do not provide a curated daily interpretation grounded in deterministic signals and a fixed watchlist.

## Target user

A technically literate self-directed investor or market observer who wants a concise, useful daily briefing instead of manually checking multiple charts and data sources every day.

## MVP scope

The first version of Market Agent will include:

- a fixed watchlist
- daily market data ingestion
- deterministic signal detection
- AI-generated daily briefing
- storage of snapshots and briefing history

## Initial monitored assets

The initial watchlist will focus on a small curated group, such as:

- selected CEDEAR-related assets
- MEP exchange rate
- BTC
- ETH
- one or two reference market tickers such as SPY or QQQ

## Signal philosophy

Signals must be simple, explainable, and reproducible.

The MVP will favor transparency over complexity. It will detect a small set of relevant conditions, such as:

- price above or below EMA 9
- short-term breakout or breakdown
- abnormal movement
- unusual volume when available
- simple volatility expansion or compression

## AI role

The AI layer is responsible for turning structured findings into a useful daily briefing.

AI is used for:

- summarization
- prioritization
- interpretation
- wording
- daily briefing generation

AI is not used to invent facts or generate raw market signals.

## Non-goals

The first version of the project will not include:

- automated trading
- portfolio execution
- high-frequency strategies
- predictive black-box models
- broad macro/news intelligence
- social sentiment analysis
- fully customizable user portfolios

## Success criteria

The MVP will be successful if it can:

- ingest daily prices reliably
- detect a small set of useful and explainable signals
- generate a daily briefing grounded in actual data
- maintain a traceable history of signals and outputs
- be presented as a credible portfolio project for agentic AI and backend engineering

## Long-term direction

Future versions may expand into:

- richer signal libraries
- configurable watchlists
- email or messaging delivery
- dashboards
- historical signal analysis
- news/context enrichment
- user-specific briefing preferences