# 在 Core 中注册 OperationFactory

本文面向在 `SpireLoc.Core` 中添加 operation 的开发者，说明如何让 CLI 通过反射发现并构造 operation。

## 基本概念

CLI 启动时会扫描 Core 程序集中的 `OperationFactoryAttribute`。Attribute 中的路径决定 pipe 语法：

```csharp
[OperationFactory("model-id", "ritsulib")]
```

对应：

```powershell
spireloc pipe --model-id ritsulib ...
```

路径的第一段是带 `--` 的步骤头，后续段是不带 `--` 的子指令。Action YAML 和 JSON 使用相同路径，其中第一段是 step key，后续段放在 `kind`：

```yaml
steps:
  - model-id:
      kind: ritsulib
      mod-id: TestMod
```

应为 factory 和参数填写面向 CLI 用户的 `Description`。这些文本会出现在：

```powershell
spireloc operation list
spireloc operation help model-id ritsulib
```

## 支持的四种注册方式

### 返回 ILocOperation 的静态方法

```csharp
public static class ExampleOperationFactories
{
    [OperationFactory("example", "copy", Description = "Copy one workspace slot to another.")]
    public static ILocOperation CreateCopy(
        [OperationParameter("from", 0, Description = "Source workspace slot.")]
        string fromSlot,
        [OperationParameter("to", 1, Description = "Destination workspace slot.")]
        string toSlot) =>
        new CopyOperation(fromSlot, toSlot);
}
```

Factory 方法必须是静态方法，返回类型必须实现 `ILocOperation`。

### ILocOperation 实现类的构造函数

对于主构造函数，使用 `method:` 指定 attribute 的目标：

```csharp
[method: OperationFactory(
    "input",
    "yaml",
    Description = "Read a directory of nested YAML localization files.")]
public sealed class ReadYamlOperation(
    [OperationParameter("path", 0, Description = "Root localization directory.")]
    string rootPath,
    [OperationParameter("to", Description = "Destination workspace slot.")]
    string toSlot = "main") : ILocOperation
{
    public LocOperationResult Execute(LocWorkspace workspace, LocExecutionContext context)
    {
        throw new NotImplementedException();
    }
}
```

普通构造函数也可以直接标记：

```csharp
public sealed class ExampleOperation : ILocOperation
{
    [OperationFactory("example")]
    public ExampleOperation([OperationParameter("value", 0)] string value)
    {
        // ...
    }

    public LocOperationResult Execute(LocWorkspace workspace, LocExecutionContext context) =>
        throw new NotImplementedException();
}
```

### 返回 UnaryLocBundleProcessor 的静态方法

```csharp
public static class ModelIdProcessorFactories
{
    [OperationFactory(
        "model-id",
        "ritsulib",
        Description = "Convert model IDs using RitsuLib conventions.")]
    public static UnaryLocBundleProcessor CreateRitsuLib(
        [OperationParameter("mod-id", 0, Description = "Mod ID used by RitsuLib.")]
        string modId,
        [OperationParameter(
            "reversed",
            IsFlag = true,
            Description = "Convert game localization back to source form.")]
        bool reversed = false) =>
        new RitsuLibModelIdProcessor(
            reversed ? ModelIdDirection.ToSource : ModelIdDirection.ToGame,
            modId);
}
```

CLI 会自动把 processor 包装为 `UnaryLocBundleProcessorStep`，并注入两个具名参数：

```text
--from <slot>    默认 main
--to <slot>      默认 main
```

因此，返回 `UnaryLocBundleProcessor` 的 factory 不能自行声明名为 `from` 或 `to` 的参数。

### UnaryLocBundleProcessor 派生类的构造函数

构造函数也可以直接注册，规则与上面相同：

```csharp
[method: OperationFactory("example", "transform", Description = "Transform localization entries.")]
public sealed class ExampleProcessor(
    [OperationParameter("prefix", 0, Description = "Prefix added to selected IDs.")]
    string prefix) : UnaryLocBundleProcessor
{
    public override LocBundle Process(LocBundle bundle, DiagnosticCollection? diagnostics = null) =>
        bundle;
}
```

## 参数绑定

`OperationParameterAttribute` 的主要属性如下：

