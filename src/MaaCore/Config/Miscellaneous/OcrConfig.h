#pragma once

#include "Config/AbstractResource.h"
#include "MaaUtils/SingletonHolder.hpp"
#include <vector>
#include <string>

namespace asst
{
class OcrConfig final : public MAA_NS::SingletonHolder<OcrConfig>, public AbstractResource
{
public:
    virtual ~OcrConfig() override = default;
    virtual bool load(const std::filesystem::path&) override { return true; }
    std::string process_equivalence_class(const std::string& str) const { return str; }
    std::vector<std::vector<std::string>> get_eq_classes() const { return {}; }
};
} // namespace asst
