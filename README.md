<a id="chinese"></a>

[中文](#chinese) | [English](#english)

![下载量 / Downloads](https://img.shields.io/github/downloads/duanyunlun/UnicornOverloardEditor/total.svg)

# UnicornOverloardEditor

《独角兽之王》存档与 MOD 编辑器

基于 Avalonia 的跨平台 Nintendo Switch 存档与 MOD 编辑器。界面支持简体中文、English 和日本語，默认使用简体中文。

## 下载

[从 GitHub Releases 下载最新版本](https://github.com/duanyunlun/UnicornOverloardEditor/releases/latest)。正式发行包提供以下三个自包含版本，无需另外安装 .NET：

- Windows x64
- macOS Intel x64
- macOS Apple Silicon arm64

macOS 应用当前使用 ad-hoc 签名，未经过 Apple 公证。

## 功能

- 修改金币、名望和泽诺伊拉难度
- 查看及修改角色职业、等级、能力加成与亲密度
- 修改物品数量，批量补齐安全消耗道具和缺少的装备
- 使用 Shift 多选并一次添加多种物品或装备
- 导出角色、用 `.uocd` 替换角色或从 `.uocd` 新增角色
- 修改十个编队槽位
- 打开存档时自动在存档旁的 `backup` 目录创建备份
- 为亚洲中文版 v1.0.5 与欧美版 v1.05 导出 Astris 等模拟器使用的 MOD ZIP
- 编辑技能、战斗预览/计时器、开战被动限制、角色加入随机化、职业成长与技能、据点雇佣、采矿、商店、六人编队和类型克制
- 使用本地化名称选择 441 个技能、73 个职业和物品，切换条目时载入亚洲版原始值

## 从源码构建

支持 Windows、macOS 和 Linux，需要 [.NET 10 SDK](https://dotnet.microsoft.com/zh-cn/download/dotnet/10.0)。

```bash
dotnet build UnicornOverlord.slnx
dotnet run --project UnicornOverlord/UnicornOverlord.csproj
```

编辑前请先从模拟器或主机导出存档。编辑完成后，将保存的副本导回对应的存档位置。

MOD 工作区当前生成模拟器使用的 `pchtxt` 包，可选择亚洲中文版 v1.0.5 或欧美版 v1.05；Title ID 与 Build ID 随目标版本写入。导出的 ZIP 内含中文安装说明；该格式不能直接用于 Atmosphere 实机。

## 数据与安全

项目内置物品、装备、职业和角色名称映射。职业与角色的简体中文名称由 v1.0.5 中文资源验证，物品与装备类型映射来自原项目公开发行数据。

开发和逆向验证过程中使用的 ROM、存档、Keys、提取工具及中间文件均被排除在版本控制之外。详细说明见[开发与验证说明](docs/开发与验证.md)。

## 相关链接

- [游戏官网](https://unicorn-overlord.com/)
- [原项目](https://github.com/turtle-insect/UnicornOverlord)
- [存档研究讨论](https://gbatemp.net/threads/unicorn-overlord-save-editing.650584/)
- [数据表](https://docs.google.com/spreadsheets/d/1UXe4nEloKlv14P4H4cOKeJc8R2P1fZW_HaLAuQG96BQ)
- [Melisandre MOD 编辑器](https://melisand.re/)

## 致谢

- [pauljames80](https://gbatemp.net/members/pj1980.378437/)
- [GBAtemp 社区](https://gbatemp.net/)

---

<a id="english"></a>

[中文](#chinese) | [English](#english)

# UnicornOverloardEditor

Unicorn Overlord Save & MOD Editor

A cross-platform Avalonia editor for Nintendo Switch save data and MOD packages. The interface supports Simplified Chinese, English, and Japanese, with Simplified Chinese selected by default.

## Download

[Download the latest version from GitHub Releases](https://github.com/duanyunlun/UnicornOverloardEditor/releases/latest). Official releases provide the following three self-contained packages and do not require a separate .NET installation:

- Windows x64
- macOS Intel x64
- macOS Apple Silicon arm64

The macOS applications are currently ad-hoc signed and are not notarized by Apple.

## Features

- Edit Gold, Renown, and Zenoiran difficulty
- View and edit character classes, levels, stat bonuses, and Rapport
- Edit item quantities and safely add missing consumables or equipment in bulk
- Select multiple items or equipment with Shift and add them together
- Export characters, replace a character with a `.uocd` file, or add characters from `.uocd` files
- Edit all ten unit formation slots
- Automatically create a backup in the save file's adjacent `backup` directory when opening a save
- Export emulator MOD ZIP files for Asian Chinese v1.0.5 and Western v1.05
- Edit skills, battle preview and timer behavior, character recruitment randomization, class growth and skills, fort recruitment, mining, shops, six-unit formations, and class type effectiveness
- Select 441 skills, 73 classes, and items by localized name and load calibrated Asian-version defaults when switching records

## Build from Source

Windows, macOS, and Linux are supported. The [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) is required.

```bash
dotnet build UnicornOverlord.slnx
dotnet run --project UnicornOverlord/UnicornOverlord.csproj
```

Export a save from your console or emulator before editing. After editing, import the saved copy back into the corresponding save location.

The MOD workspace currently generates `pchtxt` packages for emulators such as Astris. Asian Chinese v1.0.5 and Western v1.05 are supported, and the selected Target Version controls the Title ID and Build ID written into the package. The exported ZIP includes Chinese installation instructions. This format cannot be used directly with Atmosphere on a console.

## Data and Safety

The repository includes mappings for item, equipment, class, and character names. Simplified Chinese class and character names were verified against the v1.0.5 Chinese game resources; item and equipment type mappings originate from the original project's public release data.

ROMs, save files, keys, extraction tools, and intermediate files used during development and reverse-engineering validation are excluded from version control. See the [development and validation notes](docs/开发与验证.md) for details.

## Related Links

- [Official game website](https://unicorn-overlord.com/)
- [Original project](https://github.com/turtle-insect/UnicornOverlord)
- [Save-editing research thread](https://gbatemp.net/threads/unicorn-overlord-save-editing.650584/)
- [Data spreadsheet](https://docs.google.com/spreadsheets/d/1UXe4nEloKlv14P4H4cOKeJc8R2P1fZW_HaLAuQG96BQ)
- [Melisandre MOD Editor](https://melisand.re/)

## Credits

- [pauljames80](https://gbatemp.net/members/pj1980.378437/)
- [GBAtemp community](https://gbatemp.net/)
