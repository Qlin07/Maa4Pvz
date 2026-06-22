#pragma once
#include "Task/InterfaceTask.h"

namespace asst
{
class ProcessTask;

class OpenActivityTask final : public InterfaceTask
{
public:
    inline static constexpr std::string_view TaskType = "OpenActivity";

    OpenActivityTask(const AsstCallback& callback, Assistant* inst);
    virtual ~OpenActivityTask() override = default;
};
}
