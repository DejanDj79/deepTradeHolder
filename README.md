# deepTradeHolder

DeepCharts C# custom indicator for monitoring how long active trades have been open.

## Current status

The project is in the first SDK-integration phase. The initial goal is to verify that the DLL:

1. builds against the official DeepCharts indicator API,
2. is copied to `Documents\Deepchart\Indicators`,
3. appears in **Indicators → Personal** as **Trade Hold Timer**,
4. renders a small fixed overlay on the chart.

Trading-position/fill integration will be added after we verify the currently available `ITradingAPI` surface in the installed DeepCharts build.

## Requirements

- DeepCharts desktop
- .NET 10 SDK
- DeepCharts installed by default in:
  `C:\Program Files\Volumetrica Trading\Deepchart\`

## Build

The Developer ID is intentionally **not committed** to this public repository.

From the repository root:

```powershell
dotnet build src/deepTradeHolder.csproj -p:DeepchartDevId=YOUR-DEVELOPER-ID
```

If DeepCharts is installed somewhere else:

```powershell
dotnet build src/deepTradeHolder.csproj `
  -p:DeepchartDevId=YOUR-DEVELOPER-ID `
  -p:DeepchartDir="D:\Path\To\Deepchart\"
```

By default the resulting DLL is written to:

```text
%USERPROFILE%\Documents\Deepchart\Indicators\
```

Then open/restart DeepCharts and look under **Indicators → Personal**.

## V1 direction

The planned V1 is a compact, movable, square-corner panel that can show multiple concurrent trade timers. Main behavior:

- timer starts on the actual first fill,
- scale-in does not reset the original timer,
- partial exit does not stop the timer,
- timer stops when the position is fully flat,
- a reversal closes the old timer and starts a new one,
- configurable minimum hold and safety buffer,
- fixed panel height with 5/10/15 visible rows and internal scrolling,
- user-selectable colors and initial chart position.

See [docs/TradeHoldTimer-V1.md](docs/TradeHoldTimer-V1.md) for the working specification.

## Source

The project structure follows Volumetrica Trading's official DeepCharts indicator SDK/examples:
https://github.com/VolumetricaTrading/deepchart-indicators-api
