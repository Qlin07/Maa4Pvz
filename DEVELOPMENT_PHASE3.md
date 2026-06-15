# 第三阶段：写第一个任务

> 严格按照 DEVELOPMENT.md 第三阶段规划执行，参考原项目 MaaAssistantArknights 的技术实现。

**前提**：第二阶段已完成——框架能编译、连接模拟器、模板匹配 score 0.99 到位。

**目标**：从"只识别不点击"升级到"识别 → 自动点击 → 进入子界面 → 识别更多 → 形成完整闭环"。

---

## 3.1 理解 ProcessTask 的执行引擎

第二阶段我们已经跑通了识别，但没让程序真正点下去。原因是 `action` 写的是 `"DoNothing"`。原项目 `ProcessTask::run_action()` 处理所有动作类型：

```cpp:src/MaaCore/Task/ProcessTask.cpp (line 194-229)
ProcessTask::NodeStatus ProcessTask::run_action(const HitDetail& hits) const
{
    switch (task->action) {
    case ProcessTaskAction::ClickRect:
        exec_click_task(task->specific_rect);   // 点固定坐标
        return NodeStatus::Success;
    case ProcessTaskAction::ClickSelf: {
        Rect rect = hits.rect;                  // 点匹配到的位置中心
        if (!task->rect_move.empty())
            rect = rect.move(task->rect_move);   // 支持偏移
        exec_click_task(rect);
        return NodeStatus::Success;
    }
    case ProcessTaskAction::Swipe:               // 滑动
        exec_swipe_task(...);
        return NodeStatus::Success;
    case ProcessTaskAction::DoNothing:
        return NodeStatus::Success;
    case ProcessTaskAction::Stop:
        Log.info("Action: Stop");
        return NodeStatus::Interrupted;          // 停止整条任务链
    }
}
```

`TaskChainCompleted` 的触发条件：最后一个子任务取到 `Stop` action。

---

## 3.2 改造 HomepageTask：从 DoNothing 到 ClickSelf

只需改 JSON，**不需要重新编译**（JSON 和模板是运行时加载的）：

```json
{
    "Homepage": {
        "algorithm": "JustReturn",
        "action": "DoNothing",
        "next": ["BackpackBtn"]
    },
    "BackpackBtn": {
        "algorithm": "MatchTemplate",
        "template": "homepage/btn_backpack.png",
        "templThreshold": 0.8,
        "action": "ClickSelf",
        "next": ["Stop"]
    },
    "Stop": {
        "algorithm": "JustReturn",
        "action": "Stop",
        "next": []
    }
}
```

改掉 `next` 从 `["BackpackBtn","GalaxyBtn","PlantsBtn"]` 改为只匹配一个 `["BackpackBtn"]`，并把 `action` 从 `DoNothing` 改成 `ClickSelf`，阈值提高到 0.8。

运行：

```powershell
.\build\bin\Release\debug_demo.exe
```

预期日志：

```
[INF] SubTaskCompleted | ClickSelf | rect:[1029,622,91,93] | score:0.99
[INF] Action: Stop
[INF] TaskChainCompleted
```

同时在模拟器上看到背包按钮被点击，进入了背包界面。

> **里程碑**：从纯识别升级到自动点击。

---

## 3.3 多步骤流程：主页 → 进背包 → 识别背包页 → 返回

原项目中典型的"入口分发 + 多皮肤匹配"模式（参考 `Mall.json`）：

```json
// resource/tasks/homepage_tasks.json
{
    "Homepage": {
        "algorithm": "JustReturn",
        "action": "DoNothing",
        "next": ["BackpackBtn"]
    },
    "BackpackBtn": {
        "algorithm": "MatchTemplate",
        "template": "homepage/btn_backpack.png",
        "templThreshold": 0.8,
        "action": "ClickSelf",
        "postDelay": 1500,
        "next": ["BackpackPageCheck", "BackpackBtn"]
    },
    "BackpackPageCheck": {
        "algorithm": "MatchTemplate",
        "template": "backpack/backpack_title.png",
        "templThreshold": 0.8,
        "action": "DoNothing",
        "next": ["BackToHomepage"]
    },
    "BackToHomepage": {
        "algorithm": "MatchTemplate",
        "template": "common/btn_back.png",
        "templThreshold": 0.8,
        "action": "ClickSelf",
        "postDelay": 1000,
        "next": ["HomepageCheck"]
    },
    "HomepageCheck": {
        "algorithm": "MatchTemplate",
        "template": "homepage/btn_Plants.png",
        "templThreshold": 0.8,
        "action": "DoNothing",
        "next": ["Stop"]
    },
    "Stop": {
        "algorithm": "JustReturn",
        "action": "Stop",
        "next": []
    }
}
```

### JSON 流程控制关键字段

| 字段 | 作用 | 示例 |
|------|------|------|
| `next: ["A", "B"]` | 匹配成功→尝试 A 的匹配，A 失败→尝试 B | `["BackpackPageCheck", "BackpackBtn"]` |
| `#self` | 自循环（等动画结束） | `"next": ["#self", "NextStep"]` |
| `maxTimes` | 最大执行次数 | `"maxTimes": 20` |
| `exceededNext` | 超过 maxTimes 后跳转 | `"exceededNext": ["Fallback"]` |
| `onErrorNext` | 匹配失败后走这条 | `"onErrorNext": ["RetryRoute"]` |
| `postDelay` | 动作后等待（毫秒） | `"postDelay": 1500` |

### 流程控制逻辑

