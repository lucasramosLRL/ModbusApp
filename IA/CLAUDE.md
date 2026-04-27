# ModbusApp - AI Context

## Project Overview
Modbus communication system in C# for energy metering devices (KRON brand).
Desktop-first, with mobile (MAUI) coming later. Both share `Modbus.Core`.

**Solutions:**
- `Modbus.Core` — shared logic (domain, protocol, transport, services, polling, persistence)
- `Modbus.Desktop` — Avalonia UI (Windows/Linux/macOS)
- `Modbus.Mobile` — MAUI (future)

**Target devices:** KRON KS-3000, KRON Konect 120 (same register map). More models coming.

---

## Current State (as of last session)

### What is fully working
- Modbus TCP and RTU transport
- Background polling engine (`PollingEngine`) polling all active devices every 5s
- Device list with connection status (Connected / Disconnected + last seen)
- Add device flow: scan (RTU broadcast or TCP broadcast) → select result → save
- Real-time electrical readings screen (voltages, currents, power, frequency, power factor)
- SQLite persistence via EF Core (no migrations — uses `EnsureCreated` + manual ALTER TABLE patches)
- Device model seeding (`DeviceModelSeeder`) — idempotent, runs on every startup
- SQPF (float byte order) read dynamically from device register 42.901 (holding reg, FC03, 0-based address 2900) every poll cycle
- Hub navigation: device list → device hub → real-time readings (back chain works)

### Navigation structure
```
DeviceListView → [Open] → DeviceHubView → [Leitura] → DeviceDetailView
                                         → [Memória de Massa] (disabled, future)
                                         → [Configurar] (disabled, future)
```
Navigation is driven by `MainViewModel.CurrentPage`. All VMs fire `NavigationRequested` events
that bubble up through `DeviceListViewModel` to `MainViewModel`.

### Pending / future features
- Mass memory readings
- Register write / configure screen
- Mobile app (MAUI)
- SQPF configuration UI (reading is implemented, writing is not)

---

## Architecture

### Modbus.Core layers
```
Domain/
  Entities/     ModbusDevice, DeviceModel, RegisterDefinition, RegisterValue
  Enums/        TransportType, RegisterType, DataType, WordOrder (BigEndian, LittleEndian, ByteSwapped, UseSqpf)
  Repositories/ IDeviceRepository, IDeviceModelRepository, IRegisterValueRepository
  ValueObjects/ TcpConfig, RtuConfig

Protocol/       ModbusProtocol — builds/parses Modbus frames (RTU/TCP)
Transport/
  Tcp/          TcpModbusTransport
  Rtu/          RtuModbusTransport

Services/
  IModbusService, ModbusServiceFactory
  RegisterDecoder — decodes raw Modbus words to double; handles SQPF via DecodeFloat32WithSqpf()
  Scanning/     IDeviceScanService, DeviceScanService — RTU broadcast + TCP UDP broadcast

Polling/
  IPollingEngine, PollingEngine — timed loop, RTU semaphore gate, 4s per-device timeout
  Events: RegisterValuesUpdated, DeviceConnectionFailed

Persistence/
  ModbusDbContext (SQLite, EF Core, EnsureCreated)
  Repositories/ — EF implementations
  DeviceModelSeeder — seeds register maps for known models on every startup
  Configurations/ — EF fluent configs; WordOrder stored as string
```

### Modbus.Desktop layers
```
ViewModels/
  MainViewModel         — root, owns CurrentPage, wires NavigationRequested from children
  DeviceListViewModel   — device list, polling status updates, navigation hub
  DeviceHubViewModel    — per-device hub (new screen, launched from device list)
  DeviceDetailViewModel — real-time readings; parent is Action onGoBack (not DeviceListViewModel)
  AddDeviceViewModel    — add device wizard (scan + manual form)
  SettingsViewModel     — RTU port settings
  DeviceItemViewModel   — device row; exposes IsConnected, StatusText, LastSeenText, ErrorMessage

Views/
  MainWindow       — sidebar (220px) + ContentControl bound to CurrentPage
  DeviceListView   — list of DeviceItemViewModels
  DeviceHubView    — 3 feature cards (readings active, others disabled)
  DeviceDetailView — tabs: real-time readings grid + raw registers DataGrid
  AddDeviceView    — scan + form
  SettingsView     — COM port, baud rate, etc.

Services/
  LocalizationService   — string dictionary; keys in PortugueseStrings.cs + EnglishStrings.cs
  RtuSettingsService    — singleton, persists RTU port config
```

### View resolution
`App.axaml` has explicit `DataTemplate` entries mapping each ViewModel type to its View.
`MainWindow` just has `<ContentControl Content="{Binding CurrentPage}" />`.

---

## Key Technical Decisions

