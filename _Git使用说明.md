# Git 使用说明

> 本工作区已初始化 git 仓库。这份说明讲**怎么用**、**推到哪**、**换机器怎么拉**。

---

## 一、当前状态

```
仓库根:      C:\Users\Administrator\Desktop\dsh
分支:        main（或 master）
提交:        已有 4 个
入库体积:    8.0 MB / 870 文件
被忽略:      826 MB（99%）—— 下载素材、日志、第三方反编译、备份、编译产物、个人资料
```

**已配好的远程：**
```
local   D:\git\dsh.git   （本地裸仓库，随时可用，不需要账号）
        ⇒ 这是「离线远程」：多一份完整历史，可从这里 clone
```

---

## 二、日常操作

```powershell
cd C:\Users\Administrator\Desktop\dsh

# 看状态
git status

# 提交改动
git add -A
git commit -m "说明这次改了什么"

# 推到本地裸仓库（零依赖，随时可做）
git push local --all

# 推到云端（见第三节）
.\push.ps1 -Provider github -User <用户名> -Repo dsh -Token <token>
```

**建议频率：** 每次有意义的改动后 `commit`；每天至少 `push local` 一次。

---

## 三、推到云端

### 3.1 已实测的通道

| 站 | git 协议 | 速度 | 说明 |
|---|---|---|---|
| **GitHub** | **✅ 通** | 613ms | 走本机 Watt Toolkit 本地加速（hosts 把 github.com 指到 127.0.0.1，但加速器在本地反代） |
| **Gitee** | **✅ 通** | 174ms | 国内站，最快 |
| GitCode | ⚠ 部分镜像可用 | 791ms | 自建仓库可以，镜像路径常 403 |

### 3.2 GitHub

```powershell
# 1. 生成 token：https://github.com/settings/tokens  → 勾 repo 权限
# 2. 建库：https://github.com/new  → 名字 dsh，勾 Private，【不要】勾 README/gitignore
# 3. 推：
.\push.ps1 -Provider github -User <你的用户名> -Repo dsh -Token <token>
```

### 3.3 Gitee

```powershell
# 1. 生成 token：https://gitee.com/profile/personal_access_tokens
# 2. 建库：https://gitee.com/projects/new  → 名字 dsh，选私有
# 3. 推：
.\push.ps1 -Provider gitee -User <你的用户名> -Repo dsh -Token <token>
```

**⚠ token 只在本次调用里用，推完 `push.ps1` 会把 remote 改回不带 token 的 URL。**

### 3.4 完全不想用云端

**`D:\git\dsh.git` 就是完整备份**。想再稳一点，用网盘/Syncthing 同步这个目录即可（只有 4 MB）。

---

## 四、换机器

### 4.1 从云端克隆（推荐）

```powershell
git clone https://github.com/<用户名>/dsh.git D:\dsh
cd D:\dsh
powershell -NoProfile -ExecutionPolicy Bypass -File bootstrap.ps1 -GameRoot "D:\EFT" -ServerHost "<服务端>"
```

### 4.2 从本地裸仓库克隆（局域网内）

```powershell
git clone "\\<源机器>\D$\git\dsh.git" D:\dsh
```

### 4.3 ★ 三样都要搬，缺一不可

| # | 要搬的东西 | 怎么搬 | 漏了会怎样 |
|---|---|---|---|
| **1** | **`~/.dsh/AGENTS.md`** | **单独拷**（不在 git 里！） | **procedures 的读卡约定失效，agent 不再主动查经验卡** |
| **2** | 工作区本体 | `git clone`（8 MB） | — |
| **3** | 机器本地路径 | 跑 `bootstrap.ps1` 生成 `machines.json` | 脚本和 agent 找不到游戏端/服务端 |

```powershell
# 第 1 样：
Copy-Item "$env:USERPROFILE\.dsh\AGENTS.md" "<新机器>\C:\Users\<用户>\.dsh\AGENTS.md"
```

### 4.4 缓存区那 826 MB 怎么办

**不进 git**。需要时单独同步：

```
缓存区\素材包\     550 MB   下载的原始包 —— 通常不需要搬
缓存区\日志与数据\ 10 MB    日志归档 —— 排查历史问题时才要
缓存区\备份\       50 MB    备份 —— 按需
缓存区\参考源码\   210 MB   第三方反编译源码 —— 查实现时要
   └─ _shijiu_src\  0.6 MB  ★ 这个【已在 git 里】（常查，体积小）
```

**⇒ 实际建议：只搬 ① ② ③，参考源码用的时候再单独拉。**

---

## 五、`.gitignore` 都排除了什么

```
编译产物    **/obj/  **/bin/  *.pdb  *.cache
素材包      缓存区/素材包/
日志        缓存区/日志与数据/
备份        缓存区/备份/
第三方源码  缓存区/参考源码/{_fika_src,_sain,_manimal,_questbackport,...}
临时文件    *.tmp  *.new  *.tmp_old  *.bak_*
个人资料    考研进度追踪.xlsx  考研数二-极限知识点总结.md
机器本地    machines.json          ★ 换机器后由 bootstrap 重新生成
```

**会被跟踪的（870 个文件）：**
```
AGENTS.md  _索引.md  .gitignore  bootstrap.ps1  push.ps1
procedures/**                          经验卡
工作区/**/*.cs, *.csproj, README.md     自有源码与说明
缓存区/参考源码/_shijiu_src/            拾玖反编译源码（0.6 MB，常查）
缓存区/**/README.md                     各分区说明
```

---

## 六、常见问题

**Q: `git status` 显示 800 MB 要提交？**
A: 检查 `.gitignore` 是否还在，以及 `git rm -r --cached <路径>` 清掉误加的。

**Q: push 到 GitHub 报 403 / 超时？**
A: 确认 Watt Toolkit 在运行（它提供本地加速）。或改用 Gitee。

**Q: 中文文件名显示成 `\345\267\245...`？**
A: 已经配好 `core.quotepath false`，正常显示。若在别的机器上遇到，跑一次 `git config core.quotepath false`。

**Q: 换行符警告？**
A: 已经配好 `core.autocrlf false`。

**Q: 想彻底重置历史？**
A: `rm -rf .git; git init; git add -A; git commit -m "初始化"` —— 但先 `push local` 备份。
