# Antigravity ACP Transparent Proxy Shim (垫片包装器)

用于解决 JetBrains 系列 IDE（如 IntelliJ IDEA）中 **Google Antigravity ACP Agent** 无法连接代理、OAuth 凭据刷新超时（`ProxyError: ('Unable to connect to proxy', SSLError)`）及 STDIO/SSE 通信问题的通用透明垫片方案。

---

## 📁 目录结构

```text
antigravity-acp-proxy/
├── README.md               # 项目使用说明文档
├── config/
│   └── proxy.conf          # 代理配置文件模板（支持纯端口/完整URL）
├── src/
│   └── wrapper.cs          # 原生 C# Win32 透明垫片源码
├── bin/
│   └── wrapper.exe         # 编译后的免运行时依赖可执行文件
└── scripts/
    ├── build.bat           # 使用系统自带 csc.exe 一键编译脚本
    └── deploy.bat          # 一键备份原程序并部署到 IDE 目标目录脚本
```

---

## 🚀 核心工作原理

1. **执行劫持与原程序保留**：
   - 将原程序改名为 `agy_acp_server_real.exe` 和 `localharness_external_real.exe`。
   - 用本垫片 `wrapper.exe` 替换为原名称 `agy_acp_server.exe` 与 `localharness_external.exe`。
2. **极简动态代理注入**：
   - 垫片启动时读取同级目录下的 `proxy.conf`。
   - 自动将代理注入至 `HTTP_PROXY`、`HTTPS_PROXY`、`ALL_PROXY`、`GRPC_PROXY`（大小写全部覆盖），并内置免除 `localhost,127.0.0.1` 环回请求。
3. **零侵入透明管道透传**：
   - 采用 Win32 API `CreateProcessW`，完全继承与透传标准输入/输出/错误句柄（`STDIN`、`STDOUT`、`STDERR`）与退出码，不干扰 ACP JSON-RPC 协议与 gRPC 交互。

---

## ⚙️ 快速使用说明

### 1. 调整代理端口
打开 `config/proxy.conf`（或目标目录下的 `proxy.conf`），只需要修改其中的**端口号**即可生效，**无需重新编译**：
```text
7892
```
> 支持格式：
> - 纯端口号：`7892`、`55595` 等（自动补全为 `http://127.0.0.1:端口`）
> - 完整 URL：`http://127.0.0.1:7892` 或 `socks5://127.0.0.1:7891`

### 2. 编译项目（如有修改代码）
直接双击运行：
```cmd
scripts\build.bat
```
*(使用 Windows 系统自带的 .NET Framework 64 位编译器 `csc.exe`，无需安装额外 SDK)*

### 3. 一键部署
双击运行：
```cmd
scripts\deploy.bat
```
会自动识别并将垫片同步部署至：
- `C:\Users\Administrator\AppData\Local\JetBrains\IntelliJIdea2026.2\acp-agents\antigravity-acp\1.3.0\`
- `D:\soft\idea-2026.2.3.win\appdata\.IntelliJIdea\system\acp-agents\antigravity-acp\1.3.0\`
- 自动备份原始程序为 `*_real.exe`，保证随时可还原。
