# UT Launcher — Specifica

> Nome provvisorio. Launcher/installer multipiattaforma (Windows e Linux) per **Unreal Tournament 99**, **Unreal Tournament 2004** e **Unreal Tournament 4 (pre-alpha 2017)**, pensato per giocare insieme tra amici con **versioni garantite identiche** su entrambi i sistemi.

---

## 1. Obiettivi

1. L'utente sceglie un gioco, clicca **Installa**, e alla fine clicca **Gioca**. Nessuna conoscenza di Proton, Wine, patch o dipendenze richiesta.
2. Stessa versione del gioco per tutti, su Windows e Linux: il cross-play deve funzionare per costruzione.
3. Ogni file scaricato o importato è verificato con **SHA-256** rispetto al manifest.
4. Ogni problema è diagnosticabile: log sempre disponibili, anche per gli amici meno esperti.

### Fuori perimetro (per ora)
- UT3 e altri giochi.
- Funzioni di rete avanzate (Ospita/Unisciti, Tailscale, server dedicati): **fase 2**.
- macOS.

---

## 2. Stack

| Ambito | Scelta |
|---|---|
| Linguaggio | C# / .NET 10 (LTS) |
| UI | Avalonia (MVVM, CommunityToolkit.Mvvm) |
| Log | Microsoft.Extensions.Logging + Serilog (file ruotato + sink in memoria per la console) |
| Test | xUnit |
| Distribuzione Windows | eseguibile self-contained `win-x64` (single file) |
| Distribuzione Linux | tarball self-contained `linux-x64` + script `install.sh` (estrae in `~/.local/share/UTLauncher`, crea voce `.desktop`). Stessa build per Debian, Ubuntu, Fedora, Arch: nessuna dipendenza da GTK/Qt di sistema (Avalonia usa Skia) né dal package manager, basta una glibc recente. AppImage resta disponibile come artefatto secondario opzionale, non come metodo principale (evita il rischio "libfuse2 mancante" sulle distro recenti). **No Flatpak** per ora (la sandbox complica umu). |

### Struttura della soluzione

```
src/
  UTLauncher.Core/      # logica: manifest, download, hash, estrazione, moduli gioco, Proton. NIENTE UI.
  UTLauncher.App/       # UI Avalonia
  UTLauncher.Cli/       # CLI per test e VM senza interfaccia grafica
tests/
  UTLauncher.Core.Tests/
manifest/
  manifest.json
docs/
  SPEC.md
tools/
  compute-hashes.sh     # calcola dimensione + SHA-256 di un URL/file per aggiornare il manifest
```

La separazione Core / App / Cli è obbligatoria: tutta la logica deve essere testabile e usabile da riga di comando.

---

## 3. Manifest

- File JSON (`manifest/manifest.json`), versionato nel repo.
- Il launcher usa una copia inclusa nel build e, se raggiungibile, scarica la versione più recente dal repo GitHub (URL configurabile). Se quella remota non è valida, usa quella inclusa.
- Contiene: strumenti esterni (7-Zip, unshield, umu, Proton) con URL e hash, fonti di ogni gioco con URL multipli + dimensione + SHA-256, patch fissate per tag, comandi di avvio, configurazioni speciali.
- **Regola d'oro:** nessun file entra in un'installazione se il suo SHA-256 non corrisponde a una voce del manifest.

---

## 4. Regole trasversali

### 4.1 Task e feedback
Ogni operazione è un `LauncherTask` con:
- stato: `Pending`, `Running`, `Completed`, `Failed`, `Cancelled`;
- progresso: **indeterminato** (activity indicator) oppure **determinato** (percentuale);
- testo del passo corrente (es. "Estrazione Maps/DM-Deck16][.unr");
- per i download: byte correnti/totali, velocità, tempo stimato.