1. **第一个匹配到的任务才执行**：`next` 数组按顺序尝试，`BackpackPageCheck` 匹配到了就不执行 `BackpackBtn`
2. **都没匹配到→重试**：等下一帧截图重新跑 `next` 列表
3. **`#self` 的使用**：`"next":["#self","NextStep"]` → 一直匹配自身直到匹配不到（动画消失），才尝试 NextStep

原项目 `SwitchTheme.json` 示例——滑动画动主题：

```json
{
    "SwitchTheme@SwipeToLightTheme": {
        "algorithm": "JustReturn",
        "action": "Swipe",
        "specificRect": [200, 170, 10, 10],
        "rectMove": [200, 600, 10, 10],
        "specialParams": [150, 0, 1, 1],
        "postDelay": 500,
        "maxTimes": 10,
        "next": ["SelectLightTheme", "SwipeToLightTheme"]
    }
}
```

- `specificRect` = 起始点矩形
- `rectMove` = 终点偏移量（实际终点 = `specificRect + rectMove`）
- `specialParams` = `[duration, extra_swipe, slope_in, slope_out]`

---

## 3.4 用 ImageCropper 采集新的模板

进入背包需要识别背包页面的特征，以及"返回"按钮：

```powershell
cd d:\project\maa\Maa4Pvz\tools\ImageCropper

# 先清掉旧截图
Remove-Item src\*

# ADB 实时模式
python main.py 127.0.0.1:7555
```

按下表采集：

| 模板 | 描述 | 保存为 |
|------|------|--------|
| 背包界面的标题 | 打开背包后顶部的标题文字/图标 | `resource/template/backpack/backpack_title.png` |
| 返回按钮 | 通用的返回上一页按钮 | `resource/template/common/btn_back.png` |

没有额外文件就创建对应子目录：

```powershell
mkdir resource\template\backpack
mkdir resource\template\common
```

---

## 3.5 传递参数——让任务可配置

`AsstAppendTask` 第三个参数是 JSON 字符串，会传给 `InterfaceTask::set_params()`。

在 `HomepageTask.cpp` 中添加参数解析（参考原项目 `FightTask::set_params`）：

```cpp
bool asst::HomepageTask::set_params(const json::value& params)
{
    m_task_ptr->set_enable(params.get("enable", true));
    m_task_ptr
        ->set_times_limit("BackpackBtn", params.get("max_clicks", 1))
        .set_tasks({ "Homepage" });
    return true;
}
```

新的 `main.cpp` 调用：

```cpp
json::object params { { "max_clicks", 1 }, { "enable", true } };
AsstAppendTask(ptr, "Homepage", params.to_string().c_str());
```

> 注意：`HomepageTask.h` 需要把 `m_subtasks` 改为成员变量（从构造函数移到类成员），才能在其他函数中引用。

---

## 3.6 OCR 文字识别

模板匹配对按钮图标效果好，但对**动态文字**（如数量、等级、名字）就无效——这时候用 OCR。

在原项目中 OCR 用法（参考 `MiniGame@ALL.json`）：

```json
{
    "MyOCRStep": {
        "algorithm": "OcrDetect",
        "text": ["加入赛事"],
        "action": "ClickSelf",
        "roi": [1072, 581, 201, 125],
        "next": ["NextStep", "MyOCRStep"]
    }
}
```

`text` 数组是**或匹配**——识别到"加入赛事"四个字中的任意部分都算命中，点击文字所在位置。

OCR 依赖 PaddleOCR，当前我们的 stub 还没有接入真正的 OCR 引擎。如果暂时不需要，跳过本节，需要时再接入。

---

## 3.7 用 SetTaskParams 动态调整阈值

运行时可以通过 `AsstSetTaskParams` 动态修改识别参数，不需要重启：

```cpp
// 连接后、start 前降低阈值
AsstAppendTask(ptr, "Homepage", "{\"enable\":true}");
// 把第一个任务的阈值调低
AsstSetTaskParams(ptr, 1, "{\"templThreshold\":0.6}");
AsstStart(ptr);
```

这在调试时非常方便——跑一次程序，改 JSON 参数，再跑，不用重新编译。

---

## 3.8 调试——查看匹配过程图

当 `ASST_DEBUG` 宏开启时，`VisionHelper::save_img()` 会在 `debug/` 目录输出画有匹配框的图片。在 CMake 中添加：

```powershell
cmake -S . -B build -G "Visual Studio 18 2026" -A x64 \
  -DMAADEPS_TRIPLET=maa-x64-windows -DBUILD_DEBUG_DEMO=ON \
  -DCMAKE_CXX_FLAGS="/DASST_DEBUG"
```

编译后，每次匹配失败或成功都会在 `build\bin\Release\debug\` 目录留下截图，打开能看到匹配框具体画在哪个位置。

---

## 3.9 里程碑总结

| 序号 | 验证点 | 通过标准 |
|------|--------|----------|
| 1 | ClickSelf 点击 | 日志出现 `ClickSelf (x, y)`，模拟器上按钮被点击 |
| 2 | 多步流程 | 主页→进背包→识别背包页→返回→回到主页，日志全绿 |
| 3 | 参数传递 | `AsstSetTaskParams` 或 `AsstAppendTask` 传参生效 |
| 4 | 自循环 `#self` | 等待一个会消失的动画，动画结束后自动进入下一步 |

**完成后进入第四阶段**：多任务组合（PackageTask 串联多个 InterfaceTask）、C++ 自定义 Plugin（继承 AbstractTaskPlugin）。
