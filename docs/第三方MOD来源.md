# 第三方 MOD 来源

本项目新增的经验倍率、敌军动态等级、关卡编队、战术预设及默认装备表功能参考 UOSquadEditor：

- 仓库：<https://github.com/thu1478/UOSquadEditor>
- 固定版本：`395732f4e1d07fec0f9d1b7c12322950a072e633`
- 主要参考：`Tools/mission_editor/src/exportMissionMod.ts`、`tacticsResolve.ts`、`resolvePchtxt.ts`、`Scripts/gen_xp_scale.py`、`Release/enemy_level_scale/`。
- 原生界面沿用本项目已有分类与控件；浏览器版复用上游 React 关卡、战术和默认装备组件，接入本项目原有分类及统一工程导出，保留 MIT 通知。
- `mission_catalog.json.gz` 是上游数据目录的离线精简快照，去除重复的已解析战术展示，并用本地亚洲版 v1.0.5 的原版表校正预设、职业技能、默认条件和装备技能。上游目录中少量已修改默认值不作为原版发行。
- 原始程序、密钥、固件和游戏完整资源不包含在发行包中。

## 上游代码许可

以下为上游 MIT 许可通知；游戏名称、角色和数据不属于该 MIT 许可。使用者需要合法拥有游戏。

MIT License

Copyright (c) 2026 Richard Nguyen

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

This license covers original source code and documentation in the upstream
repository. It does not cover Unicorn Overlord or any names, characters,
artwork, or game data belonging to Atlus, Sega, Vanillaware, and/or Nintendo.
This project is unofficial and not affiliated with those rights holders.
