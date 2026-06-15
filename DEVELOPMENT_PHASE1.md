# 第一阶段：编译框架 + 连接 Android 模拟器

> **目标**：在 Windows 上编译 `MaaCore.dll`，写一个最小 demo 连接模拟器并成功截图。

---

## 步骤 0：准备环境

确认你的机器上有：

| 工具 | 用途 | 安装方式 |
|------|------|----------|
| **Visual Studio 2022** | C++ 编译器（MSVC） | 安装时勾选"使用 C++ 的桌面开发" |
| **CMake 3.28+** | 构建系统 | VS 自带，或 https://cmake.org |
| **Git** | 获取 submodule | https://git-scm.com |
| **Python 3.x** | 运行辅助脚本 | https://python.org |
| **安卓模拟器** | 目标设备 | 推荐 Mumu / 蓝叠 / AVD |

---

## 步骤 1：拉取依赖 submodule

MaaUtils 包含 OpenCV、ONNXRuntime、Boost 等所有预编译依赖。在 `Maa4Pvz` 目录下运行：

```powershell
cd d:\project\maa\Maa4Pvz
git init
git submodule add https://github.com/MaaXYZ/MaaUtils src/MaaUtils
git submodule update --init --depth 1 src/MaaUtils
```

> `MaaUtils` 的 `MaaDeps/` 目录下按平台/架构存放了所有预编译好的 `.dll` 和 `.lib`。如果 `MaaDeps` 下没有 Windows x64 的内容，需要运行 `python tools/maadeps-download.py x64-windows`（tools 已删，需要从原仓库拷贝该脚本或用其他方式获取 MaaDeps）。

---

## 步骤 2：修复无法编译的源文件

### 2.1 ResourceLoader.cpp 是最大问题

`src/MaaCore/Config/ResourceLoader.cpp` 引用并加载了大量已删除的游戏专属配置类，**必须大幅裁剪**。

最简单的做法：把 `ResourceLoader::load()` 函数精简为只加载通用配置。

打开 `src/MaaCore/Config/ResourceLoader.cpp`，**删除所有 `#include` 行** 中引用 `Miscellaneous/`、`Roguelike/` 的语句，只保留：

```cpp
#include "ResourceLoader.h"
#include <filesystem>
#include "GeneralConfig.h"
#include "OnnxSessions.h"
#include "TaskData.h"
#include "TemplResource.h"
#include "Utils/Logger.hpp"
```

然后把 `load()` 函数体替换为只加载通用配置的精简版（见下方 §2.2）。

### 2.2 精简后的 ResourceLoader::load()

```cpp
bool asst::ResourceLoader::load(const std::filesystem::path& path)
{
    using namespace asst::utils::path_literals;

    if (!std::filesystem::exists(path)) {
        Log.error("Resource path not exists, path:", path);
        return false;
    }

    std::unique_lock<std::mutex> lock(m_entry_mutex);

    auto load_with_custom = [&]<typename T>(const std::filesystem::path& filename, const char* res_name) -> bool {
        auto full_path = path / filename;
        if (!load_resource<T>(full_path)) {
            Log.error(res_name, " load failed, path:", full_path);
            return false;
        }
        return true;
    };

    // GeneralConfig (必须有 config.json)
    if (!load_with_custom.template operator()<GeneralConfig>("config.json"_p, "GeneralConfig")) {
        return false;
    }

    // TaskData (如果有 tasks/ 目录才加载)
    if (std::filesystem::is_directory(path / "tasks"_p)) {
        if (!load_with_custom.template operator()<TaskData>("tasks"_p, "TaskData")) {
            return false;
        }
    }

    m_loaded = true;
    return m_loaded;
}
```

### 2.3 TaskData.cpp 可能需要调整

如果 `TaskData::parse()` 内部引用了已删除的模板匹配逻辑，暂时让加载函数返回 true（空实现）即可。

---

## 步骤 3：创建最小的 config.json

框架启动必须加载 `resource/config.json`。在 `resource/` 下创建：

