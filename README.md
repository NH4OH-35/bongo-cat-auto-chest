# BongoClaim ฅ

**A downloadable Windows chest helper for Bongo Cat.** Applies a small, reversible patch to your installed game instead of replacing it with an outdated game DLL.

[Download the portable ZIP](https://github.com/NH4OH-35/bongo-claim/releases/latest) · [Report a problem](https://github.com/NH4OH-35/bongo-claim/issues)

![BongoClaim desktop app](docs/desktop.png)

## Use it

1. Download `BongoClaim-v1.0.0-windows.zip` from Releases and **extract the whole ZIP**.
2. Exit Bongo Cat normally.
3. Open `BongoClaim.exe`. Keep `Mono.Cecil.dll` beside it.
4. The app finds your running game or Steam library. If needed, browse to the folder containing `BongoCat.exe`.
5. Click **Check build**, then **Enable auto claim**.
6. Start Bongo Cat from Steam. The patch works inside the game; this helper does not need to stay open.

Normal chests are automated by default. **Emote chests are opt-in** and still use their normal in-game pets cost. To change that option after installing, restore the original first, then enable again with the new option.

The patch does not generate keyboard presses, inflate your pets counter, shorten cooldowns, bypass affordability, or change server eligibility. It calls the game's existing `ShopItem.Buy()` when its normal ready flag and affordability check allow it. An eligible chest still needs enough earned pets.

## Why the old DLL stopped being suitable

The archived [Bongo-Cat-Auto-Claim](https://github.com/zelda0079/Bongo-Cat-Auto-Claim) ships an entire older `Assembly-CSharp.dll`. The inspected July version inserted a purchase into a generated timer method. The current build has different timer code, an `OnChestReady()` method, and newer gameplay features. Dropping an old game DLL into that installation discards those newer changes.

BongoClaim instead:

- Inspects the assembly actually installed on your computer.
- Checks the expected chest, timer, affordability and purchase-lock structure.
- Adds one small helper and hooks two existing methods.
- Backs up your exact original bytes before installing.
- Verifies the patched assembly by reading it back.
- Refuses unsupported layouts or an existing conflicting auto-claim mod.

## Backup, restore and Steam updates

**Restore original** disables the patch and restores the original DLL byte-for-byte. Your game saves and inventory files are not touched.

Backups are kept inside the selected game folder:

```text
.bongo-claim/
  install.json
  backups/<original-sha256>.dll
```

Backups are verified against their SHA-256 before use. If Steam updates your game, the tool refuses to restore an older backup over a changed game DLL. Recheck and reapply on a compatible new build; previous backups are retained. If an update changes the relevant code layout, BongoClaim refuses to patch it.

If you previously installed the archived replacement DLL, use Steam **Properties → Installed Files → Verify integrity of game files** first. This gets the current original game files back. You can also use that Steam recovery path if your BongoClaim backup is missing or damaged.

## Verified scope

Initial compatibility was inspected against Steam build **25723683** on Windows, on 12 October 2026:

```text
Original Assembly-CSharp.dll SHA-256
7566e8d9846cdb38eadcdb729cfcf676dc338cd738e99f664dc6ef3b79e87b83
```

The current original assembly was successfully patched as a separate copy and read back. Static comparison found exactly **two changed existing method bodies**, one added helper method, and one added constant field. All other existing method bodies remained unchanged. This is build compatibility evidence, not a promise about future updates or server behavior.

Seventeen executable tests use a source-owned simulated game assembly to cover normal claims, eligibility, affordability, pending requests, emote opt-in, retrying an already-visible chest, null references, duplicate prevention, install idempotence, corrupted backups, Steam updates, unsupported layouts and exact restoration.

**Live Steam chest delivery has not yet been observed in this initial release.** The desktop UI and current-build patching have been checked; actual rewards remain subject to the game's normal server behavior. Please report your build ID, normal/emote mode, and the error text if a claim fails.

## Requirements and troubleshooting

- Windows 10 or 11 with .NET Framework 4.8 (normally already present).
- The Steam Windows version of Bongo Cat, using the supported managed assembly layout.
- Write access to your own game folder; no administrator privileges are requested.
- A closed game during installation or restoration. The tool will not force-close it.

If the game is installed in a protected folder or Windows blocks a write, the tool reports the failure rather than changing security settings. Keep antivirus protection enabled. A missing dependency error means the ZIP was not fully extracted. Release binaries are unsigned; source and build instructions are provided below.

## Build from source

Run in PowerShell on Windows:

```powershell
.\build.ps1 -Test
```

No .NET SDK or Visual Studio is required: the script uses the .NET Framework compiler already on Windows. The only downloaded build dependency is the pinned, hash-verified Mono.Cecil NuGet package. Outputs go in `artifacts/`, and dependencies in `tools/`; neither is tracked.

Package the distributable:

```powershell
.\package.ps1
```

The ZIP allowlist includes the desktop app, CLI, Mono.Cecil, instructions and licenses. No game assembly, test fixture, backup, save file or account data is packaged.

## Command line

For diagnostics and scripted use, `BongoClaim.Cli.exe` offers:

```powershell
.\BongoClaim.Cli.exe --inspect 'G:\SteamLibrary\steamapps\common\BongoCat'
.\BongoClaim.Cli.exe --install 'G:\SteamLibrary\steamapps\common\BongoCat'
.\BongoClaim.Cli.exe --restore 'G:\SteamLibrary\steamapps\common\BongoCat'
.\BongoClaim.Cli.exe --patch-copy 'path\Assembly-CSharp.dll' "$env:TEMP\patched-copy.dll"
```

`--emotes` opts in to emote chests during install or patch-copy. `--verbose` includes a diagnostic stack trace. `--patch-copy` refuses to overwrite the source or an existing output.

## How the patch works

`PatchEngine.cs` adds a private helper to `BongoCat.Shop`. It checks the selected chest type, `ChestIsReady`, `_openingChest`, the shop-item reference and `CanBuy()`, then invokes the original `Buy()`. The game's own `StartBuy()` keeps its pets and pending-server checks intact.

The helper runs at the beginning of `OnChestReady()` and the game's existing timer state machine. The timer hook allows a later retry even when a popup is already shown. The game's timer interval, failed-request cooldown and purchase callbacks are preserved.

No game or Steam assembly is shipped. Development inspection used [REA](https://github.com/morluto/rea) and [Mono.Cecil](https://github.com/jbevain/cecil). See [third-party notices](THIRD-PARTY-NOTICES.md).

## License

MIT for BongoClaim. This is an independent, unofficial project; it is not affiliated with Bongo Cat or Steam.