| 属性 | 含义 |
| --- | --- |
| `Name` | CLI 和 Action 文件中的参数名。省略时由 C# 参数名自动转换为 kebab-case。 |
| `Position` | 位置参数编号；默认 `-1`，表示只能按名称传入。 |
| `IsFlag` | 将 bool 参数注册为无值开关。 |
| `Description` | `operation help` 中显示的参数说明。 |

### 具名参数

没有位置编号的参数使用 `--name value`：

```csharp
[OperationParameter("to", Description = "Destination workspace slot.")]
string toSlot = "main"
```

```powershell
--to output
```

若省略 `OperationParameterAttribute`，参数名会自动推断。例如 `namespaceTop` 会变为 `--namespace-top`。不过建议为面向用户的参数显式填写名称和描述。

### 位置参数

```csharp
[OperationParameter("mod-id", 0, Description = "Mod identifier.")]
string modId
```

以下两种写法等价：

```powershell
--model-id ritsulib TestMod
--model-id ritsulib --mod-id TestMod
```

位置编号必须从 `0` 开始连续排列。可选位置参数之后不能出现必填位置参数。

### Flag

```csharp
[OperationParameter("reversed", IsFlag = true, Description = "Reverse the conversion.")]
bool reversed = false
```

调用时只写参数名：

```powershell
--reversed
```

Flag 必须是 `bool`，不能同时是位置参数或列表参数。未提供 flag 时绑定为 `false`。

### 默认值

C# 可选参数的默认值会成为 CLI 默认值：

```csharp
[OperationParameter("to")]
string toSlot = "main"
```

没有 C# 默认值且不是 flag 的参数为必填参数。

### 列表参数

列表只支持以下精确类型：

```csharp
IReadOnlyList<string>
IReadOnlyList<int>
```

具名列表在 CLI 中通过重复参数传入：

```csharp
[OperationParameter("from", Description = "Source slots in merge order.")]
IReadOnlyList<string> fromSlots
```

```powershell
--from a --from b --from c
```

Action YAML 使用序列，JSON 使用数组：

```yaml
- merge:
    from: [a, b, c]
```

列表也可以是位置参数，但必须是最后一个位置参数。它会接收当前 operation 中剩余的所有位置值，直到遇到下一个步骤头：

```csharp
[OperationParameter("from", 0)]
IReadOnlyList<string> fromSlots
```

```powershell
--merge a b c --output yaml ./localization
```

列表不能注册为 flag。不要使用数组、`List<T>` 或 C# `params` 替代 `IReadOnlyList<T>`。

## 路径和命名限制

- Factory 路径不能为空，各段不能为空、不能包含空白，也不能以 `-` 开头。
- 完整 factory 路径在所有被扫描的程序集中必须唯一。
- `action` 是保留的步骤头，不能用于 operation。
- `kind` 是 Action 文件的保留参数名，不能作为 operation 参数名。
- 参数名不能与任何已注册的步骤头相同。
- 同一个 factory 内不能出现重复参数名。
- `ref`、`out` 和 C# `params` 参数不受支持。
- 对于 `UnaryLocBundleProcessor` factory，`from` 和 `to` 由 CLI 保留并自动注入。

## 建议的实现边界

Factory 应只负责验证构造参数和创建 operation/processor。实际的 workspace 读取、文件 IO、转换和 diagnostic 报告应放在 `ILocOperation.Execute` 或 `UnaryLocBundleProcessor.Process` 中。

若 factory 构造失败，CLI 会把异常转换为“无法创建步骤”的错误；operation 执行期间的可恢复问题应通过 Core 的 diagnostic 模型报告。

## 自检

添加 factory 后至少执行：

```powershell
dotnet build SpireLoc.sln
dotnet test SpireLoc.sln
dotnet run --project SpireLoc.Cli -- operation list
dotnet run --project SpireLoc.Cli -- operation help <path...>
```

建议为以下行为添加 CLI 测试：

- Registry 能发现 factory，且路径和描述正确。
- CLI 位置参数、具名参数、flag 和默认值能正确绑定。
- 若使用列表，验证重复具名参数或末尾位置参数的顺序。
- Factory 能构造预期的 `ILocOperation`，或者能被正确包装为 `UnaryLocBundleProcessorStep`。
