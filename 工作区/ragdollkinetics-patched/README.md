# RagdollKinetics（打过补丁）

> **作用**：Hysocs/RagdollKinetics 1.2.0 + 我们的一处修 bug。让尸体布娃娃不飞天
> **影响范围**：客户端表现层（尸体物理/动画），不改任何网络协议
> **状态**：可用（已修一处上游 bug）
> **部署位置**：四端 `BepInEx\plugins\Hysocs-RagdollKinetics\RagdollKinetics.dll`
> **目录**：`工作区\ragdollkinetics-patched`
> **上游**：https://github.com/Hysocs/ragdollkinetics-spt （HEAD 6f03270 / v1.2.0）

## 它做什么

动画驱动的生物力学布娃娃物理：让尸体继承死亡前的运动、用受约束关节、渐进肌肉放松、并保持物理响应。

防「尸体飞天」的六条手段（读源码核实）：

1. `CorpseRagdoll.ApplyImpulse` 的 `thrust × ImpulseScale(0.5) × 0.2`，再 clamp 到 **1.25 m/s**
2. 冲量在 0.65 秒内 SmoothStep 衰减到 0
3. 建骨架时 `_remainingInheritedMomentum.y = 0`、`body.angularVelocity = 0`
4. `maxAngularVelocity ≤ 12`、`solverVelocityIterations ≥ 6`、`drag ≥ 0.08`
5. `CharacterJoint` → `ConfigurableJoint`（`enablePreprocessing = false`）
6. 兜底：任一刚体离死亡点 **> 35 米** ⇒ 整具尸体拉回 + 速度归零
   另加：`TryPutToSleep` 返回 false（禁睡）、`CheckCorpseIsStill` 延迟 15 秒冻结

## 我们改了什么（dsh-fix）

### 修补：`AnimationStartLocal` 未初始化 ⇒ 每帧每骨写 NaN ⇒ Unity 刷屏

**现象**
```
Player.log 里一场 Labs 刷 63,360 条（占全文 94%）：
Warning: Invalid quaternion rotation. Setting quaternion rotation to identity
BepInEx 的 LogOutput.log 里是 0 条（走的不是同一条通道）
```

**根因链**
```
Physics/RagdollController.cs:202   internal Quaternion AnimationStartLocal;   ← C# 默认 (0,0,0,0)
:348-362  ConfigureAnimationTargets 有三条早退路径，都不赋值：
            _futureAnimation == null
            TryGetAnimationTarget(bone.Name) 失败（Running => _running && _ready，尸体创建时 _ready 可能还是 false）
            AnimationParentTarget == null
:551-552  Quaternion.Inverse(bone.AnimationStartLocal)
          Unity 实现是 new Quaternion(-x,-y,-z,w) / (x²+y²+z²+w²) ⇒ 除以 0 ⇒ NaN
:567-569  bone.Joint.targetRotation = <NaN>
          ⇒ Unity 打印 "Invalid quaternion rotation. Setting quaternion rotation to identity"
:502-504  foreach (Bone bone in _bones) UpdateBoneDrive(...)   ← 无门控，每帧每骨
          ⇒ 一具尸体 ~15 骨 × 60 fps = 900 次/秒
```

**修法（2 处）**
```csharp
// ① 字段默认 identity
internal Quaternion AnimationStartLocal = Quaternion.identity;

// ② ApplyAnimationDrive 开头兜底
if (Quaternion.Dot(bone.AnimationStartLocal, bone.AnimationStartLocal) < 0.0001f)
    return;
```

**注意**：`Quaternion` 没有 `sqrMagnitude`，平方模长要用 `Quaternion.Dot(q, q)`。

**⚠ 不要用「关掉 Future Animation Skeleton」来绕过** —— `_futureAnimation == null` 时
`ConfigureAnimationTargets` 第一行就 return，所有骨骼的 `AnimationStartLocal` 都是零，
NaN 只会更多。

## 怎么构建

```powershell
cd "C:\Users\Administrator\Desktop\dsh\工作区\ragdollkinetics-patched\Client"
dotnet build -c Release -p:SptRoot='F:\EFT' -p:SkipDeploy=true -p:SkipPackage=true
```

- 上游 csproj 默认 `SptRoot=C:\SPT\4.1.2`，用 `-p:SptRoot=F:\EFT` 指向本地 4.1.6，能编译通过（API 兼容）
- `SkipDeploy=true` 必须加，否则 csproj 的 `DeployClient` target 会自动拷到 `$(SptRoot)\BepInEx\plugins\Hysocs-RagdollKinetics`
- 产物：`Client\bin\Release\netstandard2.1\RagdollKinetics.dll`

当前产物：93,184 B，SHA256 `c04e9c8cb657588deefe73be69fbad0380ac7b69885527de254264fe6d9df39a`

## 与 Fika 的兼容性（已核实）

- 10 个 patch 全打 EFT 类型，零 Fika 依赖
- Fika 自己也调 `CorpseRagdoll.ApplyImpulse`（`FikaPlayer.cs:1443` 本地 / `ObservedPlayer.cs:994` 远端）
  ⇒ 网络冲量同样被缩放
- `ObservedCorpse` 没重写 `TryToCreateRagdoll` ⇒ 远端尸体也会挂上 `RagdollSkeleton`
- 但 `ObservedCorpse` 重写了 `CheckCorpseIsStill` ⇒ **「延迟冻结」对远端尸体不生效**
- Fika 的 `Player_OnDead_Patch` 是 Transpiler `LocalPlayer.OnDead`，本 mod 是 Prefix `Player.OnDead` ⇒ 不冲突

## 已知局限

Fika 2.4.6 把尸体位置同步整条链拆了（`WorldPacket.RagdollPackets` 字段已删、`CorpsePositionSyncer`
发送端与接收端全部注释），所以各端尸体位置**仍会不一致**。这个 mod 只是本地兜底
（飞太远拉回死亡点），做不到跨端对齐。

## 与 Smoothie 的关系

Smoothie 的 `Run physics at most 60 times a second`（默认开）会让物理只跑 60Hz，
高刷屏上尸体下落看起来一卡一卡。作者原话在 sp-mod 的 1.0.8 Version Notes：
> If grenades or falling bodies look a little less smooth to you on a high refresh rate
> monitor, turn off "Run physics at most 60 times a second"

## 注意

- `obj\` / `bin\` 是构建缓存，不要手动改
- 部署前先确认游戏端/服务端已停止，否则文件被占用（在跑的话放 `.new`，重启生效）
- 本 dll 会通过 NarcoNet 自动分发到其它客户端，改动会影响所有人
- 上游更新后要重新打这两个补丁
