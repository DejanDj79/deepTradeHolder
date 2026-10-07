# Trade Hold Timer — V1 specification

## Objective

Build a DeepCharts C# indicator that automatically measures the elapsed time of active trades and makes minimum-hold compliance obvious during fast scalping.

The first personal use case is a prop-firm account with a 15-second hold rule. The timing values must remain configurable so the indicator is not tied to one firm.

## Core timing rules

- Start a timer on the **actual first execution/fill**, never when the order is submitted.
- Keep the timer running while the relevant position remains open.
- A scale-in/add does **not** reset the original timer.
- A partial exit does **not** stop the timer.
- Stop the timer only when the position becomes fully flat.
- A Long → Short or Short → Long reversal closes the old trade timer and starts a new one.
- Prefer an execution/fill timestamp exposed by DeepCharts over local order-submission time.

Default thresholds:

- Minimum hold: **15 s**
- Safety buffer: **+2 s**
- SAFE threshold: **17 s**

The indicator measures elapsed time only; it must not claim that a prop-firm rule is guaranteed satisfied.

## Timer states

1. **Below minimum** — show elapsed time and remaining seconds.
2. **MIN REACHED** — minimum threshold has passed.
3. **SAFE** — configured safety buffer has also passed.

## Multi-trade panel

Use one compact table-style overlay rather than one floating card per trade.

Suggested columns:

```text
# | SIDE | SYMBOL | QTY | ELAPSED | PROGRESS | STATUS
```

Optional later columns:

- adds/fills,
- entry time.

The panel must have a fixed height. Initial visible-row choices:

- 5,
- 10,
- 15.

Default: **5**.

When more trades exist than fit in the configured row count, use an internal vertical scrollbar / mouse-wheel scrolling. The chart must not keep expanding.

## Visual direction

- Square/straight corners — **no rounded cards**.
- Compact professional trading-tool appearance.
- Thin configurable border.
- High information density.
- Header example: `TRADE HOLD TIMER (8)`.
- Thin progress bar per active trade.
- Panel must remain readable on a dark chart.

## Positioning

The whole panel should be draggable anywhere on the chart if the API allows pointer/drag handling.

Initial-position choices should eventually include:

- Top Left
- Top Right
- Bottom Left
- Bottom Right
- Center Left
- Center Right
- Custom

If supported, add **Remember last position** so manual placement overrides the initial-position preset after first use.

## User-configurable appearance

Planned settings:

- Background color
- Border color / thickness
- Text color
- Header background / text
- Long / Buy color
- Short / Sell color
- Warning color
- Minimum-reached color
- Safe color
- Progress background / fill
- Font size
- Row height
- Panel width
- Visible rows

## Sorting

Planned options:

- Newest first
- Oldest first
- Least time remaining first
- Longest open first

This should be configurable after we test which ordering is most useful during actual scalping.

## Closed trades

Default proposal: keep a just-closed row visible for about **2 seconds** with:

```text
CLOSED · 00:24.8
```

Then remove it from the active panel.

Full history belongs in a later version.

## Alerts

Optional:

- alert at minimum threshold,
- alert at SAFE threshold.

Default can remain OFF until the first real-use tests.

## Required DeepCharts API capability

We need read access to trading state, ideally:

- account,
- current position,
- symbol,
- side,
- quantity,
- fills/executions,
- execution timestamp,
- position-change events or equivalent.

The exact class/event names are not important. The required behavior is the ability to reliably detect:

```text
Flat → Long
Flat → Short
Scale-in
Partial exit
Full exit
Long → Short reversal
Short → Long reversal
```

## API validation sequence

Before building the final UI, test the installed DeepCharts API in this order:

1. Verify the indicator DLL loads under **Indicators → Personal**.
2. Verify a chart overlay renders.
3. Inspect/use `TradingApi : ITradingAPI`.
4. Display current SIM position, side and quantity.
5. Detect first fill / open.
6. Detect scale-in.
7. Detect partial exit.
8. Detect full close.
9. Detect reversal.
10. Confirm timestamp precision.
11. Only then build the full multi-row panel.

## Account limitation currently known

DeepCharts stated during the October 2026 live session that live-account access is planned later; current trading API functionality is focused on SIM/backtesting.

A prop-firm account may itself be simulated economically, but we must **test how DeepCharts classifies/exposes that external account** rather than assume it behaves like the built-in SIM account.

Development can proceed and be fully tested against DeepCharts SIM even if the prop account is not yet visible.

## V1 priorities

### Must have

- automatic open/close timer,
- scale-in/partial-exit/reversal handling,
- configurable minimum and buffer,
- multiple simultaneous timers,
- fixed-height panel,
- scrolling,
- 5/10/15 visible-row configuration,
- movable panel,
- configurable initial position,
- square corners,
- configurable colors,
- clear below-minimum / MIN REACHED / SAFE state.

### Nice to have

- sorting,
- optional columns,
- sound alerts,
- remembered panel position,
- brief closed-row confirmation,
- adds/fills count.

### Later

- session trade history,
- prop-firm compliance percentages,
- profit-based compliance,
- prop-firm presets,
- marketplace/server-hosted commercial version.

## Current implementation phase

Phase 1 is intentionally minimal:

- official DeepCharts project structure,
- stable assembly/namespace,
- indicator registration,
- basic settings,
- fixed text overlay proving the SDK/DLL is loaded.

Do not redesign the concept after this phase. Once the DLL loads successfully, continue with `TradingApi` validation before investing time in the final panel renderer.
