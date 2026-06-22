#pragma once
#include "Task/InterfaceTask.h"

namespace asst
{
class OpenActivityTask;
class ProcessTask;

class FtCricketsTask final : public InterfaceTask
{
public:
    inline static constexpr std::string_view TaskType = "FtCrickets";

    FtCricketsTask(const AsstCallback& callback, Assistant* inst);
    virtual ~FtCricketsTask() override = default;
};
}
