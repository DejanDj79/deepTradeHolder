# deepTradeHolder

A DeepCharts C# custom indicator for monitoring the age of active futures entries and making a configurable minimum-hold threshold obvious during fast trading.

## Current status

The V1 trading logic has been implemented and tested on DeepCharts SIM:

- reads the current position through `TradingApi : ITradingAPI`,
- detects Flat → Long and Flat → Short,
- creates a separate timer when absolute net position quantity increases,
- keeps existing timers running during scale-in,
- handles partial reductions,
- clears timers when the position becomes flat,
- resets correctly on Long ↔ Short reversal,
- shows `HOLD`, `MIN REACHED`, and `SAFE`,
- shows an overall `POSITION: NOT ALL SAFE / MIN REACHED / ALL SAFE` state,
- can play a one-shot sound when the whole active position first becomes `ALL SAFE`.

Default timing is **15 seconds minimum + 2 seconds safety buffer = SAFE at 17 seconds**.

The indicator is a timing aid. It does not guarantee compliance with any prop-firm rule.

## How the multi-entry timer works

DeepCharts currently exposes aggregate position quantities:

- `PositionNetQuantity`
- `PositionBuyQuantity`
- `PositionSellQuantity`

It does not expose the identity or original fill timestamp of each currently open futures lot through the inspected `ITradingAPI` surface.

For that reason, deepTradeHolder reconstructs active entries from changes in net quantity:

```text
FLAT → LONG 1       = create entry timer #1
LONG 1 → LONG 2     = create entry timer #2
LONG 2 → LONG 3     = create entry timer #3
LONG 3 → LONG 2     = partial reduction
LONG 2 → FLAT       = clear active timers
LONG → SHORT        = clear old side and start the new side
```

Each increase in absolute position quantity gets its own start time.

For partial reductions, the API does not identify which original entry was closed. The indicator therefore removes displayed entry quantity using **FIFO**. This affects only which individual timer row remains visible after a partial reduction; the primary safety signal is the overall status.

## Overall position status

The overall status is based on the **youngest active entry**:

- younger than the minimum → `POSITION: NOT ALL SAFE`
- minimum reached but still inside the safety buffer → `POSITION: MIN REACHED`
- youngest entry past minimum + buffer → `POSITION: ALL SAFE`

If the youngest active entry is SAFE, every older active entry is also SAFE.

## Settings

### Timing

- **Minimum hold (seconds)** — default `15`
- **Safety buffer (seconds)** — default `2`

### Alerts

- **Enable ALL SAFE alert**
- **Alert sound** — uses the sounds configured in DeepCharts

The sound fires once when the overall status enters `ALL SAFE`. Adding a new entry resets the alert state so it can fire again when the enlarged position becomes fully safe.

### Layout

- **Visible rows** — maximum number of active entry timers displayed; if there are more, the newest rows are shown
- **Panel position** — `TopLeft`, `TopRight`, `BottomLeft`, `BottomRight`
- **Font size**

## Requirements

- DeepCharts desktop
- .NET 10 SDK
- DeepCharts installed by default in:
  `C:\Program Files\Volumetrica Trading\Deepchart\`

The project references the DeepCharts `VolSysAPI.dll` and `VolumetricaCore.dll`.

## Build

The Developer ID is intentionally **not committed** to this public repository.

From the repository root:

```powershell
dotnet build src\deepTradeHolder.csproj -p:DeepchartDevId=YOUR-DEVELOPER-ID
```

If DeepCharts is installed somewhere else:

```powershell
dotnet build src\deepTradeHolder.csproj `
  -p:DeepchartDevId=YOUR-DEVELOPER-ID `
  -p:DeepchartDir="D:\Path\To\Deepchart\"
```

On systems where Documents is redirected to OneDrive, this project prefers the corresponding OneDrive DeepCharts Indicators folder. Otherwise it falls back to the normal Documents path.

After building, reload/restart DeepCharts and look under **Indicators → Personal → Trade Hold Timer**.

## Known limitations

- Trading integration is desktop-only because DeepCharts marks `TradingApi` / `ITradingAPI` as desktop-only.
- The inspected API does not expose individual open-lot identity or original execution timestamps.
- If the indicator is added while a position is already open, its timer starts when that position is first observed by the indicator, not at the historical fill time.
- Partial reductions cannot be matched to a specific original entry, so FIFO is used for the displayed entry list.
- The current documented indicator API does not expose mouse/drag events or a real scroll-container control. The V1 therefore uses corner-position presets and displays the newest `Visible rows`.

## Source

The project structure follows Volumetrica Trading's official DeepCharts indicator SDK/examples:

https://github.com/VolumetricaTrading/deepchart-indicators-api

See [docs/TradeHoldTimer-V1.md](docs/TradeHoldTimer-V1.md) for implementation notes and tested behavior.
