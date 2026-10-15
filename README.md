# Bongo Cat Auto Chest ฅ

**Auto-claim normal and emote chests, open stored tokens, and control your pet score in Bongo Cat.** Play normally through Steam after installing the patch. Optional startup repair checks the patch before launching the game and reapplies it after compatible Steam updates. The helper does not need to stay open during play.

[Download Bongo Cat Auto Chest v1.3.0 for Windows](https://github.com/NH4OH-35/bongo-cat-auto-chest/releases/download/v1.3.0/BongoCatAutoChest-v1.3.0-windows.zip) · [All releases](https://github.com/NH4OH-35/bongo-cat-auto-chest/releases) · [Report a problem](https://github.com/NH4OH-35/bongo-cat-auto-chest/issues)

![Bongo Cat Auto Chest v1.2 in Bongo Cat General settings: normal and emote auto-claim, Keep pets, token claim buttons, and pet score editor](docs/game-settings.png)

The screenshot shows the in-game controls introduced in **v1.2**, which are unchanged in **v1.3**. Your token counts and pet score will differ. **Keep pets when claiming** is checked in this example; it starts **off** on a fresh installation.

## 1. Install or upgrade

**Already using v1.2? Download the v1.3 helper for startup repair.** If the game still shows all six added rows, its patch is already installed. If a Steam update removed them, follow the installation steps below.

1. Download **BongoCatAutoChest-v1.3.0-windows.zip** using the link above. Do not download GitHub's **Source code** ZIP for installation.
2. Right-click the downloaded ZIP → **Extract All…** → **Extract**. Open the extracted folder; do not run the app from inside the ZIP.
3. Exit **Bongo Cat** normally. Leave Steam available for launching the game later.
4. Double-click **BongoCatAutoChest.exe**. Keep **Mono.Cecil.dll** in the same folder. **BongoCatAutoChest.Cli.exe** is optional and is not needed for normal use.
5. Check the game folder shown in Bongo Cat Auto Chest. If it is empty or wrong, click **Browse…** and select the folder containing **BongoCat.exe**. Select the **folder**, not the EXE file, **BongoCat_Data**, or **Managed**. You can find it through Steam → Bongo Cat → **Properties → Installed Files → Browse**.
6. The helper checks the build automatically. You can click **Check build** to check again. If the build is supported, click **Install auto claim →**. Existing v1.0/v1.1 installations show **Upgrade controls →** instead.
7. Wait for the success message. The button changes to **Controls installed**. Close Bongo Cat Auto Chest and launch **Bongo Cat from Steam**.

For automatic checks at Windows sign-in, continue with [Set up startup repair](#set-up-startup-repair).

The installer backs up your exact original game DLL before patching it. Upgrades use that verified original backup and preserve your saved normal/emote switch settings.

## 2. Open the controls

1. Start Bongo Cat normally through Steam.
2. Focus the game and press **Escape** or **M** to open its menu.
3. Click the **gear** tab, then **General**.
4. Scroll down inside the panel until you see the rows shown in the screenshot.

Leave **Steam and Bongo Cat running** for auto-claim to work. You can close the helper after launching the game. If startup repair is enabled, it runs again at Windows sign-in to check the patch and start the game.

## 3. Turn automatic claims on or off

1. Check **Auto claim normal chests** to enable automatic normal-chest claims; uncheck it to pause them.
2. Check or uncheck **Auto claim emote chests** independently.
3. Close the menu and continue playing. Your choices take effect immediately and save for the next launch.

| Setting | Fresh-install default | Effect |
| --- | --- | --- |
| Auto claim normal chests | On | Claims an eligible normal chest when the game's timer is ready. |
| Auto claim emote chests | On | Claims an eligible emote chest when the game's timer is ready. |
| Keep pets when claiming | Off | When enabled, normal and emote chest claims do not deduct available pets. |

Saved preferences take precedence over these defaults. A request already sent can finish after you disable its switch. Automatic claims use the game's existing timer and exchange requirements; claims cost pets while **Keep pets when claiming** is off.

## 4. Claim stored chest tokens

1. Open **gear → General** and find **Normal tokens: N | claim** or **Emote tokens: N | claim**.
2. If the number is greater than **0**, click the row for the chest type you want.
3. Wait for the game and Steam to finish that claim. Each successful claim consumes **one owned token**.
4. Click again to open another token, if you still have one.

These buttons can open stored tokens while the local 30-minute countdown is still running. They work independently of the auto-claim switches. A successful manual claim starts the game's normal cooldown again.

**“0 | claim” means you currently have no owned tokens of that type to open.** The row is disabled. “Loading” means Steam inventory is not ready yet. The screenshot's zero counts are an example, not a target value.

Claims also require the game's other exchange inputs, enough pets unless **Keep pets** is enabled, and no pending request. Steam decides whether an exchange succeeds. These controls do not create tokens or change Steam's drop schedule; editing your pet score does not create tokens either.

## 5. Stop chest claims deducting pets

1. In **gear → General**, check **Keep pets when claiming**.
2. Claim a normal or emote chest automatically, through a stored-token row, or through the game's original chest button.

Available pets are preserved while this switch is on. Uncheck it to restore the normal pet cost. The switch saves for the next launch. Owned tokens are still consumed, and gifts retain their original spending rules. An in-progress request uses the price choice from when it started.

## 6. Edit your available pet score

1. Click **Pets: N | edit** in General, or press **F10** with the game menu open.
2. Type the **full new score** using the number row or numpad. The editor starts empty; you do not need to erase the old score first. For example, type `100000` to set available pets to 100,000.
3. Use **Backspace** to correct a digit.
4. Press **Enter** to save, or **Escape** to cancel. Clicking the editor again or closing the menu also cancels.

Use a nonnegative whole number. If the row says **Invalid score; type again**, enter a smaller valid number. The maximum is `2,147,483,646 − total pets previously spent`.

Saving changes your available pets through the game's Steam stats API. Lifetime achievement taps and total pets previously spent are preserved. Restoring the original game DLL removes the controls but does not undo a score you saved.

## Keyboard shortcuts

Focus Bongo Cat and keep its menu open. Shortcuts do not run while another text field is active. Toggle and token shortcuts pause during score editing.

| Key | Action |
| --- | --- |
| **F5** | Toggle Keep pets when claiming |
| **F7** | Toggle normal auto-claim |
| **F9** | Toggle emote auto-claim |
| **F10** | Start editing pet score |
| **F11** | Claim one owned normal token |
| **Insert** | Claim one owned emote token |

During score editing, **Enter** saves, **Escape** cancels, and **Backspace** deletes a digit. If plain F9 is missed on your PC, **Shift + F9** reaches the same emote toggle. The shortcuts update the same saved controls as mouse clicks.

## Set up startup repair

A Steam update can replace the patched game DLL with the official one. This removes the added controls until the patch is reapplied. **v1.3 startup repair** checks for that before launching Bongo Cat and repairs supported builds automatically.

1. In Bongo Cat, open **gear → General** and enable the game's **Autostart** setting if you want it to start when you sign in to Windows.
2. Let Steam finish any game update, then exit Bongo Cat normally.
3. Open **BongoCatAutoChest.exe** from the extracted v1.3 folder and check the selected game folder.
4. Click **Check build**. Install the patch if it is absent and the current build is supported.
5. Click **Enable startup repair** and wait for its success message. The button changes to **Disable startup repair**.
6. Close the helper. At your next Windows sign-in, startup repair checks the game while it is closed, reapplies a compatible missing patch if needed, then launches Bongo Cat through Steam.

The helper keeps its own executable and Mono.Cecil dependency in `%LOCALAPPDATA%\BongoCatAutoChest` for startup use. Moving or deleting the extracted download folder does not remove that startup copy. You can keep the original download for later maintenance.

**Keep the game's Autostart setting on if you want this startup check.** Turning Autostart off removes the game's startup entry. Turning it back on points that entry directly at the game again; open the v1.3 helper and click **Enable startup repair** again to restore the check.

To stop automatic repair, click **Disable startup repair** in the helper. This restores the previous startup command if Bongo Cat Auto Chest still owns the entry. It restores the startup choice that existed before you enabled repair. The installed chest controls remain.

### Repair and launch manually after an update

1. Let Steam finish updating Bongo Cat, then exit the game.
2. Open PowerShell in the extracted v1.3 folder.
3. Run the following, replacing the example with your game folder:

```powershell
.\BongoCatAutoChest.Cli.exe --launch 'G:\SteamLibrary\steamapps\common\BongoCat'
```

This checks or repairs the patch, then starts the game through Steam. Startup repair uses the same launch action at Windows sign-in. A direct Steam launch uses whatever DLL is currently installed; it does not run this repair check.

The helper refuses to patch a running game, a pending Steam update, or an unsupported assembly layout. If Steam replaces the DLL during launch, the helper reports it and asks you to exit the game and launch through the helper again after the update finishes. Unsupported builds need a compatible helper update.

## Troubleshooting

| What you see | What to do |
| --- | --- |
| **Controls installed** in the helper | The patch is already installed. Start Bongo Cat from Steam; no second installation is needed. |
| Missing rows in General | Scroll down. If only the two auto-claim switches remain, exit the game and use **Upgrade controls**. If all added rows disappeared after an update, check the build and install again, or use the v1.3 startup repair launcher. |
| **0** tokens or **loading** | Zero means no owned token is available. For loading, allow Steam inventory to finish loading with Steam running. |
| Tokens exist but a claim does not start | Wait for any pending request. Check the game's exchange requirements and your pet balance, or enable **Keep pets**. |
| Pets decreased during a claim | Check **Keep pets when claiming** before starting another claim. It defaults off; changing it does not alter a request already sent. |
| Missing **Mono.Cecil.dll** or dependency error | Extract the whole release ZIP and keep its files together. |
| Game is running during install, repair or restore | Exit Bongo Cat normally, then try again. The helper will not force-close it. |
| Auto-claim disappeared after a Steam update | Steam may have replaced the patched DLL. Finish the update, exit the game, then run the v1.3 helper's `--launch` action or install the patch again. |
| Startup repair reports a pending Steam update | Allow Steam to finish the update before retrying. The helper will not patch during the update. |
| Game starts at sign-in but the controls are missing | If you toggled the game's Autostart setting off and on, reopen the helper and click **Enable startup repair** again. |
| DLL changed during launch | Exit the game, let Steam finish, then launch through the helper again. |
| Unsupported build or a blocked file write | Read the reported error. The helper refuses incompatible layouts and does not change Windows security settings. Report the build ID and error text. |

Requires **Windows 10 or 11**, **.NET Framework 4.8** (normally already installed), the Steam Windows version of Bongo Cat, and write access to its game folder. No administrator privileges are requested. Release binaries are unsigned. Keep antivirus protection enabled.

## Restore the original game

1. Exit Bongo Cat normally.
2. Open **BongoCatAutoChest.exe** and check the selected game folder.
3. Click **Restore original** and wait for success.
4. Close the helper and start Bongo Cat through Steam.

Restoration also disables startup repair and restores the previous startup command when the helper still owns it. It returns the matching original DLL byte-for-byte. Game saves and inventory files are not touched. A pet score you explicitly saved remains saved.

Backups live inside the selected game folder:

```text
.bongo-claim/
  install.json
  backups/<original-sha256>.dll
```

The helper verifies backup hashes before using them. If Steam changes the installed DLL, the helper refuses to restore an older backup over it.

## After a Steam update or an older replacement DLL

1. Exit Bongo Cat after Steam finishes the update.
2. Open Bongo Cat Auto Chest and click **Check build**.
3. If the current build is supported and the patch is absent, install it again, then start the game through Steam. Alternatively, use the v1.3 `--launch` action to check, repair and launch in one step. Previous backups are retained.
4. If the layout is unsupported, wait for a compatible Bongo Cat Auto Chest update and report your build ID.

If you previously installed the archived replacement DLL, or your original backup is missing or damaged, use Steam → Bongo Cat → **Properties → Installed Files → Verify integrity of game files** to recover the current official files before applying Bongo Cat Auto Chest.

## Compatibility and validation

The **14 October 2026** Steam update installed Windows build **25936063** and replaced the patched DLL. On **15 October 2026**, the helper inspected that build, confirmed its layout was compatible, and reapplied the patch. Saved switch preferences were retained. Startup enable/disable/re-enable restored the original command exactly, and the permanent helper/dependency copies matched their source hashes. A controlled live replacement with the verified official DLL was automatically repaired by the saved launcher before starting through Steam; the patched file and startup command remained intact after launch, and the game log contained no runtime errors. A full Windows reboot with startup repair has not been recorded.

```text
Build 25936063 original Assembly-CSharp.dll SHA-256
96bbcbb2733988fd31ce261891eb3027888389e03b6a23dbd63e9e7f518adfb8
```

Earlier compatibility and live checks were performed on **12 October 2026**, on Steam Windows build **25723683**:

```text
Original Assembly-CSharp.dll SHA-256
7566e8d9846cdb38eadcdb729cfcf676dc338cd738e99f664dc6ef3b79e87b83
```

**58 executable tests passed** using a source-owned simulated game assembly. They cover automatic and manual claims, independent switches, token quantities and consumption, claims during cooldown, missing inputs and pending requests, captured price choices, gift spending, score save/cancel/backspace/overflow, focus gates, upgrades, corrupted backups, Steam updates, rollback and exact restoration. Eight launcher tests additionally cover automatic preparation, repeated no-op checks, supported Steam replacements, retained original backups, unsupported builds, pending updates and malformed manifest flags.

Live checks verified normal and emote automatic claims in earlier versions. In v1.2, all six added rows rendered; a stored normal-token claim succeeded during countdown without reducing pets; an emote-token claim through Insert reached its successful cooldown during countdown; and score input followed by cancellation left the score unchanged. The final game log contained no runtime errors. **Live score saving and cosmetic identities were not recorded**; score saving and persistence are covered by the simulated-game tests. The supplied screenshot documents the current controls.

The installer inspects the current game assembly and runtime dependencies, then reads back the patched output. It refuses unsupported layouts and conflicting auto-claim mods. Future game builds and Steam server behavior remain outside these checks.

## Build from source

Run in PowerShell on Windows:

```powershell
.\build.ps1 -Test
.\package.ps1
```

No .NET SDK or Visual Studio is required. The build uses the Windows .NET Framework compiler and a pinned, hash-verified Mono.Cecil NuGet package. Outputs go in `artifacts/`; dependencies go in `tools/`. Neither directory is tracked.

The release ZIP contains **BongoCatAutoChest.exe**, **BongoCatAutoChest.Cli.exe**, **Mono.Cecil.dll**, this README, **docs/game-settings.png**, and the licenses. The screenshot is included so it displays in the extracted instructions. No game DLL, test fixture, backup, save, or account data is packaged. The adjacent `.zip.sha256` file contains the expected SHA-256 hash for checking the ZIP.

## Command line

For diagnostics and scripted use:

```powershell
.\BongoCatAutoChest.Cli.exe --inspect 'G:\SteamLibrary\steamapps\common\BongoCat'
.\BongoCatAutoChest.Cli.exe --install 'G:\SteamLibrary\steamapps\common\BongoCat'
.\BongoCatAutoChest.Cli.exe --restore 'G:\SteamLibrary\steamapps\common\BongoCat'
.\BongoCatAutoChest.Cli.exe --enable-startup 'G:\SteamLibrary\steamapps\common\BongoCat'
.\BongoCatAutoChest.Cli.exe --disable-startup 'G:\SteamLibrary\steamapps\common\BongoCat'
.\BongoCatAutoChest.Cli.exe --launch 'G:\SteamLibrary\steamapps\common\BongoCat'
.\BongoCatAutoChest.Cli.exe --patch-copy 'path\Assembly-CSharp.dll' "$env:TEMP\patched-copy.dll"
```

`--enable-startup` installs the stable launcher and wraps the existing Bongo Cat startup entry. `--disable-startup` restores the previous command when this helper still owns the entry. `--launch` checks the current build while the game is closed, installs a missing compatible patch, and launches through Steam.

Both chest types default on. `--normal-only` makes the initial emote default off; a saved in-game preference takes precedence. `--emotes` remains accepted for compatibility with v1.0. `--verbose` includes a diagnostic stack trace. `--patch-copy` refuses to overwrite its source or an existing output.

## How the patch works

The archived [Bongo-Cat-Auto-Claim](https://github.com/zelda0079/Bongo-Cat-Auto-Claim) distributes an older complete game DLL. Bongo Cat Auto Chest patches the assembly actually installed on your PC, checks the expected structure, and retains your exact original for restoration.

`PatchEngine.cs` inserts a guarded claim helper into the game's chest-ready callback and timer. It reads saved normal/emote switches and checks readiness and purchase locks before calling the original `ShopItem.Buy()`. The game's timer interval, purchase callbacks and failed-request cooldown are retained.

`NativeControlsPatch.cs` adds three saved switches and three native action rows. `OwnedTokensPatch.cs` binds source-owned feature methods from an embedded compile-time template to the installed game's Unity and Steam APIs. Only new feature members are copied; no stub types or template dependency are installed.

`RuntimeFeatures.cs` reads the real normal and emote inventory quantities, checks the full exchange requirements before manual purchases, and implements the inline score editor. Keep pets changes the captured local price flag for chest purchases. No game or Steam assembly is shipped.

Startup repair uses the same compatibility, backup and atomic-write checks as manual installation. It changes only the current user's Bongo Cat startup entry, preserves its previous command for disabling or restoration, and keeps older exact backups after Steam updates.

Development inspection used [REA](https://github.com/morluto/rea) and [Mono.Cecil](https://github.com/jbevain/cecil). See [third-party notices](THIRD-PARTY-NOTICES.md).

## License

MIT for Bongo Cat Auto Chest. This is an independent, unofficial project; it is not affiliated with Bongo Cat or Steam.
