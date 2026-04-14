namespace Modbus.Desktop.Services.Strings;

internal static class PortugueseStrings
{
    public static readonly Dictionary<string, string> All = new()
    {
        // Navigation
        ["NavDevices"]           = "Dispositivos",
        ["NavSettings"]          = "Configurações",

        // MainWindow sidebar
        ["AppSubtitle"]          = "Monitor de Dispositivos",

        // DeviceListView
        ["Devices"]              = "Dispositivos",
        ["AddDevice"]            = "+ Adicionar Dispositivo",
        ["Refresh"]              = "Atualizar",
        ["NoDevicesFound"]       = "Nenhum dispositivo encontrado. Adicione um dispositivo para começar.",
        ["Open"]                 = "Abrir →",
        ["Delete"]               = "Excluir",
        ["NeverSeen"]            = "Nunca visto",
        ["UnknownModel"]         = "Modelo desconhecido",
        ["AddrPrefix"]           = "End. {0}",

        // Delete dialog
        ["ConfirmDelete"]        = "Confirmar Exclusão",
        ["ConfirmDeleteMsg"]     = "Excluir \"{0}\"? Esta ação não pode ser desfeita.",
        ["Cancel"]               = "Cancelar",

        // AddDeviceView
        ["Back"]                 = "← Voltar",
        ["AddDeviceTitle"]       = "Adicionar Dispositivo",
        ["TransportType"]        = "Tipo de Transporte",
        ["RtuOption"]            = "RTU (Porta Serial)",
        ["TcpOption"]            = "TCP (Ethernet / Wi-Fi)",
        ["SerialPortSettings"]   = "Configurações da Porta Serial",
        ["ComPort"]              = "Porta COM",
        ["RefreshPortList"]      = "Atualizar lista de portas",
        ["BaudRate"]             = "Baud Rate",
        ["DataBits"]             = "Data Bits",
        ["Parity"]               = "Parity",
        ["StopBits"]             = "Stop Bits",
        ["AddressRange"]         = "Faixa de Endereços",
        ["NetworkSettings"]      = "Configurações de Rede",
        ["NetworkSettingsHint"]  = "A descoberta usa broadcast UDP (255.255.255.255). Os dispositivos na rede local responderão automaticamente.",
        ["SearchNetwork"]        = "Buscar na Rede",
        ["FoundDevices"]         = "Dispositivos Encontrados",
        ["NoFoundDevicesYet"]    = "Nenhum dispositivo encontrado. Execute uma varredura ou preencha os detalhes manualmente abaixo.",
        ["DeviceDetails"]        = "Detalhes do Dispositivo",
        ["Name"]                 = "Nome",
        ["DeviceNameWatermark"]  = "ex.: KS-3000 #0012345",
        ["Address"]              = "Endereço (1–247)",
        ["DeviceIp"]             = "IP do Dispositivo",
        ["DeviceIpWatermark"]    = "ex.: 192.168.1.42",
        ["SaveDevice"]           = "Salvar Dispositivo",

        // AddDeviceViewModel status messages
        ["Ready"]                = "Pronto",
        ["SelectComPortFirst"]   = "Selecione uma porta COM primeiro.",
        ["ScanningProgress"]     = "Varrendo {0} ({1}/{2})",
        ["ScanDone"]             = "Concluído — {0} dispositivo(s) encontrado(s).",
        ["ScanNoResponse"]       = "Varredura concluída. Nenhum dispositivo respondeu.",
        ["ScanCancelled"]        = "Varredura cancelada.",
        ["ErrorPrefix"]          = "Erro: {0}",
        ["DuplicateSerial"]      = "Já existe um dispositivo com o número de série {0:D7}.",
        ["DeviceSaved"]          = "Dispositivo salvo. Selecione outro da lista ou volte.",

        // DeviceDetailView
        ["Transport"]            = "Transporte",
        ["Connection"]           = "Conexão",
        ["SlaveAddress"]         = "Endereço",
        ["Model"]                = "Modelo",
        ["Unknown"]              = "Desconhecido",
        ["RegisterValues"]       = "Valores de Registrador",
        ["NoRegisterValues"]     = "Sem valores de registrador. Os valores aparecerão após a primeira leitura bem-sucedida.",
        ["ColName"]              = "Nome",
        ["ColAddress"]           = "Endereço",
        ["ColType"]              = "Tipo",
        ["ColValue"]             = "Valor",
        ["ColLastUpdated"]       = "Última Atualização",

        // Electrical Readings — Tabs
        ["TabRealTime"]          = "Leituras em Tempo Real",
        ["TabRawRegisters"]      = "Registradores Brutos",
        ["NoLiveData"]           = "Sem dados ao vivo. Aguardando próxima leitura...",

        // Electrical Readings — Groups
        ["GroupVoltages"]        = "Tensões",
        ["GroupCurrents"]        = "Correntes",
        ["GroupFrequency"]       = "Frequência",
        ["GroupActivePower"]     = "Potência Ativa",
        ["GroupReactivePower"]   = "Potência Reativa",
        ["GroupApparentPower"]   = "Potência Aparente",
        ["GroupPowerFactor"]     = "Fator de Potência",

        // Electrical Readings — Register descriptions
        ["RegU0"]                = "Trifásica",
        ["RegU12"]               = "Fase A-B",
        ["RegU23"]               = "Fase B-C",
        ["RegU31"]               = "Fase C-A",
        ["RegU1"]                = "Linha 1",
        ["RegU2"]                = "Linha 2",
        ["RegU3"]                = "Linha 3",
        ["RegI0"]                = "Trifásica",
        ["RegI1"]                = "Linha 1",
        ["RegI2"]                = "Linha 2",
        ["RegI3"]                = "Linha 3",
        ["RegFreq"]              = "Linha 1",
        ["RegP0"]                = "Trifásica",
        ["RegP1"]                = "Linha 1",
        ["RegP2"]                = "Linha 2",
        ["RegP3"]                = "Linha 3",
        ["RegQ0"]                = "Trifásica",
        ["RegQ1"]                = "Linha 1",
        ["RegQ2"]                = "Linha 2",
        ["RegQ3"]                = "Linha 3",
        ["RegS0"]                = "Trifásica",
        ["RegS1"]                = "Linha 1",
        ["RegS2"]                = "Linha 2",
        ["RegS3"]                = "Linha 3",
        ["RegFP0"]               = "Trifásico",
        ["RegFP1"]               = "Linha 1",
        ["RegFP2"]               = "Linha 2",
        ["RegFP3"]               = "Linha 3",

        // DeviceItemViewModel
        ["Connected"]            = "Conectado",
        ["Disconnected"]         = "Desconectado",

        // RegisterValueViewModel
        ["RegisterFallback"]     = "Registrador {0}",

        // SettingsView
        ["SettingsTitle"]        = "Configurações",
        ["Language"]             = "Idioma",
        ["ChangeInSettings"]     = "Alterar nas Configurações",
        ["NoPortConfigured"]     = "Nenhuma porta COM configurada.",
        ["PortUnavailable"]      = "Porta não conectada. Verifique se o dispositivo está plugado.",
    };
}
