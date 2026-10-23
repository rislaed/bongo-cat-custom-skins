# Custom Character Creation Guide for Bongo Cat

This documentation provides a comprehensive guide for players, artists, and modders to install, build, and create custom characters for Bongo Cat.

## 1. Quick Installation

### Windows

1. Download the latest `BongoCatCustomSkins.zip` release from [GitHub Releases](https://github.com/rislaed/bongo-cat-custom-skins/releases/latest).
2. Locate your Bongo Cat game directory in Steam:
    - Open your Steam Library.
    - Right-click **Bongo Cat** and select **Properties...**.
    - Switch to the **Installed Files** tab and click **Browse...**.
3. Extract `BongoCatCustomSkins.zip` directly into the game folder (where `BongoCat.exe` is located).
    - This places `DoorstopLoader.dll`, `doorstop_config.ini`, and the `Mods/` directory alongside the game executable.

### Linux & Steam Deck (Proton / Wine)

1. Extract `BongoCatCustomSkins.zip` into your Bongo Cat folder (typically `~/.steam/steam/steamapps/common/BongoCat/`).
2. Configure Proton DLL override in Steam:
    - In Steam, right-click **Bongo Cat** and select **Properties...**.
    - In the **General** tab under **Launch Options**, set:
      `WINEDLLOVERRIDES="version=n,b" %command%`
    - This instructs Wine/Proton to load the modded `version.dll` (Doorstop) instead of the built-in system library.

## 2. Building from Source

The build script compiles the solution, strips Unity 6 metadata attributes via Mono.Cecil, packages `BongoCatCustomSkins.zip`, and updates game files:

```text
python build.py -g <path> [--no-deploy] [--no-skins]
    -g, --game-dir: path = auto Target Bongo Cat installation directory.
    --no-deploy: bool = false Package release archive without deploying to game.
    --no-skins: bool = false Exclude CustomSkins directory from zip and deployment.
```

## 3. Directory Structure & File Specifications

All custom characters are loaded from the `Mods/CustomSkins/` directory located inside your Bongo Cat installation folder. You can structure character files using either of the following two layouts:

### Option A: Subfolder Layout (Recommended)

Place each character into its own isolated subfolder (e.g. `Mods/CustomSkins/WizardCat/`). Files can use either clean standard names (`Left.png`, `Skin.json`) or names prefixed with the character's name (`WizardCatLeft.png`, `WizardCat.json`):

```text
BongoCat/
├── BongoCat.exe
├── DoorstopLoader.dll
└── Mods/
    ├── BongoCatCustomSkins.dll
    └── CustomSkins/
        └── WizardCat/
            ├── Skin.json       [Optional] Metadata, wardrobe styling, and rendering flags
            ├── Icon.png        [Optional] Preview card icon in the wardrobe (defaults to Left.png)
            ├── Left.png        [Required] Base resting idle pose; fallback for missing frames
            ├── LeftPunch.png   [Optional] Left paw / hand down tapping reaction
            ├── Right.png       [Optional] Resting right paw (only used if fullBody is false)
            ├── RightPunch.png  [Optional] Right paw / hand down tapping reaction
            ├── BothPunch.png   [Optional] Simultaneous key tap; or idle if inverted
            ├── Petting.png     [Optional] Mouse click and petting reaction
            └── Sleeping.png    [Optional] AFK sleep reaction after inactivity
```

### Option B: Flat Root Layout

Place all sprite files and the configuration JSON directly inside `Mods/CustomSkins/` using the character name as a prefix:

```text
Mods/CustomSkins/
├── WizardCat.json          [Optional] Metadata, wardrobe styling, and rendering flags
├── WizardCatIcon.png       [Optional] Preview card icon in the wardrobe (defaults to Left.png)
├── WizardCatLeft.png       [Required] Base resting idle pose; fallback for missing frames
├── WizardCatLeftPunch.png  [Optional] Left paw / hand down tapping reaction
├── WizardCatRight.png      [Optional] Resting right paw (only used if fullBody is false)
├── WizardCatRightPunch.png [Optional] Right paw / hand down tapping reaction
├── WizardCatBothPunch.png  [Optional] Simultaneous key tap; or idle if inverted
├── WizardCatPetting.png    [Optional] Mouse click and petting reaction
└── WizardCatSleeping.png   [Optional] AFK sleep reaction after inactivity
```

### Sprite Specifications & Rules

- **Resolution & Format:** All sprites must be 256 x 256 pixels, 32-bit RGBA PNG files with transparent backgrounds.
- **Fallback Cascading:** If an optional reaction sprite (`Petting.png`, `Sleeping.png`, `LeftPunch.png`, etc.) is omitted, the loader automatically falls back to `Left.png`.
- **Automated Edge Protection:** The loader automatically sanitizes transparent RGB values and surrounds each loaded texture with a 1-pixel transparent safety padding. You do not need to manually pad your textures; bilinear white halos and edge wrapping lines are prevented automatically.

## 4. Rendering Architectures (Full-Body vs. Layered)

Bongo Cat supports two visual architectures. Choosing the right one determines how your character is drawn and animated:

### Full-Body Mode (`"fullBody": true`)

- **Ideal for:** Humanoids, original species, furries, creatures with unique anatomy, or any art style where limbs are connected directly to the body.
- **Example:** Bovicoon Romi uses this architecture as a monolithic illustration where all poses and animations are self-contained in full-character drawings.
- **How it functions:** Every animation frame is drawn as a complete, monolithic illustration on the main body layer. The game's native cat paws are hidden, avoiding duplicate limbs or visual clipping, while maintaining click detection and petting reactions.
- **Recommended Sprites:** `Left`, `LeftPunch`, `RightPunch`, `BothPunch`, `Petting`, and `Sleeping` (dedicated `Right` paw is omitted; `Icon` is optional).

### Layered Mode (`"fullBody": false`)

- **Ideal for:** Classic Bongo Cat reskins and recolors that follow the original two-layer anatomy.
- **How it functions:** The main body sprite (`Left`) contains the torso and left paw resting on the table. The right paw is drawn separately in `Right.png` and positioned over the body layer. When pressing right-hand keys, the game swaps the right paw to `RightPunch.png`.
- **Required Sprites:** All standard frames, including the dedicated `Right.png` paw sprite.

## 5. Configuration File (`Skin.json` or `<Name>.json`)

Place a JSON file alongside your sprites. Below are complete, copy-pasteable configuration examples for both character architectures.

### Full-Body Character Example (`Skin.json`)

```json
{
    "name": "WizardCat",
    "displayName": "Archmage Cat",
    "description": "Master of arcane feline arts.",
    "quality": "legendary",
    "fullBody": true,
    "disableClothing": true,
    "invertIdleAndBothPunch": false,
    "incompatibleItems": [
        "Black Wizard Hat",
        "Top Hat",
        "Chef Hat",
        "Gat",
        "113",
        "149"
    ]
}
```

### Layered Character Example (`Skin.json`)

```json
{
    "name": "CalicoBongo",
    "displayName": "Calico Kitty",
    "description": "A classic tri-color calico friend.",
    "quality": "rare",
    "fullBody": false,
    "disableClothing": false,
    "invertIdleAndBothPunch": false,
    "incompatibleItems": []
}
```

## 6. Comprehensive Configuration Reference

Every field in the configuration JSON is explained below in detail:

### `name` (string)

- **Default:** Folder name if inside a subfolder, otherwise empty string.
- **Description:** The unique internal identifier for the character. Used as the lookup key in sprite registration, ownership validation, and saved offset maps. Avoid renaming this once offsets have been configured, as offsets in `__offsets__.json` are bound to this name.

### `displayName` (string)

- **Default:** Defaults to `name` if omitted or empty.
- **Description:** The friendly title displayed in the in-game wardrobe menu tooltip and character selection card. Allows you to keep a technical internal `name` (e.g. `"SciFi_Astronaut_01"`) while displaying a clean title (`"Astronaut Cat"`).

### `description` (string)

- **Default:** `""` (empty string).
- **Description:** Flavor text, character backstory, or artist attribution displayed in the wardrobe tooltip beneath the item title.

### `quality` (string)

- **Default:** `"legendary"`.
- **Options:** `"common"`, `"uncommon"`, `"rare"`, `"legendary"`.
- **Description:** Determines the rarity border color and glowing visual styling of the character card in the in-game wardrobe grid:
  - `"common"`: Neutral slate gray border.
  - `"uncommon"`: Forest green border.
  - `"rare"`: Deep sapphire blue border.
  - `"legendary"`: Brilliant gold/amber glowing border.

### `fullBody` (boolean)

- **Default:** `false`.
- **Description:** Controls whether the character uses monolithic single-layer drawings (`true`) or traditional two-layer cat anatomy (`false`). When set to `true`, native cat paws are hidden automatically so that custom arm and body drawings render cleanly without overlapping vanilla paws.

### `disableClothing` (boolean)

- **Default:** `false`.
- **Description:** Set to `true` if your character's art already includes custom clothes, shirts, or robes drawn directly onto the body. When enabled, equipped wardrobe clothing items (shirts, hoodies, vests) are automatically hidden while this skin is active, preventing visual clipping and clashing layers.

### `invertIdleAndBothPunch` (boolean)

- **Default:** `false`.
- **Description:** Inverts the resting and tapping poses between `Left` and `BothPunch`. In vanilla Bongo Cat, `Left` is the resting pose (both paws resting down) and `BothPunch` is both paws tapping down. If your character's resting anatomy has hands raised or resting on the keyboard in `BothPunch` and tapping down in `Left`, set this option to `true`.

### `incompatibleItems` (array of strings / integers)

- **Default:** `[]` (empty list).
- **Description:** A blacklist of clothing, hats, or accessories that cannot be equipped with this character. When this character is equipped, any incompatible items currently worn are automatically unequipped, and they appear grayed out with a lock indicator in the wardrobe.
- **How Matching Works:** You can specify items using any of the following formats:
  - **Exact item display name:** Matches the standard English wardrobe name (case-insensitive).
  - **Space-stripped name:** Matches internal sprite identifiers.
  - **Numeric Steam definition ID:** Matches the item's unique Steam runtime ID.

#### Practical `incompatibleItems` Examples

```json
"incompatibleItems": [
    "Top Hat",
    "Cowboy Hat",
    "Chef Hat",
    "Captain Hat",
    "Gat",
    "Aviator Hat",
    "Big Pumpkin Hat",
    "Autumn Leaf Crown",
    "113",
    "149",
    "245"
]
```

#### Why use `incompatibleItems`?

- **Anatomy Conflicts:** Characters with tall animal ears, horns, or antennas that clip through standard hats.
- **Pre-Drawn Accessories:** Characters that already have a hat or crown drawn directly into their sprite art.
- **Size Mismatches:** Oversized or bulky hats that obstruct the character's face.

### `id` (integer)

- **Default:** `0`.
- **Description:** Optional internal numeric item ID. If omitted or set to `0`, the mod deterministically calculates a unique ID starting from `900000` based on a stable hash of the character's `name`. You do not need to configure this manually.

## 7. In-Game Offset & Scale Editor

Every character has unique proportions, head heights, and ear placements. The mod features a real-time in-game editor allowing you to align hats and instruments with precision.

### Keybindings & Controls

- **`F9` — Hat Alignment Mode:**
  - The currently equipped hat begins a slow breathing pulse (smoothly fading between 0.85 and 1.0 alpha) to show it is selected.
  - **Left Mouse Click + Drag:** Reposition the hat on the character's head.
  - **Mouse Scroll Wheel:** Scale the hat up or down (range: 0.2x to 4.0x in steps of 0.05x).
  - Press `F9` again to exit editor mode.

- **`F10` — Instrument & Body Alignment Mode:**
  - The character body and table begin a slow breathing pulse (smoothly fading between 0.70 and 1.0 alpha).
  - **Left Mouse Click + Drag:** Reposition the instruments relative to the character.
  - **Mouse Scroll Wheel:** Scale the instruments up or down.
  - Press `F10` again to exit editor mode.

- **Standard Desktop Movement:**
  - When neither `F9` nor `F10` is active, clicking and dragging Bongo Cat moves the character across your desktop as usual without altering item offsets.
  - All adjustments made in editor mode are written immediately to `Mods/CustomSkins/__offsets__.json` upon mouse release or scroll. These values load automatically whenever the skin is selected, persisting seamlessly across game restarts.

## 8. Mod Architecture & Technical Details

### Mod Loader & Injection (`DoorstopLoader`)

Bongo Cat uses Unity Doorstop (`version.dll`) to load custom code without modifying vanilla game files. Upon launching `BongoCat.exe`, Doorstop starts the Mono runtime and invokes `DoorstopLoader.dll` before the engine scene loads:

- **Dependency Discovery:** Registers an `AppDomain.AssemblyResolve` hook so the game runtime seamlessly resolves assemblies and libraries located in `Mods/` and subdirectories.
- **Main Thread Initialization:** Safely waits until Unity loads `Assembly-CSharp` before executing engine code.
- **Universal Mod Discovery:** Scans the `Mods/` folder recursively for `.dll` assemblies and invokes their static parameterless entrypoint (`Initialize`, `Init`, `Load`, or `Main`).

### Mono.Cecil Attribute Stripping

Standard C# compilers (including MSBuild and Roslyn) automatically inject framework metadata attributes like `TargetFrameworkAttribute` into compiled assemblies. In Unity 6's embedded runtime (`mono-2.0-bdwgc.dll`), these attributes cause domain initialization crashes during Doorstop's early boot phase.

`Mono.Cecil.dll` is used directly after compilation to clear assembly custom attributes from `DoorstopLoader.dll` and `BongoCatCustomSkins.dll`, ensuring full compatibility with Unity 6.

### BepInEx Coexistence

If BepInEx is installed in the game directory (`BepInEx/core/BepInEx.Preloader.dll`), `DoorstopLoader` automatically detects it and chains execution to BepInEx's preloader before booting custom skins. This allows many mods to run concurrently without configuration conflicts.