```json
{
    "version": "0.0.1",
    "options": {
        "taskDelay": 0,
        "SSSFightScreencapInterval": 1000,
        "RoguelikeFightScreencapInterval": 1000,
        "CopilotFightScreencapInterval": 1000,
        "controlDelayRange": [0, 0],
        "adbExtraSwipeDist": 200,
        "adbExtraSwipeDuration": 100,
        "adbSwipeDurationMultiplier": 10.0,
        "adbSwipeXDistanceMultiplier": 0.8,
        "minitouchExtraSwipeDist": 200,
        "minitouchExtraSwipeDuration": 100,
        "minitouchSwipeDefaultDuration": 200,
        "minitouchSwipeExtraEndDelay": 100,
        "swipeWithPauseRequiredDistance": 20,
        "minitouchProgramsOrder": [
            "minitouch",
            "MaaTouch",
            "minitouch-x86_64",
            "MaaTouch-x86_64"
        ],
        "debug": {
            "cleanFilesFreq": 50,
            "maxDebugFileNum": 100
        }
    },
    "packageName": {
        "Official": "com.hypergryph.arknights"
    },
    "connection": [
        {
            "baseConfig": "General",
            "$devices": "adb devices",
            "$address_regex": ".*",
            "$connect": "adb connect [AdbSerial]",
            "$display_id": "(([0-9]+):.*|.*)",
            "$uuid": "adb -s [AdbSerial] shell settings get secure android_id",
            "$click": "adb -s [AdbSerial] shell input tap [x] [y]",
            "$input": "adb -s [AdbSerial] shell input text [text]",
            "$swipe": "adb -s [AdbSerial] shell input touchscreen swipe [x1] [y1] [x2] [y2] [duration]",
            "$press_esc": "adb -s [AdbSerial] shell input keyevent 111",
            "$display": "adb -s [AdbSerial] shell wm size",
            "$screencap_raw_with_gzip": "adb -s [AdbSerial] exec-out screencap | gzip -1",
            "$screencap_raw_by_nc": "",
            "$nc_address": "adb -s [AdbSerial] shell ip route | awk '{print $NF; exit}'",
            "$screencap_encode": "adb -s [AdbSerial] exec-out screencap -p",
            "$release": "adb -s [AdbSerial] shell input keyevent KEYCODE_HOME && adb -s [AdbSerial] shell input keyevent KEYCODE_HOME",
            "$start": "adb -s [AdbSerial] shell monkey -p [PackageName] 1",
            "$stop": "adb -s [AdbSerial] shell am force-stop [PackageName]",
            "$abilist": "adb -s [AdbSerial] shell getprop ro.product.cpu.abilist",
            "$version": "adb -s [AdbSerial] shell getprop ro.build.version.sdk",
            "$orientation": "adb -s [AdbSerial] shell dumpsys input | grep SurfaceOrientation | awk '{print $2}'",
            "$push_minitouch": "adb -s [AdbSerial] push [minitouchLocalPath] [minitouchAndroidPath]",
            "$chmod_minitouch": "adb -s [AdbSerial] shell chmod 700 [minitouchAndroidPath]",
            "$call_minitouch": "adb -s [AdbSerial] shell [minitouchAndroidPath] -i",
            "$call_maatouch": "adb -s [AdbSerial] shell [minitouchAndroidPath]",
            "$event_id": "adb -s [AdbSerial] shell getevent -p | grep -B 100 ABS_MT_POSITION_X | grep -m1 -o 'event[0-9]*'",
            "$back_to_home": "adb -s [AdbSerial] shell input keyevent 3",
            "extras": {}
        }
    ]
}
```

> 上面的 `connection[0]` 就是 ADB 命令的模板配置。`[AdbSerial]`、`[x]`、`[y]` 等是运行时由代码替换的占位符。你可以把 `packageName.Official` 的值改成你目标游戏的包名。

---

## 步骤 4：修改 CMakeLists.txt

### 4.1 顶层 CMakeLists.txt

编辑 `Maa4Pvz/CMakeLists.txt`，删除或设为 OFF 所有不需要的 option：

```cmake
cmake_minimum_required(VERSION 3.28)

project(MAA)

option(BUILD_WPF_GUI "build MaaWpfGui" OFF)
option(BUILD_DEBUG_DEMO "build debug demo" ON)    # 改为 ON
option(BUILD_XCFRAMEWORK "build xcframework for macOS app" OFF)
option(BUILD_SMOKE_TEST "build smoke_test" OFF)
option(INSTALL_PYTHON "install python ffi" OFF)
option(INSTALL_RESOURCE "install resource" ON)      # 改为 ON
option(INSTALL_FLATTEN "do not use bin lib include directory" ON)
option(WITH_EMULATOR_EXTRAS "build with emulator extras" ON)  # Mumu/雷电额外截图支持
option(WITH_MAC_SCK "build with macOS ScreenCaptureKit" OFF)
option(WITH_HASH_VERSION "generate version from git hash" OFF)
option(BUILD_RESOURCE_UPDATER "build resource updater tool" OFF)

# ... 其余行保持不变 ...
```

