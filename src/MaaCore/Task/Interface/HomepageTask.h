#pragma once
#include "Task/InterfaceTask.h"

namespace asst
{
class ProcessTask;

class HomepageTask final : public InterfaceTask
{
public:
    inline static constexpr std::string_view TaskType = "Homepage";

    HomepageTask(const AsstCallback& callback, Assistant* inst);
    virtual ~HomepageTask() override = default;
};
}