Regole:
- Qualsiasi operazione non istantanea mostra almeno un **activity indicator**.
- Qualsiasi operazione che può durare **più di 10 secondi** mostra anche una **barra di completamento** quando il totale è noto.
- I task lunghi sono **annullabili**; l'annullamento pulisce i file parziali (tranne i download, che restano riprendibili).

### 4.2 Log
- Pannello **Console** apribile sempre (pulsante fisso + scorciatoia `F12`): livello, ora, modulo, filtro per livello, ricerca, **Copia** ed **Esporta**.
- File di log ruotati (ultimi 5):
  - Windows: `%LOCALAPPDATA%\UTLauncher\logs\`
  - Linux: `${XDG_DATA_HOME:-~/.local/share}/UTLauncher/logs/`
- Stdout/stderr e **exit code** di ogni processo esterno (7-Zip, unshield, umu, winetricks, vcredist, DXSETUP, il gioco stesso) finiscono nel log.
- Modalità verbosa (impostazione o `--verbose`): comandi completi, URL, variabili d'ambiente passate a umu/Proton.
- Ogni errore mostrato all'utente ha un pulsante **Mostra log** che apre la console sull'errore.

### 4.3 Download
- Più URL per fonte: si prova il successivo se uno fallisce.
- **Ripresa** tramite HTTP Range (file `.part` + metadati).
- SHA-256 calcolato in streaming durante il download; dopo una ripresa si ricalcola sul file completo.
- Alcune fonti sono solo HTTP (UT4ever): per questo la verifica hash è obbligatoria.

### 4.4 Fonti alternative
Per ogni fonte principale di gioco l'utente può:
1. **Scaricare da link**: campo precompilato con l'URL del manifest, modificabile (mirror, Drive, Nextcloud…).
2. **Aprire un file locale** (zip/ISO).

In entrambi i casi il file deve corrispondere a un hash del manifest. Se non corrisponde, l'installazione viene **rifiutata** con spiegazione (file corrotto o versione diversa) e proposta di riscaricare o scegliere un altro file. Nessuna opzione "installa comunque".

### 4.5 Codice versione
Ogni installazione mostra un **codice versione** breve (es. `UT04-3374p23`, dal manifest). Due giocatori con lo stesso codice possono giocare insieme.

### 4.6 Registro installazioni
File JSON nella cartella dati del launcher: per ogni gioco installato, percorso, codice versione, hash delle fonti usate, data, piattaforma, (Linux/UT4) percorso del prefisso.

### 4.7 Operazioni per gioco
Installa · Gioca · Apri cartella · Verifica/Ripara · Disinstalla.

---

## 5. Linux: controllo sistema e Proton

### 5.1 Controllo sistema (primo avvio + a richiesta)
Semafori con spiegazione e comando da copiare per Fedora / Ubuntu-Mint / Arch:
- spazio su disco (per gioco);
- Vulkan funzionante (`vulkaninfo --summary`) — serve solo per UT4;
- Python 3 (per la versione portabile di umu) — solo UT4;
- user namespace disponibili (container Steam Runtime) — solo UT4;
- librerie di sistema opzionali per UT99/UT2004 (OpenAL, SDL3, libomp): se mancano si usano quelle incluse nella patch.

### 5.2 Proton gestito dal launcher (solo UT4)
- `umu-run`: se presente nel sistema si usa quello, altrimenti si scarica la versione portabile (manifest) in `…/UTLauncher/tools/umu/`.
- Proton: versione **fissata nel manifest**, scaricata e verificata dal launcher in `…/UTLauncher/tools/proton/`, passata a umu con `PROTONPATH`.
- Prefisso dedicato per gioco: `…/UTLauncher/prefixes/ut4/`.
- `vcrun2013` installato nel prefisso via winetricks tramite umu.
- L'utente non vede nulla di tutto questo: solo task con nomi comprensibili ("Preparazione ambiente di compatibilità").

---

## 6. Moduli gioco

I passi sotto sono il riassunto. **Per UT99 e UT2004 la fonte di verità dei dettagli sono gli script OldUnreal** al commit indicato nel manifest (`reference`): i passi vanno portati in C# fedelmente, inclusi file ignorati, fix di case e librerie.

### 6.1 UT99 (GOTY) — nativo su entrambi i sistemi
1. Scarica/verifica `UT_GOTY_CD1.iso` e `utbonuspack4-zip.7z`.
2. Scarica/verifica la patch `v469e` per la piattaforma (Windows `-Windows-x86.zip`, Linux `-Linux-amd64.tar.bz2`).
3. Estrae la ISO (7-Zip) nella destinazione, **escludendo** i pattern elencati negli script OldUnreal (ini predefiniti, binari Windows su Linux, vecchi setup, traduzioni).
4. Estrae il Bonus Pack 4.
5. Estrae la patch sopra i file.
6. Decomprime le mappe `.uz` (come `steps/unpack_uz_maps.sh`).
7. Applica i fix specifici (`steps/ut99_special_fixes.sh`) e rimuove le traduzioni extra.
8. Windows: dipendenze come l'installer NSIS OldUnreal (VC++ Redistributable x86/x64, DirectX).
9. Crea voce nel menu / scorciatoia (facoltativa, chiesta all'utente).
10. Avvio: Windows `System/UnrealTournament.exe`, Linux `System64/ut-bin`.

### 6.2 UT2004 — nativo su entrambi i sistemi
1. Scarica/verifica `UT2004.iso` (due alternative con hash diversi, vedi manifest).
2. Scarica/verifica la patch `3374-preview-23`.
3. Estrae la ISO in una cartella di staging.
4. Estrae i `.cab` InstallShield con **unshield** (`steps/ut2004_unpack_cabs.sh`).
5. Copia le cartelle dati nella destinazione secondo la tabella di `steps/ut2004_install_files.sh`.
6. Estrae la patch.
7. Applica i fix (`steps/ut2004_special_fixes.sh`): file con case errato, librerie di sistema preferite a quelle incluse, `MainMenuClass` → `GUI2K4.UT2K4MainMenuWS`.
8. Windows: dipendenze come l'installer NSIS OldUnreal.
9. Avvio: Linux `System/ut2004-bin-amd64`, Windows da verificare dopo la patch.
10. **CD key:** non gestita. Se in prova risulta necessaria → campo opzionale nelle impostazioni del gioco, salvato solo in locale. Mai chiavi nel manifest o nel codice.

### 6.3 UT4 (pre-alpha 2017, build UT4ever v1.1.0)
Logica ricavata da `utshakka/ut4installer`, **reimplementata** (il repo non ha una licenza esplicita: non copiare codice).

Fonti accettate: pacchetto completo `UT4 Installer - v1.1.0.zip` (si estrae lo zip interno) oppure zip del gioco diretto. Entrambi con hash del manifest.

Passi comuni:
1. Controllo spazio (≥ 19,5 GB per il gioco estratto, più lo zip se ancora presente).
2. La destinazione deve terminare in `UnrealTournament` e non esistere: lo zip contiene già quella cartella e va estratto nella cartella **padre**.
3. Estrazione con progresso (System.IO.Compression, zip64).
4. **Engine.ini**: in `Documents/UnrealTournament/Saved/Config/WindowsNoEditor/Engine.ini`
   - se non esiste: crearlo con le 7 sezioni del manifest (`Domain=<masterServer>`, `Protocol=https`);
   - se esiste: **aggiungere solo le sezioni mancanti**, senza toccare il resto.
5. **InstallInfo.bin di UT4UU** (entrambi i percorsi del manifest). Formato `BinaryWriter` .NET, in ordine: 6 × `bool` (createShortcut, isDryRun, createSymbolicLinks, upgradeEngineModules, refreshingExperience, tryToInstallInLocalGameServer), 3 × `string` (sourceLocation, installLocation, replacementSuffix), 2 × `byte` (platformTarget, buildConfiguration). Leggere i valori esistenti, sostituire `sourceLocation` con `C:\Generic\Install\Path` e `installLocation` con il percorso di installazione **come lo vede Windows**, riscrivere il resto invariato.
6. Pannello **Primo accesso**: registrazione su ut4.timiimit.com (pulsante che apre la pagina), generazione del codice, incolla nel gioco al primo avvio. Nessuna credenziale gestita dal launcher.

Solo Windows:
- Dipendenze: DirectX June 2010 (controllo dei 4 file in System32) e VC++ 2013 x64 (controllo chiavi di registro). Se mancano: installazione silenziosa, con elevazione **solo per quel passo**.
- Cartella predefinita nella cartella utente (non `Program Files`), per evitare l'UAC sull'installazione del gioco.
- Avvio: `Engine/Binaries/Win64/UE4-Win64-Shipping.exe` con `UnrealTournament -epicapp=UnrealTournamentDev -epicenv=Prod -EpicPortal`, working dir = cartella dell'exe.

Solo Linux:
- Gioco installato nella cartella scelta dall'utente; nel prefisso si crea il symlink `drive_c/Games/UnrealTournament` → cartella reale.
- `installLocation` in InstallInfo.bin = `C:\Games\UnrealTournament` (sempre uguale).
- Engine.ini scritto in `<prefisso>/drive_c/users/steamuser/Documents/...`.
- Avvio via `umu-run` con `WINEPREFIX`, `PROTONPATH`, `GAMEID`, stesso exe e stessi argomenti di Windows, working dir equivalente.
- Da verificare in prova: copia-incolla del codice di login sotto Wayland/XWayland, persistenza del login.

---

## 7. CLI (per test e VM)

```
utlauncher list
utlauncher install <ut99|ut2004|ut4> [--dest <path>] [--source-url <url>] [--source-file <path>] [--verbose]
utlauncher verify  <gioco>
utlauncher launch  <gioco>
utlauncher uninstall <gioco>
utlauncher doctor            # controllo sistema
utlauncher hash <file|url>   # dimensione + SHA-256
```
Stessi task, stesso log, progresso testuale.

---

## 8. Piano di sviluppo

1. **Scheletro**: soluzione, progetti, logging (file + console), manifest loader con validazione, CLI `list` e `hash`.
2. **Infrastruttura**: downloader (mirror, ripresa, SHA-256 in streaming), task/progresso, registro installazioni, gestione strumenti esterni (7-Zip, unshield) scaricati e verificati.
3. **UT99** end-to-end su Linux (CLI) → test in VM.
4. **UT2004** end-to-end su Linux (CLI) → test in VM.
5. **UI Avalonia**: lista giochi, installazione con progresso, console log, impostazioni, controllo sistema.
6. **Windows**: UT99 e UT2004 con dipendenze.
7. **UT4**: Windows, poi Linux con umu/Proton.
8. Packaging: exe single-file Windows; tarball self-contained + `install.sh` Linux (AppImage opzionale, secondario).

### Criteri di accettazione (fase VM Linux)
- Il launcher si avvia (UI) e la CLI funziona.
- `install ut99` e `install ut2004` completano senza errori, con hash verificati e log puliti.
- `verify` passa su entrambe le installazioni.
- Interrompendo un download e rilanciando, il download riprende.
- Un file con hash errato viene rifiutato.

---

## 9. Punti aperti
- CD key UT2004 (probabilmente non necessaria).
- Nome eseguibile Windows di UT2004 dopo patch 3374.
- UT4 su Linux: comportamento di UT4UU con `InstallInfo.bin` sotto Proton, login e copia-incolla.
- Hash del file UT4 v1.0.3 sul server UT4ever (possibile mirror).
- Scelta e hash di 7-Zip, unshield, umu, GE-Proton.
- Nome definitivo e licenza del launcher; contatto di cortesia con UT4ever (_shakka) e OldUnreal.
