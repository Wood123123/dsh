# 在服务端机器上运行：查清 SPT 实际监听情况 + 本地端点测试
$out = 'C:\Users\Wood\Documents\_sptprobe.txt'
"" | Out-File $out -Encoding UTF8

function W($s) { $s | Out-File $out -Append -Encoding UTF8 }

W "=== 时间 ==="
W ("  Now    = " + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss'))
W ("  UtcNow = " + ([datetime]::UtcNow).ToString('yyyy-MM-dd HH:mm:ss'))

W ""
W "=== SPT.Server 进程 ==="
Get-Process -Name 'SPT.Server' -ErrorAction SilentlyContinue | ForEach-Object {
    W ("  PID=" + $_.Id + "  Start=" + $_.StartTime.ToString('yyyy-MM-dd HH:mm:ss') + "  Mem=" + [math]::Round($_.WorkingSet64/1MB) + "MB")
}

W ""
W "=== Get-NetTCPConnection 全部 LISTEN ==="
try {
    Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue |
        Sort-Object LocalPort |
        ForEach-Object {
            $pname = ''
            try { $pname = (Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue).ProcessName } catch {}
            W ("  " + $_.LocalAddress + ":" + $_.LocalPort + "  PID=" + $_.OwningProcess + "  " + $pname)
        }
} catch { W ("  Get-NetTCPConnection 失败: " + $_.Exception.Message) }

W ""
W "=== 用 SPT.Server 的 PID 反查它持有的监听端口 ==="
$spid = (Get-Process -Name 'SPT.Server' -ErrorAction SilentlyContinue | Select-Object -First 1).Id
if ($spid) {
    try {
        Get-NetTCPConnection -State Listen -OwningProcess $spid -ErrorAction SilentlyContinue |
            ForEach-Object { W ("  LISTEN " + $_.LocalAddress + ":" + $_.LocalPort) }
    } catch { W ("  查询失败: " + $_.Exception.Message) }
    W ("  SPT.Server PID = " + $spid)
} else { W "  没有 SPT.Server 进程" }

W ""
W "=== 本地 HTTP 端点测试 ==="
foreach ($u in @(
    'http://127.0.0.1:6969/client/server/list',
    'https://127.0.0.1:6969/client/server/list',
    'http://26.216.11.222:6969/headlesspanel',
    'https://26.216.11.222:6969/headlesspanel'
)) {
    try {
        $r = Invoke-WebRequest -Uri $u -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
        W ("  OK   " + $u + "  -> HTTP " + $r.StatusCode + "  " + $r.RawContentLength + "B")
    } catch {
        $m = $_.Exception.Message
        if ($m.Length -gt 110) { $m = $m.Substring(0, 110) }
        W ("  FAIL " + $u + "  -> " + $m)
    }
}

W ""
W "=== SPT http.json ==="
$hj = 'C:\Users\Wood\Documents\EFT-SERVER\SPT_Runtime\SPT_Data\configs\http.json'
if (Test-Path $hj) { Get-Content $hj -Raw | Out-File $out -Append -Encoding UTF8 } else { W "  找不到 http.json" }

W ""
W "=== SPT 启动参数（命令行里的端口线索）==="
try {
    Get-CimInstance Win32_Process -Filter "Name='SPT.Server.exe'" -ErrorAction SilentlyContinue |
        ForEach-Object { W ("  CMD: " + $_.CommandLine) }
} catch { W ("  失败: " + $_.Exception.Message) }
