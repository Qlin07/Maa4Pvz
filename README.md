# Maa_baseline

基于 MAA 框架的游戏自动化项目。

## 当前状态 v0.1

| 能力 | 状态 |
|------|------|
| 编译 | Windows x64 Release |
| 连接模拟器 | MuMu Player 12 (ADB) |
| 截图 | RawByNc ~116ms/帧 |
| 触摸 | Minitouch 已初始化 |
| 模板匹配 | OpenCV, score 0.99 |
| JSON 任务驱动 | ProcessTask 链路完整 |

---

## 环境要求

- Windows 10+
- Visual Studio 2026（含 C++ 桌面开发）
- CMake 4.x+
- Python 3.9+（仅 ImageCropper 工具需要）

## 快速开始

### 1. 克隆并初始化

```powershell
git clone --recursive https://github.com/Qlin07/Maa4Pvz.git
cd Maa4Pvz
git submodule update --init src/MaaUtils
```

### 2. 下载预编译依赖

```powershell
python tools\maadeps-download.py x64-windows
```

### 3. 编译

```powershell
cmake -S . -B build -G "Visual Studio 18 2026" -A x64 -DMAADEPS_TRIPLET=maa-x64-windows -DBUILD_DEBUG_DEMO=ON
cmake --build build --config Release
```

### 4. 连接模拟器并测试

确保 MuMu 模拟器已启动，游戏处于前台：

```powershell
.\build\bin\Release\debug_demo.exe
```

日志输出在 `build\bin\Release\debug\asst.log`。

---

## 项目结构

```
Maa4Pvz/
├── resource/
│   ├── config.json        ← ADB 连接配置、模拟器命令模板
│   ├── tasks/              ← JSON 任务定义（识别+动作流程）
│   │   └── homepage_tasks.json
│   └── template/           ← 模板图片（按功能分目录）
│       └── homepage/
│           ├── btn_backpack.png
│           ├── btn_galaxy.png
│           └── btn_Plants.png
├── src/
│   ├── MaaCore/            ← 核心库
│   │   ├── Assistant.cpp   ← 任务注册入口
│   │   ├── Task/
│   │   │   ├── InterfaceTask.h    ← 所有任务的基类
│   │   │   ├── ProcessTask.cpp    ← JSON 驱动的任务引擎
│   │   │   └── Interface/         ← 你的 InterfaceTask 子类放这里
│   │   │       └── HomepageTask.h/.cpp
│   │   ├── Vision/         ← 图像识别
│   │   │   ├── Matcher.cpp         ← 模板匹配实现
│   │   │   ├── OCRer.cpp          ← OCR 文字识别
│   │   │   └── Miscellaneous/
│   │   │       └── PipelineAnalyzer.cpp  ← 统一分析器
│   │   ├── Controller/     ← 设备连接
│   │   └── Config/         ← 资源加载
│   ├── Cpp/
│   │   └── main.cpp        ← Demo 入口
│   └── MaaUtils/           ← 基础库 (submodule)
├── tools/
│   └── ImageCropper/       ← 模板裁剪工具
│       └── main.py          ← python main.py [adb地址]
└── DEVELOPMENT_PHASE2.md   ← 第二阶段开发指南
```

---

## 核心架构（四层）

```
main.cpp
  AsstAppendTask(ptr, "Homepage", params)
    └─ Assistant::append_task()
        └─ HomepageTask (InterfaceTask)
            └─ ProcessTask::set_tasks({"Homepage"})
                └─ resource/tasks/*.json → { "Homepage": {...} }
                    └─ PipelineAnalyzer → Matcher → OpenCV matchTemplate
```

---

## 如何添加新功能

### 1. 加一个新按钮的识别

**Step 1** — 用 ImageCropper 裁剪模板：

```powershell
cd tools\ImageCropper
pip install opencv_python~=4.13
python main.py 127.0.0.1:7555
# 框选按钮 → 按 S 保存 → 改名为 btn_xxx.png
# 放到 resource/template/ 下对应子目录
```

**Step 2** — 在 `resource/tasks/xxx.json` 中加任务：

```json
{
    "MyNewBtn": {
        "algorithm": "MatchTemplate",
        "template": "my_feature/btn_xxx.png",
        "templThreshold": 0.8,
        "action": "ClickSelf",
        "next": ["NextStep"]
    }
}
```

