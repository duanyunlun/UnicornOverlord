# 浏览器关卡编队切片

## 来源与范围

- 源码平移自 `.tools/uosquad-upstream/Tools/mission_editor`，固定提交 `395732f4e1d07fec0f9d1b7c12322950a072e633`。MIT 归属完整保留在本目录上级 `LICENSE`。
- 仅包含必要 React/TypeScript/CSS、原 package 与锁、tsconfig、纯静态 `vite.config.ts`（`base: './'`）。未复制 public 游戏数据、Python、上游开发服务器插件或服务器端点。
- 保留任务筛选、地区/等级排序、阵营、六座位交换、装备、职业战术、命名预设创建/复制/IF/排序/影响引用、默认装备、JSON/ZIP 导入导出、pchtxt 职业/装备技能预览。
- 不修改主页面或 C#。本编辑器仅导出自身工程；主站基础属性/AP/成长模块单独导出，或由父层显式合并补丁并校验冲突。

## 构建与验证

在仓库根目录执行：

```sh
npm ci --prefix web/mission --cache "$PWD/.tools/npm-cache" --ignore-scripts --no-audit --no-fund
npm run build --prefix web/mission
node --test web/mission/tests/core.test.mjs
```

验证环境为 Node 24.18.0；Node 测试直接执行 TypeScript（需要支持类型剥离的 Node）。构建产物为 `web/mission/dist/`，将其**内容**部署到站点 `mission/`。不要直接将源码 `index.html` 当作生产页面。`node_modules/` 和 `dist/` 在切片内忽略；无提交。

页面仅请求同源 `../data/mission_catalog.json.gz`，通过 `fetch` + `DecompressionStream('gzip')` 解压。静态服务器应将其作为 gzip **文件**提供，不要另外加导致自动解压的 `Content-Encoding: gzip`。需要支持 DecompressionStream 的浏览器。

目录使用原版 `class_tactics`、`equipaiset_presets`、`item_skills[{id: itemID, skill_id, if0, if1, ...}]` 重建省略的单位/引用战术。另使用 `skill_default_conditions`、`charaset_usage`。空的非零预设严格保持无战术，不回退职业默认。

## 单 iframe 协议

初始地址：`./mission/?target=asia&view=missions`。

- `target`：`asia` / `western`。默认 asia。亚洲 TitleID `010054B01AD92000`、BuildID `9C3116F0333EA157526612D17354B3755737C4F2`；欧美 TitleID `010069401ADB8000`、BuildID `C841FFE2717FF03A13990480C51DA73F091C04FA`。
- `view`：`missions` / `presets` / `classes` / `gear`。每次只显示相应面板，原 tab 栏隐藏。父站将前两项放编队、后两项放职业；应保留同一个 iframe DOM 节点，不通过重设 src 切页。
- 所有接收消息要求 `event.origin === location.origin` **且** `event.source === window.parent`；所有回复使用具体 `location.origin`，不用 `*`。
- 等待 `uo-ready {target,view}` 后加载工程或请求补丁。该消息也会在目标/视图变化后发送。

父层 → iframe：

```js
frame.contentWindow.postMessage({ type: 'uo-view', view: 'gear', target: 'asia' }, location.origin);
frame.contentWindow.postMessage({ type: 'uo-target', target: 'western' }, location.origin);
frame.contentWindow.postMessage({ type: 'uo-request-edits', requestId }, location.origin);
frame.contentWindow.postMessage({ type: 'uo-load-edits', edits, target: 'asia', requestId }, location.origin);
frame.contentWindow.postMessage({ type: 'uo-request-patch', target: 'asia', requestId }, location.origin);
```

iframe → 父层：

- `uo-edits {requestId,target,view,edits}`：当前编辑状态，供保存工程；不是补丁，也不代表已获共享装备确认。
- `uo-loaded {requestId,target,edits}` 或 `uo-loaded {requestId,error}`：复用文件导入解析和完整编辑校验；失败不替换旧状态。加载是显式恢复工程，不弹“替换当前编辑”确认。共享装备可恢复为待确认状态。
- `uo-patch {requestId,target,content,edits}` 或 `uo-patch {requestId,error,edits}`：复用严格导出校验及共享装备确认；成功的 `content` 是 pchtxt 字符串。**没有修改时 content 为 null，不追加 enginefix，也不触发下载。** 有修改时包含六条已校准的饰品修复，父层负责与普通模块统一冲突校验、去重和 ZIP 打包。错误时不可继续合包。
- `uo-navigate {view}`：面板内部“前往预设”等操作请求父层切换分类；父层应回送 `uo-view`。编辑及选择保留在同一个 iframe 内。

切 view 不丢编辑；切 target 保留编辑但清除当前 pchtxt 预览，避免跨 BuildID 展示。恢复工程同样清除预览。

## 安全与限制

- 导出拒绝空闲预设耗尽、未知/非法 ID、未解析临时引用、重复绑定、超过八行、越界 IF/flags、保留 CharaSet 0/1、相互冲突的字节写入等状态。不再靠 warning 静默略过。
- 私有**战术预设**可分配；共享**角色装备模板**不可自动克隆。任何装备导出均有显式全局影响确认，取消则报错/不导出。`class_equiptypes` 不开放且导入/导出拒绝修改。
- ZIP 复用上游原生 store 实现，并校验结构、大小、CRC、重复/不安全路径；支持 store/deflate，包括中央目录记录大小的流式 ZIP。限制 2048 项、单项解压 16 MiB、总量 64 MiB；拒绝加密、多卷、ZIP64。JSON 单独导入也保留入口。
- pchtxt 仅叠加到职业技能/学习等级/技能默认条件与装备授予技能的预览；不承诺模拟任意补丁、编队或装备表写入，也不会将外部 pchtxt 自动混入导出。要求正确 BuildID、offset_shift 0x100；尊重 enabled/disabled/stop。
- 任务默认装备显示是目录快照，编辑默认装备表不会重算运行时的所有等级/档位装备；UI 已明确说明。实际导出按原版表地址写入。
- 主要静态操作/说明已中文化，游戏名称直接使用目录提供文本，未编造译名；部分次要提示保留上游英文。用户名称始终通过 React 文本渲染，无 dangerouslySetInnerHTML。
- 构建与 Node 测试不等于游戏内验收；仍需父站集成验证及对应合法游戏版本运行验证。
