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

### Polling: parallel + per-transport reconnect strategy
`PollingEngine.RunLoopAsync` polls all active devices **in parallel** (`Task.WhenAll`) so TCP devices never wait for RTU devices to finish. Per-device failures are isolated via `PollDeviceSafeAsync` (swallows all exceptions so one failing device doesn't skip others). `PollTimeout = 8s` per device.

**RTU:** always disconnect + reconnect at the start of every poll to release the COM port between cycles. Disconnects again after the poll completes.  
**TCP:** reuses the existing connection (`IsConnected` check). Only reconnects when the connection is down (first poll, or after a failure). Stale TCP sockets are detected on the first failed I/O → caught by `DoPollAsync`'s catch → `DisconnectAsync` called → `IsConnected = false` → reconnects on the next poll.

Multiple RTU devices are still serialized among themselves via `_rtuGate` (`SemaphoreSlim(1,1)`). TCP devices run fully in parallel.

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
- **Frameworks:** xUnit + FluentAssertions + NSubstitute (mocking) + coverlet.collector + Microsoft.EntityFrameworkCore.Sqlite (integration tests)
- **Run all tests:** `dotnet test Modbus.Core.Tests/Modbus.Core.Tests.csproj`
- **InternalsVisibleTo:** `Modbus.Core.csproj` exposes internals to `Modbus.Core.Tests` — `Crc16`, `PollingEngine.GroupRegisters`, `PollingEngine.ReadBlock`

### Folder structure (mirrors source project)
```
Modbus.Core.Tests/
  Infrastructure/
    TestDbContextFactory.cs         — helper: creates isolated in-memory SQLite context per test
  Persistence/
    DeviceModelSeederTests.cs       — 9 tests: idempotent seeding, register count, WordOrder, both models
    DeviceModelRepositoryTests.cs   — 6 tests: CRUD, GetByNameAsync, Registers navigation
    DeviceRepositoryTests.cs        — 8 tests: TCP/RTU persist, ExistsBy*, DeleteAsync
    RegisterValueRepositoryTests.cs — 4 tests: UpsertAsync insert/update/mixed, isolation by device
  Polling/
    PollingEngineTests.cs           — 11 tests: lifecycle, RTU gate, SQPF fallback/success, GroupRegisters unit
  Protocol/
    Rtu/
      Crc16Tests.cs                       — 8 tests: Compute/Append/Validate with known Modbus CRC vectors
      ModbusRtuFrameBuilderTests.cs       — 12 tests: FC03/04/06/16/17, address ranges, CRC validation
      ModbusRtuFrameParserTests.cs        — 12 tests: parse, error responses, ReportSlaveId
    Tcp/
      ModbusTcpFrameBuilderTests.cs       — 11 tests: MBAP header, Transaction ID increment, all FCs
      ModbusTcpFrameParserTests.cs        — 11 tests: parse, error responses, frame too short
  Services/
    RegisterDecoderTests.cs         — 46 tests: all DataType × WordOrder combos, SQPF permutations, scale factors
    RegisterFieldTests.cs           — 25 tests: ExtractValue (whole/multi-word/bit-field), ExtractString, ApplyBits, BCD, ExtractTime/Date
    DeviceConfigServiceTests.cs     — 9 tests: FC03/FC04 routing, bit-field merge, 32-word chunking, retry, partial failure, WriteAsync
```

### What is covered (Phases 1–5 complete — 179 tests passing)
- **Phase 1 — RegisterDecoder** — all DataType × WordOrder combinations, SQPF byte-permutation with 3 known SQPF values, scale factors, edge cases (invalid enum values)
- **Phase 2 — Protocol layer** — Crc16, RTU/TCP frame builders (FC03/04/06/16/17), RTU/TCP frame parsers (parse, error responses, CRC, too-short)
- **Phase 3 — PollingEngine** — lifecycle (AddDevice/Start/Stop), RTU gate semaphore (suspend blocks RTU, resume allows poll), SQPF fallback to 0x3210 when holding read fails, SQPF success uses returned value; `GroupRegisters` unit-tested directly as `internal`
- **Phase 3 — DeviceModelSeeder** — creates models when absent, skips AddAsync when already exists, seeds 29 registers, Float32 Input → UseSqpf, NS UInt32 → ByteSwapped, SqpfRegisterAddress = 2900, both KS-3000 and Konect 120 seeded
- **Phase 4 — EF Core integration** — `TestDbContextFactory` with SQLite `:memory:`, `DeviceModelRepository` (CRUD, GetByNameAsync, Registers navigation), `DeviceRepository` (TCP/RTU config, ExistsByTcpIp/RtuSlaveId, Delete), `RegisterValueRepository` (UpsertAsync insert/update/mixed, device isolation)
- **Phase 5 — Config screen** — `RegisterField` (whole/multi-word/bit-field extraction, ExtractString with null truncation, ApplyBits, BCD helpers, ExtractTime/Date), `DeviceConfigService` (FC03/FC04 routing, bit-fields merged to one read, 32-word chunking for large strings, transient retry up to 3×, ModbusProtocolException no retry, partial failure returns available data)

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

**Visibility rule:** if a private method is a pure function worth testing in isolation, make it `internal` and rely on `InternalsVisibleTo`. Don't expose via `public` solely for tests. Example applied: `PollingEngine.GroupRegisters` and `PollingEngine.ReadBlock` are `internal`.

**SQPF test vector derivation:** the algorithm in `RegisterDecoder.DecodeFloat32WithSqpf` is `raw |= t[i] << (floatByteIdx * 8)` where `floatByteIdx = (sqpfValue >> (i*4)) & 0xF` and `t[i]` is the i-th transmitted byte (`t[0]=words[0]Hi, t[1]=words[0]Lo, t[2]=words[1]Hi, t[3]=words[1]Lo`). To build a test vector for value V with SQPF S: write V's IEEE 754 bytes (byte0=LSB ... byte3=MSB), then for each i set `t[i] = byte_at_position((S>>(i*4))&0xF)`, finally pack `words[0] = (t[0]<<8) | t[1]`, `words[1] = (t[2]<<8) | t[3]`.

**Avoid in test data:** values where `uint → double` rounding produces off-by-one (e.g. `0xFFFFFFFF`, `0x7FFFFFFF`). Removed from current test cases; if needed in future, use larger `BeApproximately` tolerance or convert through `int.MaxValue`/`uint.MaxValue` constants.

### Bugs caught by the test suite
- **`ModbusProtocolException` format string** — `$"...0x{functionCode:X2}..."` raised `FormatException` because `:X2` is invalid for enum types (only `G/g/X/x/F/f/D/d` accepted, no width specifier). Fixed by casting to `byte` before formatting: `0x{(byte)functionCode:X2}`. Production code path was never exercised because real devices hadn't returned error responses in this code path until tests forced it.


---

## Configure Screen Architecture

### Overview
```
DeviceCapabilityRegistry  →  which sections/fields each model has (flags enum)
DeviceConfigProfile       →  which register addresses each model exposes
IDeviceConfigService      →  stateless FC03/FC04 block reader + FC06 writer
DeviceConfigureViewModel  →  consumes both; exposes Has* props and loaded field values
```

### DeviceCapabilities (flags enum)
`Modbus.Core/Domain/Enums/DeviceCapabilities.cs` — controls sidebar visibility in the configure view.
Flags: `Ethernet`, `Wireless`, `Sntp`, `Iot`, `Clock`, `InputsOutputs`, `FieldKe`, `FieldCurrentInvert`.
`DeviceCapabilityRegistry.Get(deviceCode)` maps device code → flags. Models not in the map return `None`.

To enable or hide a feature for a model, edit `Modbus.Core/Services/DeviceCapabilityRegistry.cs` and add or remove the flag from that model's entry:
```csharp
// Show Ethernet tab:    include DeviceCapabilities.Ethernet
// Hide Ethernet tab:    omit  DeviceCapabilities.Ethernet
// Show KE field:        include DeviceCapabilities.FieldKe
```
Current model capabilities:
- **KS-3000 (0xF2)**: Wireless, Sntp, Iot, Clock, InputsOutputs, FieldKe, FieldCurrentInvert — **no Ethernet** (Wi-Fi only)
- **Konect 120 (0xF3)**: Ethernet, Wireless, Sntp, Iot, Clock, InputsOutputs, FieldKe, FieldCurrentInvert

### RegisterField struct
`Modbus.Core/Services/RegisterField.cs` — describes one configuration field.

Three usage patterns when filling `DeviceConfigProfileRegistry.cs`:

```csharp
// Whole register (16-bit)
AddrKe = 40005

// Multi-word (Float32, IP = 2 words, MAC = 3 words, string = N words)
AddrTp          = new RegisterField(40001, WordCount: 2)
AddrIpAddress   = new RegisterField(40010, WordCount: 2)
AddrMacAddress  = new RegisterField(30100, WordCount: 3)   // FC04 → 3xxxx

// Bit-field (multiple fields sharing one register)
AddrCurrentInvert = new RegisterField(40007, BitOffset: 15, BitWidth: 1)
AddrSntpEnabled   = new RegisterField(40007, BitOffset: 12, BitWidth: 1)
AddrTl            = new RegisterField(40006, BitOffset: 8, BitWidth: 8)  // KS-3000: high byte
AddrTi            = new RegisterField(40006, BitOffset: 0, BitWidth: 8)  // KS-3000: low byte
```

**KS-3000 byte order quirk**: in register 40006, TL is stored in the **high** byte and TI in the **low** byte — opposite of what the natural "first field at LSB" intuition suggests. Verify by reading register against the old KRON software before assuming layout.

Modicon convention: `4xxxx` = FC03 holding register (read/write); `3xxxx` = FC04 input register (read-only).
Plain integer assignment (`AddrKe = 40005`) is valid — implicit conversion creates `RegisterField(addr)`.

Key methods on `RegisterField`:
- `ExtractValue(regs)` — returns the field value from the register dictionary (handles multi-word and bit-fields)
- `ApplyBits(currentReg, fieldValue)` — read-modify-write helper for bit-field writes

### DeviceConfigService
`Modbus.Core/Services/DeviceConfigService.cs` — stateless, opens/closes connection per call.
- `ReadAsync(device, addresses)` — accepts Modicon numbers, splits FC03/FC04, coalesces **only adjacent addresses (`MaxGap = 1`)**, returns `Dictionary<modiconAddr, ushort>`
- Per-block resilience: if any block throws (TimeoutException / ModbusProtocolException), the failure is logged to `Debug.WriteLine` (`[DeviceConfigService] FC03 <range> failed: ...`) and other blocks continue. The returned dict simply has those addresses missing; consumers must guard with null checks (already the pattern in `ApplyRegisters`).
- Overall timeout `TimeoutSeconds = 30` covers the full bulk read.
- `WriteAsync(device, address, value)` — FC06, address must be `4xxxx`
- Registered as `AddTransient<IDeviceConfigService, DeviceConfigService>()` in `App.axaml.cs`

**Why `MaxGap = 1` (only contiguous)**: with larger gaps the coalesced block would include addresses the device doesn't have. Some devices return a Modbus exception (handled now — see RTU transport note below), but others go fully silent → 1s read timeout per chunk. `MaxGap = 1` only coalesces adjacent registers (gap of exactly 1 between sorted addresses), so we never request a "phantom" register.

### DeviceConfigureViewModel
Constructor: `(DeviceItemViewModel device, IDeviceConfigService, Func<Task> suspendRtuPolling, Action resumeRtuPolling, Action onGoBack)`
- `HasEthernet`, `HasWireless`, `HasSntp`, `HasIot`, `HasClock`, `HasInputsOutputs`, `HasFieldKe`, `HasCurrentInvert` — computed from `DeviceCapabilityRegistry`
- `LoadAsync()` — suspends RTU polling if needed, reads all profile addresses, calls `ApplyRegisters()`
- `ApplyRegisters()` — fully implemented for the KS-3000 profile (Geral, Wireless, SNTP, IoT, Relógio, Entradas/Saídas — all read-only). Float32 holding regs go through `DecodeFloat32` helper using `RegisterDecoder.Decode(..., WordOrder.ByteSwapped)` (see "Float32 byte order" below). Bit-fields and integers via `RegisterField.ExtractValue`. Strings via `ExtractString`. Clock via `ExtractTime`/`ExtractDate`. SQPF via `ApplySeqPf` (see "SQPF nibble labels" below).
- **Sidebar navigation is data-driven**: VM exposes `Sections` (`IReadOnlyList<SidebarSection>` built in constructor) — only the sections enabled by `HasXxx` capabilities are added. `SidebarSection(int Code, string Label)` carries the section identity (0=General, 1=Ethernet, 2=Wireless, ...) so `IsGeneral`/`IsEthernet`/etc work based on `SelectedSection?.Code` regardless of how many sections were filtered out. The XAML uses `ItemsSource="{Binding Sections}"` + `SelectedItem="{Binding SelectedSection}"`.

**Avalonia gotcha that drove this design**: inline `<ListBoxItem>` children declared directly inside a `<ListBox>` (instead of via `ItemsSource`) do NOT inherit the parent VM as DataContext — the `ListBoxItem` becomes the item itself and any `{Binding HasXxx}` silently resolves to `null`. Workarounds with `$parent[UserControl]`/`ReflectionBinding`/`ElementName` either fail to compile (compiled bindings can't find the cast target type) or fail to resolve at runtime. The data-driven `ItemsSource` approach sidesteps the problem entirely and is the canonical Avalonia pattern.

### Device register limits
- **FC03/FC04 read**: max **32 registers per request** — `DeviceConfigService` splits blocks automatically
- **FC16 write (future)**: max **22 registers per request** — must be split when writing string fields; `WriteStringAsync` not yet implemented

### String fields
String registers are read as ASCII, high byte first per word, null-terminated.
Use `RegisterField.ExtractString(regs)` in `ApplyRegisters` — returns `string?`, already trimmed.
Example: MQTT URL spans 43461–43495 (35 words = 70 chars max) → `new RegisterField(43461, WordCount: 35)`.
The service transparently issues two FC03 calls (32 + 3) for any block exceeding the 32-word limit.

### Filling in register addresses
Edit `Modbus.Core/Services/DeviceConfigProfileRegistry.cs`.
The file has a usage guide at the top. Each model (Ks3000, Konect120) has one property per field — replace `null` with the appropriate `RegisterField`.
Multi-word and bit-field addresses pointing to the same register are deduplicated automatically by `AllAddresses`.

### KS-3000 byte order: every multi-byte value is little-endian on the wire
The device stores **every** multi-byte numeric/binary value byte-reversed compared to what Modbus normally expects. Modbus 16-bit registers are still transmitted MSB-first per spec, but the underlying byte sequence of values is little-endian. Apply byte-reversal at the decoding layer:

| Field type | Where the reversal is applied | How |
|---|---|---|
| **Float32 holding** (TP, TC, HourmeterThr) | `DeviceConfigureViewModel.DecodeFloat32` | `RegisterDecoder.Decode(..., WordOrder.ByteSwapped)` — full 4-byte reversal |
| **IP / Mask / Gateway / DNS** (32-bit IPv4) | `DeviceConfigureViewModel.FormatIp` | Reads low byte first: `$"{v&0xFF}.{(v>>8)&0xFF}.{(v>>16)&0xFF}.{(v>>24)&0xFF}"` |
| **MAC** (48-bit, 3 words) | `DeviceConfigureViewModel.FormatMac` | `Array.Reverse(bytes)` after collecting 6 bytes |
| **Single 16-bit ints** (Timezone signed, SyncInterval unsigned) | `DeviceConfigureViewModel.SwapBytes` | `(v << 8) \| (v >> 8)` before casting |

**Float32 caveat**: SQPF (input registers only) is independent from this. Holding-register Float32 always uses `WordOrder.ByteSwapped`. Never call `BitConverter.Int32BitsToSingle((int)ExtractValue(...))` directly — it assumes ABCD and would read 1.0 as ~0.0.

**Single 16-bit caveat**: bit-fields and pure flag bits (e.g. `AddrTl` and `AddrTi` sharing 40006, or `AddrSntpEnabled` bit in 40007) do **NOT** need swapping because individual bit positions don't change when you swap bytes within a single register — `ExtractValue` reads the whole word and bit-shifts. The swap only matters when you treat a 16-bit register as a single integer value (Timezone, SyncInterval and presumably KE/SendInterval/DebounceEdp once we have ground-truth values to verify).

**Likely candidates still untested**: KE (40005), SendInterval (42101), DebounceEdp (40171), and others 16-bit ints. If the next ground-truth comparison shows them off by orders of magnitude, apply `SwapBytes` the same way.

### ExtractString cuts at the first null
`RegisterField.ExtractString` now finds the first `0x00` byte and truncates there (instead of only `TrimEnd('\0')`). Modbus devices commonly leave junk in unused buffer positions past the C-string terminator — without the cut, that junk shows up after the real string (e.g. `"a.st1.ntp.br?"` instead of `"a.st1.ntp.br"`).

### SQPF nibble labels
KRON's display convention for register 42901 nibbles is **inverted from the natural F0..F2 expectation**:
- nibble value `0` → label **F2**
- nibble value `1` → label **F1**
- nibble value `2` → label **F0**
- nibble value `3` → label **EXP**

So raw `0x3210` (Padrão KRON) displays as **F2, F1, F0, EXP** (in position order from `i=0` low-nibble to `i=3` high-nibble). The `ApplySeqPf` method in `DeviceConfigureViewModel` and the initial `_pfPos` array both follow this convention. If a future screen needs to display these labels, reuse the same array (`["F2", "F1", "F0", "EXP"]`).

### MQTT Port is ASCII, not numeric
KS-3000 stores the MQTT broker port as a **6-character ASCII string** at registers 43496–43498 (3 words), not as a 16-bit integer. The VM exposes `MqttPort` as `string?` and the XAML uses a `TextBox` (not `NumericUpDown`). All other MQTT fields (URL, User, Token, Topic, etc.) are also ASCII per the device doc.

### RTU exception-response detection
`RtuModbusTransport.ReadExactAsync` inspects the second byte of every response: if `(byte[1] & 0x80) != 0`, it's a Modbus exception frame (5 bytes total: slave + FC|0x80 + code + 2 CRC) and the transport stops reading immediately instead of waiting for a normal full-length response. Without this, requesting an unmapped address against a device that returns "Illegal Data Address" would hang for the full 1s read timeout per request, and `DeviceConfigService` would burn through its 30s budget. This matters once we implement writes (FC06/FC16) — devices often respond with exceptions for bad addresses, and the parser already throws `ModbusProtocolException` from those.

---

### Pending / future features - Attention! Keep it in the end of the file
- Investigate if its necessary to prompt the user to reset the software when the language is changed, it seens like some texts won't change until a complete restart
- Register write / configure screen / SQPF configuration UI (reading is implemented, writing is not). When implementing writes: KS-3000 doc says any string write in 43461+ needs a **Coil Reset** sent afterwards to commit the change.
- Konect 120 config profile is fully filled — no `null` fields remaining. Key differences from KS-3000: has Ethernet (DHCP = D11 of 40007, IP/Mask/Gateway = 43101/43103/43105, MAC = 39501); Wi-Fi DHCP is D5 of 40007 (KS-3000 uses D11); Wi-Fi IP/Mask/Gateway = 43111/43123/43125; Wi-Fi MAC = 39504 (KS-3000 = 39501); BT MAC = 39507 (same as KS-3000); DNS server shared between Ethernet and Wi-Fi at 43117; `AddrModuleVersion` = 39511 (same as KS-3000).
- Verify Wi-Fi register addresses for KS-3000 (43101–43108 block was failing with timeouts during initial wiring — may need address correction once doc is consulted).
- Mobile app (MAUI) connected to the same core as the desktop version with the same functions and styling
- Mass memory readings