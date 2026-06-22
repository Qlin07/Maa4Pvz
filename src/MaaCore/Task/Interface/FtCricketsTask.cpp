#include "FtCricketsTask.h"
#include "OpenActivityTask.h"
#include "Task/ProcessTask.h"

asst::FtCricketsTask::FtCricketsTask(const AsstCallback& callback, Assistant* inst)
    : InterfaceTask(callback, inst, TaskType)
{
    // 1. 复用 OpenActivity：从主页进入活动合集
    auto open_activity_ptr = std::make_shared<OpenActivityTask>(callback, inst);

    // 2. FtCrickets 自有流程：选关卡 → 匹配 → 战斗循环
    auto ft_crickets_ptr = std::make_shared<ProcessTask>(callback, inst, TaskType);
    ft_crickets_ptr->set_tasks({ "FtCrickets@SelectDaveCup" });

    m_subtasks.emplace_back(open_activity_ptr);
    m_subtasks.emplace_back(ft_crickets_ptr);
}
