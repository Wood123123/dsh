# Git 使用说明

> 本工作区有 **4 个 git 仓库**：1 个元仓库 + 3 个可发布的 mod 仓库。
> 这份说明讲**怎么用**、**推到哪**、**换机器怎么拉**。

---

## 一、仓库总览

| # | 仓库 | 路径 | 跟踪 | 裸仓库 |
|---|---|---|---|---|
| ① | **dsh** | `C:\Users\Administrator\Desktop\dsh` | 索引、文档、经验卡、**未发布**的 mod | `D:\git\dsh.git` |
| ② | **HeadlessPanel** | `…\dsh\工作区\headless-panel` | 服务端 Web 面板 | `D:\git\HeadlessPanel.git` |
| ③ | **FikaInventoryResync** | `…\dsh\工作区\fika-inventory-resync` | 物品栏重同步 | `D:\git\FikaInventoryResync.git` |
| ④ | **TacticalSprint-Companions** | `…\dsh\工作区\TacticalSprint-Companions` | 三个战术冲刺附属插件 | `D:\git\TacticalSprint-Companions.git` |

**⇒ ②③④ 在 ① 的 `.gitignore` 里**（避免 embedded repository / gitlink）。

**体积：**
```
① dsh                        8.0 MB / 856 文件  （排除了 826 MB 素材/日志/参考源码）
② HeadlessPanel              0.09 MB / 12 文件
③ FikaInventoryResync        0.05 MB / 6 文件
④ TacticalSprint-Companions  0.05 MB / 22 文件
```

---

## 二、日常操作

### 推全部（推荐）

```powershell
cd C:\Users\Administrator\Desktop\dsh
.\push-all.ps1                       # 推本地裸仓库
.\push-all.ps1 -Only dsh             # 只推元仓库
.\push-all.ps1 -Provider gitee -User <用户名> -Token <token>   # 推 Gitee
```

### 单个仓库手动操作

```powershell
cd C:\Users\Administrator\Desktop\dsh\工作区\headless-panel
git add -A
git commit -m "说明这次改了什么"
git push origin --all
```

### 元仓库（dsh）

```powershell
cd C:\Users\Administrator\Desktop\dsh
git add -A
git commit -m "更新索引/经验卡"
git push local --all
```

---

## 三、推到云端

### 3.1 已实测的通道

| 站 | git 协议 | 速度 | 依赖 |
|---|---|---|---|
| **Gitee** | ✅ 通 | **174ms** | **无** |
| **GitHub** | ✅ 通 | 613ms | **★ 需要 Steam++ 在运行**（hosts 把 github.com 指到 127.0.0.1，靠加速器在本地 :443 反代） |

> **GitHub 还有一条不依赖 Steam++ 的通道**：SSH over 443（`ssh.github.com:443`，实测 5/5 通）。
> 配好 SSH 密钥 + `~/.ssh/config` 指向 `ssh.github.com:443` 即可。见第六节。

### 3.2 Gitee（推荐）

**先建 4 个空仓库**（不要勾选初始化 README / .gitignore）：

```
https://gitee.com/projects/new
  dsh
  HeadlessPanel
  FikaInventoryResync
  TacticalSprint-Companions
```

**生成 token：** https://gitee.com/profile/personal_access_tokens

**推：**
```powershell
.\push-all.ps1 -Provider gitee -User <你的用户名> -Token <token>
```

### 3.3 GitHub

```powershell
# token: https://github.com/settings/tokens  → 勾 repo
.\push-all.ps1 -Provider github -User <用户名> -Token <token>
```

**⚠ token 只在本次调用里用，推完 remote URL 会改回不带 token 的。**

---

## 四、换机器

### 4.1 克隆

```powershell
# 主仓库
git clone https://gitee.com/<用户名>/dsh.git D:\dsh
cd D:\dsh
powershell -NoProfile -ExecutionPolicy Bypass -File bootstrap.ps1 -GameRoot "D:\EFT" -ServerHost "<服务端>"

# 三个 mod 仓库（按需，或在 dsh 里继续编辑后再各自 push）
git clone https://gitee.com/<用户名>/HeadlessPanel.git
git clone https://gitee.com/<用户名>/FikaInventoryResync.git
git clone https://gitee.com/<用户名>/TacticalSprint-Companions.git
```

