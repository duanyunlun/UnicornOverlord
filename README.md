![下载量](https://img.shields.io/github/downloads/duanyunlun/UnicornOverlord/total.svg)

# 独角兽之王存档与 MOD 编辑器

基于 Avalonia 的跨平台《独角兽之王》Nintendo Switch 存档与 MOD 编辑器，界面默认使用简体中文。

## 功能

- 修改金币、名望和泽诺伊拉难度
- 查看及修改角色职业、等级、能力加成与亲密度
- 修改物品数量，批量补齐安全消耗道具和缺少的装备
- 使用 Shift 多选并一次添加多种物品或装备
- 导出角色、用 `.uocd` 替换角色或从 `.uocd` 新增角色
- 修改十个编队槽位
- 打开存档时自动在存档旁的 `backup` 目录创建备份
- 为亚洲中文版 v1.0.5 导出 Astris 等模拟器使用的 MOD ZIP
- 当前支持冻结战斗计时器与职业成长保底，并展示后续 MOD 编辑板块的接入状态

## 运行环境

- Windows、macOS 或 Linux
- [.NET 10 SDK 或运行时](https://dotnet.microsoft.com/zh-cn/download/dotnet/10.0)

## 构建与运行

```bash
dotnet build UnicornOverlord.slnx
dotnet run --project UnicornOverlord/UnicornOverlord.csproj
```

编辑前请先从模拟器或主机导出存档。编辑完成后，将保存的副本导回对应的存档位置。

MOD 工作区当前生成模拟器使用的 `pchtxt` 包，目标 Title ID 为 `010054B01AD92000`，Build ID 为 `9C3116F0333EA157526612D17354B3755737C4F2`。导出的 ZIP 内含中文安装说明；该格式不能直接用于 Atmosphere 实机。

## 数据与安全

项目内置物品、装备、职业和角色名称映射。职业与角色的简体中文名称由 v1.0.5 中文资源验证，物品与装备类型映射来自原项目公开发行数据。

开发和逆向验证过程中使用的 ROM、存档、Keys、提取工具及中间文件均被排除在版本控制之外。详细说明见 [开发与验证说明](docs/开发与验证.md)。

## 相关链接

- [游戏官网](https://unicorn-overlord.com/)
- [原项目](https://github.com/turtle-insect/UnicornOverlord)
- [存档研究讨论](https://gbatemp.net/threads/unicorn-overlord-save-editing.650584/)
- [数据表](https://docs.google.com/spreadsheets/d/1UXe4nEloKlv14P4H4cOKeJc8R2P1fZW_HaLAuQG96BQ)
- [Melisandre MOD 编辑器](https://melisand.re/)

## 致谢

- [pauljames80](https://gbatemp.net/members/pj1980.378437/)
- [GBAtemp 社区](https://gbatemp.net/)
