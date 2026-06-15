# 第二阶段：建立游戏识别基线

> 严格按照 DEVELOPMENT.md 第二阶段规划执行，参考原项目 MaaAssistantArknights 的技术实现。

---

## 前置：为什么需要模板加载

原项目加载任务时调用的是 `load_resource_with_templ<TaskData>`（见 `ResourceLoader.cpp`），它会先解析 JSON 收集所需模板文件名，再走 `TemplResource::load()` 从磁盘加载模板图片到内存。当前我们的框架用的是 `load_resource<TaskData>`，**跳过了模板加载步骤**。

这个差异必须在开始第二阶段前修复，否则模板永远加载不到内存里，匹配永远失败。

> 顺便提醒：`resource/template/` 子目录不存在时 `TemplResource::load()` 会直接返回 false。所以目录必须先建好。

---

## 2.1 理解识别流程

### 架构全景

```
main.cpp
  AsstAppendTask(ptr, "MyTask", params)
    → Assistant::append_task()
        → 根据 TaskType 字符串创建 InterfaceTask
          → InterfaceTask 内含 ProcessTask
            → ProcessTask::set_tasks({"EntryPoint"})
              → 去 resource/tasks/*.json 中找 "EntryPoint"
                → 根据 algorithm 字段创建 Matcher / OCRer / FeatureMatcher
                  → 从 TemplResource 取模板图片
                    → 在截图上滑动匹配
                      → 匹配到后执行 action (ClickSelf / DoNothing / ...)
```

### 核心类

| 类 | 文件 | 职责 |
|---|---|---|
| `ProcessTask` | `Task/ProcessTask.cpp` | JSON 驱动的主任务引擎，`set_tasks()` → `find_and_run_task()` |
| `Matcher` | `Vision/Matcher.cpp` | 模板匹配的具体实现，调用 OpenCV `matchTemplate` |
| `MatcherConfig` | `Vision/Config/MatcherConfig.cpp` | 从 TaskInfo 读取匹配参数（阈值、ROI、mask 等） |
| `TaskData` | `Config/TaskData.cpp` | 解析 JSON → 生成 TaskInfo 对象池，用 `Task.get("name")` 查询 |
| `TemplResource` | `Config/TemplResource.cpp` | 管理模板图片的加载和缓存 |
| `PipelineAnalyzer` | `Vision/Miscellaneous/PipelineAnalyzer.h` | 串联 Matcher/OCRer/FeatureMatcher 的统一分析器 |

### 匹配流程（源码追踪）

1. `ProcessTask::find_and_run_task(list)` → 创建 `PipelineAnalyzer`
2. `PipelineAnalyzer::analyze()` → 对 list 中每个任务尝试匹配
3. 匹配到后用 `task_ptr->algorithm` 分支：
   - `MatchTemplate` → `Matcher::analyze()` → OpenCV `matchTemplate`
   - `OcrDetect` → `OCRer::analyze()` → PaddleOCR 文字识别
4. 返回 `HitDetail { rect, score, task_ptr }`
5. 根据 `task_ptr->action` 执行 `ClickSelf` / `DoNothing` 等

> **关键**：整个流程靠 JSON 定义 + 模板图片驱动，不需要手写 C++ 识别逻辑。

---

## 2.2 采集样本（模板图片）

### 2.2.1 获取截图

用模拟器自带截图功能截一张完整游戏画面（PNG 格式）。

### 2.2.2 安装 ImageCropper

```powershell
cd d:\project\maa\Maa4Pvz\tools\ImageCropper
pip install opencv_python~=4.13
```

### 2.2.3 框选 ROI

```powershell
# 把截图放到 src/ 目录
Copy-Item "你的截图.png" src\

python main.py
```

| 操作 | 按键 |
|------|------|
| 左键拖拽 | 框选一个按钮/图标 |
| 按 S | 保存选区到 `dst/` |
| 按 Z | 撤销上一个选区 |
| 滚轮 | 缩放 |
| 右键拖拽 | 平移 |
| Q / Esc | 退出 |

### 2.2.4 整理模板

从 `dst/` 挑裁剪好的图片，按功能分目录放到 `resource/template/` 下。原项目结构参考：

```
resource/template/
├── my_feature_1/
│   ├── btn_start.png
│   └── btn_confirm.png
└── my_feature_2/
    └── icon_daily.png
```

> 模板图片会被 `ControlScaleProxy` 自动缩放到和实际截图一致的比例。只要采集时的截图像素够，不需要精确匹配某个分辨率。

---

## 2.3 测试识别

### 2.3.1 创建模板目录

```powershell
mkdir d:\project\maa\Maa4Pvz\resource\template
```

把裁好的模板图片放进去。

### 2.3.2 写 JSON 任务定义

在 `resource/tasks/` 下新建 `my_tasks.json`：

