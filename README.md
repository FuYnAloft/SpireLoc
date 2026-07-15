# SpireLoc

SpireLoc 是面向《杀戮尖塔 2》（Slay the Spire 2）模组本地化的命令行处理工具。它可以在适合人工维护的结构化文件与游戏使用的 JSON 文件之间转换，并通过可组合的 pipeline 处理 Model ID 及模组库的兼容格式。

当前项目处于 preview 阶段。CLI、action 格式和 operation 名称仍可能调整。

## 安装

SpireLoc 是基于 .NET 9 的 NuGet Tool。使用前需要安装 [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)。

全局安装：

```pwsh
dotnet tool install --global SpireLoc --prerelease
```

升级与卸载：

```pwsh
dotnet tool update --global SpireLoc --prerelease
dotnet tool uninstall --global SpireLoc
```

也可以通过 tool manifest 将它安装为仓库内的 local tool：

```pwsh
dotnet new tool-manifest
dotnet tool install SpireLoc --prerelease
dotnet tool run spireloc --help
```

## 快速开始

SpireLoc 自带 BaseLib 与 RitsuLib action。它们默认从 YAML 源目录读取，并向游戏本地化目录写入 flat JSON。

BaseLib：

```pwsh
# 源文件 -> 游戏文件
spireloc baselib TestMod ./Tools/localization ./TestMod/localization

# 游戏文件 -> 源文件
spireloc baselib TestMod ./Tools/localization ./TestMod/localization --reversed
```

RitsuLib：

```pwsh
# 源文件 -> 游戏文件
spireloc ritsulib TestMod ./Tools/localization ./TestMod/localization

# 游戏文件 -> 源文件
spireloc ritsulib TestMod ./Tools/localization ./TestMod/localization --reversed
```

这里三个位置参数依次是 BaseLib 的顶层命名空间（或 RitsuLib 的 Mod ID）、源本地化目录、游戏本地化目录。完整选项可通过以下命令查看：

```pwsh
spireloc action help baselib
spireloc action help ritsulib
spireloc action list
```

## Pipeline

`pipe` 命令按照参数中的先后顺序构造并执行 operation：

```pwsh
spireloc pipe `
  --input yaml ./Tools/localization `
  --model-id ritsulib TestMod `
  --output flat-json ./TestMod/localization
```

反向处理时，`--reversed` 属于其前面的 `--model-id ritsulib` operation：

```pwsh
spireloc pipe `
  --input flat-json ./TestMod/localization `
  --model-id ritsulib TestMod --reversed `
  --output yaml ./Tools/localization
```

使用下面的命令浏览现有 operation 及其参数：

```pwsh
spireloc operation list
spireloc operation help input
spireloc operation help model-id ritsulib
```

需要使用自定义 Model ID 前缀时，可以列出要处理的表；`table:index` 使用从 `0` 开始的 key segment 索引，省略 `:index` 时默认为 `0`。例如 `cards:1` 处理 key 的第二段：

```pwsh
spireloc pipe `
  --input yaml ./Tools/localization `
  --model-id prefix TEST_ cards ancients:2 `
  --output flat-json ./TestMod/localization
```

反向转换使用同一组 prefix 和表规则，并添加 `--reversed`。

## Action

Action 是可带参数、可组合的 YAML pipeline。它会在执行前展开为普通 operation，因此也可以通过 `--action` 嵌入另一条 pipeline。

下面的 `convert.yaml` 接受一个位置参数 `mod-id`：

```yaml
version: 1
description: Convert this mod's source localization to game JSON.

parameters:
  mod-id:
    type: string
    position: 0
    description: RitsuLib mod ID.

steps:
  - input:
      kind: yaml
      path: $(ActionDir)/localization

  - model-id:
      kind: ritsulib
      mod-id: $(mod-id)

  - output:
      kind: flat-json
      path: $(ActionDir)/output
```

直接运行或嵌入 pipeline：

```pwsh
spireloc action run ./convert.yaml TestMod

spireloc pipe `
  --action ./convert.yaml TestMod
```

Action 还支持 `uses`、`with`、默认参数、布尔 flag、模板变量以及展开期的 `if` 条件。

## 文件布局与格式

所有目录型输入输出采用相同布局：

```text
localization/
├─ eng/
│  └─ cards.yaml
└─ zhs/
   ├─ cards.yaml
   └─ relics.yaml
```

目录的第一级是语言 ID，第二级文件名（不含扩展名）是本地化表名。当前支持：

- `yaml`：嵌套 YAML mapping，叶子必须是字符串，扩展名为 `.yaml`。
- `toml`：嵌套 TOML table，叶子必须是字符串，扩展名为 `.toml`。
- `nested-json`：嵌套 JSON object，叶子必须是字符串，扩展名为 `.json`。
- `flat-json`：游戏侧使用的扁平 JSON object；结构化 key 以 `.` 连接，扩展名为 `.json`。

## 当前能力

- YAML、TOML、nested JSON 与 flat JSON 的目录化读取和写入。
- 原版游戏、BaseLib、RitsuLib 的 Model ID 双向转换。
- 可按 `table[:keyIndex]` 选择表和 key 位置的自定义 Model ID prefix 双向转换。
- MinionLib component 本地化在独立 `components` 表与游戏侧 `cards` 表之间的双向兼容转换。
- RitsuLib ModelCapability 本地化表与 `cards` 表之间的拆分和合并。
- 参数化 YAML action、嵌套 `uses`、条件展开，以及随工具发布的内置 action。

## 开发

```pwsh
dotnet build SpireLoc.sln
dotnet test SpireLoc.sln
```

为 Core 添加可供 CLI 使用的 operation 时，请阅读 [OperationFactory 注册指南](https://github.com/FuYnAloft/SpireLoc/blob/main/docs/internal/operation-factory-registration.md)。

## 许可证与声明

SpireLoc 根据 [MIT License](https://github.com/FuYnAloft/SpireLoc/blob/main/LICENSE) 发布。

本项目不是 Mega Crit 的官方项目，与 Mega Crit 不存在隶属、合作或认可关系。《杀戮尖塔 2》、Slay the Spire 2 及相关名称和商标归其各自权利人所有。
