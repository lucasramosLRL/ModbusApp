# ModbusApp - AI Context

## Project Overview
Modbus communication system in C# for energy metering devices (KRON brand).
Desktop-first, with mobile (MAUI) coming later. Both share `Modbus.Core`.

**Solutions:**
- `Modbus.Core` — shared logic (domain, protocol, transport, services, polling, persistence)
- `Modbus.Desktop` — Avalonia UI (Windows/Linux/macOS)
- `Modbus.Core.Tests` — xUnit test project covering Core logic
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
- SQLite persistence via EF Core Migrations (`db.Database.Migrate()` on startup, with legacy-DB baselining for clients upgrading from pre-migration builds)
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

### Test coverage (Phases 1-2 complete)
- `Modbus.Core.Tests` project — xUnit + FluentAssertions + NSubstitute
- 97 tests passing — covers RegisterDecoder, Crc16, RTU/TCP frame builders and parsers
- `InternalsVisibleTo("Modbus.Core.Tests")` configured in `Modbus.Core.csproj`
- Phase 3 (PollingEngine + DeviceModelSeeder with mocks) and Phase 4 (EF Core repos with in-memory SQLite) — not yet implemented

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
  ModbusDbContext (SQLite, EF Core Migrations)
  DatabaseInitializer — startup entry: baselines legacy DBs then runs Migrate()
  DesignTimeDbContextFactory — lets `dotnet ef` instantiate the context without the UI host
  Migrations/ — EF-generated migration files (InitialSchema is the baseline)
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
- **EF Core Migrations** — `DatabaseInitializer.Initialize(db)` runs at startup and calls `db.Database.Migrate()`. Schema changes: add/modify entity, then `dotnet ef migrations add <Name> --project Modbus.Core --startup-project Modbus.Core --output-dir Persistence/Migrations`. Migrations live in `Modbus.Core/Persistence/Migrations/`.
- **Legacy DB baselining**: `DatabaseInitializer` detects pre-migration databases (no `__EFMigrationsHistory` but `Devices` table exists) and idempotently patches missing columns (`SqpfRegisterAddress`, `FirmwareVersion`) before inserting the `InitialSchema` migration as already applied. After baselining, normal `Migrate()` applies any newer migrations.
- **EF design-time**: `DesignTimeDbContextFactory` in `Modbus.Core` lets `dotnet ef` build the context without spinning up the Avalonia host. Use `--startup-project Modbus.Core` for EF CLI commands. `dotnet-ef` is a local tool (see `.config/dotnet-tools.json`); run `dotnet tool restore` after cloning.
- `WordOrder` stored as **string** (HasConversion<string>()) — "UseSqpf", "ByteSwapped", etc.
- `TcpConfig` and `RtuConfig` are owned entities (EF `OwnsOne`) — loaded automatically, no Include needed

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

---

## Testing

### Project setup
- **Project:** `Modbus.Core.Tests/Modbus.Core.Tests.csproj` (net8.0)
- **Frameworks:** xUnit + FluentAssertions + NSubstitute (mocking) + coverlet.collector
- **Run all tests:** `dotnet test Modbus.Core.Tests/Modbus.Core.Tests.csproj`
- **InternalsVisibleTo:** `Modbus.Core.csproj` exposes internals to `Modbus.Core.Tests` (needed for `Crc16` and future `internal` test targets)

### Folder structure (mirrors source project)
```
Modbus.Core.Tests/
  Services/
    RegisterDecoderTests.cs       — 37 tests: all DataType × WordOrder combos, SQPF permutations, scale factors
  Protocol/
    Rtu/
      Crc16Tests.cs                       — 8 tests: Compute/Append/Validate with known Modbus CRC vectors
      ModbusRtuFrameBuilderTests.cs       — 12 tests: FC03/04/06/16/17, address ranges, CRC validation
      ModbusRtuFrameParserTests.cs        — 12 tests: parse, error responses, ReportSlaveId
    Tcp/
      ModbusTcpFrameBuilderTests.cs       — 11 tests: MBAP header, Transaction ID increment, all FCs
      ModbusTcpFrameParserTests.cs        — 11 tests: parse, error responses, frame too short
```

