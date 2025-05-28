# Patron Monitoring Agent

## Přehled

Endpoint Management Software (EMS) je profesionální klientská aplikace pro správu a monitoring koncových bodů (PC/serverů) ve firemním prostředí s podporou více klientů, bezpečnou autentizací, vzdálenými příkazy a auto-updatem. Správa probíhá přes API na Laravel serveru (PHP).

---

## Hlavní funkce

- **Ovládání přes Laravel API** (Bearer token, JSON)
- **Podpora více klientů** (UUID, validace serverem)
- **Serilog s JSON logy, event log**
- **ID a log TeamVieweru (rozšiřitelné)**
- **IoC, interface-driven design, DI kontejner**
- **Konfigurace v registrech (AES šifrovaný token, interval)**
- **Retry logika pro API**
- **Watchdog služba (hlídání agenta i tray aplikace)**
- **Remote commands (restart, vypnutí, exec, update)**
- **Detekce a hlášení vypnutí/restartu stanice**
- **Auto-update**
- **Monitoring mapped network drives (uživatelská session)**
- **MSI instalátor**
- **Unit testy (NUnit)**
- **Tray aplikace (stav, ruční restart, heartbeat pro watchdog)**
- **Strukturované logy, monitoring, serializované logy (JSON)**
- **Systémový monitoring: CPU, GPU, RAM, disky, systémové logy**
- **Monitoring user session: uživatel, čas, mapped drives, přístupné cesty**
- **API Rate-limit handling**
- **Extensibility (plugin architektura)**
- **Bezpečnostní review**
- **Disaster recovery**

---

## Architektura

- **Program.cs** – Start, DI kontejner
- **Interfaces** – IApiClient, IConfigurationProvider, ILoggerService, IRemoteCommandHandler, IUpdateService, IDriveMonitor, IWatchdog, ISystemMonitor, ISessionMonitor
- **Services** – ApiClient, RegistryConfigurationProvider, SerilogLogger, RemoteCommandHandler, UpdateService, DriveMonitor, WatchdogService, SystemMonitor, SessionMonitor
- **PluginInterface** – rozšíření monitoring/remote funkcí
- **TrayApp** – Windows Forms/WPF tray aplikace
- **Unit Tests** – NUnit projekt

---

## Konfigurace a registrace

- **Token (AES šifrovaný):** `HKLM\Software\Company\EMS\Token`
- **Interval:** `HKLM\Software\Company\EMS\Interval`
- **UUID klienta:** `HKLM\Software\Company\EMS\UUID`

---

## API SPECIFIKACE

### Autentizace

- Všechny endpointy vyžadují Bearer token (`Authorization: Bearer ...`).

---

### 1. Registrace/Validace klienta

```
POST /api/clients/register
Content-Type: application/json
Authorization: Bearer {token}

{
  "uuid": "<UUID>",
  "hostname": "<PC-NAME>",
  "system": {
    "os": "Windows 11 Pro",
    "cpu": "Intel Core i7-13700K",
    "ram": "32GB",
    "gpu": "NVIDIA RTX 4070",
    "disks": [
      {"name":"C:", "size":"1TB", "free":"600GB", "type":"SSD"}
    ]
  }
}
```

**Odpověď:**
```json
{
  "status": "ok",
  "interval": 60,
  "token": "..........",
  "update_url": null
}
```

---

### 2. Heartbeat

```
POST /api/clients/{uuid}/heartbeat
Content-Type: application/json
Authorization: Bearer {token}

{
  "status": "online",
  "last_error": "",
  "drives": [
    { "letter": "Z:", "path": "\\\\server\\data" }
  ],
  "system_monitor": {
    "cpu_usage": 14.3,
    "ram_usage": 53.2,
    "gpu_usage": 12.1,
    "disks": [
      {"name":"C:", "free":"600GB", "usage": 40.1}
    ]
  },
  "session_monitor": {
    "user": "jnovak",
    "session_start": "2025-05-23T06:30:00Z",
    "mapped_drives": [
      {"letter":"Z:","path":"\\\\server\\data"}
    ],
    "accessible_paths": [
      "C:\\Users\\jnovak\\Documents",
      "Z:\\"
    ]
  }
}
```

**Odpověď:**
```json
{
  "status": "ok",
  "remote_commands": [],
  "update_url": null
}
```

---

### 3. Log upload

```
POST /api/clients/{uuid}/log
Content-Type: application/json
Authorization: Bearer {token}

{
  "log": "<json-log-blob>",
  "system_logs": [
    {"source":"System","entryType":"Error","time":"2025-05-23T06:00:00Z","message":"Disk error..."}
  ]
}
```

**Odpověď:**
```json
{
  "status": "ok"
}
```

---

### 4. Remote commands

- Remote commands jsou vráceny v odpovědi serveru v poli `remote_commands`:

```json
{
  "remote_commands": [
    { "type": "restart" },
    { "type": "shutdown" },
    { "type": "exec", "command": "ipconfig /all" },
    { "type": "update", "url": "https://..." }
  ]
}
```

---

### 5. Hlášení vypnutí/restartu stanice

Při vypnutí nebo restartu počítače agent služba odešle na server informaci o události:

