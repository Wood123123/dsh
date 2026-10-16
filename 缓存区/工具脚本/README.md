# 工具脚本

> 复用性脚本。**只读参考，不是自动化流程的一部分。**
> 上级索引：`..\..\_索引.md`

## 脚本

| 脚本 | 用途 |
|---|---|
| `syscheck.ps1` | 系统/环境检查（磁盘、进程、路径可达性） |
| `_cecil_dump.ps1` | 用 Mono.Cecil 导出程序集类型/方法清单 |
| `_idgen_dump.ps1` | 导出 ID 生成器相关信息 |
| `_perf_split.ps1` | 性能数据切分统计 |
| `_scan.ps1` | 通用扫描（文件/字符串） |
| `_sptprobe.ps1` | SPT 服务端探测 |
| `_questfix\` | 任务库修复脚本 |

## 环境注意

```
★ 本机 shell 是 Windows PowerShell 5.1（不是 pwsh 7）
   · 不支持 ?. 等新语法
   · .ps1 存盘要 UTF-8 带 BOM，否则中文乱码
★ 用 .NET 正则时 \s 包含 \n
   ⇒ 要按行匹配用 [ \t] 而不是 \s
★ 读被占用的日志要用 FileShare.ReadWrite
★ ConvertFrom-Json 遇到大小写重复的键会失败
```

## 常用片段

**读被占用的日志：**
```powershell
$fs = New-Object System.IO.FileStream($path,[System.IO.FileMode]::Open,[System.IO.FileAccess]::Read,([System.IO.FileShare]::ReadWrite -bor [System.IO.FileShare]::Delete))
$sr = New-Object System.IO.StreamReader($fs); $t = $sr.ReadToEnd(); $sr.Close(); $fs.Close()
```

**远程执行（WinRM 被封时走 DCOM/WMI）：**
```powershell
$opt = New-CimSessionOption -Protocol Dcom
$sess = New-CimSession -ComputerName 'Xtzj-20200101vo' -SessionOption $opt
Invoke-CimMethod -CimSession $sess -ClassName Win32_Process -MethodName Create -Arguments @{CommandLine='cmd.exe /c ...';CurrentDirectory='C:\Users\Wood'}
```