#pragma once

#include "Config/AbstractResource.h"
#include "MaaUtils/SingletonHolder.hpp"
#include "Common/AsstTypes.h"
#include <string>
#include <vector>
#include <opencv2/core.hpp>

namespace asst
{
class OcrPack : public AbstractResource
{
public:
    struct Result : public TextRect
    {
        using TextRect::TextRect;
    };
    using ResultsVec = std::vector<Result>;

    virtual ~OcrPack() override = default;
    virtual bool load(const std::filesystem::path&) override { return true; }
    virtual ResultsVec recognize(const cv::Mat&, bool, const Rect&) { return {}; }
};

class WordOcr final : public MAA_NS::SingletonHolder<WordOcr>, public OcrPack
{
public:
    ResultsVec recognize(const cv::Mat&, bool, const Rect&) override { return {}; }
    void use_cpu() {}
    void use_gpu(int) {}
};

class CharOcr final : public MAA_NS::SingletonHolder<CharOcr>, public OcrPack
{
public:
    ResultsVec recognize(const cv::Mat&, bool, const Rect&) override { return {}; }
    void use_cpu() {}
    void use_gpu(int) {}
};
} // namespace asst
