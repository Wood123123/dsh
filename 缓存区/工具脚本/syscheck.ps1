$ErrorActionPreference = 'SilentlyContinue'
$ProgressPreference = 'SilentlyContinue'

function H($t) { Write-Output ""; Write-Output ("=== " + $t + " ===") }

H "1. 操作系统"
Get-CimInstance Win32_OperatingSystem |
    Select-Object Caption, Version, BuildNumber, OSArchitecture, InstallDate, LastBootUpTime |
    Format-List

H "2. 主机 / 机型"
Get-CimInstance Win32_ComputerSystem |
    Select-Object Manufacturer, Model, SystemFamily, SystemType, TotalPhysicalMemory, NumberOfProcessors, NumberOfLogicalProcessors |
    Format-List
Get-CimInstance Win32_SystemEnclosure |
    Select-Object ChassisTypes, SMBIOSAssetTag |
    Format-List

H "3. BIOS / UEFI"
Get-CimInstance Win32_BIOS |
    Select-Object Manufacturer, Name, SMBIOSBIOSVersion, Version, ReleaseDate |
    Format-List
$sb = Confirm-SecureBootUEFI 2>$null
Write-Output ("SecureBootEnabled : " + $(if ($null -eq $sb) { "无法查询（可能为 Legacy BIOS）" } else { $sb }))
$fw = $env:firmware_type
Write-Output ("FirmwareType      : " + $(if ($fw) { $fw } else { "未知" }))
$pt = Get-CimInstance -Namespace root\cimv2 -ClassName Win32_DiskPartition |
    Where-Object { $_.Type -match 'GPT|System' } |
    Select-Object -First 3 DiskIndex, Name, Type
$pt | Format-Table -AutoSize

H "4. 主板"
Get-CimInstance Win32_BaseBoard |
    Select-Object Manufacturer, Product, Version, SerialNumber |
    Format-List

H "5. 显卡"
Get-CimInstance Win32_VideoController |
    Select-Object Name, VideoProcessor, DriverVersion, DriverDate, CurrentHorizontalResolution, CurrentVerticalResolution, CurrentRefreshRate, Status, PNPDeviceID |
    Format-List

H "6. 显示器（型号/序列号）"
Get-CimInstance -Namespace root\wmi -ClassName WmiMonitorID | ForEach-Object {
    [PSCustomObject]@{
        Mfg    = (-join [char[]]($_.ManufacturerName  | Where-Object { $_ -ne 0 })).Trim()
        Name   = (-join [char[]]($_.UserFriendlyName  | Where-Object { $_ -ne 0 })).Trim()
        Serial = (-join [char[]]($_.SerialNumberID    | Where-Object { $_ -ne 0 })).Trim()
        Year   = $_.YearOfManufacture
        Active = $_.Active
    }
} | Format-List

H "7. 显示器实际连接接口  <-- 本次重点"
$techMap = @{
    '-2' = 'Uninitialized'; '-1' = 'Other'
    '0' = 'VGA'; '1' = 'S-Video'; '2' = 'Composite'; '3' = 'Component'
    '4' = 'DVI'; '5' = 'HDMI'; '6' = 'LVDS'
    '8' = 'D_JPN'; '9' = 'SDI'
    '10' = 'DisplayPort (外接)'; '11' = 'DisplayPort (内嵌/eDP)'
    '12' = 'UDI External'; '13' = 'UDI Embedded'; '14' = 'SDTV Dongle'
    '2147483648' = 'Internal Panel'
}
Get-CimInstance -Namespace root\wmi -ClassName WmiMonitorConnectionParams | ForEach-Object {
    $k = [string]$_.VideoOutputTechnology
    [PSCustomObject]@{
        Instance   = $_.InstanceName
        TechValue  = $k
        Connection = $(if ($techMap.ContainsKey($k)) { $techMap[$k] } else { "未知($k)" })
    }
} | Format-List

H "8. 显卡输出接口清单（EDID 注册表）"
$dispRoot = 'HKLM:\SYSTEM\CurrentControlSet\Enum\DISPLAY'
Get-ChildItem $dispRoot -ErrorAction SilentlyContinue | ForEach-Object {
    $mfg = $_.PSChildName
    Get-ChildItem $_.PSPath -ErrorAction SilentlyContinue | ForEach-Object {
        $inst = $_
        $devParams = Get-ItemProperty -Path $inst.PSPath -ErrorAction SilentlyContinue
        [PSCustomObject]@{
            Monitor   = $mfg + '\' + $inst.PSChildName
            DriverKey = $devParams.Driver
        }
    }
} | Format-Table -AutoSize

H "9. 快速启动 / 休眠设置"
$p = Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power' -ErrorAction SilentlyContinue
Write-Output ("HiberbootEnabled (快速启动, 1=开启) : " + $p.HiberbootEnabled)
Write-Output ("HibernateEnabled (休眠)             : " + $p.HibernateEnabled)
$pf = Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power' -ErrorAction SilentlyContinue
Write-Output ("HiberFileType                       : " + $pf.HiberFileType)

H "10. 启动方式 / 上次启动时间"
(Get-CimInstance Win32_OperatingSystem).LastBootUpTime
Write-Output ("当前时间: " + (Get-Date))

H "11. 关键设备问题（设备管理器中标记异常的）"
Get-CimInstance Win32_PnPEntity |
    Where-Object { $_.ConfigManagerErrorCode -ne 0 } |
    Select-Object Name, ConfigManagerErrorCode, DeviceID |
    Format-Table -AutoSize

H "12. 已安装的显示相关驱动"
Get-CimInstance Win32_PnPSignedDriver |
    Where-Object { $_.DeviceClass -match 'DISPLAY|MONITOR' } |
    Select-Object DeviceName, DriverVersion, DriverDate, Manufacturer |
    Format-Table -AutoSize

Write-Output ""
Write-Output "=== DIAG DONE ==="
