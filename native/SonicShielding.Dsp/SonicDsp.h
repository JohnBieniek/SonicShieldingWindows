#pragma once

#include <array>
#include <cstddef>

namespace sonic
{
    constexpr std::size_t BandCount = 9;

    struct Profile
    {
        bool enabled = true;
        std::array<float, BandCount> reductionPercent{ 0, 0, 0, 0, 0, 0, 50, 90, 96 };
    };

    class Dsp
    {
    public:
        void Configure(unsigned sampleRate, unsigned channels, const Profile& profile) noexcept;
        void Reset() noexcept;
        void Process(float* interleavedSamples, std::size_t frameCount) noexcept;

    private:
        struct Biquad
        {
            float b0 = 1, b1 = 0, b2 = 0, a1 = 0, a2 = 0;
            std::array<float, 32> z1{}, z2{};
        };

        unsigned channels_ = 0;
        std::array<Biquad, BandCount> filters_{};
    };
}
