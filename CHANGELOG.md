# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.4.0] - wip

### Added

- Mods can now add context tag that starts with "cssm_tab_" (for example "cssm_tab_catalogue") to be sorted into a separate tab.
- New default tab icon for the case where the tab has no associated item, can be disabled via config.

### Change

- Improve furniture draw in shop menu to prevent overflows.

## [0.3.3] - 2025-08-01

### Added

- New tab option sort seeds for current season to separate tab.

## [0.3.2] - 2025-04-26

### Added

- Search now has option to include description (default off).

### Fixed

- Buying items within a tab no longer reset you to the top.
- Adjusted drawing code for stack count when small cell sizes.

## [0.3.1] - 2025-04-20

### Fixed

- Gamepad search should open keyboard.
- Eliminate use of Repeat to avoid trapping in infinite loop.

## [0.3.0] - 2025-04-19

### Added

- More options for tabs.
- Ability to change 5, 25, and 999 buy stacks.
- Display purchase stack count while keys are pressed.
- Gamepad can now activate and deactivate search with L-Stick.
- Search can be repositioned in config if neded.

### Fixed

- Adjust search bar draw location for bigger backpack compatibility.
- Patched Customize Dresser and More Dresser Variety's draw patches for compatiblity.
- Fixed a vanilla bug where you can't buy 999 when it is a trade item.

## [0.2.1] - 2025-04-16

### Fixed

- fix a crash when the for sale list is resized.

## [0.2.0] - 2025-04-16

### Added

- Initial implementation.
