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
  - RTU: if user edits the slave address before saving, FC 0x42 (configAddress) is sent to write the new address, device reboots, app waits and confirms before adding to DB
- Real-time electrical readings screen (voltages, currents, power, frequency, power factor)
- SQLite persistence via EF Core Migrations (`db.Database.Migrate()` on startup, with legacy-DB baselining for clients upgrading from pre-migration builds)
- Device model seeding (`DeviceModelSeeder`) — idempotent, runs on every startup
- SQPF (float byte order) read dynamically from device register 42.901 (holding reg, FC03, 0-based address 2900) every poll cycle
- Hub navigation: device list → device hub → real-time readings (back chain works)
- Configure screen: slave address editable for RTU — change is applied via FC 0x42 at the end of save, DB updated, screen stays open and re-reads at new address
- **Mass memory screen**: reads all blocks via FC 0x14 (ReadFileRecord), shows timestamp + grandeza values + checksum per block; resume/restart dialog when paused or on error; TXT export in legacy-software format

### Navigation structure
```
DeviceListView → [Open] → DeviceHubView → [Leitura]         → DeviceDetailView
                                         → [Memória de Massa] → MassMemoryView
                                         → [Configurar]       → DeviceConfigureView
```
Navigation is driven by `MainViewModel.CurrentPage`. All VMs fire `NavigationRequested` events
that bubble up through `DeviceListViewModel` to `MainViewModel`.

### Test coverage (Phases 1-6 complete)
- `Modbus.Core.Tests` project — xUnit + FluentAssertions + NSubstitute
- `InternalsVisibleTo("Modbus.Core.Tests")` configured in `Modbus.Core.csproj`
- See the Testing section below for the full breakdown (242+ tests passing)

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
  MainViewModel            — root, owns CurrentPage, wires NavigationRequested from children
  DeviceListViewModel      — device list, polling status updates, navigation hub
  DeviceHubViewModel       — per-device hub (new screen, launched from device list)
  DeviceDetailViewModel    — real-time readings; parent is Action onGoBack (not DeviceListViewModel)
  AddDeviceViewModel       — add device wizard (scan + manual form)
  SettingsViewModel        — RTU port settings
  DeviceItemViewModel      — device row; exposes IsConnected, StatusText, LastSeenText, ErrorMessage
  MassMemoryViewModel      — mass memory: header info, block reading loop, resume/restart, TXT export
  DeviceConfigureViewModel — configure screen; reads/writes registers via IDeviceConfigService

Views/
  MainWindow            — sidebar (220px) + ContentControl bound to CurrentPage
  DeviceListView        — list of DeviceItemViewModels
  DeviceHubView         — 3 feature cards (all active)
  DeviceDetailView      — tabs: real-time readings grid + raw registers DataGrid
  AddDeviceView         — scan + form
  SettingsView          — COM port, baud rate, etc.
  MassMemoryView        — header strip + DataGrid with dynamic columns; code-behind handles resume dialog + file picker
  DeviceConfigureView   — sidebar + section panels; code-behind handles mass-memory reset dialog

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

### TCP scan — pausa pós-conexão antes do ReportSlaveId
No scan TCP (`DeviceScanService.ScanTcpAsync`), depois do broadcast UDP descobrir os IPs, para cada
medidor o fluxo é: `ConnectAsync` (abre o socket) → `ReportSlaveIdAsync` (FC17, lê firmware) →
leitura do serial. Disparar o ReportSlaveId **imediatamente** após abrir o socket fazia o medidor
não responder a tempo — o firmware (vindo do `rawData[2]` do ReportSlaveId) ficava em branco de
forma intermitente, principalmente no **Konect 120**.

A correção é um `await Task.Delay(TcpPostConnectDelayMs)` entre `ConnectAsync` e `ReportSlaveIdAsync`
— o medidor precisa de um instante após a conexão antes de aceitar a primeira requisição.

**Valor calibrado empiricamente = `TcpPostConnectDelayMs` (500ms):**
- **500ms** → 100% confiável (KS-3000 e Konect 120) — valor escolhido pela máxima confiabilidade
- 300ms → margem acima do ponto de falha, mas não tão folgado quanto 500ms
- 250ms → falhou a leitura de firmware do KS-3000 ~1x em algumas tentativas

**Em aberto:** avaliar se essa mesma pausa pós-`Connect` deve ser aplicada em TODO socket TCP recém
-criado (não só no scan), para evitar que a primeira leitura saia rápido demais logo após conectar
(ex.: `PollingEngine` ao reconectar TCP, `DeviceConfigService` que abre/fecha conexão por chamada).

### SQPF (Sequência do Ponto Flutuante)
Float32 byte order is configurable on the device via holding register **42.901**
(FC03, 0-based Modbus address = **2900**).

- `RegisterDefinition.WordOrder = UseSqpf` marks Float32 input registers as SQPF-dependent
- `UInt32` registers (e.g., NS serial number) use `WordOrder.ByteSwapped` directly — exempt from SQPF
- Holding registers never use SQPF
- `PollingEngine` reads register 2900 via `ReadHoldingRegistersAsync` once per poll cycle
- If the read fails (exception), falls back silently to `0x3210` (Padrão KRON = ByteSwapped)
- SQPF is writable from the configure screen; the device applies the new byte ordering.
- `RegisterDecoder.DecodeFloat32WithSqpf(words, sqpfValue, scale)` uses the KRON nibble convention (see below)

**SQPF nibble convention (verified against original KRON C source — `MB_ReadInputRegister`):**
`buffer_prog[j] = nibble(3-j)` (read nibbles HIGH-to-LOW), `floatByteIdx = 3 - buffer_prog[j]`

In C#: `int nibble = (sqpfValue >> ((3-i)*4)) & 0xF; int floatByteIdx = 3 - nibble; raw |= (uint)t[i] << (floatByteIdx*8);`

**IMPORTANT**: The old (wrong) algorithm was `floatByteIdx = (sqpfValue >> (i*4)) & 0xF` (nibbles LOW-to-HIGH). It happened to produce correct results for 0x3210, 0x2301, and 0x0123 because those values satisfy `3-nibble(3-i) == nibble(i)`, but gives garbage for any other SQPF (e.g. 0x1230).

