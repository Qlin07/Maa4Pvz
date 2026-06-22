# 第四阶段：扩展游戏逻辑

> 参考原项目 Maa4Arknights 的技术架构，逐步扩展 PvZ 的自动化能力。

**前提**：第三阶段已完成——JSON 驱动的任务流程跑通，HomepageTask 能自动点击进入子页面并返回。

**目标**：实现完整的游戏自动化循环——多个独立任务协同工作、复杂逻辑用 C++ Task 子类实现、Plugin 机制扩展行为、多任务组合为 PackageTask、用户参数可配置。

---

## 4.1 理解任务层次结构

原项目的类继承关系：

```
AbstractTask                   （基类：提供 run/retry/callback/plugin 机制）
├── ProcessTask                （JSON 驱动的任务引擎，匹配→执行→跳转）
├── AbstractTaskPlugin         （Plugin 基类：通过 verify() 钩入消息流）
├── PackageTask                （串联多个子任务，顺序执行）
│   └── InterfaceTask          （对外暴露的任务入口，支持 set_params）
│       ├── HomepageTask       （当前已实现）
│       ├── FightTask          （原项目参考）
│       ├── StartUpTask        （原项目参考）
│       └── ...                （更多 InterfaceTask）
└── Misc/Custom Tasks          （纯 C++ 逻辑任务）
```

**关键设计原则**（从原项目提炼）：
- 80% 的逻辑在 JSON 中驱动，只有复杂判断才写 C++
- InterfaceTask 是用户通过 `AsstAppendTask()` 调用的入口
- PackageTask 内部组合多个 ProcessTask，ProcessTask 又由 JSON 任务链驱动
- Plugin 通过 `verify()` 响应消息（如 SubTaskStart、SubTaskCompleted），在特定时机插入额外逻辑

---

## 4.2 创建多个 InterfaceTask（多任务入口）

目前只有一个 `HomepageTask`。需要创建更多独立任务，覆盖 PvZ 的核心自动化功能。

### 要解决的问题

1. **按需创建任务**

   不预先规划任务体系。后续开发中，由使用者指定：
   - 创建一个名为 XXX 的任务
   - 描述该任务的业务逻辑
   - 先逐个实现单个功能，等功能稳定后再组合为一键执行的复合任务
        参考原项目的任务划分（FightTask / InfrastTask / AwardTask 等），为 PvZ 规划：

            | TaskType | 名称 | 功能 | 参考原项目 |
            |----------|------|------|-----------|
            | `Homepage` | 主页任务 | 启动游戏回到主页（已有） | StartUpTask |
            | `Backpack` | 背包任务 | 进入背包管理道具/植物 | InfrastTask |
            | `Adventure` | 冒险模式 | 选关卡→战斗→结算 | FightTask |
            | `DailyReward` | 每日奖励 | 领取签到/任务奖励 | AwardTask |
            | `Shop` | 商店 | 进入商店购买物品 | MallTask |
            | `DailyRoutine` | 一键日常 | 组合以上多个任务 | （组合类） |
2. **每个 InterfaceTask 的标准结构**

   参考原项目 `FightTask` 和 `AwardTask` 的模式：
   - 构造函数中创建多个 ProcessTask 子任务，压入 `m_subtasks`
   - 每个 ProcessTask 通过 `set_tasks()` 绑定 JSON 任务链入口名
   - `set_params()` 从用户 JSON 参数中读取配置，控制子任务的 enable/times

3. **注册到 Assistant.cpp**

   当前用 if-else 注册，改为原项目的宏展开模式 `ASST_ASSISTANT_APPEND_TASK_FROM_STRING_IF_BRANCH`，后续新增任务只需插入一行宏调用

4. **为每个任务编写对应的 JSON 任务链**

   在 `resource/tasks/` 下按模块创建 JSON 文件（如 `backpack_tasks.json`、`adventure_tasks.json`）
   - 初期全部用 `"algorithm": "JustReturn"` 占位
   - 后续用 ImageCropper 采集模板后逐步替换为 `"MatchTemplate"` / `"OcrDetect"`

5. **更新 CMakeLists.txt**

   新增的 cpp/h 文件需要加入构建，用 `file(GLOB_RECURSE)` 自动收集 `Task/Interface/` 下的源文件

**里程碑**：通过 `AsstAppendTask("Backpack", ...)` / `AsstAppendTask("Adventure", ...)` 能独立运行多个不同任务。

---

## 4.3 按需创建 Task 子类（复杂逻辑）

大部分逻辑直接用 JSON 即可。但以下场景需要写 C++ Task 子类：