**Step 3** — 如果没有编译，直接改 JSON 即时生效（模板图和 JSON 都是运行时加载的）。

### 2. 加一个完整的 InterfaceTask

当你要用 `AsstAppendTask(ptr, "MyTask", params)` 调用时：

**Step 1** — 新建 `src/MaaCore/Task/Interface/MyTask.h`：

```cpp
#pragma once
#include "Task/InterfaceTask.h"

namespace asst {
class ProcessTask;
class MyTask final : public InterfaceTask {
public:
    inline static constexpr std::string_view TaskType = "MyTask";
    MyTask(const AsstCallback& callback, Assistant* inst);
};
}
```

**Step 2** — 新建 `MyTask.cpp`：

```cpp
#include "MyTask.h"
#include "Task/ProcessTask.h"

asst::MyTask::MyTask(const AsstCallback& callback, Assistant* inst)
    : InterfaceTask(callback, inst, TaskType)
{
    auto task_ptr = std::make_shared<ProcessTask>(callback, inst, TaskType);
    task_ptr->set_tasks({ "MyTaskJsonEntry" });  // JSON 入口名
    m_subtasks.emplace_back(task_ptr);
}
```

**Step 3** — 在 `src/MaaCore/Assistant.cpp` 注册：

```cpp
#include "Task/Interface/MyTask.h"

// 在 append_task() 中添加：
if (type == MyTask::TaskType) {
    ptr = std::make_shared<MyTask>(append_callback_for_inst, this);
}
```

**Step 4** — 重新编译（新增 .cpp 需要）：

```powershell
cmake -S . -B build -G "Visual Studio 18 2026" -A x64 -DMAADEPS_TRIPLET=maa-x64-windows -DBUILD_DEBUG_DEMO=ON
cmake --build build --config Release
```

### 3. JSON 任务字段速查

| 字段 | 说明 | 示例 |
|------|------|------|
| `algorithm` | `MatchTemplate` / `OcrDetect` / `JustReturn` | `"MatchTemplate"` |
| `template` | 相对于 `resource/template/` 的路径 | `"homepage/btn_xxx.png"` |
| `templThreshold` | 匹配阈值 (0~1) | `0.8` |
| `action` | `ClickSelf` / `ClickRect` / `DoNothing` / `Stop` / `Swipe` | `"ClickSelf"` |
| `roi` | 限定搜索区域 `[x, y, w, h]` | `[0, 0, 1280, 720]` |
| `next` | 成功后下一步 | `["Step2"]` |
| `onErrorNext` | 失败后下一步 | `["Retry"]` |
| `maxTimes` | 最大重试次数 | `20` |
| `preDelay` / `postDelay` | 动作前后延迟(ms) | `500` |
| `baseTask` | 继承另一个任务 | `"MyButton"` |
| `maskRange` | 灰度过滤 `[min, max]` | `[1, 255]` |

> 更多字段见 `src/MaaCore/Config/TaskData/TaskDataTypes.h`。

---

## 调试

### 查看匹配日志

```powershell
# 跑完后看：
cat .\build\bin\Release\debug\asst.log | findstr "match_templ\|score:"
```

### 降低阈值测试

如果一直匹配不上，先降到 0.5 确认模板图本身没问题：

```json
{ "templThreshold": 0.5 }
```

### 只识别不点击

```json
{ "action": "DoNothing" }
```

日志仍会输出匹配坐标，不会误触游戏。

---

## 常见问题

| 问题 | 原因 | 解决 |
|------|------|------|
| `cmake --preset` 报 `$comment` 错误 | CMake 版本 < 4.x | 用原始命令：`cmake -S . -B build -G "Visual Studio 18 2026" ...` |
| 连接失败 `error: closed` | 模拟器 ADB 端口暂不可用 | 重启 MuMu 模拟器 |
| 模板匹配分数低 (< 0.5) | 分辨率不匹配 | 确保模拟器分辨率 = 720×1280，模板和截图同分辨率裁剪 |
| 新增 .cpp 不编译 | CMake GLOB 未重新扫描 | 重新运行 cmake 配置命令 |
| `load resource failed` | config.json 解析失败 | 检查 `resource/config.json` 的 `configName` 字段 |
| `MaaDeps not found` | 预编译依赖未下载 | `python tools\maadeps-download.py x64-windows` |
