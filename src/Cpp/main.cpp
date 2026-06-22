#include "AsstCaller.h"

#include <filesystem>
#include <iostream>
#include <thread>

int main(int argc, char** argv)
{
    auto working_path = std::filesystem::path(argv[0]).parent_path();

    if (!std::filesystem::exists(working_path / "resource")) {
        std::cerr << "resource folder not found!" << std::endl;
        return -1;
    }

    if (!AsstLoadResource(working_path.string().c_str())) {
        std::cerr << "load resource failed" << std::endl;
        return -1;
    }

    auto ptr = AsstCreate();
    if (ptr == nullptr) {
        std::cerr << "create failed" << std::endl;
        return -1;
    }

    AsstAsyncConnect(
        ptr,
        "D:\\Program Files\\Netease\\MuMu Player 12\\shell\\adb.exe",
        "127.0.0.1:7555",
        nullptr,
        true);

    if (!AsstConnected(ptr)) {
        std::cerr << "connect failed, check ADB path and emulator address" << std::endl;
        AsstDestroy(ptr);
        return -1;
    }
    std::cout << "=== Connected! ===" << std::endl;

    AsstAppendTask(ptr, "FtCrickets", nullptr);
    AsstStart(ptr);
    while (AsstRunning(ptr)) {
        std::this_thread::yield();
    }

    AsstDestroy(ptr);
    return 0;
}