| 场景 | 为什么 JSON 不够 | 原项目参考 |
|------|-----------------|-----------|
| 条件判断（if/else） | JSON 只有顺序/重试，无分支 | StageNavigationTask |
| 循环等待（while） | JSON 的 `#self` 只能等匹配消失 | ReclamationCraftTask |
| 数据解析 | 需要 OCR 结果做计算 | DepotRecognitionTask |
| 跨任务状态共享 | 需要在多个子任务间传递数据 | RoguelikeConfig |

### 要解决的问题

1. **确定哪些逻辑需要写 C++**

   先用 JSON 把所有流程走一遍。凡是"走到这步需要先判断一下再决定下一步"的地方，才考虑抽成 C++ Task 子类

2. **自定义 Task 子类的标准结构**

   - 继承 `AbstractTask`
   - 实现 `_run()` 方法，内部可调用 `ctrler()` 截图/点击，或组合 ProcessTask
   - 通过 `set_params()` 从 InterfaceTask 接收参数

3. **创建 `Task/Adventure/` 等子目录**

   按游戏功能模块组织自定义 Task 子类，如：
   - `Task/Adventure/LevelSelectTask` — 根据 OCR 识别的关卡名选择不同关卡
   - `Task/Battle/AutoPlantLogic` — 战斗中根据状态判断种植策略

4. **在 InterfaceTask 中集成自定义 Task**

   将自定义 Task 作为子任务压入 `m_subtasks`，与 ProcessTask 混合编排

**里程碑**：能用 C++ Task 子类实现一个需要条件判断的自动化流程（如根据 OCR 识别的数值决定操作）。

---

## 4.4 创建 Task Plugin（通过消息钩子扩展行为）

Plugin 的核心机制（已在 `AbstractTask.cpp` 的 `callback()` 中实现）：
- ProcessTask 每次执行子任务时会调用 `callback(AsstMsg::SubTaskStart/SubTaskCompleted, ...)`
- 遍历已注册的 Plugin，调用 `verify(msg, details)` 判断是否该介入
- `verify()` 返回 true 则执行 Plugin 的 `run()`

### 要解决的问题

1. **规划 PvZ 的 Plugin 体系**

   | Plugin | 触发时机 | 功能 | 原项目参考 |
   |--------|---------|------|-----------|
   | AutoPlantPlugin | 战斗子任务开始时 | 检测可种植位置并自动种植 | RoguelikeBattleTaskPlugin |
   | BattleResultPlugin | 结算子任务完成时 | 识别胜败并回调 | StageDropsTaskPlugin |
   | SunshineCounterPlugin | 战斗中持续触发 | OCR 识别阳光数量 | MedicineCounterTaskPlugin |

2. **Plugin 的 `verify()` 要精确**

   `verify()` 会被每个 `callback()` 调用，只做简单的条件判断（msg 类型 + 任务名），复杂逻辑放到 `_run()` 里

3. **Plugin 的注册方式**

   在 InterfaceTask 的构造函数中，通过 `process_task_ptr->register_plugin<XxxPlugin>()` 注册
   - 参考原项目 `FightTask` 中 `m_fight_task_ptr->register_plugin<StageDropsTaskPlugin>()` 的模式

4. **Plugin 的优先级和阻断**

   - `priority()` 控制多个 Plugin 的执行顺序
   - `block()` 为 true 时，执行完该 Plugin 后不再执行后续 Plugin

**里程碑**：Plugin 的 `verify()` + `_run()` 能在正确的时机执行，实现战斗中的自动行为。

---

## 4.5 多任务组合——PackageTask

原项目中 `PackageTask` 的作用：将多个子任务（InterfaceTask 或 ProcessTask）组合起来顺序执行。

### 要解决的问题

1. **通过 `AsstAppendTask` 添加多个独立任务**

   最简单的方式——添加多个任务，框架自动排队执行，不需要额外代码

2. **创建组合 InterfaceTask——DailyRoutineTask**

   参考原项目中无直接对应但概念类似的做法：创建一个 InterfaceTask 内部组合多个其他 InterfaceTask
   - 内部持有 `DailyRewardTask`、`BackpackTask`、`AdventureTask` 的共享指针
   - `set_params()` 接收嵌套 JSON，分别透传给各子任务
   - 用户只需调用 `AsstAppendTask("DailyRoutine", ...)` 一键完成所有日常

3. **子任务的 enable/disable 控制**

   通过 `set_params()` 中的布尔参数控制哪些子任务跳过（如只领奖励不打关卡）
   - 参考原项目 `AwardTask::set_params()` 分别控制 award/mail/recruit 的 enable

**里程碑**：`AsstAppendTask("DailyRoutine", ...)` 能按顺序执行领奖→整理背包→打关卡。

