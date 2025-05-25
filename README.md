# Patron Monitoring Agent

## P�ehled

Endpoint Management Software (EMS) je profesion�ln� klientsk� aplikace pro spr�vu a monitoring koncov�ch bod� (PC/server�) ve firemn�m prost�ed� s podporou v�ce klient�, bezpe�nou autentizac�, vzd�len�mi p��kazy a auto-updatem. Spr�va prob�h� p�es API na Laravel serveru (PHP).

---

## Hlavn� funkce

- **Ovl�d�n� p�es Laravel API** (Bearer token, JSON)
- **Podpora v�ce klient�** (UUID, validace serverem)
- **Serilog s JSON logy, event log**
- **ID a log TeamVieweru (roz�i�iteln�)**
- **IoC, interface-driven design, DI kontejner**
- **Konfigurace v registrech (AES �ifrovan� token, interval)**
- **Retry logika pro API**
- **Watchdog slu�ba (hl�d�n� agenta i tray aplikace)**
- **Remote commands (restart, vypnut�, exec, update)**
- **Detekce a hl�en� vypnut�/restartu stanice**
- **Auto-update**
- **Monitoring mapped network drives (u�ivatelsk� session)**
- **MSI instal�tor**
- **Unit testy (NUnit)**
- **Tray aplikace (stav, ru�n� restart, heartbeat pro watchdog)**
- **Strukturovan� logy, monitoring, serializovan� logy (JSON)**
- **Syst�mov� monitoring: CPU, GPU, RAM, disky, syst�mov� logy**
- **Monitoring user session: u�ivatel, �as, mapped drives, p��stupn� cesty**
- **API Rate-limit handling**
- **Extensibility (plugin architektura)**
- **Bezpe�nostn� review**
- **Disaster recovery**

---

## Architektura

- **Program.cs** � Start, DI kontejner
- **Interfaces** � IApiClient, IConfigurationProvider, ILoggerService, IRemoteCommandHandler, IUpdateService, IDriveMonitor, IWatchdog, ISystemMonitor, ISessionMonitor
- **Services** � ApiClient, RegistryConfigurationProvider, SerilogLogger, RemoteCommandHandler, UpdateService, DriveMonitor, WatchdogService, SystemMonitor, SessionMonitor
- **PluginInterface** � roz���en� monitoring/remote funkc�
- **TrayApp** � Windows Forms/WPF tray aplikace
- **Unit Tests** � NUnit projekt

---

## Konfigurace a registrace

- **Token (AES �ifrovan�):** `HKLM\Software\Company\EMS\Token`
- **Interval:** `HKLM\Software\Company\EMS\Interval`
- **UUID klienta:** `HKLM\Software\Company\EMS\UUID`

---

## API SPECIFIKACE

### Autentizace

- V�echny endpointy vy�aduj� Bearer token (`Authorization: Bearer ...`).

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

**Odpov��:**
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

**Odpov��:**
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

**Odpov��:**
```json
{
  "status": "ok"
}
```

---

### 4. Remote commands

- Remote commands jsou vr�ceny v odpov�di serveru v poli `remote_commands`:

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

### 5. Hl�en� vypnut�/restartu stanice

P�i vypnut� nebo restartu po��ta�e agent slu�ba ode�le na server informaci o ud�losti:

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

**Odpov��:**
```json
{
  "status": "ok"
}
```

Pozn�mka: Typ ud�losti (`type`) m��e b�t nap�. `"system_shutdown"`, `"system_restart"` nebo `"service_stop"` dle zji�t�n�ho d�vodu.

---

### 6. API Rate-limit handling

- Pokud API vr�t� HTTP 429 (Too Many Requests), klient:
  - Respektuje hlavi�ku `Retry-After` (pokud je p��tomna).
  - Pokud chyb�, pou�ije exponenci�ln� back-off.
  - Ud�losti o rate-limitu loguje v�etn� endpointu, �asu, do Serilog logu.
- Pokud je p�ekro�en limit opakovan�, klient automaticky zv��� interval heartbeat a log upload� dle konfigurace serveru nebo p�ednastaven�ch pravidel.

---

### 7. Disaster recovery

- Pokud nen� server dostupn�:
  - Klient data (logy, monitoring) ukl�d� lok�ln� (do �ifrovan�ho souboru).
  - Po obnoven� spojen� dojde k d�vkov�mu uploadu.
  - Kritick� incidenty (nap�. restart watchdogem) jsou nahl�eny po obnov� spojen�.
  - Watchdog restartuje slu�bu p�i p�du/hangu.
  - Mo�nost lok�ln� notifikace (tray app, event log, voliteln� e-mail/SMS spr�vci).

---

### 8. Extensibility

- Ka�d� monitoring nebo remote modul je plugin (implementuje interface, DI).
- API/JSON umo��uje roz�i�ovat pole bez naru�en� zp�tn� kompatibility.
- Tray app, watchdog a hlavn� slu�ba komunikuj� p�es sd�len� rozhran�.
- Nov� typy remote commandu lze p�idat na server i klient, ani� by star�� klient selhal (nezn�m� typy ignoruje).

---

### 9. Bezpe�nostn� review

- Token a citliv� data v registrech pouze �ifrovan� (AES).
- AES kl�� chr�n�n (obfuscace, DPAPI).
- Remote exec omezen� whitelistem p��kaz�, voliteln� jen pro adminy.
- Logov�n� pokus� o neautorizovan� p��stup, selh�n� autentizace.
- Update bal��ky jsou podepsan�, kontrola hash/signatury.
- V�echna spojen� pouze p�es HTTPS s validac� certifik�tu.
- Pravideln� bezpe�nostn� review a penetra�n� testy.

---

## P��klady rozhran� a slu�eb (C#)

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

## Serilog nastaven�

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.File("logs/ems.json", rollingInterval: RollingInterval.Day, formatProvider: null, formatter: new Serilog.Formatting.Json.JsonFormatter())
    .WriteTo.EventLog("EMS", restrictedToMinimumLevel: LogEventLevel.Error)
    .CreateLogger();
```

---

## MSI instal�tor

- WiX Toolset nebo Advanced Installer.
- Silent install, z�pis registry, obnoviteln� konfigurace.

---

## Unit testy (NUnit)

- Testy pro: API komunikaci, �ifrov�n� tokenu, monitoring, retry logiku, remote command parser.

---

## V�vojov� diagram

```mermaid
flowchart TD
    Slu�ba -- heartbeat/log --> LaravelAPI
    LaravelAPI -- remote_commands --> Slu�ba
    Slu�ba -- mapped drives, status, session info --> TrayApp
    Slu�ba -- Watchdog --> Watchdog
    Slu�ba -- syst�m/logy --> LaravelAPI
    Slu�ba -- monitoring CPU/RAM/GPU/Disk --> LaravelAPI
    Slu�ba -- userSession monitoring --> LaravelAPI
    UpdateService -- update check --> LaravelAPI
    MSI -- registry/config --> Slu�ba
    Slu�ba -- pluginy/extenze --> PluginInterface
    Slu�ba -- offline logy --> Disk
    Disk -- reconnect --> LaravelAPI
```

---

## Kontakt a podpora

- Pro roz���en�, support �i implementaci nov�ch funkc� kontaktujte autora projektu.

---

## Pozn�mky

- API lze d�le roz�i�ovat nap�. o audit logy, inventarizaci SW/HW, dal�� monitoring pluginy.
- Pro detailn� p��klady k�du nebo konkr�tn� ��sti architektury kontaktujte autora nebo si vy��dejte konkr�tn� uk�zky.