### 4.2 ★ 三样都要搬，缺一不可

| # | 要搬的 | 怎么搬 | 漏了会怎样 |
|---|---|---|---|
| **1** | **`~/.dsh/AGENTS.md`** | **单独拷**（不在任何 git 里！） | **procedures 的读卡约定失效** |
| **2** | 工作区本体 | `git clone`（共约 8 MB） | — |
| **3** | 机器本地路径 | 跑 `bootstrap.ps1` 生成 `machines.json` | 脚本找不到游戏端/服务端 |

```powershell
Copy-Item "$env:USERPROFILE\.dsh\AGENTS.md" "<新机器>\C:\Users\<用户>\.dsh\AGENTS.md"
```

### 4.3 缓存区那 826 MB

**不进 git**。需要时单独同步：

```
缓存区\素材包\     550 MB   下载的原始包 —— 通常不需要搬
缓存区\日志与数据\  10 MB   日志归档 —— 排查历史问题时才要
缓存区\备份\        50 MB   备份 —— 按需
缓存区\参考源码\   210 MB   第三方反编译源码 —— 查实现时要
   └─ _shijiu_src\  0.6 MB  ★ 这个【已在 git 里】（常查，体积小）
```

---

## 五、`.gitignore` 排除了什么（元仓库）

```
编译产物    **/obj/  **/bin/  *.pdb  *.cache
素材包      缓存区/素材包/
日志        缓存区/日志与数据/
备份        缓存区/备份/
第三方源码  缓存区/参考源码/{_fika_src,_sain,_manimal,_questbackport,...}
临时文件    *.tmp  *.new  *.tmp_old  *.bak_*
个人资料    考研进度追踪.xlsx  考研数二-极限知识点总结.md
机器本地    machines.json                ★ 由 bootstrap 重新生成
独立仓库    工作区/headless-panel/
            工作区/fika-inventory-resync/
            工作区/TacticalSprint-Companions/
```

---

## 六、★ 附：给 GitHub 配一条不依赖 Steam++ 的 SSH 通道

```
实测：ssh.github.com:443  TCP 5/5 成功，SSH 握手成功
      ssh.github.com 的 IP = 20.205.243.160（真实 IP，没被 hosts 拦）
      github.com 的真实 IP 也能 TLS 握手成功
⇒ GitHub 网络本身是通的，唯一障碍是 Steam++ 改的 hosts
```

**配置：**
```powershell
# 1. 生成密钥
ssh-keygen -t ed25519 -C "dsh-workspace"

# 2. 把 ~/.ssh/id_ed25519.pub 贴到 https://github.com/settings/keys

# 3. 配 ~/.ssh/config（走 443 端口，绕开被拦的 github.com）
Add-Content ~/.ssh/config @"
Host github.com
    HostName ssh.github.com
    Port 443
    User git
"@

# 4. 用 SSH URL
git remote set-url origin git@github.com:<用户名>/dsh.git

# 验证（应回 "Hi <用户名>! You've successfully authenticated"）
ssh -T git@github.com
```

---

## 七、常见问题

**Q: `git status` 显示几百 MB 要提交？**
A: 检查 `.gitignore` 是否还在；用 `git rm -r --cached <路径>` 清掉误加的。

**Q: push 到 GitHub 报 403 / 超时？**
A: 确认 Steam++ 在运行，或改用 Gitee，或按第六节配 SSH。

**Q: 中文文件名显示成 `\345\267\245...`？**
A: 已配好 `core.quotepath false`。

**Q: 元仓库里 `工作区\headless-panel` 变成 gitlink / embedded repository？**
A: 它已在 `.gitignore` 里。若之前误加过，用 `git rm --cached -r "工作区/headless-panel"` 清掉。

**Q: 换行符警告？**
A: 已配好 `core.autocrlf false`。