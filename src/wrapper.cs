using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace AntigravityAcpProxy
{
    class Program
    {
        [StructLayout(LayoutKind.Sequential)]
        struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr GetCommandLineW();

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CreateProcessW(
            string lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetStdHandle(int nStdHandle);

        const int STD_INPUT_HANDLE = -10;
        const int STD_OUTPUT_HANDLE = -11;
        const int STD_ERROR_HANDLE = -12;
        const uint INFINITE = 0xFFFFFFFF;
        const int STARTF_USESTDHANDLES = 0x00000100;

        /// <summary>
        /// 统一设置常用 HTTP/HTTPS/SOCKS5/gRPC 等环境变量
        /// </summary>
        static void SetProxyValue(string proxy)
        {
            if (string.IsNullOrEmpty(proxy)) return;

            // 智能支持纯端口号输入（如 7892 或 55595）
            int port;
            if (int.TryParse(proxy, out port))
            {
                proxy = "http://127.0.0.1:" + port;
            }

            Environment.SetEnvironmentVariable("HTTP_PROXY", proxy);
            Environment.SetEnvironmentVariable("http_proxy", proxy);
            Environment.SetEnvironmentVariable("HTTPS_PROXY", proxy);
            Environment.SetEnvironmentVariable("https_proxy", proxy);
            Environment.SetEnvironmentVariable("ALL_PROXY", proxy);
            Environment.SetEnvironmentVariable("all_proxy", proxy);
            Environment.SetEnvironmentVariable("GRPC_PROXY", proxy);
            Environment.SetEnvironmentVariable("grpc_proxy", proxy);
        }

        /// <summary>
        /// 从配置文件读取配置，支持单行端口号、KEY=VALUE 格式以及注释行
        /// </summary>
        static void LoadProxyConfig(string configPath)
        {
            if (!File.Exists(configPath)) return;

            try
            {
                string[] lines = File.ReadAllLines(configPath);
                foreach (string rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith(";"))
                        continue;

                    int sep = line.IndexOf('=');
                    if (sep == -1) sep = line.IndexOf(':');

                    if (sep > 0)
                    {
                        string key = line.Substring(0, sep).Trim().ToUpperInvariant();
                        string val = line.Substring(sep + 1).Trim();

                        if (key == "PROXY" || key == "HTTP_PROXY" || key == "ALL_PROXY")
                        {
                            SetProxyValue(val);
                        }
                        else if (key == "NO_PROXY")
                        {
                            Environment.SetEnvironmentVariable("NO_PROXY", val);
                            Environment.SetEnvironmentVariable("no_proxy", val);
                        }
                        else
                        {
                            Environment.SetEnvironmentVariable(key, val);
                            Environment.SetEnvironmentVariable(key.ToLowerInvariant(), val);
                        }
                    }
                    else
                    {
                        // 极简单行模式（例如只写 7892 或 http://127.0.0.1:7892）
                        SetProxyValue(line);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[ACP-Wrapper] Config load warning: " + ex.Message);
            }
        }

        static int Main()
        {
            string currentExe = Process.GetCurrentProcess().MainModule.FileName;
            string dir = Path.GetDirectoryName(currentExe);
            string baseName = Path.GetFileNameWithoutExtension(currentExe);
            string realExe = Path.Combine(dir, baseName + "_real.exe");

            // 1. 本地回环地址默认直接放行（不走代理）
            Environment.SetEnvironmentVariable("NO_PROXY", "localhost,127.0.0.1");
            Environment.SetEnvironmentVariable("no_proxy", "localhost,127.0.0.1");

            // 2. 默认备用代理（默认 7892）
            SetProxyValue("http://127.0.0.1:7892");

            // 3. 读取目录下的 proxy.conf 覆盖
            LoadProxyConfig(Path.Combine(dir, "proxy.conf"));

            if (!File.Exists(realExe))
            {
                Console.Error.WriteLine("[ACP-Wrapper] Target real binary not found: " + realExe);
                return 1;
            }

            // 4. 重建完整命令行参数
            string rawCommandLine = Marshal.PtrToStringUni(GetCommandLineW());
            string targetCommandLine;

            int firstArgIndex = 0;
            if (!string.IsNullOrEmpty(rawCommandLine))
            {
                rawCommandLine = rawCommandLine.TrimStart();
                if (rawCommandLine.StartsWith("\""))
                {
                    int nextQuote = rawCommandLine.IndexOf('\"', 1);
                    firstArgIndex = (nextQuote != -1) ? nextQuote + 1 : rawCommandLine.Length;
                }
                else
                {
                    int nextSpace = rawCommandLine.IndexOf(' ');
                    firstArgIndex = (nextSpace != -1) ? nextSpace : rawCommandLine.Length;
                }
                string remainingArgs = rawCommandLine.Substring(firstArgIndex).TrimStart();
                targetCommandLine = "\"" + realExe + "\" " + remainingArgs;
            }
            else
            {
                targetCommandLine = "\"" + realExe + "\"";
            }

            // 5. 设置标准输入输出句柄继承
            STARTUPINFO si = new STARTUPINFO();
            si.cb = Marshal.SizeOf(si);
            si.dwFlags = STARTF_USESTDHANDLES;
            si.hStdInput = GetStdHandle(STD_INPUT_HANDLE);
            si.hStdOutput = GetStdHandle(STD_OUTPUT_HANDLE);
            si.hStdError = GetStdHandle(STD_ERROR_HANDLE);

            PROCESS_INFORMATION pi = new PROCESS_INFORMATION();

            bool success = CreateProcessW(
                null,
                targetCommandLine,
                IntPtr.Zero,
                IntPtr.Zero,
                true,
                0,
                IntPtr.Zero,
                null,
                ref si,
                out pi
            );

            if (!success)
            {
                int err = Marshal.GetLastWin32Error();
                Console.Error.WriteLine("[ACP-Wrapper] Failed to start: " + realExe + " (" + err + ")");
                return 1;
            }

            WaitForSingleObject(pi.hProcess, INFINITE);

            uint exitCode = 0;
            GetExitCodeProcess(pi.hProcess, out exitCode);

            CloseHandle(pi.hProcess);
            CloseHandle(pi.hThread);

            return (int)exitCode;
        }
    }
}
