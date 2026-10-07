# Trade Hold Timer — V1 implementation notes

## Objective

deepTradeHolder is a DeepCharts desktop indicator that measures how long currently active futures entries have been observed open and makes a configurable minimum-hold threshold obvious during fast scalping.

Default thresholds:

- Minimum hold: **15 s**
- Safety buffer: **+2 s**
- SAFE threshold: **17 s**

The indicator is a timing aid only. It does not claim that any prop-firm rule is guaranteed satisfied.

## Tested DeepCharts API surface

The installed DeepCharts build exposes:

```text
TradingApi : VolSysAPI.Trading.ITradingAPI

PositionNetQuantity
PositionBuyQuantity
PositionSellQuantity

GetAllOrdersStatus()
GetOrderInfo(orderId)
```

`OrderStatusInfo` exposes order IDs, pending quantity, filled quantity, order type and order status, but the inspected API does not expose enough information to map each currently open futures lot to a unique side + original fill timestamp.

Therefore V1 uses changes in `PositionNetQuantity` as the authoritative position lifecycle signal.

## Active-entry model

Observed transitions are interpreted as follows:

```text
0 → nonzero                 new position / first active entry
same side, abs qty grows    new scale-in entry with independent timer
same side, abs qty shrinks  partial reduction
nonzero → 0                 fully flat
positive ↔ negative         reversal; old entries cleared, new side starts
```

Each increase in absolute quantity creates an independent active-entry timer.

Example:

```text
LONG 1   00:31.4   SAFE
LONG 1   00:18.2   SAFE
LONG 1   00:06.7   HOLD
```

## Partial exits

DeepCharts reports the aggregate quantity change, for example `3 → 2`, but the inspected API does not identify which original entry was closed.

V1 uses **FIFO matching** for the displayed active-entry list.

This is not intended to reconstruct broker accounting. Its purpose is to maintain a deterministic list of active timers while the primary decision signal remains the overall position status.

## Overall safety status

The global state is based on the youngest active entry:

1. **POSITION: NOT ALL SAFE** — youngest entry is below the minimum.
2. **POSITION: MIN REACHED** — youngest entry passed the minimum but not the safety buffer.
3. **POSITION: ALL SAFE** — youngest entry passed minimum + safety buffer.
4. **POSITION: WAITING** — flat.

Because every other active entry is older than the youngest one, `ALL SAFE` means all tracked active entries have crossed the configured safe threshold.

## Individual timer states

Each visible entry row shows:

- side,
- quantity attributed to that observed entry,
- elapsed time,
- status.

States:

- `HOLD`
- `MIN REACHED`
- `SAFE`

## Alert

V1 includes an optional DeepCharts sound alert when the overall position first reaches `ALL SAFE`.

The alert is one-shot:

- it does not repeat while the position remains `ALL SAFE`,
- adding a new entry moves the overall state out of `ALL SAFE` and resets the alert,
- it can fire again when the enlarged position becomes `ALL SAFE`.

## Panel

The tested V1 panel supports:

- `TopLeft`
- `TopRight`
- `BottomLeft`
- `BottomRight`
- configurable font size
- configurable maximum visible rows

When there are more tracked entries than `Visible rows`, the newest rows are shown.

The official indicator documentation exposes annotations (`Text`, `Line`, `Rectangle`, etc.) but does not document mouse/drag callbacks or a scroll-container UI control. Draggable placement and an interactive scrollbar are therefore **not V1 requirements**.

## Important timing limitation

If the indicator is already running while flat, a transition from `0 → nonzero` is observed when DeepCharts reports the filled position and the timer starts at that observed transition.

If the indicator is loaded while a position is already open, the original fill timestamp is unavailable through the inspected API surface. V1 starts that reconstructed entry at the first moment it observes the existing position.

## Validation completed on SIM

The following scenarios have been manually tested successfully:

- flat state,
- first LONG/SHORT entry,
- timer counting,
- `HOLD → MIN REACHED → SAFE`,
- multiple scale-in entries with separate timers,
- overall youngest-entry safety state,
- partial reduction,
- full flat,
- new trade after flat,
- reversal,
- one-shot `ALL SAFE` sound alert.

## Desktop-only status

DeepCharts marks `TradingApi` and all `ITradingAPI` members as desktop-only. deepTradeHolder is therefore a desktop-calculated indicator.

SIM behavior has been validated. External/prop account behavior must be verified on the specific account connection rather than assumed from SIM behavior.

## Future candidates

Possible later improvements, only if supported reliably by the API and useful in real trading:

- configurable row sorting,
- brief recently-closed entry display,
- more appearance settings,
- additional alerts,
- direct execution timestamps if DeepCharts exposes them in a future API,
- better per-entry exit matching if unique open-lot identity becomes available.
