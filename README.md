# Dragon Den Mod Manager (Community)

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows%20x64-blue)](#requirements)
[![Forge](https://img.shields.io/badge/Forge-sp--mod.com-orange)](https://sp-mod.com)

> Community-maintained fork of [Drexira/DragonDen-ModManager](https://github.com/Drexira/DragonDen-ModManager).  
> **Unofficial.** Not affiliated with Drexira, SPT, or The Forge staff.

A desktop mod manager for **SPT (Single Player Tarkov)** built with Avalonia UI and .NET 9. Browse and install mods from The Forge, manage installed client/server mods, and switch the UI between **English**, **Simplified Chinese**, and **Traditional Chinese**.

---

## Why this fork?

Upstream **v0.0.8** still targets the old Forge host and older SPT folder layouts. After SPT 4.1 and the Forge move to **https://sp-mod.com**, the stock build stops working for many users.

This community build focuses on:

| Area | Change |
|------|--------|
| **Forge** | Default API base URL → `https://sp-mod.com` (legacy hosts rewritten / migrated) |
| **SPT 4.1+** | Detects `SPT_Runtime\SPT.Server.exe` and related mod paths |
| **First run** | Token and SPT path dialogs can be **skipped**; configure later in Settings |
| **Localization** | Full UI language switch: **en** / **zh-CN** / **zh-TW** (Settings → Language) |

---

## Features

- Browse Forge mods (search, `@author`, categories, sort, pagination)
- Install / uninstall / enable / disable mods with a queue and 7-Zip extraction
- Local SQLite cache for faster browsing
- SPT version awareness and compatibility hints
- Installed-mod tools: update, change version, list files, edit configs
- Optional read-only Forge API token
- Single-instance launch (second start focuses the existing window)

---

## Requirements

- **Windows 10 / 11** (x64)
- An existing **SPT** install (4.1.x recommended)
  - Root folder should contain **BepInEx** and **SPT_Runtime**
  - Server binary: `SPT_Runtime\SPT.Server.exe` (legacy `SPT\` / root layouts still attempted)
- Network access to [https://sp-mod.com](https://sp-mod.com) for browsing and downloads
- Optional: Forge API token with **Read** scope — [create token](https://sp-mod.com/user/api-tokens)

---

## Download & install

1. Download the latest release from this repository’s **Releases** page.
2. Extract the archive to any folder.
3. Keep **`tools`** (7-Zip) next to `DragonDen.ModManager.exe`.
4. Run `DragonDen.ModManager.exe`.
5. On first launch you may skip token / SPT path and set them under **Settings**.
6. In **Settings → Language**, choose English, 简体中文, or 繁體中文.

**If the app still points at the old Forge domain**, delete the local config and restart:

```text
%LocalAppData%\DragonDen.ModManager\appsettings.json