```json
{
    "MyFirstTask": {
        "algorithm": "MatchTemplate",
        "template": "my_feature/btn_start.png",
        "templThreshold": 0.7,
        "action": "DoNothing",
        "roi": [0, 0, 1280, 720],
        "next": ["Stop"]
    },
    "Stop": {
        "algorithm": "JustReturn",
        "action": "Stop",
        "next": []
    }
}
```

> `action: "DoNothing"` 确保只识别不点击——这是第二阶段的目标。第三阶段才换成 `ClickSelf` 实际操作。

### 2.3.3 创建 InterfaceTask

新建 `src/MaaCore/Task/Interface/MyFirstTask.h`：

```cpp
#pragma once
#include "Task/InterfaceTask.h"

namespace asst
{
class ProcessTask;

class MyFirstTask final : public InterfaceTask
{
public:
    inline static constexpr std::string_view TaskType = "MyFirst";

    MyFirstTask(const AsstCallback& callback, Assistant* inst);
    virtual ~MyFirstTask() override = default;
};
}
```

新建 `src/MaaCore/Task/Interface/MyFirstTask.cpp`：

```cpp
#include "MyFirstTask.h"
#include "Task/ProcessTask.h"

asst::MyFirstTask::MyFirstTask(const AsstCallback& callback, Assistant* inst)
    : InterfaceTask(callback, inst, TaskType)
{
    auto task_ptr = std::make_shared<ProcessTask>(callback, inst, TaskType);
    task_ptr->set_tasks({ "MyFirstTask" });
    m_subtasks.emplace_back(task_ptr);
}
```

> `"MyFirstTask"` 是 JSON 中的任务名，`"MyFirst"` 是 `AsstAppendTask` 调用时的 type 字符串。

### 2.3.4 在 Assistant.cpp 注册

编辑 `src/MaaCore/Assistant.cpp`，在文件头加 include：

```cpp
#include "Task/Interface/MyFirstTask.h"
```

在 `append_task` 函数中修改：

```cpp
// 找到：
// No game-specific tasks registered yet. Add your tasks here:
//   else if (type == MyTask::TaskType) { ... }

// 替换为：
else if (type == MyFirstTask::TaskType) {
    ptr = std::make_shared<MyFirstTask>(append_callback_for_inst, this);
}
```

### 2.3.5 修改 main.cpp

```cpp
AsstAppendTask(ptr, "MyFirst", nullptr);
AsstStart(ptr);
while (AsstRunning(ptr)) {
    std::this_thread::yield();
}
```

### 2.3.6 编译运行

```powershell
cmake --build build --config Release
.\build\bin\Release\debug_demo.exe
```

---

## 2.4 调试

### 匹配不上

降低阈值到 0.5 测试：

```json
{ "MyFirstTask": { "templThreshold": 0.5 } }
```

### 匹配到错误位置

加 `roi` 限定搜索区域（ImageCropper 中框选的坐标可用）：

```json
{ "MyFirstTask": { "roi": [200, 300, 100, 80] } }
```

### 模板图有纯黑/纯白边框

加灰度 mask 过滤：

```json
{ "MyFirstTask": { "maskRange": [1, 255] } }
```

### 游戏有多个皮肤/主题

用 `baseTask` 继承机制：

```json
{
    "MyButtonDark": {
        "baseTask": "MyButton",
        "template": "dark/btn.png"
    }
}
```

---

## 2.5 修复 ResourceLoader（已由框架脚本完成，此处备查）

当前 `ResourceLoader::load()` 的 TaskData 加载段需要改为带模板的版本。

**修改前**（当前代码）：

```cpp
if (std::filesystem::is_directory(path / "tasks"_p)) {
    if (!load_with_custom.template operator()<TaskData>("tasks"_p, "TaskData")) {
        return false;
    }
}
```

**修改后**（加上模板加载）：

```cpp
if (std::filesystem::is_directory(path / "tasks"_p) &&
    std::filesystem::is_directory(path / "template"_p)) {
    if (!load_resource_with_templ.template operator()<TaskData>("tasks"_p, "template"_p, "TaskData")) {
        return false;
    }
}
else if (std::filesystem::is_directory(path / "tasks"_p)) {
    if (!load_with_custom.template operator()<TaskData>("tasks"_p, "TaskData")) {
        return false;
    }
}
```

> `load_resource_with_templ` 已在 `ResourceLoader.h` 中定义，直接可用。它的逻辑是：先加载 TaskData（解析 JSON，收集需要的模板文件名），再加载 TemplResource（按需从 `template/` 目录加载图片）。

---

## 里程碑

运行 `debug_demo.exe`，日志中出现类似：

```
[INF] MatchTemplate my_feature/btn_start.png found at (320, 480) score 0.92
```

即成功——框架已经能在游戏截图上找到你定义的按钮了。

**下一步**（第三阶段）：把 `action` 从 `DoNothing` 改为 `ClickSelf`，让程序自动点击。
