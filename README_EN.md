# SMTAlert - EVE Online Alert Radar

A standalone WPF desktop application extracted from [SMT (Slazanger's Eve Map Tool)](https://github.com/Slazanger/SMT), focused on providing an alert radar overlay and ZKB kill feed for EVE Online.

## Features

- **Alert Radar Overlay** — Borderless transparent star map showing nearby systems around your location, with real-time warning/clear/stale indicators
- **ZKB Kill Feed** — Real-time kill data for your region, color-coded by standings
- **ESI Character Management** — Independent ESI SSO authorization supporting multiple characters and alliances
- **Intel Channel Monitor** — Monitors in-game intel channels, parses system names, and plays alert sounds
- **Bilingual (EN/ZH)** — Runtime UI language switching with ship name translation

## Build

### Dependencies

| Dependency | Description |
|------|------|
| .NET 8.0 SDK | Build framework |
| `EVEData` project | Data layer from main SMT project: star map, BFS navigation, ZKB engine, ESI utilities |
| NAudio 2.2.1 | Alert sound playback |
| Newtonsoft.Json 13.0.4 | JSON serialization |
| System.Configuration.ConfigurationManager 8.0.0 | Configuration management |

### Build Steps

1. Clone the main SMT project:
   ```
   git clone https://github.com/Slazanger/SMT.git
   ```

2. Clone this repository into the `SMTAlert` subdirectory:
   ```
   cd SMT
   git clone https://github.com/yuruichang/SMTAlert.git SMTAlert
   ```

3. Open `SMTAlert/SMTAlert.sln` in Visual Studio or build via CLI:
   ```
   dotnet build SMTAlert/SMTAlert.sln --configuration Release
   ```

4. Output is at `SMTAlert/bin/x64/Release/`. Run `SMTAlert.exe`.

**Note:** This project references the EVEData project (`..\EVEData\EVEData.csproj`) and must be placed as a sibling directory to the SMT main project to compile.

## Usage

1. On first launch, add a character via "Add Character" — authenticates through EVE SSO
2. After authorization, character location is automatically tracked; set alert range (1-10 jumps)
3. Click "Open Radar Overlay" to display the star map radar
4. Click "Open ZKB Monitor" for real-time kill data
5. Configure intel channel name and clear keywords in Settings

## Screenshots

![Main Window](images/main_window.png)
![Alert Radar](images/overlay.png)
![ZKB Monitor](images/zkb_monitor.png)
![Settings](images/settings.png)

## License

Based on the original SMT project (MIT License). Modifications and additions in SMTAlert are also released under the MIT License.

## Credits

- [Slazanger/SMT](https://github.com/Slazanger/SMT) — Original project
- [EVE Online](https://www.eveonline.com/) — CCP Games
- [zkillboard.com](https://zkillboard.com/) — Kill data
- [EVEStandard](https://github.com/gehnge/EVEStandard) — ESI API library
