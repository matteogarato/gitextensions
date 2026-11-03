# Migrazione Avalonia — stato e prossimi passi

Documento unico che sostituisce i precedenti report di sviluppo (status, parity, known-issues,
testing, release, retirement, resume-point, audit). Il dettaglio per-workflow è nel dato strutturato
[feature-inventory.csv](./feature-inventory.csv). Per build, publish e uso consulta
[src/app/GitExtensions.Avalonia/README.md](../../src/app/GitExtensions.Avalonia/README.md).

## Stato attuale

- Avalonia è una shell UI incrementale sopra lo stesso motore Git (`GitCommands`, `GitExtensions.Extensibility`),
  settings e infrastruttura di traduzione. È il default della solution/debug launch e dello shortcut MSI
  configurato; WinForms resta installato come **Git Extensions (Classic)**.
- Stato di rilascio: **`INTERNAL_PREVIEW`**; il ritiro di WinForms è **BLOCCATO**. Nessun workflow è `VERIFIED_PARITY`
  (47 workflow: in prevalenza `VERIFIED_PARTIAL`, alcuni `IMPLEMENTED_UNVERIFIED`, `BLOCKED` o `INTENTIONALLY_PLATFORM_SPECIFIC`).
- Build: soluzione Avalonia a 0 errori. Test Avalonia su Linux: **311/311**.
- Piattaforma verificata: Ubuntu 26.04 x64, .NET SDK 10.0.x. Windows (runtime Avalonia, MSI), macOS, altre distro,
  SSH/credential helper e strumenti esterni non sono validati.
- Le modifiche sono nel working tree, non committate (base `bfeb62ca3`, branch `AvaloniaPorting`).

### Workflow portati (tutti parziali, con test)

| Area | Contenuto |
|---|---|
| Browse | Apertura/cambio repo, griglia revisioni con grafo, ref, dettagli commit, diff testuale, dashboard recenti, filtro per messaggio/autore/committer |
| Working tree | Stage/unstage, stage righe selezionate (file tracciati modificati), discard/reset, stash (save/apply/pop/drop, parziale, opzioni, handoff conflitti) |
| **Commit** | Amend, sign-off, no-verify, template (git/registrati/settings), cronologia messaggi, autore, firma GPG/key, reset author (solo amend), commit-and-push (`AppSettings.ShowCommitAndPush`), validazioni messaggio con conferma, conferma su HEAD detached, conferma amend (`DontConfirmAmend`), precompilazione del messaggio da `MERGE_MSG`/messaggio salvato e dal commit HEAD all'attivazione di amend, script `BeforeCommit`/`AfterCommit` (`CommitWorkflow.cs`, `WorkingTreePanel`, `FormBrowse`) |
| Branch/tag | Crea (anche orphan/senza checkout), checkout (merge/reset/stash), rinomina, delete locale/forzato/remoto, tag lightweight/annotated e delete locale |
| Remoti | Gestione remoti, pull/push con opzioni (`PushWithOptionsAsync`), fetch, clone, init |
| Merge/rebase/pick | Merge con opzioni, rebase standard/interattivo/range/autosquash, cherry-pick/revert (anche merge commit), banner recovery continue/skip/abort, finestra conflitti + mergetool |
| Altro | Submodule ricorsivi, patch (create/apply/view), file history/blame, reflog read-only, command log, archive, cleanup, verify, hotkey (5 azioni), script utente, menu Plugins opt-in (`IRepositoryCommandPlugin`), tema chiaro/scuro |

Convenzione per ogni workflow: classe testabile `src/app/GitUI.Avalonia/CommandsDialogs/<X>Workflow.cs` che costruisce
argomenti con `Commands.*`/`IGitModule` (mai un secondo motore Git); cablaggio in `FormBrowse`, `WorkingTreePanel`,
`RepoObjectsTree`; azioni distruttive con conferma e default "No"; test in `tests/app/UnitTests/GitUI.Avalonia.Tests/`.

## Limiti e problemi noti

- **Traduzioni**: `English.xlf` non rigenerabile su Linux (`TranslationApp` richiede `Microsoft.WindowsDesktop.App`).
  Mancano le stringhe delle nuove UI commit (amend confirmation, history, templates, author, GPG, reset author, commit and push, git commit template),
  Archive, Verify e Plugins. Non modificare l'XLF a mano: eseguire `update-loc.cmd` su Windows dopo la build.
