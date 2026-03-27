  ---                                                                  
  Project Structure                                         
                                                                       
  ModbusApp/                                                
  ├── Modbus.Core/         ← All logic, NO UI                        
  └── Modbus.Desktop/      ← Avalonia UI, wires everything together    
                                                                       
  Think of it like a restaurant:                                       
  - Modbus.Core is the kitchen — it does all the real work             
  - Modbus.Desktop is the dining room — it presents information to the 
  user                                                               
                                                                       
  ---                                                       
  Modbus.Core — Layer by Layer                                         
                                                                       
  Layer 1: Domain (Domain/)                                          
                                                                       
  These are plain C# classes that represent your data. No logic, just  
  structure.                                                         
                                                                       
  ModbusDevice       — a physical device (has IP/COM port, slave ID,
  name)                                                                
  DeviceModel        — a device type/template (e.g. "PowerMeter X200")
  RegisterDefinition — one register in a device model (address, name,  
  unit, scale)                                                         
  RegisterValue      — a reading from a device (address + value +      
  timestamp)                                                           
                                                                     
  DeviceModel has many RegisterDefinitions.                            
  ModbusDevice belongs to a DeviceModel.                    
  ModbusDevice has many RegisterValues (historical readings).          
                                                                       
  Layer 2: Protocol (Protocol/)                                      
                                                                       
  Handles the raw Modbus wire format — building and parsing byte       
  arrays.                                                            
                                                                       
  ModbusTcpFrameBuilder   — builds TCP packets (adds MBAP header:
  transaction ID, length, etc.)                                        
  ModbusTcpFrameParser    — reads TCP responses back into register
  values                                                               
  ModbusRtuFrameBuilder   — builds RTU packets (adds slave ID + CRC16
  checksum at the end)                                                 
  ModbusRtuFrameParser    — reads RTU responses
                                                                       
  You never touch this layer directly. It's used internally by the     
  transport layer.                                                   
                                                                       
  Layer 3: Transport (Transport/)                           
                                                                     
  Handles the actual connection — opening sockets, sending bytes,      
  receiving bytes.
                                                                       
  IModbusTransport       — the interface (contract)         
  TcpModbusTransport     — opens a TCP socket to an IP:port,         
  sends/receives bytes                                                 
  RtuModbusTransport     — opens a serial port (COM/ttyS),
  sends/receives bytes                                                 
                                                            
  The transport doesn't know what the bytes mean. It just moves them.  
                                                            
  Layer 4: Services (Services/)                                        
                                                            
  This is the main entry point your app talks to. It combines transport
   + protocol.
                                                                       
  IModbusService         — the contract: ConnectAsync,      
  ReadHoldingRegisters, WriteSingle, etc.                              
  ModbusService          — implements IModbusService using a transport
  + frame builder/parser                                               
  IModbusServiceFactory  — creates an IModbusService for a given
  ModbusDevice                                                         
  ModbusServiceFactory   — looks at the device's TransportType and
  creates the right service                                            
                                                            
  One IModbusService = one connection to one device.                   
                                                            
  Layer 5: Persistence (Persistence/)                                  
                                                            
  Saves and loads data from a local SQLite database using EF Core.     
   
  ModbusDbContext                  — the EF Core "gateway" to the      
  database                                                             
  DeviceRepository                 — load/save/delete ModbusDevice   
  records                                                              
  DeviceModelRepository            — load/save DeviceModel records
  RegisterValueRepository          — load/upsert RegisterValue records 
   
  EF Core handles the SQL for you. You just call GetAllAsync() and get 
  back C# objects.                                          
                                                                       
  Layer 6: Polling (Polling/)                               
                                                                     
  Runs in the background, continuously reading all devices.

  IPollingEngine         — start/stop polling, add/remove devices
  PollingEngine          — every 5 seconds, for each active device:    
                             1. Connect if not connected
                             2. Read all registers defined in the      
  device model                                              
                             3. Fire RegisterValuesUpdated event (UI   
  updates)                                                             
                             or fire DeviceConnectionFailed event (on
  error)                                                               
                                                            
  The polling engine is the "heartbeat" of the app. It fires events    
  that the UI listens to.                                   
                                                                       
  ---                                                       
  Modbus.Desktop — Layer by Layer                                    
                                 
  Dependency Injection (App.axaml.cs)
                                                                       
  When the app starts, before any window opens, we register all        
  services into a container:                                           
                                                                       
  services.AddDbContext<ModbusDbContext>(...)   // database          
  services.AddTransient<IDeviceRepository>()   // repositories         
  services.AddSingleton<IPollingEngine>()       // one global polling  
  engine                                                               
  services.AddSingleton<MainViewModel>()        // one main VM for the 
  window                                                               
                                                                     
  Transient = a new instance every time you ask for one.               
  Singleton = one instance shared across the entire app.    
                                                                       
  The container is like a factory — instead of new                     
  DeviceRepository(db), you ask the container and it figures out the   
  dependencies for you.                                                
                                                                     
  ViewModels (ViewModels/)                                           

  ViewModels are the bridge between the data and the UI. They hold     
  state and commands. The UI binds to them.
                                                                       
  MainViewModel          — owns the sidebar navigation and the current
  visible page                                                         
  DeviceListViewModel    — holds the list of devices, triggers load,
  handles polling events                                               
  DeviceItemViewModel    — wraps one ModbusDevice, tracks   
  IsConnected/HasError/LastSeenAt                                      
  DeviceDetailViewModel  — holds register values for one device, loaded
   from DB                                                             
  RegisterValueViewModel — wraps one RegisterValue + its    
  RegisterDefinition (for the name/unit)                               
  NavItem                — just a title + icon for the sidebar button
                                                                       
  CommunityToolkit.Mvvm generates the boilerplate for you:             
  - [ObservableProperty] on _isConnected → auto-generates IsConnected  
  property + PropertyChanged event                                     
  - [RelayCommand] on LoadDevicesAsync() → auto-generates            
  LoadDevicesCommand you can bind buttons to                           
                                                                       
  Views (Views/)                                                     
                                                                       
  Views are the visual layer. They are AXAML files (similar to         
  XML/HTML) that describe layout and bind to ViewModel properties.   
                                                                       
  MainWindow.axaml       — the shell: 220px dark sidebar + content area
   on the right                                                        
  DeviceListView.axaml   — list of device cards
  DeviceDetailView.axaml — device info + register values table         
                                                            
  Key concept — DataTemplates:                                         
  In App.axaml, we define:                                  
  <DataTemplate DataType="{x:Type vm:DeviceListViewModel}">            
      <views:DeviceListView />                              
  </DataTemplate>                                                      
  This tells Avalonia: "when you see a DeviceListViewModel as content, 
  render it using DeviceListView".                                   
                                                                       
  The MainWindow has:                                       
  <ContentControl Content="{Binding CurrentPage}" />                   
  When CurrentPage changes (e.g. from DeviceListViewModel to
  DeviceDetailViewModel), Avalonia automatically swaps the view. This  
  is navigation without any navigator class.                           
                                                                       
  ---                                                                  
  Data Flow — From Hardware to Screen                                
                                                                       
  Here's what happens when a device is polled:
                                                                       
  PollingEngine (background thread)                         
      → TcpModbusTransport.SendAsync()       [opens socket, sends      
  bytes]                                                               
      → ModbusTcpFrameParser.Parse()         [decodes the response]    
      → fires RegisterValuesUpdated event                              
                                                                     
  DeviceListViewModel (subscriber)                                     
      → Dispatcher.UIThread.InvokeAsync()    [marshal to UI thread -
  IMPORTANT]                                                           
      → DeviceItemViewModel.IsConnected = true              
      → DeviceItemViewModel.LastSeenAt = now                           
                                                            
  UI (Avalonia binding)                                                
      → TextBlock.Text = "{Binding StatusText}"  [auto-updates because
  of INotifyPropertyChanged]                                           
                                                            
  The Dispatcher.UIThread.InvokeAsync step is critical — you can never 
  update UI properties from a background thread in any UI framework.
  The dispatcher queues the update onto the UI thread safely.          
                                                            
  ---                                                                
 * Add device implementation *
 All done. Here's a summary of everything that was built:            
                                                                      
  ---                                   
  What was implemented                                                
                                                                      
  Modbus.Core — 8 files                                               
                                                                      
  File: Domain/Entities/DeviceModel.cs                                
  Change: Added byte? DeviceCode property                             
  ────────────────────────────────────────                            
  File: Persistence/Configurations/DeviceModelConfiguration.cs        
  Change: Configured DeviceCode as nullable column                    
  ────────────────────────────────────────                          
  File: Services/DeviceCodeRegistry.cs                                
  Change: Static registry: 0xF2 → "KS-3000"                           
  ────────────────────────────────────────                            
  File: Services/Scanning/DeviceScanResult.cs                         
  Change: Scan result model with FirmwareVersionText/SerialNumberText
    helpers                                                           
  ────────────────────────────────────────                            
  File: Services/Scanning/ScanProgress.cs                             
  Change: Progress model (Current, Total, Found, CurrentLabel)        
  ────────────────────────────────────────                            
  File: Services/Scanning/IDeviceScanService.cs                       
  Change: Interface with ScanRtuAsync / ScanTcpAsync returning        
    IAsyncEnumerable<DeviceScanResult>                             
  ────────────────────────────────────────                            
  File: Services/Scanning/DeviceScanService.cs                        
  Change: RTU: one port, iterates addresses. TCP: one connection per  
    IP. Reads serial from Input Reg 0–1 (UInt32 BE). Decodes device   
    code via registry.                                                
  ────────────────────────────────────────                  
  File: Persistence/DeviceModelSeeder.cs                              
  Change: Idempotent startup seeder — inserts KS-3000 (and any future
    registry entries) if not already present                          
                                                            
  Modbus.Desktop — 8 files  

  File: ViewModels/ScanResultViewModel.cs                           
  Change: Immutable display wrapper around DeviceScanResult         
  ────────────────────────────────────────                          
  File: ViewModels/AddDeviceViewModel.cs                            
  Change: Full scan + form VM: transport toggle, RTU/TCP params, async
                                                                      
    cancellable scan, auto-fill on result selection, save flow with
    polling reload                                                    
  ────────────────────────────────────────                  
  File: Views/AddDeviceView.axaml                                     
  Change: Complete UI: transport radio, RTU/TCP param panels, scan
    button + progress, results list, device details form, save button 
  ────────────────────────────────────────                  
  File: Views/AddDeviceView.axaml.cs                                  
  Change: Code-behind
  ────────────────────────────────────────                            
  File: ViewModels/DeviceListViewModel.cs                             
  Change: Added IDeviceScanService + IDeviceModelRepository injection;
                                                                      
    OpenAddDeviceCommand; LoadDevicesAsync made internal    
  ────────────────────────────────────────
  File: Views/DeviceListView.axaml                                    
  Change: Added + Add Device button in header
  ────────────────────────────────────────                            
  File: App.axaml                                                     
  Change: Added DataTemplate for AddDeviceViewModel → AddDeviceView
  ────────────────────────────────────────                            
  File: App.axaml.cs                                                  
  Change: Registered IDeviceScanService; seeder runs at startup after
    EnsureCreated                                                     
                                                            
  To test with your KS-3000 on COM5, 9600 8N2                         
   
  1. Launch app → delete modbus.db first if it existed before (to pick
   up the new DeviceCode column)                            
  2. Click + Add Device → RTU is pre-selected                         
  3. Select COM5, baud 9600, parity None, stop bits Two, data bits 8
  4. Set address range 1 – 10 (or 1 – 1 for a quick test)             
  5. Click Search Network — the KS-3000 at address 1 should appear    
  with its serial number auto-filled                                  
  6. Click it → name is auto-filled as KS-3000 #XXXXXXXX              
  7. Click Save Device → device appears in the list and polling begins
    
Whats the problem now:
  - PC COM ports aren't being listed
  - TCP devices search should be done by sending an UDP broadcast to address 255.255.255.255 with the frame 00 00 00 f6
  - On add device screen when changing the Transport type radio button the fields below it should changem since RTU and TCP doesn't use the same fields
  