Known values:
| SQPF value | Byte order | Description |
|------------|------------|-------------|
| `0x3210`   | F2,F1,F0,EXP (DCBA) | Padrão KRON (default) |
| `0x2301`   | F1,F2,EXP,F0 (CDAB) | Float padrão |
| `0x0123`   | EXP,F0,F1,F2 (ABCD) | Float inverso (IEEE 754 big-endian) |

### FC 0x42 — KRON configAddress (RTU slave address change)
KRON devices use a custom function code **0x42** to change the RTU slave address. Frame (9 bytes):

```
00 42 [SN 4B BE] [new addr] [CRC-16]
│  │                         └─ Modbus CRC covers 7 data bytes
│  FC 0x42 (KRON custom)
Broadcast slave (0x00) — device identified by serial number, not current address
```

**Captured frame example** (SN=4002892, new addr=6): `00 42 00 3D 14 4C 06 ED 48`

- No response expected — device applies the address and reboots (~20s bootloader)
- Implemented in `ModbusRtuFrameBuilder.ConfigureAddress(uint serialNumber, byte newSlaveId)`
- `DeviceConfigService.WriteSlaveAddressAsync(device, newSlaveId)` creates RTU transport directly (FC 0x42 is RTU-only, bypasses `IModbusService`)
- **Add device flow**: if user changes slave address in the scan results before saving → FC 0x42 → `WaitForDeviceReachableAsync` on new address → add to DB
- **Configure screen**: `EditableSlaveId` is applied at the end of `SaveAsync` (after all other writes) → FC 0x42 → DB update → post-save re-read at new address (screen stays open)
- RTU polling suspended during the entire operation in both flows

### RTU polling gate
`PollingEngine` has a `SemaphoreSlim _rtuGate` that serializes RTU access.
During device scan, `DeviceListViewModel.SuspendRtuPollingAsync()` must be called before scanning
and `ResumeRtuPolling()` after. This prevents port conflicts between polling and scanning.

### Polling: parallel + per-transport reconnect strategy + per-device lock
`PollingEngine.RunLoopAsync` polls all active devices **in parallel** (`Task.WhenAll`) so TCP devices never wait for RTU devices to finish. `PollTimeout = 8s` per device.

**Per-device lock (`DeviceContext.Lock`, `SemaphoreSlim(1,1)`):**
- `PollDeviceSafeAsync` acquires the lock with `WaitAsync(0)` (non-blocking) — if the lock is held (e.g. config screen is open), that device is **skipped** for this cycle without affecting others.
- `AcquireDeviceLockAsync(deviceId)` — called by `DeviceHubViewModel.OpenConfigure` for TCP devices. Waits (blocking) for any in-progress poll to finish, then disconnects the transport so `DeviceConfigService` can open a fresh connection. Must be paired with `ReleaseDeviceLock(deviceId)`.
- For RTU devices, `SuspendRtuPollingAsync` / `ResumeRtuPolling` (the existing `_rtuGate` mechanism) is still used instead of the per-device lock.

**RTU:** disconnect + reconnect every poll cycle (COM port released between cycles). `_rtuGate` (`SemaphoreSlim(1,1)`) serializes all RTU devices among themselves. The `rtuCts` (8s timeout) is created **after** acquiring `_rtuGate`, so the timeout applies only to the actual Modbus I/O — not to gate wait time.

**TCP:** persistent connection — `DoPollAsync` only reconnects if `!ctx.Service.IsConnected`. Keeps socket alive between polls (~5s interval) to avoid status flickering. `AcquireDeviceLockAsync` disconnects the transport before handing off to `DeviceConfigService`.

Multiple RTU devices are serialized via `_rtuGate`. TCP devices run fully in parallel.

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
`LocalizationService` — singleton, dictionary-based, extends `ObservableObject`.
String files: `Modbus.Desktop/Services/Strings/PortugueseStrings.cs` and `EnglishStrings.cs`.
When `CurrentLanguage` changes, fires `OnPropertyChanged("Item[]")` to invalidate all indexer bindings.

**Hot-reload works without restart.** No restart prompt needed when the user changes language.

**Compiled-binding caveat:** Files with `x:DataType` use compiled bindings for all bindings, including those with an explicit `Source`. Compiled bindings with `Source={x:Static svc:LocalizationService.Instance}` + indexer (`[Key]`) do **not** respond to `"Item[]"` notifications — strings only refresh when the view is recreated.

**The correct pattern for any ViewModel whose View has `x:DataType`:**
1. Expose each label as an `[ObservableProperty]` string in the VM.
2. Add `UpdateLabels()` that reads from `LocalizationService.Instance`.
3. Subscribe to `LocalizationService.Instance.PropertyChanged` in the constructor and call `UpdateLabels()` when `e.PropertyName == "Item[]"`.
4. In XAML use `{Binding LabelProp}` — no `Source` needed.

`MainViewModel` and `SettingsViewModel` already follow this pattern. Apply it to any new ViewModel whose view has `x:DataType`.

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
      ModbusRtuFrameBuilderTests.cs       — 14 tests: FC03/04/06/16/17, FC 0x42 configAddress (known-vector + structural), address ranges, CRC validation
      ModbusRtuFrameParserTests.cs        — 12 tests: parse, error responses, ReportSlaveId
    Tcp/
      ModbusTcpFrameBuilderTests.cs       — 11 tests: MBAP header, Transaction ID increment, all FCs
      ModbusTcpFrameParserTests.cs        — 11 tests: parse, error responses, frame too short
  Services/
    RegisterDecoderTests.cs         — 46 tests: all DataType × WordOrder combos, SQPF permutations, scale factors
    RegisterFieldTests.cs           — 25 tests: ExtractValue (whole/multi-word/bit-field), ExtractString, ApplyBits, BCD, ExtractTime/Date
    DeviceConfigServiceTests.cs     — 9 tests: FC03/FC04 routing, bit-field merge, 32-word chunking, retry, partial failure, WriteAsync
    MassMemoryServiceTests.cs       — 17 tests: ParseBlock (timestamp BCD, LE float decode, checksum, index passthrough), ComputeStartPosition (6 variants), ParseReadFileRecord TCP (correct data, truncated, error response), ParseReadFileRecord RTU (correct, truncated)
