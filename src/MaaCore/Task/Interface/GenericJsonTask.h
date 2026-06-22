#pragma once
#include "Task/InterfaceTask.h"

namespace asst
{
class ProcessTask;

// 纯 JSON 驱动任务，不需要写 C++ 子类
// 约定：任务类型名 XXX 对应 resource/tasks/xxx_tasks.json 中的 XXX@EntryPoint
class GenericJsonTask final : public InterfaceTask
{
public:
    inline static constexpr std::string_view TaskType = "JsonTask";

    GenericJsonTask(const AsstCallback& callback, Assistant* inst, std::string_view task_chain);
    virtual ~GenericJsonTask() override = default;
};
}
