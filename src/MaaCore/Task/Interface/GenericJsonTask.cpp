#include "GenericJsonTask.h"
#include "Task/ProcessTask.h"

asst::GenericJsonTask::GenericJsonTask(const AsstCallback& callback, Assistant* inst, std::string_view task_chain)
    : InterfaceTask(callback, inst, TaskType)
{
    auto task_ptr = std::make_shared<ProcessTask>(callback, inst, TaskType);
    task_ptr->set_tasks({ std::string(task_chain) });
    m_subtasks.emplace_back(task_ptr);
}
