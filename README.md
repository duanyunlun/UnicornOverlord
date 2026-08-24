![下载量](https://img.shields.io/github/downloads/duanyunlun/UnicornOverlord/total.svg)

# 《圣兽之王》存档编辑器与网站翻译脚本

本仓库包含基于 Avalonia 的跨平台《圣兽之王》Nintendo Switch 存档编辑器，以及适用于 melisand.re 的 Tampermonkey 简体中文翻译脚本。README 与程序界面默认使用简体中文。

## 功能

- 修改金币、名望和泽诺伊拉难度
- 查看及修改角色职业、等级、能力加成与亲密度
- 修改或添加物品和装备
- 导入、导出及插入角色数据
- 修改十个编队槽位
- 打开存档时自动在存档旁的 `backup` 目录创建备份

## 网站中文翻译脚本

仓库同时提供适用于 Tampermonkey 的 [melisand.re 简体中文脚本](scripts/melisandre-zh-cn.user.js)，用于持续翻译 [melisand.re](https://melisand.re/) 的 Mod 配置界面。

### 安装与更新

1. 在浏览器中安装 Tampermonkey。
2. 打开[脚本安装地址](https://raw.githubusercontent.com/duanyunlun/UnicornOverlord/main/scripts/melisandre-zh-cn.user.js)，由 Tampermonkey 确认安装。
3. 重新打开或刷新 [melisand.re](https://melisand.re/)。脚本只匹配 `https://melisand.re/*`。

需要更新时，再次打开脚本安装地址并确认更新即可。也可以在 Tampermonkey 管理面板中打开脚本，将仓库内的最新内容覆盖后保存。

### 显示与布局

- 默认同时显示中文译文和英文原文；短标签采用“中文 / English”，长说明分段显示。
- 在 Tampermonkey 的脚本菜单中可切换“仅中文”或“中英双语”，切换后页面会刷新一次并保存选择。
- 桌面端主内容区最大宽度为 900px；技能和职业的搜索、筛选及排序控件会排列在同一行。
- 低于 1024px 的窄屏继续使用网站原有的自动换行布局。

### 翻译与性能

- 不调用在线翻译 API。技能、职业、物品、区域、堡垒和城镇名称优先采用游戏 v1.0.5 简体中文资源中的正式文本。
- 支持网站动态更新的列表、下拉框和展开内容，切换项目后会继续翻译，不需要手动重新运行脚本。
- 使用 DOM 变更监听、动画帧合并和节点缓存进行匹配，不使用定时轮询，也不需要在本机运行 Node.js 服务。
- 脚本本身不会读取 ROM、存档、Keys 或 Firmware，也不会把待翻译文本发送给第三方翻译服务。

网站结构发生变化时，个别新界面可能需要同步更新选择器或固定词典。遇到未翻译、误翻译或布局异常时，请附上原始英文和界面截图提交 Issue。

## 运行环境

- Windows、macOS 或 Linux
- [.NET 10 SDK 或运行时](https://dotnet.microsoft.com/zh-cn/download/dotnet/10.0)

## 构建与运行

```bash
dotnet build UnicornOverlord.slnx
dotnet run --project UnicornOverlord/UnicornOverlord.csproj
```

编辑前请先从模拟器或主机导出存档。编辑完成后，将保存的副本导回对应的存档位置。

## 数据与安全

项目内置物品、装备、职业和角色名称映射。职业与角色的简体中文名称由 v1.0.5 中文资源验证，物品与装备类型映射来自原项目公开发行数据。

开发和逆向验证过程中使用的 ROM、存档、Keys、提取工具及中间文件均被排除在版本控制之外。详细说明见 [开发与验证说明](docs/开发与验证.md)。

## 相关链接

- [游戏官网](https://unicorn-overlord.com/)
- [原项目](https://github.com/turtle-insect/UnicornOverlord)
- [存档研究讨论](https://gbatemp.net/threads/unicorn-overlord-save-editing.650584/)
- [数据表](https://docs.google.com/spreadsheets/d/1UXe4nEloKlv14P4H4cOKeJc8R2P1fZW_HaLAuQG96BQ)

## 致谢

- [pauljames80](https://gbatemp.net/members/pj1980.378437/)
- [GBAtemp 社区](https://gbatemp.net/)
