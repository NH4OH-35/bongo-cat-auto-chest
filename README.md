# BongoClaim ฅ

**Auto-claim normal and emote chests in Bongo Cat, with simple in-game switches.** Install the Windows helper once, then launch Bongo Cat normally through Steam.

[Download the portable ZIP](https://github.com/NH4OH-35/bongo-claim/releases/latest) · [Report a problem](https://github.com/NH4OH-35/bongo-claim/issues)

![Bongo Cat Settings > General showing normal and emote auto-claim enabled](docs/game-settings.png)

## Everyday use

**Already installed? Just start Bongo Cat normally from Steam.** Auto-claim runs inside the game; you can leave `BongoClaim.exe` closed. Keep Bongo Cat and Steam running. When an enabled chest is ready and you have enough earned pets, it claims automatically at the game's normal cost and cooldown.

Open the game menu with **Escape** or **M**, click the **gear**, then choose **General**. Check **Auto claim normal chests** or **Auto claim emote chests** to enable that type; uncheck it to pause. Each choice takes effect while playing and saves for the next launch. Both start enabled. A request already sent to the server can still finish after switching off.

For quick control with the game menu open, press **F7** for normal or **F9** for emote. **Shift + F9** also works for emote if plain F9 is intercepted or missed. Shortcuts work only while the game is focused and no text field is active. They update the same saved switches and checkmarks.

## First-time installation

1. Download `BongoClaim-v1.1.0-windows.zip` from Releases and **extract the whole ZIP**.
2. Exit Bongo Cat normally.
3. Open `BongoClaim.exe`. Keep `Mono.Cecil.dll` beside it.
4. The app finds your running game or Steam library. If needed, browse to the folder containing `BongoCat.exe`.
5. Click **Check build**, then **Install auto claim** (or **Upgrade controls** for v1.0).
6. Start Bongo Cat from Steam. The patch works inside the game; this helper does not need to stay open.

![BongoClaim desktop installer and restore controls](docs/desktop.png)

Both chest types still use their normal in-game pets cost. Changes to these switches do not require reinstalling or keeping BongoClaim open. When upgrading v1.0, the tool builds the new patch from your verified original backup and keeps that same original for restoration.

The patch does not generate keyboard presses, inflate your pets counter, shorten cooldowns, bypass affordability, or change server eligibility. It calls the game's existing `ShopItem.Buy()` when its normal ready flag and affordability check allow it. An eligible chest still needs enough earned pets.

## Why the old DLL stopped being suitable

The archived [Bongo-Cat-Auto-Claim](https://github.com/zelda0079/Bongo-Cat-Auto-Claim) ships an entire older `Assembly-CSharp.dll`. The inspected July version inserted a purchase into a generated timer method. The current build has different timer code, an `OnChestReady()` method, and newer gameplay features. Dropping an old game DLL into that installation discards those newer changes.

BongoClaim instead:

- Inspects the assembly actually installed on your computer.
- Checks the expected chest, timer, affordability and purchase-lock structure.
- Adds guarded claim logic and two matching native settings rows.
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

The current original assembly is inspected, patched and read back before installation. Only chest readiness, its timer and the settings initialization are hooked; purchase callbacks, prices and cooldowns are retained. This is build compatibility evidence, not a promise about future updates or server behavior.

Thirty-three executable tests use a source-owned simulated game assembly to cover claims, independent live switches, saved preferences, native settings clicks and callback isolation, focused-menu keyboard controls, eligibility, affordability, pending requests, retrying an already-visible chest, null references, duplicate prevention, v1 upgrades, corrupted backups, Steam updates, unsupported layouts and exact restoration. No proprietary game files are needed to run those tests.

**A live normal-chest smoke test passed on 12 October 2026.** After installing on build 25723683 and restarting through Steam, the normal ready-chest indicator disappeared and the normal 30-minute cooldown began without a manual claim click. The emote chest stayed ready in normal-only mode. The game's successful purchase callback starts that cooldown; its failed-request path instead retries after 60 seconds. No exceptions or runtime errors were found in the game log. The original backup matched its recorded SHA-256 exactly.

The earlier check verified normal claiming in v1.0; it did not record the cosmetic's identity. Future builds or server behavior remain outside that check. Please report your build ID, toggle settings, and error text if a claim fails.

**A live emote-chest smoke test passed for v1.1 on 12 October 2026.** Upgrading the same installation from its verified original backup enabled both chest types. After restarting through Steam, the previously ready emote indicator disappeared and its normal 30-minute cooldown began without a manual claim click. No runtime errors were found in the game log. The cosmetic's identity was not recorded.

Live F7 and Shift + F9 checks changed the normal and emote saved settings independently from on to off and back to on through the new native controls. Both were left enabled. Plain F9 was missed by the automation tool on this PC; Shift + F9 reached the same emote handler. Automated mouse clicks were blocked by the game's transparent window, so checkbox clicks were verified in the behavioral fixture and the actual scene hierarchy was inspected separately.

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

Both chest types default to enabled. `--normal-only` makes the initial emote default off; a saved in-game toggle takes precedence. `--emotes` remains accepted for compatibility with v1.0. Change the switches in the game after installation. `--verbose` includes a diagnostic stack trace. `--patch-copy` refuses to overwrite the source or an existing output.

## How the patch works

`PatchEngine.cs` adds a private helper to `BongoCat.Shop`. It reads the corresponding saved PlayerPrefs switch on each attempt, then checks `ChestIsReady`, `_openingChest`, the shop-item reference and `CanBuy()` before invoking the original `Buy()`. The game's own `StartBuy()` keeps its pets and pending-server checks intact.

The helper runs at the beginning of `OnChestReady()` and the game's existing timer state machine. The timer hook allows a later retry even when a popup is already shown. The game's timer interval, failed-request cooldown and purchase callbacks are preserved.

`NativeControlsPatch.cs` adds native settings controls with separate keys and callbacks. Their saved values are read directly by the claim helper, so toggling does not change the installed DLL or its backup fingerprint.

No game or Steam assembly is shipped. Development inspection used [REA](https://github.com/morluto/rea) and [Mono.Cecil](https://github.com/jbevain/cecil). See [third-party notices](THIRD-PARTY-NOTICES.md).

## License

MIT for BongoClaim. This is an independent, unofficial project; it is not affiliated with Bongo Cat or Steam.