### What is covered (Phases 1 + 2 complete — 97 tests passing)
- **RegisterDecoder** — all DataType × WordOrder combinations, SQPF byte-permutation with 3 known SQPF values, scale factors, edge cases (invalid enum values)
- **Crc16** — Modbus polynomial 0xA001 with known test vectors, Append (LSB-first), Validate (round-trip + corruption + length checks)
- **RTU Frame Builder** — FC03/04 (read), FC06 (write single), FC16 (write multiple), FC17 (report slave ID); CRC always validated
- **RTU Frame Parser** — parse read responses, error responses (FC | 0x80 → ModbusProtocolException), ReportSlaveId, CRC failure, too-short frames
- **TCP Frame Builder** — 12-byte fixed frames, MBAP header (TxId / ProtocolId=0 / Length / UnitId), variable-length write, transaction ID auto-increment
- **TCP Frame Parser** — same coverage as RTU parser, no CRC (TCP uses MBAP length field)

### Phases not yet implemented
- **Phase 3 — Mocked service tests:** `PollingEngine` (lifecycle, RTU semaphore, SQPF fallback, RegisterValuesUpdated/DeviceConnectionFailed events), `DeviceModelSeeder` (idempotent seeding, Float32 → UseSqpf mapping). Will require making `GroupRegisters` `internal` instead of `private`.
- **Phase 4 — EF Core integration tests:** `TestDbContextFactory` with `DataSource=:memory:` SQLite, repository CRUD, RegisterValue upsert behavior.

### Conventions and directives for future sessions

**TDD posture going forward:**
- New features in `Modbus.Core` → write a failing test first, then implement. The interface-based architecture makes mocking trivial with NSubstitute.
- Bug fixes in `Modbus.Core` → reproduce with a failing test, then fix. Especially relevant for RegisterDecoder/SQPF edge cases.
- UI / ViewModels in `Modbus.Desktop` → no automated tests yet; verify by running the app.

**Test naming convention:**
`MethodName_Scenario_ExpectedResult` — e.g. `Decode_UInt16_MaxValue_Returns65535`, `ParseReadRegisters_BadCrc_ThrowsInvalidDataException`.

**Test structure:**
- `[Theory]` + `[MemberData]` for parameterized cases (xUnit's `[InlineData]` does not support `ushort[]` — must use `MemberData` returning `IEnumerable<object[]>`).
- `[Fact]` for single-scenario tests.
- One test class per production class; folder structure mirrors source.

**FluentAssertions patterns used:**
- Numeric: `.Should().Be(expected)` for exact, `.Should().BeApproximately(expected, precision)` for floats (precision `1e-6` for direct float ops, `1e-2` for SQPF/scale).
- Collections: `.Should().HaveCount(n)`, `.Should().Equal(expected)`.
- Exceptions: `.Should().Throw<T>().Where(e => e.Property == ...)` — pattern works because production exceptions have public properties (`ModbusProtocolException.FunctionCode`, etc.).

**Visibility rule:** if a private method is a pure function worth testing in isolation (e.g. `PollingEngine.GroupRegisters`), make it `internal` and rely on `InternalsVisibleTo`. Don't expose via `public` solely for tests.

**SQPF test vector derivation:** the algorithm in `RegisterDecoder.DecodeFloat32WithSqpf` is `raw |= t[i] << (floatByteIdx * 8)` where `floatByteIdx = (sqpfValue >> (i*4)) & 0xF` and `t[i]` is the i-th transmitted byte (`t[0]=words[0]Hi, t[1]=words[0]Lo, t[2]=words[1]Hi, t[3]=words[1]Lo`). To build a test vector for value V with SQPF S: write V's IEEE 754 bytes (byte0=LSB ... byte3=MSB), then for each i set `t[i] = byte_at_position((S>>(i*4))&0xF)`, finally pack `words[0] = (t[0]<<8) | t[1]`, `words[1] = (t[2]<<8) | t[3]`.

**Avoid in test data:** values where `uint → double` rounding produces off-by-one (e.g. `0xFFFFFFFF`, `0x7FFFFFFF`). Removed from current test cases; if needed in future, use larger `BeApproximately` tolerance or convert through `int.MaxValue`/`uint.MaxValue` constants.

### Bugs caught by the test suite
- **`ModbusProtocolException` format string** — `$"...0x{functionCode:X2}..."` raised `FormatException` because `:X2` is invalid for enum types (only `G/g/X/x/F/f/D/d` accepted, no width specifier). Fixed by casting to `byte` before formatting: `0x{(byte)functionCode:X2}`. Production code path was never exercised because real devices hadn't returned error responses in this code path until tests forced it.


### Pending / future features - Attention! Keep it in the end of the file
- Investigate if its necessary to prompt the user to reset the software when the language is changed, it seens like some texts won't change until a complete restart
- Register write / configure screen / SQPF configuration UI (reading is implemented, writing is not)
- Mobile app (MAUI) connected to the same core as the desktop version with the same functions and styling
- Mass memory readings