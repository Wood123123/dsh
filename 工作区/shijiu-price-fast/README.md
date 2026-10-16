# ShiJiuPrices

> **作用**：拾玖价格模块服务端
> **影响范围**：服务端
> **状态**：✅ 部署中
> **部署位置**：SPT_Runtime\user\mods\ShiJiuPrices
> **目录**：`工作区\shijiu-price-fast`

## 源码文件

- `ShiJiuPriceFast\ShiJiuPriceFast.cs`
- `ShiJiuPricesServer\Mod.cs`

## 产物 dll（非 obj/bin）

（无）

## 怎么构建

```powershell
cd "C:\Users\Administrator\Desktop\dsh\工作区\shijiu-price-fast"
dotnet build -c Release
```

产物在 `bin\Release\`。

## 注意

- `obj\` / `bin\` 是构建缓存，不要手动改
- 部署前先确认游戏端/服务端已停止，否则文件被占用
- 本项目 dll 会通过 NarcoNet 自动分发到其它客户端，改动会影响所有人