删掉 `BUILD_WPF_GUI` 整个 if-block（那个 block 引用了 `MaaWpfGui.csproj` 和 `MaaUpdater`）。

### 4.2 MaaCore 的 CMakeLists.txt

编辑 `src/MaaCore/CMakeLists.txt`，删除末尾的 tasks 资源 glob 部分（大约第 70 行到 120 行之间，以 `# 收集resource/tasks目录下的所有文件作为资源` 开头的整段），因为那些目录已被删除。

---

## 步骤 5：初始化 submodule 拉取 MaaUtils

```powershell
cd d:\project\maa\Maa4Pvz
git submodule update --init --depth 1 src/MaaUtils
```

MaaUtils 自带 `MaaUtils.cmake`，会自动帮你下载/配置 OpenCV、ONNXRuntime、Boost、zlib 等依赖。首次构建时会自动触发下载。

---

## 步骤 6：编写最小测试程序

在 `src/Cpp/main.cpp` 中替换为最简单的 ADB 连接测试：

```cpp
#include "AsstCaller.h"
#include <filesystem>
#include <iostream>
#include <thread>

int main(int argc, char** argv)
{
    auto working_path = std::filesystem::path(argv[0]).parent_path();

    // 1. 加载资源（必须，至少要有 config.json）
    if (!AsstLoadResource(working_path.string().c_str())) {
        std::cerr << "load resource failed" << std::endl;
        return -1;
    }

    // 2. 创建 MAA 实例
    auto ptr = AsstCreate();
    if (ptr == nullptr) {
        std::cerr << "create failed" << std::endl;
        return -1;
    }

    // 连接模拟器
    //    adb_path: D:\Program Files\Netease\MuMu Player 12\shell\adb.exe (MuMu Player 12)
    //    address:  127.0.0.1:7555 (MuMu 默认)
    AsstAsyncConnect(
        ptr,
        "D:\\Program Files\\Netease\\MuMu Player 12\\shell\\adb.exe",
        "127.0.0.1:7555",
        nullptr,
        true);

    // 4. 检查连接状态
    if (!AsstConnected(ptr)) {
        std::cerr << "connect failed, check ADB and address" << std::endl;
        AsstDestroy(ptr);
        return -1;
    }
    std::cout << "=== Connected! ===" << std::endl;

    // 5. 截图
    AsstAsyncScreencap(ptr, true);   // block=true 等待截图完成
    auto size = AsstGetImageBgr(ptr, nullptr, 0);
    if (size > 0) {
        std::cout << "Screencap success! Image size: " << size << " bytes" << std::endl;
    } else {
        std::cerr << "Screencap failed" << std::endl;
    }

    // 6. 清理
    AsstDestroy(ptr);
    return 0;
}
```

---

## 步骤 7：编译

```powershell
cd d:\project\maa\Maa4Pvz

# 配置（仅 x64）
cmake -B build --preset windows-publish-x64 \
    -DMAA_HASH_VERSION="0.0.1"

# 编译
cmake --build --preset windows-publish-x64 --parallel
```

编译产物在 `build/bin/RelWithDebInfo/` 下，包含 `MaaCore.dll` 和所有依赖 DLL。

---

## 步骤 8：运行测试

```powershell
# 先确认模拟器已开启，adb 能连上
adb devices
# 应该看到类似: 127.0.0.1:7555  device

# 把 resource/ 复制到输出目录
cp -r resource build/bin/RelWithDebInfo/

# 运行
.\build\bin\RelWithDebInfo\MAA.exe
```

---

## 常见问题排查

| 现象 | 可能原因 | 解决 |
|------|----------|------|
| `load resource failed` | `config.json` 不存在或路径不对 | 确认 exe 同目录下有 `resource/config.json` |
| `connect failed` | ADB 地址不对 | MuMu Player 12 默认 127.0.0.1:7555；可在模拟器设置中确认 |
| `connect failed` | adb 路径不对 | 检查 MuMu 安装路径，adb.exe 在 `shell\adb.exe` 子目录下 |
| 截图黑屏 | 模拟器渲染引擎不兼容 | 切换模拟器渲染模式（DirectX → OpenGL） |
| 编译找不到 MaaUtils | submodule 未初始化 | `git submodule update --init src/MaaUtils` |

---

## 完成标志

看到以下输出即为成功：

```
=== Connected! ===
Screencap success! Image size: xxxxxx bytes
```

拿到游戏画面后就可以进入**第二阶段**：用模板匹配识别 UI 元素了。
