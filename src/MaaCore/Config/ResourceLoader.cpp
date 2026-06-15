#include "ResourceLoader.h"

#include <filesystem>

#include "GeneralConfig.h"
#include "OnnxSessions.h"
#include "TaskData.h"
#include "TemplResource.h"
#include "Utils/Logger.hpp"

asst::ResourceLoader::ResourceLoader()
{
    m_load_thread = std::thread(&ResourceLoader::load_thread_func, this);
}

void asst::ResourceLoader::load_thread_func()
{
    while (!m_load_thread_exit) {
        std::unique_lock<std::mutex> lock(m_load_mutex);

        if (m_load_queue.empty()) {
            m_load_cv.wait(lock);
            continue;
        }

        auto [res_ptr, path] = std::move(m_load_queue.front());
        m_load_queue.pop_front();
        lock.unlock();

        res_ptr->load(path);
    }
}

void asst::ResourceLoader::add_load_queue(AbstractResource& res, const std::filesystem::path& path)
{
    if (!std::filesystem::exists(path)) {
        return;
    }

    std::unique_lock<std::mutex> lock(m_load_mutex);
    m_load_queue.emplace_back(&res, path);
    m_load_cv.notify_all();
}

void asst::ResourceLoader::cancel()
{
    m_load_thread_exit = true;

    {
        std::unique_lock<std::mutex> lock(m_load_mutex);
        m_load_cv.notify_all();
    }

    if (m_load_thread.joinable()) {
        m_load_thread.join();
    }
}

asst::ResourceLoader::~ResourceLoader()
{
    cancel();
}

bool asst::ResourceLoader::load(const std::filesystem::path& path)
{
    using namespace asst::utils::path_literals;

    if (!std::filesystem::exists(path)) {
        Log.error("Resource path not exists, path:", path);
        return false;
    }

    std::unique_lock<std::mutex> lock(m_entry_mutex);
    LogTraceFunction;

    // 加载资源（可选 _custom.json）
    auto load_with_custom = [&]<typename T>(const std::filesystem::path& filename, const char* res_name) -> bool {
        auto full_path = path / filename;
        if (!std::filesystem::exists(full_path)) {
            Log.info(res_name, "not found, skipping:", full_path);
            return true;
        }
        if (!load_resource<T>(full_path)) {
            Log.error(res_name, "load failed, path:", full_path);
            return false;
        }
        auto custom_path = path / (full_path.stem().string() + "_custom.json");
        if (std::filesystem::exists(custom_path)) {
            Log.info("Loading custom file for ", res_name, ", path:", custom_path);
            if (!load_resource<T>(custom_path)) {
                Log.error(res_name, "load failed, path:", custom_path);
                return false;
            }
        }
        return true;
    };

    // ==================== 核心配置 ====================
    if (!load_with_custom.template operator()<GeneralConfig>("config.json"_p, "GeneralConfig")) {
        return false;
    }

    // ==================== 任务配置（先 tasks，再 templates） ====================
    if (std::filesystem::is_directory(path / "tasks"_p)) {
        if (!load_with_custom.template operator()<TaskData>("tasks"_p, "TaskData")) {
            return false;
        }
        if (std::filesystem::is_directory(path / "template"_p)) {
            auto& templ_ins = TemplResource::get_instance();
            templ_ins.set_load_required(TaskData::get_instance().get_templ_required());
            if (!templ_ins.load(path / "template"_p)) {
                return false;
            }
        }
    }

    m_loaded = true;
    Log.info(__FUNCTION__, "ret", m_loaded);
    return m_loaded;
}

void asst::ResourceLoader::set_connection_extras(const std::string& name, const json::object& diff)
{
    GeneralConfig::get_instance().set_connection_extras(name, diff);
}

bool asst::ResourceLoader::loaded() const noexcept
{
    return m_loaded;
}
