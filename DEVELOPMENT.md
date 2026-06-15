# Maa4Pvz 开发路线

基于 MAA 框架搭建新游戏自动化的分阶段开发指南。

## 第一阶段：让程序跑起来

**目标**：框架能编译通过，能连接设备，能截图/点击。

### 要解决的问题

1. **确定目标平台**
   - 只有 Android 模拟器 → 只需 ADB 链路
   - 需要 Windows 原生窗口 → 加载 Win32Controller
   - 多平台 → 全保留

2. **编译通过**
   - 需搭建依赖：MaaUtils (submodule)、OpenCV、ONNXRuntime、Boost
   - 修改 `CMakeLists.txt` 移除不需要的 option（如 `BUILD_WPF_GUI`）

3. **验证连通性**
   - 写一个最简单的 demo，参考 `src/Cpp/main.cpp` 或 `src/Python/sample.py`
   - 调用 `AsstCreate()` → `AsstAsyncConnect()` → `AsstAsyncScreencap()` → 拿到截图

**里程碑**：能用代码截到游戏画面。

## 第二阶段：建立游戏识别基线

**目标**：能识别游戏中的关键 UI 元素。

### 要解决的问题

1. **理解 OCR 识别流程**
   - 框架入口：`ProcessTask` 读取 JSON 任务定义 → 调用 `AbstractTask` 的 `find_and_click()` 等基础方法
   - OCR：PaddleOCR（文字识别），模板匹配：OpenCV（图片匹配）
   - `Vision/Config/` 下的 `MatcherConfig`、`OCRerConfig` 管理识别参数

2. **采集样本**
   - 用 `tools/ImageCropper` 在游戏截图上框出要识别的区域
   - 导出为模板图片放到 `resource/template/`

3. **测试识别**
   - 用 `Vision/Matcher.cpp` + `Vision/Hasher.cpp` 做模板匹配
   - 先不用 OCR，只用模板匹配找到按钮就算成功

**里程碑**：能用模板匹配找到游戏主界面的一个按钮。

## 第三阶段：写第一个任务

**目标**：跑通 JSON 驱动的任务流程。

### 要解决的问题

1. **写第一个 tasks.json**
   - 在 `resource/tasks/` 下创建 JSON 配置文件
   - 最简单的例子 —— 进入界面然后点击按钮：

   ```json
   {
     "MyFirstTask": {
       "algorithm": "MatchTemplate",
       "template": "my_button.png",
       "action": "ClickSelf",
       "next": ["NextTask"]
     }
   }
   ```

   - `ProcessTask` 会自动解析 JSON 并执行

2. **注册任务入口**
   - 在 `Task/Interface/` 下创建 `InterfaceTask` 子类
   - 在 `AsstCaller.cpp` 中注册任务类型名称

3. **调试流程**
   - 使用 `Vision/DebugImageHelper.hpp` 绘制匹配结果辅助调试

**里程碑**：通过 JSON 配置能让程序自动点击游戏的一个按钮。

## 第四阶段：扩展游戏逻辑

**目标**：实现完整的游戏自动化循环。

### 要解决的问题

1. **按需创建 Task 子类**
   - 复杂逻辑（判断状态、循环等待）才需要写 C++ Task 子类
   - 继承 `AbstractTask` 或 `AbstractTaskPlugin`
   - 大部分简单流程直接用 JSON 配置就够了

2. **替换 ONNX 模型**
   - 需要检测特定物体 → 训练自己的 ONNX 模型
   - 不需要目标检测 → 直接删掉 `resource/onnx/` 并在 CMake 中移除相关链接

3. **配置管理**
   - 一键执行多个任务 → 用 `PackageTask` 组合子任务
   - 用户参数通过 JSON `params` 传入

## 优先级总览

| 优先级 | 事项 | 预计难度 |
|--------|------|----------|
| **P0** | 编译框架，连接设备，成功截图 | 中 |
| **P0** | ImageCropper 采集模板，跑通模板匹配 | 低 |
| **P1** | 写第一个 JSON 任务，跑通 ProcessTask | 低 |
| **P1** | 写 InterfaceTask 子类，注册任务入口 | 中 |
| **P2** | 扩展 OCR 文字识别 | 中 |
| **P2** | 多任务组合 + PackageTask | 低 |
| **P3** | 训练专属 ONNX 检测模型 | 高 |
| **P3** | WPF GUI / HTTP 服务 / 多语言绑定 | 中 |

## 避坑提醒

1. **不要急着写 C++ 代码**：MAA 80% 的任务逻辑在 JSON 里驱动，先把 JSON 流程跑通再考虑写代码
2. **模板采集要规范**：模板图片尺寸要和游戏实际渲染一致（注意 DPI 缩放），建议在固定分辨率下采集
3. **先做单分辨率**：Controller 有 `ControlScaleProxy` 做多分辨率缩放，初期先固定一种分辨率跑通
4. **OCR 不是银弹**：尽量用模板匹配代替 OCR，模板匹配更快更稳定；只有动态文字（如数值）才用 OCR