---

## 4.6 ONNX 模型管理

当前 `resource/onnx/` 下有 3 个模型：`deploy_direction_cls.onnx`（PaddleOCR 方向分类）、`operators_det.onnx`、`skill_ready_cls.onnx`。

### 要解决的问题

1. **不需要目标检测 → 裁剪**

   删除 `operators_det.onnx` 和 `skill_ready_cls.onnx`，保留 `deploy_direction_cls.onnx`（OCR 需要）
   - 在 `ResourceLoader.cpp` 中确认只加载需要的模型
   - 在 `CMakeLists.txt` 中移除不必要的 ONNX 链接（如有）

2. **需要自定义检测 → 训练替换**

   如果 PvZ 需要检测随机出现的游戏对象（僵尸类型等）：
   - 采集数据 → 标注 → 训练 YOLOv8/RT-DETR → 导出 ONNX
   - 放入 `resource/onnx/`，在 JSON 任务中用 `"algorithm": "Detect"` 引用

3. **建议**

   初期不急着训练模型。PvZ 的 UI 元素相对固定，模板匹配 + OCR 通常够用。只在确实需要检测随机对象时才引入 ONNX

---

## 4.7 参数传递与配置管理

### 要解决的问题

1. **InterfaceTask 的 `set_params()` 标准写法**

   参考原项目 `FightTask::set_params()` 和 `AwardTask::set_params()` 的模式：
   - 从 JSON 读取参数并给默认值
   - 应用到子任务（set_enable / set_times_limit）
   - 返回 true/false 表示参数是否合法

2. **运行时动态修改参数**

   通过 `AsstSetTaskParams(task_id, json)` 可在运行中修改参数，不需要重启任务

3. **回调消息处理**

   通过 `AsstSetCallback` 接收任务状态：
   - `SubTaskStart` / `SubTaskCompleted` / `SubTaskError` — 标准生命周期消息
   - `SubTaskExtraInfo` — 自定义消息（如 Plugin 回调的战斗结果）

---

## 4.8 采集模板——按需扩展

随着任务增加，需要更多模板图片。参考第二阶段的 ImageCropper 流程。

| 任务 | 需要的模板 | 目录 |
|------|-----------|------|
| Adventure | 关卡选择界面特征、开始按钮、胜利/失败标志 | `resource/template/adventure/` |
| DailyReward | 签到按钮、任务领取按钮、已完成标志 | `resource/template/daily_reward/` |
| Shop | 商店入口、商品图标、购买确认按钮 | `resource/template/shop/` |
| Battle | 可种植位置标记、阳光图标 | `resource/template/battle/` |

---

## 优先级总览

| 优先级 | 事项 | 参考原项目 | 预计难度 |
|--------|------|-----------|----------|
| **P0** | 创建 BackpackTask + 对应 JSON 任务链 | InfrastTask | 低 |
| **P0** | 注册新任务到 Assistant.cpp（改用宏模式） | Assistant.cpp 宏展开 | 低 |
| **P1** | 创建 AdventureTask（选关→战斗→结算） | FightTask | 中 |
| **P1** | 创建 DailyRewardTask | AwardTask | 低 |
| **P1** | 创建 DailyRoutineTask（组合任务） | PackageTask 组合模式 | 低 |
| **P2** | 创建自定义 Task 子类（条件判断逻辑） | StageNavigationTask | 中 |
| **P2** | 创建 Plugin（战斗中的自动行为） | RoguelikeBattleTaskPlugin | 中 |
| **P2** | 参数传递 + set_params 完善 | FightTask::set_params | 低 |
| **P3** | 裁剪/替换 ONNX 模型 | OnnxSessions | 低 |
| **P3** | 训练自定义 ONNX 检测模型 | — | 高 |

## 避坑提醒

1. **不要过度使用 C++ Task 子类**：原项目经验表明，JSON 驱动的 ProcessTask 能覆盖 80% 场景。只有需要条件判断、数据解析、复杂状态机时才写 C++
2. **Plugin 的 verify() 要轻量**：`verify()` 会被每个 `callback()` 调用，不要在里面做耗时操作，只做简单的条件判断
3. **模板命名规范**：参考原项目的 `@` 分隔符（如 `MiniGame@ALL@GreenGrass`），用 `模块@子模块@动作` 的层次命名，便于 `times_limit` 控制
4. **继承 vs 组合**：`baseTask` 继承用于同一 UI 元素的不同皮肤，不要用它来复用逻辑——逻辑复用用 Plugin 或子任务组合
5. **测试顺序**：先用 `JustReturn` + `DoNothing` 把任务链跑通（只打印日志），再逐步替换为真正的识别算法