- `GitUICommands.StartPushDialog` in Avalonia lancia `NotPorted`; si usa `PushWithOptionsAsync`.
- `GitCommands.Tests` non parte su Linux (richiede `Microsoft.WindowsDesktop.App`).
- Credenziali: il `CredentialsManager` non-Windows non supporta la scrittura; usare un credential helper di sistema (non testato).
- Plugin: i plugin legacy `IGitPlugin` restano WinForms; discovery MEF e plugin concreti non verificati; il contratto
  in `GitExtensions.Extensibility` richiede versionamento dell'interfaccia plugin al commit.
- Impostazioni: solo tema e editor hotkey; nessuna pagina impostazioni generale, editor script, hook pull/push, hotkey completi.
- Packaging: solo publish portabile `linux-x64` (framework-dependent) + helper per launcher utente; MSI Windows mai costruito/testato;
  nessun pacchetto Linux. Integrazioni shell e associazioni protocollo puntano ancora a WinForms.
- Diagnostica persistente, accessibilità, sicurezza/provenance: non valutate.
- Lacune per workflow:
  - Commit: nessun test di processi script/hook Git reali.
  - Branch: multi-delete, rimozione worktree.
  - Pull/push: autenticazione e progresso remoto non verificati; fetch non verificato sugli effetti di rete.
  - Merge: strategy/log/message, side selection e binari nei conflitti, prompt di commit post-risoluzione.
  - Rebase: editor todo nativo; editor esterni non testati.
  - Tag: firma GPG, force, push/delete remoto. Cherry-pick/revert: `-x`, multi-selezione, messaggio.
  - Reset: pulizia non tracciati, test dedicati keep/merge. Stash: selezione avanzata.
  - Filtri: data, path, branch, contenuto diff. File history/blame: menu contestuali e opzioni blame.
  - Submodule: pull con opzioni complete di `FormPull`. Patch: whitespace/sign-off, mailbox da directory.
  - Diagnostica: command cache, export/copy, azioni dal reflog, metadati oggetti in Verify.
  - Clone: opzioni branch/depth/single-branch.

## Gate di rilascio

| Gate | Esito |
|---|---|
| Build | Pass con limiti (MSI non costruito) |
| Funzionalità core | Fail (workflow parziali) |
| Integrità dati | Pass con limiti |
| Esecuzione Linux | Pass con limiti (Ubuntu 26.04 x64) |
| Compatibilità configurazione / Windows | Pass con limiti, nessun runtime Windows validato |
| Packaging | Fail |
| Sicurezza | Non testato |

## Prossimi passi (in ordine)

1. Su Windows: build completa, `update-loc.cmd` per rigenerare `English.xlf` e committare l'XLF con il codice.
2. Validare Avalonia su Windows (runtime, MSI, shortcut) e confronto affiancato con WinForms.
3. Plugin: test di discovery MEF e almeno un plugin concreto; UI impostazioni/custom UI Avalonia.
4. Chiudere le lacune P1 elencate sopra (merge/conflitti, pull/push remoto e autenticazione, branch/worktree, tag avanzati).
5. Pagine impostazioni, editor script e hotkey completi, hook lifecycle pull/push.
6. Test end-to-end degli strumenti esterni (editor, diff/merge tool, SSH) e CI smoke con Xvfb.
7. Packaging Linux (AppImage/deb/rpm/Flatpak) e valutazione accessibilità/sicurezza.
8. Quando tutti i workflow raggiungono la parità: rimozione delle dipendenze WinForms e ritiro del Classic UI.
9. Non fare commit senza autorizzazione esplicita; convenzioni: CRLF, StyleCop, `is null`, test `Metodo_should_...`, AwesomeAssertions + NSubstitute.

## Comandi

- Build: `dotnet build eng/avalonia/GitExtensions.Avalonia.slnx /v:q`
- Test: `dotnet test tests/app/UnitTests/GitUI.Avalonia.Tests/GitUI.Avalonia.Tests.csproj -m:1 /nodeReuse:false`