### TCP Unit ID = 255
KS-3000 over TCP requires Modbus Unit ID **255** (not 1).
All TCP paths enforce this: scan service, add device defaults, scan result selection.

### SQPF (Sequência do Ponto Flutuante)
Float32 byte order is configurable on the device via holding register **42.901**
(FC03, 0-based Modbus address = **2900**).

- `RegisterDefinition.WordOrder = UseSqpf` marks Float32 input registers as SQPF-dependent
- `UInt32` registers (e.g., NS serial number) use `WordOrder.ByteSwapped` directly — exempt from SQPF
- Holding registers never use SQPF
- `PollingEngine` reads register 2900 via `ReadHoldingRegistersAsync` once per poll cycle
- If the read fails (exception), falls back silently to `0x3210` (Padrão KRON = ByteSwapped)
- `RegisterDecoder.DecodeFloat32WithSqpf(words, sqpfValue, scale)` uses the raw SQPF value
  as a byte-permutation table: nibble i = IEEE 754 float byte index at transmitted position i

**SQPF nibble convention (confirmed working):**
`raw |= t[i] << (floatByteIdx * 8)` where `floatByteIdx = (sqpfValue >> (i*4)) & 0xF`

Known values:
| SQPF value | Byte order | Description |
|------------|------------|-------------|
| `0x3210`   | F2,F1,F0,EXP (DCBA) | Padrão KRON (default) |
| `0x2301`   | F1,F2,EXP,F0 (CDAB) | Float padrão |
| `0x0123`   | EXP,F0,F1,F2 (ABCD) | Float inverso (IEEE 754 big-endian) |

### RTU polling gate
`PollingEngine` has a `SemaphoreSlim _rtuGate` that serializes RTU access.
During device scan, `DeviceListViewModel.SuspendRtuPollingAsync()` must be called before scanning
and `ResumeRtuPolling()` after. This prevents port conflicts between polling and scanning.

### RTU reconnect-per-poll
For RTU devices, the engine disconnects after every poll to release the COM port between cycles.
For TCP, it also reconnects each poll (simpler; avoids stale connection detection issues).

### EF / Database
- SQLite at `%LocalAppData%\ModbusApp\modbusapp.db`
- `EnsureCreated()` — no migrations; schema changes require manual `ALTER TABLE` patches in `App.axaml.cs`
- `WordOrder` stored as **string** (HasConversion<string>()) — "UseSqpf", "ByteSwapped", etc.
- `TcpConfig` and `RtuConfig` are owned entities (EF `OwnsOne`) — loaded automatically, no Include needed
- `DeviceModel.SqpfRegisterAddress` added via patch: `ALTER TABLE DeviceModels ADD COLUMN SqpfRegisterAddress INTEGER`

### DeviceModelSeeder
Runs on every startup. For each known model:
- Sets `SqpfRegisterAddress = 2900`
- If registers exist: updates Float32 input registers to `WordOrder.UseSqpf` via `ApplySqpfToExistingRegisters`
- If no registers: seeds full register list with `UseSqpf` on Float32 inputs

Known models: **KS-3000** (0xF2), **Konect 120** (0xF3). Both share the same `RealTimeRegs()` register map.
`RealTimeRegs()` helper accepts `WordOrder` parameter (default `ByteSwapped`).
Float32 registers pass `WordOrder.UseSqpf`; NS (UInt32) uses `WordOrder.ByteSwapped`.

---

## Register Map (KS-3000 / Konect 120)

All are FC04 (Input Registers), 0-based addresses:

| Address | Name | Type | Unit | Description |
|---------|------|------|------|-------------|
| 0 | NS | UInt32 | — | Serial Number |
| 2 | U0 | Float32 | V | Three-phase Voltage |
| 4–8 | U12,U23,U31 | Float32 | V | Phase Voltages |
| 10–14 | U1,U2,U3 | Float32 | V | Line Voltages |
| 16 | I0 | Float32 | A | Three-phase Current |
| 20–24 | I1,I2,I3 | Float32 | A | Line Currents |
| 26 | Freq | Float32 | Hz | Frequency |
| 34–40 | P0,P1,P2,P3 | Float32 | W | Active Power |
| 42–48 | Q0,Q1,Q2,Q3 | Float32 | VAr | Reactive Power |
| 50–56 | S0,S1,S2,S3 | Float32 | VA | Apparent Power |
| 58–64 | FP0,FP1,FP2,FP3 | Float32 | — | Power Factor |

SQPF config: Holding register 42.901 → FC03, 0-based address **2900**

---

## Localization
`LocalizationService` — singleton, dictionary-based.
String files: `Modbus.Desktop/Services/Strings/PortugueseStrings.cs` and `EnglishStrings.cs`.
Usage in XAML: `{Binding [KeyName], Source={x:Static svc:LocalizationService.Instance}}`
