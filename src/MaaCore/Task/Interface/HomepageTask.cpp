#include "HomepageTask.h"
#include "Task/ProcessTask.h"

asst::HomepageTask::HomepageTask(const AsstCallback& callback, Assistant* inst)
    : InterfaceTask(callback, inst, TaskType)
{
    auto task_ptr = std::make_shared<ProcessTask>(callback, inst, TaskType);
    task_ptr->set_tasks({ "Homepage" });
    m_subtasks.emplace_back(task_ptr);
}