```

### What is covered (Phases 1–6 complete + FC 0x42 frame — 242 tests passing)
- **Phase 1 — RegisterDecoder** — all DataType × WordOrder combinations, SQPF byte-permutation with 3 known SQPF values, scale factors, edge cases (invalid enum values)
- **Phase 2 — Protocol layer** — Crc16, RTU/TCP frame builders (FC03/04/06/16/17), RTU/TCP frame parsers (parse, error responses, CRC, too-short)
- **Phase 3 — PollingEngine** — lifecycle (AddDevice/Start/Stop), RTU gate semaphore (suspend blocks RTU, resume allows poll), SQPF fallback to 0x3210 when holding read fails, SQPF success uses returned value; `GroupRegisters` unit-tested directly as `internal`
- **Phase 3 — DeviceModelSeeder** — creates models when absent, skips AddAsync when already exists, seeds 29 registers, Float32 Input → UseSqpf, NS UInt32 → ByteSwapped, SqpfRegisterAddress = 2900, both KS-3000 and Konect 120 seeded
- **Phase 4 — EF Core integration** — `TestDbContextFactory` with SQLite `:memory:`, `DeviceModelRepository` (CRUD, GetByNameAsync, Registers navigation), `DeviceRepository` (TCP/RTU config, ExistsByTcpIp/RtuSlaveId, Delete), `RegisterValueRepository` (UpsertAsync insert/update/mixed, device isolation)
- **Phase 5 — Config screen** — `RegisterField` (whole/multi-word/bit-field extraction, ExtractString with null truncation, ApplyBits, BCD helpers, ExtractTime/Date), `DeviceConfigService` (FC03/FC04 routing, bit-fields merged to one read, 32-word chunking for large strings, transient retry up to 3×, ModbusProtocolException no retry, partial failure returns available data)
- **Phase 6 — Mass memory** — `MassMemoryService.ParseBlock` (BCD timestamp decode, LE float decode, checksum), `MassMemoryService.ComputeStartPosition` (sector/block fast-forward), `ParseReadFileRecord` TCP and RTU (correct data extraction with `rdl-2` fix, truncated frame, error response)

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
Constructor: `(DeviceItemViewModel device, IDeviceConfigService, Func<Task> pausePolling, Action resumePolling, Action onGoBack)`
- `pausePolling` / `resumePolling` are transport-agnostic callbacks set by `DeviceHubViewModel.OpenConfigure`: RTU → `SuspendRtuPollingAsync` / `ResumeRtuPolling`; TCP → `AcquireDeviceLockAsync(id)` / `ReleaseDeviceLock(id)`.
- `HasEthernet`, `HasWireless`, `HasSntp`, `HasIot`, `HasClock`, `HasInputsOutputs`, `HasFieldKe`, `HasCurrentInvert` — computed from `DeviceCapabilityRegistry`
- `LoadAsync()` — always calls `_pausePolling()` (transport-agnostic), reads all profile addresses, calls `ApplyRegisters()`, then `_resumePolling()` in a finally block.
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
| **Single 16-bit ints** (Timezone signed, SyncInterval unsigned, KE unsigned) | `DeviceConfigureViewModel.SwapBytes` | `(v << 8) \| (v >> 8)` before casting. Applied on both read and write — symmetric so the dirty diff stays consistent. |

**Float32 caveat**: SQPF (input registers only) is independent from this. Holding-register Float32 always uses `WordOrder.ByteSwapped`. Never call `BitConverter.Int32BitsToSingle((int)ExtractValue(...))` directly — it assumes ABCD and would read 1.0 as ~0.0.

**Single 16-bit caveat**: bit-fields and pure flag bits (e.g. `AddrTl` and `AddrTi` sharing 40006, or `AddrSntpEnabled` bit in 40007) do **NOT** need swapping because individual bit positions don't change when you swap bytes within a single register — `ExtractValue` reads the whole word and bit-shifts. The swap only matters when you treat a 16-bit register as a single integer value (Timezone, SyncInterval and presumably KE/SendInterval/DebounceEdp once we have ground-truth values to verify).

**Confirmed NOT to need swapping** (ground-truthed against the old KRON software): SendInterval (42101), DebounceEdp (40171). These are 16-bit whole-register ints that the device stores in normal MSB-first byte order, unlike KE/Timezone/SyncInterval. The pattern is not predictable from the spec — each new 16-bit int must be verified individually: write a known value (e.g. `22`) and check the old KRON software; if it displays `5632` (= `0x1600`) the bytes are reversed and `SwapBytes` must be applied on both read and write paths.

### ExtractString cuts at the first null
`RegisterField.ExtractString` now finds the first `0x00` byte and truncates there (instead of only `TrimEnd('\0')`). Modbus devices commonly leave junk in unused buffer positions past the C-string terminator — without the cut, that junk shows up after the real string (e.g. `"a.st1.ntp.br?"` instead of `"a.st1.ntp.br"`).

### SQPF nibble labels (verified against the old KRON software)
The KRON convention for register 42901 has **two non-obvious aspects** that don't follow naturally from the SQPF decoder semantics:

1. **Display order is HIGH-to-LOW**: leftmost position (PfPos0) is nibble 3 of the raw register (bits 15-12), rightmost (PfPos3) is nibble 0 (bits 3-0). Note this is the opposite of how the decoder's `for (int i = 0; i < 4; i++) { int floatByteIdx = (sqpfValue >> (i*4)) & 0xF; ... }` loop iterates the same bits.
2. **Nibble value → label mapping**:
   - `0` → **EXP**
   - `1` → **F0**
   - `2` → **F1**
   - `3` → **F2**

Default raw `0x3210` displays as `F2, F1, F0, EXP` (nibble 3 = 3 → F2, then 2 → F1, then 1 → F0, then 0 → EXP).

`DeviceConfigureViewModel.ApplySeqPf` / `EncodeSeqPf` / initial `_pfPos` all follow this convention. If a future screen needs the labels, reuse `SeqPfLabels = ["EXP", "F0", "F1", "F2"]` (where index = nibble value).

**Important: no byte-swap on SQPF.** Unlike KE/Timezone/SyncInterval, register 42901 is NOT one of the byte-swapped 16-bit ints. The raw register value is consumed verbatim by `RegisterDecoder.DecodeFloat32WithSqpf` (and by the device itself) as a Float32 byte-permutation table — applying `SwapBytes` would break that and produce wrong float readings.

**How this was verified** (so a future session doesn't trip on it again): we tested two non-default sequences in the old KRON software:
- Writing "F0, F1, F2, EXP" produces register `0x1230`.
- Writing "F0, F2, EXP, F1" produces register `0x1302`.

Both decode correctly with high-to-low + the `EXP/F0/F1/F2` mapping above. Earlier attempts to apply `SwapBytes` happened to work for the symmetric default `0x3210` but broke any asymmetric sequence with a pair-swap pattern.

### MQTT Port is ASCII, not numeric
KS-3000 stores the MQTT broker port as a **6-character ASCII string** at registers 43496–43498 (3 words), not as a 16-bit integer. The VM exposes `MqttPort` as `string?` and the XAML uses a `TextBox` (not `NumericUpDown`). All other MQTT fields (URL, User, Token, Topic, etc.) are also ASCII per the device doc.

### Coil addressing convention — wire address = KRON document number − 1
The KRON documentation (and the reference Modbus test client UI) numbers coils **1-based / Modicon**.
The **wire (protocol) address = document number − 1** — the standard Modbus 0-based conversion.
Confirmed by the user against the device + documentation, and by a captured reset frame (document
"coil 6" → wire `0x0005`, which the meter echoed/accepted).

**The code stores the WIRE address directly** and `ModbusRtuFrameBuilder.WriteSingleCoil` (and the
TCP path) transmit it byte-for-byte. So when adding a coil from the KRON doc, put `docNumber - 1`
in the code. The whole current inventory already follows this (see table) — all values are `doc−1`.

### Reset/commit coil (FC05) — sent after every save, wire address is 5
At the end of any save with ≥1 write, `DeviceConfigService.WriteBatchAsync(sendCoilResetAfter: true)` issues an **FC05 Write Single Coil** to commit the configuration; the KS-3000 / Konect 120 **reboots** on receiving it (~6s, LEDs blink + screen blanks). `DeviceConfigureViewModel.BuildWriteOperations` sets `needsCoilReset = true` whenever `ops.Count > 0`.

**Verified against a captured frame:** ground-truth reset frame for slave 2 is `02 05 00 05 FF 00 9C 08` (FC05, coil `0x0005`, value ON `0xFF00`), so `CoilResetAddress = 5`. `ModbusRtuFrameBuilderTests.WriteSingleCoil_ResetCoil_MatchesCapturedFrame` locks this byte-for-byte.

**RTU no-echo handling:** the meter applies the coil and reboots **without echoing** the FC05 response, so `RtuModbusTransport.ReadExactAsync` throws `TimeoutException` after 1s. `DeviceConfigService.SendCoilTolerantAsync` (used for both the reset coil and the IoT buffer coil) treats `TimeoutException` (alongside `IOException` / `OperationCanceledException` / `ModbusProtocolException`) as **success**. After the coil, `SaveAsync` calls `WaitForDeviceReachableAsync` to wait out the reboot before the confirmation re-read.

### IoT buffer / mass-memory reset coil — before the commit coil when IoT changes
When the user changes the **IoT grandezas selection and/or the send interval**, an extra coil must
be pulsed to clear the IoT buffer / reset mass memory **before** the commit/reset coil. The meter
then needs ~6s to settle; firing the reset coil sooner pushes it into a mass-memory self-test.

Sequence in `WriteBatchAsync` when `iotBufferResetCoil` is set + `sendCoilResetAfter`:
**writes → IoT buffer coil → wait `IotBufferResetSettleMs` (7s) → reset coil (5)**.

Per-model buffer coil (wire = doc − 1), in `DeviceConfigProfile.IotBufferResetCoil`:
- **KS-3000**: doc coil 91 → **wire 90**
- **Konect 120**: doc coil 80 → **wire 79**

`DeviceConfigureViewModel.BuildWriteOperations` sets `needsIotBufferReset` when any write op targets
`AddrGrandezasSlots1to20` / `AddrGrandezasSlots21to50` / `AddrSendInterval`; `SaveAsync` then passes
`_profile.IotBufferResetCoil` to `WriteBatchAsync`. `IotBufferResetSettleMs` is an `internal`
property so tests zero it to skip the wait.

**User confirmation (blocking, on Save):** because this erases logged mass-memory data, `SaveAsync`
awaits `ConfirmMassMemoryReset` (a `Func<Task<bool>>?` the VM exposes) right before pausing polling
— only when a buffer reset will actually fire. The View (`DeviceConfigureView` code-behind) wires
this to a confirmation `Window` on `DataContextChanged`, mirroring the delete-confirm pattern in
`DeviceListView`. Strings: `CfgMemResetTitle/Msg/Confirm`. When the callback is null (e.g. unit
context) the save proceeds without asking.

**Declining:** `RevertIotMemoryFields()` reverts only the reset-triggering fields (grandezas +
send interval) to the baseline and aborts the save (no device writes). Unrelated edits are kept,
and a later save won't re-trigger the reset since those fields now match the baseline.

### Coil inventory (all FC05 coils) — confirmed correct (wire = doc − 1)
| Doc (Modicon) | Wire addr | Function | Value | Where |
|:---:|:---:|----------|:---:|-------|
| 6  | **5**  | Reset/commit (reboots meter) | ON | `DeviceConfigService.CoilResetAddress` |
| 62 | **61** | Zero hourmeter | ON | `DeviceDetailViewModel.BuildHourmeterChannel` |
| 21 | **20** | Reset EDP-1 counter | ON | `DeviceDetailViewModel.BuildIoChannels` |
| 22 | **21** | Reset EDP-2 counter | ON | `BuildIoChannels` |
| 23 | **22** | Reset EDP-3 counter | ON | `BuildIoChannels` |
| 31 | **30** | Digital output SD-1 on/off | ON/OFF | `BuildIoChannels` |
| 32 | **31** | Digital output SD-2 on/off | ON/OFF | `BuildIoChannels` |
| 91 | **90** | IoT buffer / mass-memory reset — **KS-3000** | ON | `DeviceConfigProfileRegistry.Ks3000.IotBufferResetCoil` |
| 80 | **79** | IoT buffer / mass-memory reset — **Konect 120** | ON | `DeviceConfigProfileRegistry.Konect120.IotBufferResetCoil` |

All wire values verified by the user against the documentation — no changes needed.

### RTU exception-response detection
`RtuModbusTransport.ReadExactAsync` inspects the second byte of every response: if `(byte[1] & 0x80) != 0`, it's a Modbus exception frame (5 bytes total: slave + FC|0x80 + code + 2 CRC) and the transport stops reading immediately instead of waiting for a normal full-length response. Without this, requesting an unmapped address against a device that returns "Illegal Data Address" would hang for the full 1s read timeout per request, and `DeviceConfigService` would burn through its 30s budget. This matters once we implement writes (FC06/FC16) — devices often respond with exceptions for bad addresses, and the parser already throws `ModbusProtocolException` from those.

---

---

## Mass Memory Architecture

### Overview
```
MassMemoryService         — Core service: reads control block (FC04) + blocks (FC14)
MassMemoryControlBlock    — QSF, GP, BGS, INI, CA
MassMemoryBlock           — Timestamp, Values[], ChecksumOk, BlockIndex, IterationIndex
MassMemoryViewModel       — Drives MassMemoryView; resume/restart state; TXT export
```

### FC 0x14 (ReadFileRecord) frame structure

**Request** (RTU 12 bytes): `[slaveId][0x14][BC=0x07][RT=0x06][SET(2)][BLC(2)][QTD(2)][CRC(2)]`
- `SET` = sector number, `BLC` = block number, `QTD = 3 + 2 * GP` (registers needed for `5 + 4*GP + 1` data bytes)

**Response RTU**: `[slaveId][0x14][RDL][FRL][RT=0x06][data: QTD×2][CRC(2)]`  Total = `QTD×2 + 7`
**Response TCP**: `[MBAP(7)][0x14][RDL][FRL][RT=0x06][data: QTD×2]`  Total = `QTD×2 + 11`

**CRITICAL — `rdl - 2` fix**: `RDL = FRL(1) + RT(1) + data(QTD×2) = QTD×2+2`. Actual data bytes = `rdl - 2` (NOT `rdl - 1`). The off-by-one caused "truncated: N bytes, expected N+1" errors. Fixed in both `ModbusTcpFrameParser.ParseReadFileRecord` and `ModbusRtuFrameParser.ParseReadFileRecord`.

### Block data layout (bytes after RT=0x06)
- Bytes 0–4: **DataHora** BCD (see below)
- Bytes 5 to `4+4×GP`: GP × **Float32** (4 bytes each, **little-endian IEEE 754** — NOT SQPF-permuted)
- Byte `5+4×GP`: **Checksum** — `(byte)sum(bytes[0..5+4*GP-1])`

**CRITICAL — mass memory float encoding**: floats are stored as plain little-endian IEEE 754. Use `BinaryPrimitives.ReadSingleLittleEndian`, NOT `RegisterDecoder.DecodeFloat32WithSqpf`. Confirmed from original KRON C++ source code (`val.c[0]=buff[0]` on x86 = little-endian).

### BCD DataHora decode (5 bytes b0..b4)
```
SEG  = BCD(b0 & 0x7F)
MIN  = BCD(b1 & 0x7F)
HORA = BCD(((b1 & 0x80) >> 2) | (b2 & 0x1F))
DIA  = BCD(((b2 & 0xE0) >> 2) | (b3 & 0x07))
MES  = BCD((b3 >> 3) & 0x1F)
ANO  = BCD(b4) + 2000
where BCD(v) = ((v >> 4) & 0xF) * 10 + (v & 0xF)
```
Verified: `43 24 54 84 22` → 14/10/2022 14:24:43.

### Control block (FC04, Modicon 33931, raw address 3930, 5 registers)
10 bytes: `QSF(2)`, `GP(1)`, `BGS(3)`, `INI(2)`, `CA(2)`.

### Sector/block iteration
```
sector = ctrl.INI; block = 0
for i in 0..BGS-1:
    read block (sector, block)
    if ++block >= ctrl.CA: { block = 0; sector = (sector + 1) % ctrl.QSF }