```
POST /api/clients/{uuid}/shutdown
Content-Type: application/json
Authorization: Bearer {token}

{
  "type": "system_shutdown",    // nebo "system_restart", "service_stop"
  "time": "2025-05-24T15:49:00Z",
  "user": "ONyklicek",
  "hostname": "DESKTOP-123"
}
```

**Odpověď:**
```json
{
  "status": "ok"
}
```

Poznámka: Typ události (`type`) může být např. `"system_shutdown"`, `"system_restart"` nebo `"service_stop"` dle zjištěného důvodu.

---

### 6. API Rate-limit handling

- Pokud API vrátí HTTP 429 (Too Many Requests), klient:
  - Respektuje hlavičku `Retry-After` (pokud je přítomna).
  - Pokud chybí, použije exponenciální back-off.
  - Události o rate-limitu loguje včetně endpointu, času, do Serilog logu.
- Pokud je překročen limit opakovaně, klient automaticky zvýší interval heartbeat a log uploadů dle konfigurace serveru nebo přednastavených pravidel.

---

### 7. Disaster recovery

- Pokud není server dostupný:
  - Klient data (logy, monitoring) ukládá lokálně (do šifrovaného souboru).
  - Po obnovení spojení dojde k dávkovému uploadu.
  - Kritické incidenty (např. restart watchdogem) jsou nahlášeny po obnově spojení.
  - Watchdog restartuje službu při pádu/hangu.
  - Možnost lokální notifikace (tray app, event log, volitelně e-mail/SMS správci).

---

### 8. Extensibility

- Každý monitoring nebo remote modul je plugin (implementuje interface, DI).
- API/JSON umožňuje rozšiřovat pole bez narušení zpětné kompatibility.
- Tray app, watchdog a hlavní služba komunikují přes sdílené rozhraní.
- Nové typy remote commandu lze přidat na server i klient, aniž by starší klient selhal (neznámé typy ignoruje).

---

### 9. Bezpečnostní review

- Token a citlivá data v registrech pouze šifrovaně (AES).
- AES klíč chráněn (obfuscace, DPAPI).
- Remote exec omezený whitelistem příkazů, volitelně jen pro adminy.
- Logování pokusů o neautorizovaný přístup, selhání autentizace.
- Update balíčky jsou podepsané, kontrola hash/signatury.
- Všechna spojení pouze přes HTTPS s validací certifikátu.
- Pravidelný bezpečnostní review a penetrační testy.

---

## Příklady rozhraní a služeb (C#)

```csharp
public interface IApiClient
{
    Task<ApiResponse> PostAsync<T>(string endpoint, T data, CancellationToken ct = default);
    Task<ApiResponse> GetAsync(string endpoint, CancellationToken ct = default);
}
public interface IConfigurationProvider
{
    string GetToken();
    string GetUUID();
    int GetInterval();
}
public interface IRemoteCommandHandler
{
    Task HandleCommandsAsync(IEnumerable<RemoteCommand> commands);
}
public interface IUpdateService
{
    Task<bool> CheckForUpdateAsync();
    Task DownloadAndInstallAsync(string url);
}
public interface IDriveMonitor
{
    IEnumerable<MappedDrive> GetMappedDrives();
}
public interface ISystemMonitor
{
    SystemInfo GetSystemInfo();
    SystemUsage GetCurrentUsage();
    IEnumerable<SystemLogEntry> GetSystemLogs();
}
public interface ISessionMonitor
{
    SessionInfo GetCurrentSession();
}
```

---

## Serilog nastavení

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.File("logs/ems.json", rollingInterval: RollingInterval.Day, formatProvider: null, formatter: new Serilog.Formatting.Json.JsonFormatter())
    .WriteTo.EventLog("EMS", restrictedToMinimumLevel: LogEventLevel.Error)
    .CreateLogger();
```

---

## MSI instalátor

- WiX Toolset nebo Advanced Installer.
- Silent install, zápis registry, obnovitelné konfigurace.

---

## Unit testy (NUnit)

- Testy pro: API komunikaci, šifrování tokenu, monitoring, retry logiku, remote command parser.

---

## Vývojový diagram

```mermaid
flowchart TD
    Služba -- heartbeat/log --> LaravelAPI
    LaravelAPI -- remote_commands --> Služba
    Služba -- mapped drives, status, session info --> TrayApp
    Služba -- Watchdog --> Watchdog
    Služba -- systém/logy --> LaravelAPI
    Služba -- monitoring CPU/RAM/GPU/Disk --> LaravelAPI
    Služba -- userSession monitoring --> LaravelAPI
    UpdateService -- update check --> LaravelAPI
    MSI -- registry/config --> Služba
    Služba -- pluginy/extenze --> PluginInterface
    Služba -- offline logy --> Disk
    Disk -- reconnect --> LaravelAPI
```

---

## Kontakt a podpora

- Pro rozšíření, support či implementaci nových funkcí kontaktujte autora projektu.

---

## Poznámky

- API lze dále rozšiřovat např. o audit logy, inventarizaci SW/HW, další monitoring pluginy.
- Pro detailní příklady kódu nebo konkrétní části architektury kontaktujte autora nebo si vyžádejte konkrétní ukázky.
