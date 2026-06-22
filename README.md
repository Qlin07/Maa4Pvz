# Maa4Pvz

基于 [MAA](https://github.com/MaaAssistantArknights/MaaAssistantArknights) 框架的植物大战僵尸2 自动化助手。

## 功能

- **GUI 界面**：WPF 桌面应用，支持亮色/暗色主题、窗口置顶、最小化托盘
- **设备连接**：支持 ADB 直连安卓模拟器（MuMu、蓝叠、雷电等）
- **任务队列**：勾选任务 → 开始 → 自动执行，实时日志反馈
- **纯 JSON 驱动**：新增任务只需编写 JSON 流程文件，无需改动 C++ 代码
- **C++ 驱动任务**：复杂逻辑（条件判断、数据解析）可自定义 C++ 任务子类

### 当前可用的任务

| 任务 | 类型 | 说明 |
|------|------|------|
| 戴夫杯 | C++ 驱动 | 自动重复参加戴夫杯活动 |

## 目录结构

```
Maa4Pvz/
├── resource/                    # 资源配置
│   ├── tasks/                   # JSON 任务流程定义
│   ├── template/                # 模板匹配图片
│   ├── onnx/                    # ONNX 模型
│   └── PaddleOCR/               # PaddleOCR 模型文件
├── src/
│   ├── MaaCore/                 # C++ 核心库
│   │   ├── Task/Interface/      # C++ 任务接口
│   │   ├── Task/                # 框架核心（ProcessTask 等）
│   │   ├── Controller/          # 设备控制（ADB/Win32）
│   │   ├── Vision/              # 图像识别（模板匹配/OCR/特征匹配）
│   │   ├── Config/              # 配置加载（TaskData/ResourceLoader）
│   │   └── Assistant.cpp        # MaaCore 入口
│   ├── MaaWpfGui/               # WPF 桌面 GUI
│   │   ├── Views/UI/            # 页面（Root/TaskQueue/Settings）
│   │   ├── ViewModels/UI/       # 视图模型
│   │   ├── Main/                # Bootstrapper/AsstProxy
│   │   ├── Services/            # P/Invoke 声明
│   │   └── Res/                 # 主题/样式/多语言
│   ├── Cpp/                     # C++ CLI 示例
│   └── MaaUtils/                # 底层工具库
├── tools/
│   └── ImageCropper/            # 模板图片采集工具
├── include/
│   └── AsstCaller.h             # C API 头文件
└── CMakeLists.txt
```

## 编译

### 前置要求

- Visual Studio 2022（含 C++ 桌面开发工作负载）
- .NET 8 SDK
- CMake 3.28+
- Git

### 步骤

```powershell
# 1. 克隆并初始化子模块
git clone https://github.com/your-repo/Maa4Pvz.git
cd Maa4Pvz
git submodule update --init --recursive

# 2. 编译 MaaCore（C++）
cmake -B build -DBUILD_RESOURCE_UPDATER=OFF
cmake --build build --config Release

# 3. 创建 resource 符号链接（运行 WPF 前需要）
cmd /c mklink /J "build\bin\Release\resource" "resource"

# 4. 编译 GUI（WPF）
dotnet build src/MaaWpfGui/MaaWpfGui.csproj -c Release

# 5. 运行
.\build\bin\Release\MAA-PvZ.exe
```

> **注意**：每次新增 `.cpp` 文件后，需重新执行 `cmake -B build -DBUILD_RESOURCE_UPDATER=OFF`，因为 `CMakeLists.txt` 使用 `file(GLOB_RECURSE)` 收集源文件。

## 使用

1. 启动 `MAA-PvZ.exe`
2. 打开模拟器，进入 PvZ2 游戏主页
3. 切换到「设置」页，配置 ADB 路径和连接地址
   - MuMu 12 默认地址：`127.0.0.1:16384`
   - MuMu 6 / 蓝叠 默认地址：`127.0.0.1:7555`
4. 点击「连接测试」确认连接成功
5. 切换到「任务队列」页，勾选要执行的任务
6. 点击「开始」

---

## 创建新任务

### 方式一：纯 JSON 任务（推荐，无需改 C++ 代码）

适用于流程只有「识别 UI → 点击 → 等待 → 下一步」的线性自动化场景。

#### 步骤

**1. 创建 JSON 任务文件**

在 `resource/tasks/` 下新建 `xxx_tasks.json`，定义任务流程：

```jsonc
{
    // 入口点必须以 @EntryPoint 结尾，WPF 会自动发现
    "MyTask@EntryPoint": {
        "doc": "任务描述（会显示在 WPF 任务列表中）",
        "algorithm": "MatchTemplate",
        "template": "my_task/btn_xxx.png",
        "templThreshold": 0.8,
        "action": "ClickSelf",
        "postDelay": 1000,
        "next": ["MyTask@Step2"]
    },
    "MyTask@Step2": {
        "algorithm": "MatchTemplate",
        "template": "my_task/btn_yyy.png",
        "action": "ClickSelf",
        "postDelay": 1000,
        "next": ["MyTask@Done"]
    },
    "MyTask@Done": {
        "algorithm": "JustReturn",
        "postDelay": 500
    }
}
```

**2. 采集模板图片**

- 打开 `tools/ImageCropper/main.py`，修改 `device_serial` 为你的模拟器地址
- 运行并框选需要识别的 UI 元素
- 将生成的 `.png` 图片放到 `resource/template/my_task/` 目录下

**3. 运行**

重新启动 WPF，新任务会自动出现在任务列表中。

> **无需重新编译 MaaCore**，JSON 文件在运行时加载。

#### JSON 任务配置说明

| 字段 | 必填 | 说明 |
|------|:----:|------|
| `algorithm` | 是 | 识别算法：`MatchTemplate`（模板匹配）、`JustReturn`（直接通过）、`OcrDetect`（文字识别） |
| `template` | 条件 | 模板图片路径（`MatchTemplate` 时必填），相对于 `resource/template/` |
| `text` | 条件 | 要识别的文字（`OcrDetect` 时必填） |
| `action` | 否 | 命中后执行的动作：`ClickSelf`（点击）、`Swipe`（滑动）、`DoNothing`（无动作） |
| `postDelay` | 否 | 动作后等待毫秒数 |
| `templThreshold` | 否 | 模板匹配阈值（0~1），默认 0.7 |
| `maxTimes` | 否 | 最大重试次数，默认 20 |
| `next` | 否 | 命中后跳转的下一个任务名数组。第一个命中则跳第一个，依此类推。`"#self"` 表示重试自身 |
| `doc` | 否 | 任务描述注释 |

#### 注意事项

- 入口任务名必须以 `@EntryPoint` 结尾
- `next` 支持多个候选项，框架按顺序尝试匹配
- 调试时先用 `"algorithm": "JustReturn"` 验证流程可达，再逐步替换为真实识别
- 日志输出在 `build\bin\Release\debug\asst.log` 和 `gui.log`

---

### 方式二：C++ 驱动任务（复杂逻辑）

适用于需要条件判断、数据解析、子任务组合的场景。

#### 步骤

**1. 创建头文件** `src/MaaCore/Task/Interface/XxxTask.h`：

```cpp
#pragma once
#include "Task/InterfaceTask.h"

namespace asst {
class OpenActivityTask; // 如需复用其他任务

class XxxTask final : public InterfaceTask {
public:
    inline static constexpr std::string_view TaskType = "Xxx";

    XxxTask(const AsstCallback& callback, Assistant* inst);
    virtual ~XxxTask() override = default;
};
}
```

**2. 创建实现文件** `src/MaaCore/Task/Interface/XxxTask.cpp`：

```cpp
#include "XxxTask.h"
#include "Task/ProcessTask.h"

asst::XxxTask::XxxTask(const AsstCallback& callback, Assistant* inst)
    : InterfaceTask(callback, inst, TaskType)
{
    // 示例：组合一个 ProcessTask
    auto task_ptr = std::make_shared<ProcessTask>(callback, inst, TaskType);
    task_ptr->set_tasks({ "Xxx@EntryPoint" });
    m_subtasks.emplace_back(task_ptr);

    // 如需复用其他 C++ 任务：
    // auto other_ptr = std::make_shared<OtherTask>(callback, inst);
    // m_subtasks.emplace_back(other_ptr);
}
```

**3. 注册到 `Assistant.cpp`** 的 `append_task` 方法中，在现有 if-else 链中添加：

```cpp
else if (type == XxxTask::TaskType) {
    ptr = std::make_shared<XxxTask>(append_callback_for_inst, this);
}
```

**4. 注册到 WPF 任务列表** `TaskQueueViewModel.cs` 的 `InitTaskList()`：

```csharp
allTasks.Add(new TaskItemViewModel("任务显示名", "Xxx"));
```

**5. 重新编译**：

```powershell
cmake -B build -DBUILD_RESOURCE_UPDATER=OFF
cmake --build build --config Release
dotnet build src/MaaWpfGui/MaaWpfGui.csproj -c Release
```

---

## 架构概览

```
用户调用 AsstAppendTask("Xxx", params)
  │
  ▼
Assistant.cpp 中 append_task()
  │
  ├── 找到 C++ TaskType 注册 → 创建对应 InterfaceTask 子类
  └── 未找到 → 自动回退 GenericJsonTask("Xxx@EntryPoint")
        │
        ▼
   InterfaceTask._run()
        │
        ▼
   ProcessTask 按 JSON 任务链执行：
        while (未到终点) {
            截图 → 匹配 → 命中? → 执行 action → 跳转 next
        }
```

### 任务层次结构

```
AbstractTask                   （基类：run/retry/callback/plugin 机制）
├── ProcessTask                （JSON 驱动的任务引擎）
├── AbstractTaskPlugin         （Plugin 基类）
├── PackageTask                （串联多个子任务）
│   └── InterfaceTask          （对外暴露的任务入口）
│       ├── HomepageTask       （回到主页）
│       ├── OpenActivityTask   （打开活动合集）
│       ├── FtCricketsTask     （戴夫杯）
│       └── GenericJsonTask    （纯 JSON 任务回退）
```

### 设计原则

- 80% 的逻辑用 JSON 驱动，只有条件判断/数据解析才写 C++
- `@EntryPoint` 后缀为 JSON 任务的入口标记，WPF 自动扫描发现
- 未注册到 C++ 的任务类型自动回退为纯 JSON 加载

## 开发指南

详细开发路线参见：

- [DEVELOPMENT.md](DEVELOPMENT.md) — 框架搭建、识别管线、编译验证
- [DEVELOPMENT_PHASE4.md](DEVELOPMENT_PHASE4.md) — 扩展游戏逻辑、Plugin 机制、多任务组合

## 开发工具

- **ImageCropper**：模板图片采集工具（`tools/ImageCropper/`）
  - PC 端用 Win32/WGC 截图
  - Android 端用 ADB 截图
  - 框选区域自动导出 PNG 模板

## 参考项目

- [MaaAssistantArknights](https://github.com/MaaAssistantArknights/MaaAssistantArknights) — 原项目

## 许可证

MIT License