```
`ComputeStartPosition(ini, ca, qsf, startFrom)` — `internal static` pure function that fast-forwards to the correct (sector, block) for any `startFrom` iteration index. Used by `ReadBlocksAsync` for the resume feature.

### Resume / restart feature
- `MassMemoryBlock.IterationIndex` — the loop counter `i` for that block (0-based). Preserved even when some blocks fail (non-yielded). Used to track `_resumeFromIndex = blk.IterationIndex + 1`.
- `MassMemoryViewModel._hasPartialData` — set `true` on cancel or error when `Records.Count > 0`.
- `MassMemoryViewModel.AskResumeOrRestart` — `Func<Task<bool?>>?` set by `MassMemoryView` code-behind. Returns `true` = continue, `false` = start over, `null` = cancel.
- When "Start" pressed and `_hasPartialData` is true: shows resume dialog; if restarting, clears `Records` and resets `_resumeFromIndex = 0`.
- `ToggleReadingCommand` is `async Task` (`[RelayCommand]` + `AsyncRelayCommand` via CommunityToolkit.Mvvm).

### TXT export format
Matches legacy KRON software output exactly:
- Encoding: **Latin-1** (`Encoding.Latin1`)
- Metadata header: `Série: / Endereço: / Descrição: / IA:` lines, then blank line
- Grandeza descriptions: `{col.Code}: {col.Description}` per line, then blank line
- Columns: `%-15s` fixed-width fields, `;` separator, `CS` as last column (no trailing `;`)
- Values: 3 decimal places, **comma** as decimal separator (`F3` + `.Replace('.', ',')`), also `%-15s`
- `C(s) = s.PadRight(15)`, `Fmt(v) = v.ToString("F3", InvariantCulture).Replace('.', ',')`

---

## Cloud (MQTT) Architecture — field meters via broker

### Why
Field-installed KS meters (poles / remote sites) have their own connectivity (4G/Wi-Fi/LoRa) and
already publish decoded JSON telemetry to a KRON MQTT broker, and accept a limited set of commands on a
reply topic. The app reaches these meters **through the broker** instead of local TCP/RTU.

**Key principle: the `IModbusService` abstraction holds at the DATA plane, but the PRESENTATION diverges.**
Cloud devices are NOT polled and do NOT reuse the standard screens — see UI divergence below.

### Domain + persistence
- `TransportType.MqttCloud` (third enum value).
- `Domain/ValueObjects/MqttConfig.cs` — owned entity on `ModbusDevice` (`Mqtt`): `BrokerHost`, `Port` (8883),
  `UseTls`, `ClientId/Username/Password`, and topic templates. Mapped via `OwnsOne` (columns `Mqtt*`),
  migration `AddMqttCloudConfig`. Additive — RTU/TCP untouched.

### Cloud layer (`Modbus.Core/Cloud/`)
- `IMqttBrokerClient` / `MqttBrokerClient` — **MQTTnet 5** wrapper (NuGet on Modbus.Core). One connection per
  broker (keyed host/port/user), multiplexes topic subscriptions + publishes, auto-reconnect + re-subscribe.
- `ITelemetryPayloadMapper` / `JsonTelemetryPayloadMapper` — telemetry JSON → values (see format). Two methods:
  `Map(...)` → `RegisterValue[]` (cataloged fields only, for the register pipeline); `MapReadings(...)` →
  `TelemetryReading[]` (**every** numeric `metadata` field, keeping its raw payload code, with the matching
  `RegisterDefinition` or `null` when uncataloged). The cloud reading screen drives off `MapReadings`.
- `ICloudCommandService` / `CloudCommandService` — the KS MQTT command protocol (HRR/HRW/COIL/999-999).
- `CloudModbusService : IModbusService` — adapts the service contract to the command channel (read=HRR,
  write=HRW, coil=COIL); input-register reads → `NotSupportedException` (those arrive via telemetry).
  Returned by `ModbusServiceFactory` for `MqttCloud` (factory takes an optional `ICloudCommandService`).
- `MqttTopics.Resolve(template, device)` — substitutes `{serial}` with the **7-digit zero-padded** serial.

### Reads = event-driven (no polling)
`PollingEngine` takes optional `IMqttBrokerClient` + `ITelemetryPayloadMapper`. Cloud devices are kept in a
separate `_cloudDevices` map (NOT the 5s timer set): on `AddDevice` it subscribes to the telemetry topic and,
per message, calls `MapReadings` and raises **two** events:
- `TelemetryReceived` (`TelemetryReceivedEventArgs`, all published fields incl. uncataloged) — consumed by
  the cloud reading screen so it shows exactly what the meter publishes.
- `RegisterValuesUpdated` (cataloged fields only) — keeps device-list connection status / last-seen working
  like local devices, unchanged. Skipped when no cataloged field is present.
Empty payloads (log lines) raise nothing.

### KS MQTT command protocol (from firmware doc)
App PUBLISHES commands to the meter's subscribe topic `MqttConfig.CommandTopic` = `ks-01/{serial}/reply`:
- Holding read:  `{ "999-123": { "id":"<6 digits>", "HRR": ["40001","7"] } }`
- Holding write: `{ "999-123": { "id":"…", "HRW": ["42101","00050002…"] } }`
- Coil action:   `{ "999-999": { "id":"…", "COIL":"006" } }`
- Named config:  `{ "999-999": { "id":"…", "TP":"1.00","IA":"1","G1":"30003" } }`

- **Addressing**: Modicon — holding = `raw + 40001`, input = `raw + 30001` (same as `DeviceConfigService`).
- **Register values**: 4 hex digits each, big-endian, concatenated (`EncodeRegistersHex`/`DecodeRegistersHex`).
- **Coils**: KS `COIL` is 1-based → `COIL = wireAddress + 1` formatted `D3` (reset coil wire 5 → `"006"`).
  Coil/config commands are **fire-and-forget** (the reset reboots the meter; reply not guaranteed).
- **Responses** arrive UNwrapped on the data topic: `{ "HRR":"…hex…" }` (reads) /
  `{ "Message":"HRW Success", … }` (writes). The meter does **NOT echo the request `id`**, so
  `CloudCommandService` correlates by **single-flight** (one outstanding command per response topic);
  telemetry on the same topic is ignored (only `HRR`/`Message` objects complete a pending request).

### Telemetry format (confirmed from a real meter capture)
Data payload is a JSON **array** wrapping one object; values live under `metadata`, timestamp under `time`:
```json
[{ "variable":"data", "time":"2026-04-06 14:45:00",
   "metadata":{ "U0":201.80, "I0":0.00, "F1":59.99, "P0":0.00, "FP0":0.00, "EA":0.00, "CE":1 } }]
