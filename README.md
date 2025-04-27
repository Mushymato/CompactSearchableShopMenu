# Compact Searchable Shop Menu

A compact and searchable shop menu.

## Inspirations

There are many shop menu mods out there and this mod is really just a combination of features I personally like, here are the direct inspirations:

- [Better Shop Menu](https://www.nexusmods.com/stardewvalley/mods/2012): Grid menu.
- [Catalogue Filter](https://www.nexusmods.com/stardewvalley/mods/13137) - [Continued](https://www.nexusmods.com/stardewvalley/mods/22379): Search box position.
- [Shop Tabs](https://www.nexusmods.com/stardewvalley/mods/29435): Shop tabs.
- [Better Crafting](https://www.nexusmods.com/stardewvalley/mods/11115): Bulk purchase count display.

## Installation

1. Download and install SMAPI.
2. Download this mod and install to the Mods folder.

## Configuration

- `Shop Item Per Row`: Number of items per row when a price must be displayed (e.g. regular shops).

- `Dresser Item Per Row`: Number of items per row when there are no prices (e.g. dressers, catalogues).

- `Stack Count on Shift+Ctrl`: Number of items to buy when using Shift+Ctrl (vanilla 25).

- `Enable Search and Filters`: Enable search box and category tabs (if the shop does not already have side tabs), this setting overrides the following ones.

- `Enable Search`: Enable search box.

- `Search By Description`: Include item description in search.

- `Search Box Offset`: Adjust the search box's position (default 0 0).

- `Enable Tabs: Categories`: Enable tabs based on item categories.

- `Enable Tabs: Detailed Seeds`: Enable separate tabs for crop/tree/bush seeds.

- `Enable Tab: Recipes`: Enable separate tab for recipes.

## Translations

- English

- Simplified Chinese

Additional translations are greatly appreciated. If you would like to get DP for your work, feel free to make a separate mod page.

## Compatibility

### Compatible Mods

- Any shops added through content patcher is compatible.

- This mod uses vanilla assets for most of it's UI elements, with exception of the tabs for QiGemShop and LostItem shop which have custom tab graphics created to match those shop's themes.
    - Those are loaded from the asset folder of this mod, but you can override it in content patcher by targeting:
        - `mushymato.CompactSearchableShopMenu/tab/QiGemShop` for `QiGemShop`
        - `mushymato.CompactSearchableShopMenu/tab/LostItems` for `LostItems`

- For mod makers who used a custom shop visual theme, you can include compatibility by loading a custom tab texture to `mushymato.CompactSearchableShopMenu/tab/<yourShopId>`, 16x16 is the recommended size.

- [Happy Home Designer](https://www.nexusmods.com/stardewvalley/mods/19675) implements a completely custom menu, so it takes precedence over this mod for catalogues (as it should).

### Incompatible Mods

Since this mod heavily modifies draw related logic on shop menu, it will be incompatible with any mod assuming the vanilla layout. Refer to pinned comment for up to date info about mods known to be incompatible.

### Gamepad

The grid menu works with gamepads, and you can use LeftShoulder and RightShoulder to switch between category tabs (not rebindable).

To toggle search, press L-stick.

### Android

No.

### Mods In Screenshots

- Cornucopia - [More Crops](https://www.nexusmods.com/stardewvalley/mods/19508), [More Flowers](https://www.nexusmods.com/stardewvalley/mods/20290), [Artisan Machines](https://www.nexusmods.com/stardewvalley/mods/24842)
- [Baubles](https://www.nexusmods.com/stardewvalley/mods/29720)
- [Wildflour's Atelier Goods](https://www.nexusmods.com/stardewvalley/mods/27049)
