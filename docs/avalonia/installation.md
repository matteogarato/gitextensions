# Installing the Avalonia internal preview

## Distribution format and tested scope

The current Linux output is a **framework-dependent portable publish directory**, not an
installer or Linux package. No Flatpak, AppImage, Debian/RPM package, desktop entry, or
system-wide install/uninstall integration is provided.

The only runtime environment verified for this preview is Ubuntu 26.04 x64. Windows Avalonia,
macOS, other Linux distributions, and other architectures are not claimed as supported.

The main solution and VS Code debug launch now default to Avalonia. The Windows MSI build is
configured to install Avalonia under `Avalonia\` and use it for the primary Start menu and
desktop shortcuts; the existing WinForms executable remains available as **Git Extensions
(Classic)**. That Windows installer path and Windows runtime have not been built or tested here.
The Avalonia UI is incomplete; use the Classic UI for workflows not yet ported. Windows shell
integrations and file/protocol associations continue to use the existing WinForms executable.

## Requirements

- .NET 10 runtime (`Microsoft.NETCore.App` 10.x).
- Git available as `git` on `PATH`.
- Linux desktop libraries used by Avalonia/Skia. The CI smoke environment installs
  `libfontconfig1`, `libice6`, `libsm6`, `libx11-6`, `libxext6`, and `libxrender1`.
- An interactive X11 or Wayland desktop session.

Check the required runtime and Git before launching:

```sh
dotnet --list-runtimes
git --version
```

## Portable-folder launch

Copy or extract the complete `linux-x64` publish directory into a directory dedicated to this
preview. Keep the files together; do not copy just the executable. The tested apphost is
`GitExtensions.Avalonia`.

Launch it from a terminal:

```sh
./GitExtensions.Avalonia browse "/path/to/repository"
```

Paths with spaces and Unicode characters are supported by the tested Linux publish smoke.
Without a repository argument the window opens empty unless a configured recent repository is
available; use **Repository > Open** to select one.

For an isolated first run, set `XDG_CONFIG_HOME` to a writable directory before launching:

```sh
XDG_CONFIG_HOME="/path/to/private/test-config" \
  ./GitExtensions.Avalonia browse "/path/to/repository"
```

The shared settings layer stores the settings file under
`$XDG_CONFIG_HOME/GitExtensions/GitExtensions/GitExtensions.settings` (normally rooted in
`~/.config`). The first-run launch created this file in an isolated test configuration. The
configuration path follows the existing version-independent Git Extensions settings layout.

## Configuration and upgrades

There is no released earlier Avalonia schema to upgrade from. Compatibility with a previously
released Avalonia build therefore has not been established. The Avalonia app reuses the existing
`AppSettings` and shared settings cache rather than introducing a second preferences store.

The port derives its configuration path using the existing version-independent application
settings convention. Linux does not automatically import settings from a Windows installation;
Windows Registry settings, Windows-specific tool paths, and platform-specific credentials are
not migrated to Linux. Back up any existing settings file before testing a preview against it.
Configuration persistence across graceful close/relaunch, malformed files, and read-only
locations has not been validated for the Avalonia app. Shared settings-save failures may be
silently swallowed by existing application code.

## Removal and reinstallation

There is no uninstaller. To remove a portable preview, close it and remove only the dedicated
application directory chosen when copying/extracting the publish output. User repositories and
the separate configuration directory are not part of that application directory. Reinstallation
and preservation of preferences across a replacement were not tested.

## Troubleshooting and issue reports

- If the .NET 10 runtime is missing, install the .NET 10 runtime before launching.
- If Git is missing, install Git and ensure `git` is on `PATH`.
- For Linux-specific problems, launch from a terminal and retain the diagnostic output. A
  persistent application log location is not currently provided.
- Include the Linux distribution/version, architecture, .NET runtime, Git version, exact command,
  and sanitized error output in a report. Remove repository paths, remote URLs, usernames,
  tokens, credentials, and other private data before sharing logs.

See [release-candidate.md](./release-candidate.md) for the tested artifact, verification record,
and current release decision.
