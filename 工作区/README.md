# 工作区

> **这里放「活跃项目」** —— 会改、会编译、会部署的东西。
> 上级索引：`..\_索引.md`

## 规则

- 每个子目录 = **一个独立的源码项目**，内含 `README.md` 说明「作用 / 影响范围 / 状态 / 部署位置」
- 改完代码要 `dotnet build -c Release`，然后按 README 里的「部署位置」拷过去
- **不要在别处另建副本**：所有源码只放一份，就在这里
- `obj\` / `bin\` 是构建缓存，不要手动改

## 目录一览

**部署中（改动影响线上）：**
```
_cookingsync            手雷烹饪时长联机同步
_shijiufikafix          绕过拾玖 Fika 版本门（修「结果未确认」）
_shijiuhistoryfix       服务端：拾玖战绩重连冲突自动修复
_lootdiag               拾取诊断（只读）
_logshipper             日志上传到面板
_transitprefetch        BTR 传送预取修复
_weightgate             战术冲刺负重限制
_reconnectbutton        主菜单重连按钮
_tacticalsprint_fika    战术冲刺动画同步
manimal-audio-gate      Icebreaker 音频门控
fika-inventory-resync   库存重同步 + LockFix
headless-panel          服务端面板
shijiu-price-fast       拾玖价格模块（服务端）
```

**已停用/已移除（源码留档）：**
```
_transitdiag        Transit 诊断探针   —— 已从四端移除
_transitoverlay     F9 屏幕面板       —— 已从四端移除
_shijiufix          拾玖重连跳过       —— 从未生效（FindType bug）
_tripwiresyncfix    绊线同步修复       —— 补丁从未挂上，已移除
```

**诊断/辅助：**
```
_busydiag / _headlessdiag / _headlesstelemetry
_kitfix / _speedfix / _tacticalsprint_build / _tripwire_build
```