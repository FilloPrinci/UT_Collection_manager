# CLAUDE.md

Istruzioni per Claude Code su questo repository.

## Progetto
Launcher/installer multipiattaforma (Windows + Linux) per UT99, UT2004 e UT4 (pre-alpha 2017).
**Leggi sempre `docs/SPEC.md` prima di lavorare**: è la specifica completa. `manifest/manifest.json` è la fonte di verità per versioni, URL e hash.

## Stack
- C# / .NET 10 (LTS), UI Avalonia (MVVM con CommunityToolkit.Mvvm), log con Serilog, test con xUnit.
- Distribuzione Linux: tarball self-contained + script `install.sh`, stessa build per Debian/Ubuntu/Fedora/Arch (vedi SPEC.md §2). Niente pacchetti .deb/.rpm/pacman separati.
- Progetti: `UTLauncher.Core` (tutta la logica, nessuna dipendenza UI), `UTLauncher.App` (Avalonia), `UTLauncher.Cli`, `UTLauncher.Core.Tests`.

## Regole non negoziabili
1. **Verifica SHA-256** di ogni file scaricato o importato contro il manifest. Se non corrisponde: rifiuta, logga, spiega. Mai "installa comunque".
2. **Feedback**: ogni operazione non istantanea ha un activity indicator; se può superare i 10 secondi anche una barra di completamento (quando il totale è noto). Tutto passa per il sistema di task di Core.
3. **Log**: ogni passo logga cosa fa. Processi esterni: comando, stdout, stderr ed exit code sempre nel log. Mai inghiottire eccezioni.
4. **Niente codice copiato** da `utshakka/ut4installer` (nessuna licenza esplicita): reimplementa la logica descritta in SPEC.
5. Per UT99/UT2004 i passi di installazione vanno portati **fedelmente** dagli script OldUnreal al commit indicato nel manifest (`OldUnreal/FullGameInstallers`, cartelle `Linux/src/` e `Windows/`). Quando un dettaglio non è in SPEC, lo script OldUnreal ha ragione.
6. Nessuna CD key, credenziale o dato personale nel repo o nel manifest.
7. Percorsi: niente percorsi hardcoded con `/` o `\`; usa `Path.Combine` e le API di piattaforma. Linux è case-sensitive.
8. Codice che dipende dalla piattaforma dietro interfacce (`IPlatform`, ecc.) con implementazioni Windows/Linux.

## Convenzioni
- Codice, identificatori e commenti in inglese; testi dell'interfaccia in italiano (predisponi le risorse per la localizzazione).
- `async`/`await` con `CancellationToken` ovunque ci sia I/O o processi esterni.
- `IProgress<T>` per il progresso, mai aggiornare la UI dal Core.
- Test unitari per: parsing manifest, verifica hash, merge di Engine.ini, lettura/scrittura InstallInfo.bin, calcolo percorsi.

## Test in VM Linux
Target della prima fase: VM Linux (Fedora o Ubuntu) senza bisogno di GPU.
```bash
dotnet build
dotnet test
dotnet run --project src/UTLauncher.Cli -- doctor
dotnet run --project src/UTLauncher.Cli -- install ut99 --dest ~/Games/UT99 --verbose
dotnet run --project src/UTLauncher.Cli -- install ut2004 --dest ~/Games/UT2004 --verbose
dotnet run --project src/UTLauncher.Cli -- verify ut99
dotnet run --project src/UTLauncher.App
```
Criteri di accettazione della fase: vedi `docs/SPEC.md` §8.

UT4 non si testa in VM (10 GB + GPU): viene testato manualmente su macchina reale.

## Ordine di lavoro
Segui il piano in `docs/SPEC.md` §8. Un passo alla volta, con commit piccoli e descrittivi. Prima di passare al passo successivo: build pulita e test verdi.