```
- `JsonTelemetryPayloadMapper` unwraps array→`metadata`, parses `time` (`yyyy-MM-dd HH:mm:ss`), and matches
  fields to register-definition names. `Map` ignores log lines and uncataloged fields; `MapReadings` requires
  a `metadata` object (so log lines stay empty) but **surfaces uncataloged fields** (e.g. `CE`) with a null
  definition so the screen can show them under "Outros".
- **Field aliases** (telemetry name → register name, the rest match directly):
  `F1→Freq`, `EA→EA+`, `ER→ER+`, `EAN→EA-`, `ERN→ER-`.
- The **data topic is installation-configurable** (a capture used just `ks`) — entered in the Add-device UI
  and stored in both `MqttConfig.TelemetryTopic` and `ReplyTopic`; the command topic stays serial-based.

### UI divergence (decided with the user — cloud is NOT the same as RTU/TCP)
`DeviceHubViewModel.IsCloud` branches the per-device hub:
- **Mass-memory card hidden** (no FC14 equivalent); hub **skips the FC 0x79 capability probe** for cloud
  (it would fire a real MQTT command and block on the reply timeout).
- **Leituras (telemetria)** → `CloudReadingViewModel` / `CloudReadingView` — pure push, **fully dynamic**:
  subscribes `TelemetryReceived` and creates one row the first time each quantity appears in a payload, so
  it shows **only** what the meter actually publishes (its user-selected G1..G50) + last-update time.
  Rows are grouped by code prefix (Tensões/Correntes/… and Energias/Demandas); fields with no register
  definition land in **"Outros"** using the raw payload code as label. Empty until the first message
  (`HasData` gates the "aguardando telemetria" hint). No polling, no I/O/status tabs, no bus pausing.
- **Configuração MQTT** → `CloudConfigureViewModel` / `CloudConfigureView` — only the MQTT-settable params via
  the **999-999 named commands**: TP, TC, TL, TI, KE, THRS, RT, IA, relay `sd1`, `G1..G20`, plus action coils
  (reset device 006 / reset energies 040 / init hourmeter 062 / reset MQTT buffer 091). Empty fields are left
  unchanged; writes go through `ICloudCommandService.SendConfigAsync` (fire-and-forget).
- `ICloudCommandService` is threaded by DI: `App.axaml.cs` → `DeviceListViewModel` → `DeviceHubViewModel` →
  cloud VMs. DataTemplates for both cloud views added in `App.axaml`.

### Add-device flow (cloud)
`AddDeviceViewModel` gained a **"Nuvem (MQTT)"** transport option: broker host/port/TLS/credentials, serial
(identifies topics), device model (gives the mapper its register map), and the configurable **data topic**.
No RTU/UDP scan in this mode.

### STILL PENDING from firmware (confirm before field test)
- Exact topic for command RESPONSES (HRR/HRW replies) — currently assumed to be the same configurable data
  topic; confirm and adjust `MqttConfig.ReplyTopic` if different.
- Confirmation that single-flight correlation is acceptable (meter omits the request `id` in replies).

### Cloud tests (`Modbus.Core.Tests/Cloud/`)
`JsonTelemetryPayloadMapperTests` (array/metadata, aliases, `time`, log-ignore, **plus `MapReadings`**:
published-only/order, uncataloged field surfaces with null def, alias keeps raw code, works with no model,
log-ignore), `CloudModbusServiceTests` (delegation, NotSupported), `CloudCommandServiceTests` (HRR/HRW
envelopes, Modicon conversion, hex codec, COIL, 999-999 config, timeout), `CloudPollingTests` (telemetry →
`RegisterValuesUpdated`, no factory call). Total suite **289 tests passing**.

### Where I left off (resume here) — feature is WIP, not field-validated
Done: Core layer + both cloud UI screens implemented; `dotnet build` compiles; 284 Core tests pass.
Next session, in order:
1. **Clean build + run** on the dev machine (last session the app was running, so the Desktop DLL
   couldn't be copied — only the copy step failed, compilation was clean). DB auto-migrates on startup
   (`Migrate()` applies `AddMqttCloudConfig`), no manual DB reset needed.
2. **End-to-end with the real meter** (not yet done): Add device → "Nuvem (MQTT)" → broker host/port/TLS,
   serial, **data topic** (the capture used just `ks`), model KS-3000. Publish telemetry → confirm the
   readings screen updates. Then send a config command (e.g. IA, or an action coil) and **observe which
   topic the response lands on**.
3. **Confirm with firmware** and adjust if needed: the command RESPONSE topic (code currently assumes the
   same configurable data topic as telemetry → `MqttConfig.ReplyTopic`) and that single-flight correlation
   is acceptable (replies omit the request `id`).
4. Known rough edges to revisit: telemetry `time` is parsed as wall-clock (no timezone) — the reading screen
   instead stamps `LastUpdate` with the receive time; the readings screen is now payload-driven (shows what
   the meter actually publishes, including uncataloged fields under "Outros"), but the **config** screen still
   has no G1..G50 read-back; config writes are fire-and-forget (no success feedback from the meter).

---

## Mobile Roadmap — experimento paralelo (Avalonia + MAUI)

**Objetivo:** dois apps mobile **independentes** consumindo o mesmo `Modbus.Core`, para comparar as
duas tecnologias ponta a ponta. Decisões fixadas com o usuário:
- **Apps independentes** — cada app tem suas próprias ViewModels (copiadas/adaptadas do desktop). Só
  `Modbus.Core` é compartilhado. Divergência entre as VMs dos apps é esperada e aceita.
- **Transportes no mobile:** **TCP + Cloud (MQTT)**. RTU/serial descartado (inviável iOS, complexo
  Android) → sem `IDeviceScanService`/`System.IO.Ports` no mobile.
- **MVP (fatia vertical):** Adicionar dispositivo → Lista com status → Leituras em tempo real. Config,
  memória de massa e abas de I/O ficam para depois.
- **Ordem:** Avalonia primeiro, depois MAUI espelhando as mesmas fases.

**Notas técnicas para retomar:**
- `Modbus.Core` é net8.0 puro (sem UI) — referenciar direto. DB path
  `Environment.SpecialFolder.LocalApplicationData` + `ModbusApp/modbusapp.db` funciona em Android/iOS.
  Rodar `DatabaseInitializer.Initialize` + `DeviceModelSeeder` no startup (padrão de `Modbus.Desktop/App.axaml.cs`).
- DI mínimo do MVP: `ModbusDbContext`, repos (`IDeviceRepository`/`IDeviceModelRepository`/`IRegisterValueRepository`),
  `IModbusServiceFactory`, `IPollingEngine`, Cloud (`IMqttBrokerClient`/`ITelemetryPayloadMapper`/`ICloudCommandService`),
  `DeviceModelSeeder`. Pular config/massmemory/scan no MVP.
- Acoplamento Avalonia a tratar nas VMs: `Dispatcher.UIThread.InvokeAsync` (DeviceList/AddDevice/CloudReading)
  — no **Avalonia mobile** funciona igual; no **MAUI** vira `MainThread.BeginInvokeOnMainThread`.
- Manter Avalonia mobile alinhado ao desktop (**11.2.3**). Validar SQLite (SQLitePCLRaw bundle) na Fase A1.

**Estado das fases** (atualizar a cada sessão — TODO/DOING/DONE):
- [DONE] **Fase 0** — Registrar este roadmap no CLAUDE.md
- [DONE] **Fase A1** (Avalonia) — Scaffold (`Modbus.Mobile.Avalonia` compartilhado + `Modbus.Mobile.Avalonia.Android` head) + DI mínimo + DB migra/seed; **validado: app sobe no Genymotion** mostrando "DB inicializado · N modelos seedados" (Avalonia 11.2.3 + EF Core + SQLite nativo OK no device). Projetos adicionados ao `ModbusApp.sln`. Ver "Setup mobile Avalonia (A1)" abaixo.
- [TODO] **Fase A2** (Avalonia) — Lista de dispositivos + Adicionar (TCP + Cloud; sem RTU). Persistência via repos do Core.
- [TODO] **Fase A3** (Avalonia) — Leituras em tempo real + polling (TCP) + telemetria Cloud. **Fecha o MVP do app A.**
- [TODO] **Fase A4** (Avalonia, opcional) — Localização, tema, back-stack, deploy em device físico.
- [TODO] **Fase M1** (MAUI) — Scaffold + DI + DB (espelha A1; `MauiProgram.cs`, workloads MAUI).
- [TODO] **Fase M2** (MAUI) — Lista + Adicionar (espelha A2; `MainThread` no lugar de `Dispatcher`).
- [TODO] **Fase M3** (MAUI) — Leituras em tempo real + polling. **Fecha o MVP do app B.**
- [TODO] **Fase C** — Avaliação comparativa (esforço, tamanho, performance, fidelidade visual, integração); registrar conclusão aqui.

**Riscos:** Android Wi-Fi exige celular na mesma rede do medidor (TCP local); scan UDP broadcast pode
precisar de `WifiManager.MulticastLock` → MVP pode começar com IP manual.

### Setup mobile Avalonia (A1) — como buildar/rodar e achados
**Projetos:** `Modbus.Mobile.Avalonia` (net8.0, compartilhado: App.axaml + DI + ViewLocator +
MainView/MainViewModel) e `Modbus.Mobile.Avalonia.Android` (net8.0-android, head:
`MainActivity : AvaloniaMainActivity<App>`, `ApplicationId = com.kron.modbusapp.avalonia`).
Avalonia fixado em **11.2.3** (igual desktop). DI = subconjunto MVP do desktop (sem scan/config/massmemory).

**Pré-requisitos do ambiente (já satisfeitos nesta máquina):**
- Workload: `dotnet workload install android` (instalada — Microsoft.Android.Sdk 34.0.154).
- Android SDK: `%LOCALAPPDATA%\Android\Sdk` (Android Studio); `adb` em `platform-tools\adb.exe`.
- JDK 17: `C:\Program Files\Android\Android Studio\jbr` (exportar `JAVA_HOME` se o build reclamar).

**Rodar/Debug (emulador OU celular USB):** ligar a VM Genymotion e/ou plugar o celular (com
"Depuração USB" ativada e autorizada), então `Tasks: Android: Debug (build + deploy + run)` no
VSCode (ou `pwsh scripts/run-android-avalonia.ps1`). O script
([scripts/run-android-avalonia.ps1](../scripts/run-android-avalonia.ps1)) resolve o adb (SDK →
fallback Genymotion), **lista os devices e escolhe** (auto se 1; menu se vários; ou `-Device <serial>`),
checa `boot_completed`, faz `dotnet build -t:Install` no head, lança via `monkey` e streama
`adb logcat --pid=<app>` (pega exceções gerenciadas + `Debug.WriteLine`). Flags: `-NoLogcat`,
`-AllLog`, `-Device`, `-Configuration`. Não há F5/debug nativo (breakpoints) no A1 — o "launch" é
build+deploy+run+logcat. **Importante:** o `.ps1` deve ficar só em ASCII (PowerShell 5.1 quebra o
parser com acentos/`•`/`—` sem BOM).

**Achados / gotchas (não repetir):**
- **Tema da Activity**: `AvaloniaMainActivity` herda de `AppCompatActivity` → o tema em
  `Resources/values/styles.xml` **precisa** descender de `Theme.AppCompat` (usei
  `Theme.AppCompat.Light.NoActionBar`). Um `@android:style/Theme.Material...` causa crash no
  startup: *"You need to use a Theme.AppCompat theme (or descendant)"*.
- **SQLite nativo funciona** no Android via `Microsoft.EntityFrameworkCore.Sqlite`
  (SQLitePCLRaw bundle) sem `Batteries_V2.Init()` manual. Os warnings
  `monodroid-assembly: open_from_bundles: failed to load assembly ...` são probes benignos.
- **Genymotion adb**: garantir que o Genymotion use o adb do SDK (Settings → ADB → custom SDK
  tools) — o device aparece como `192.168.56.x:5555`. `sys.boot_completed=1` antes de instalar
  (install falha com `cmd: Can't find service: package` se a VM ainda estiver no boot animation).
- Template `avalonia.xplat` (12.0.4) gera net10/Avalonia 12/Central Package Management — **não**
  usei direto; reescrevi os csproj fixando net8.0(-android) + Avalonia 11.2.3.

---

### Pending / future features - Attention! Keep it in the end of the file
- ~~Mobile app (MAUI) connected to the same core as the desktop~~ → ver "Mobile Roadmap" acima (em execução por fases)