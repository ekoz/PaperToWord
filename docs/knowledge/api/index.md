# API 接口清单

> 生成时间：2026-11-19
> 信息来源：代码扫描（准确性依赖注解完整性）；本仓库不存在 OpenAPI / Swagger / .proto / .thrift / WSDL 契约文件
> 接口总数：0 个（HTTP REST: 0，Thrift: 0，gRPC: 0，Dubbo: 0）

## 结论

**未检测到对外网络接口。**

PaperToWord 是一个 C# / .NET Framework 4.x 的 Windows WinForms 桌面工具，唯一产物是可双击运行的 `PaperToWord.exe`。程序只在本地进程中完成"读取图片 → 裁切/缩放 → 生成 .docx 文件"，不监听端口、不发起 HTTP/RPC 调用，也不注册任何服务端接口。

本文件中的"接口"计数为 0。`desktop-entry-api.md` 仅记录桌面程序的非网络入口（命令行参数、文件拖放、进程内公共契约），用于区分程序边界，**不得当作 HTTP/RPC 对外 API 使用**。

## 判断依据

### 扫描过的路径

| 路径 | 内容 | 结果 |
| :--- | :--- | :--- |
| `Source/` | Program.cs、Core.cs、MainForm.cs、NatSort.cs、Ooxml.cs、Zip.cs（共 6 个 .cs 文件，全部逐一通读） | 无网络接口定义 |
| `dist/使用说明.txt` | 用户使用说明 | 明确说明"无需联网"，仅本机 GUI / 命令行批处理 |
| 仓库根目录顶层 | 仅有 `.helixharness/`、`dist/`、`docs/`、`Source/` 四个目录，无根级文件 | 无 `*.proto`、`*.thrift`、OpenAPI/Swagger（`*.yaml`/`*.json`）、`*.wsdl` 契约文件 |

### 符号级证据

- 全部源码的 `using` 仅涉及：`System`、`System.Collections.Generic`、`System.Drawing`、`System.Drawing.Imaging`、`System.Globalization`、`System.IO`、`System.Runtime.InteropServices`、`System.Text`、`System.Threading`、`System.Windows.Forms`。
- 未出现 `System.Net.*`、`HttpClient`、`WebClient`、`WebRequest`、`HttpListener`、`Socket`、`TcpListener`、`System.ServiceModel`（WCF）、gRPC、Thrift、Dubbo 等任何网络/远程调用符号。
- 唯一的非托管调用（P/Invoke）为 `kernel32.dll` 的 `AttachConsole` / `GetConsoleWindow` 与 `user32.dll` 的 `MessageBeep`，用途是命令行模式挂接控制台和完成提示音，不涉及网络。
- 唯一进程入口 `Program.Main(string[] args)` 只有两条分支：命令行模式 `RunCli`，或 `System.Windows.Forms.Application.Run(new MainForm())` 启动桌面窗口。
- 项目无 Web 宿主（无 ASP.NET、Kestrel、IIS 集成）、无 `@RestController`/`@RequestMapping` 等注解（非 Java 技术栈）、无 IDL 文件，因此 HTTP REST、Thrift RPC、gRPC、Dubbo 四类扫描规则均无命中。

### 各协议扫描结果

| 协议 | 扫描规则 | 命中数 | 说明 |
| :--- | :--- | :--- | :--- |
| HTTP REST | `@RestController` / `@Controller` 及映射注解 | 0 | 非 Java / Spring 项目，无 Web 控制器 |
| Thrift RPC | `*.thrift` 或 `XxxHandler implements Xxx.Iface` | 0 | 无 IDL、无 Thrift 依赖 |
| gRPC | `*Grpc.*ImplBase` 或 `*.proto` | 0 | 无 proto 文件、无 gRPC 依赖 |
| Dubbo | `@DubboService` / Dubbo `@Service` | 0 | 非 Java 项目，无 Dubbo 依赖 |

## 模块索引

| 序号 | 模块 | 协议 | 接口数 | 详情文件 |
| :--- | :--- | :--- | :--- | :--- |
| — | 桌面程序入口（非网络，不计入 API 总数） | 进程内 CLI / WinForms 事件 / 本地文件 I/O | 0 | [desktop-entry-api.md](./desktop-entry-api.md) |

## 索引建议

建议根 `docs/knowledge/index.md` 在"API 接口"条目下收录本文件，并注明：本仓库无对外网络接口，`api/desktop-entry-api.md` 仅为桌面程序入口参考。
