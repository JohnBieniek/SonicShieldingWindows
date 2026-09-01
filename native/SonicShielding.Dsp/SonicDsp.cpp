#include "SonicDsp.h"

#include <algorithm>
#include <cmath>

namespace
{
    constexpr std::array<float, sonic::BandCount> Frequencies{ 63, 125, 250, 500, 1000, 2000, 4000, 8000, 12000 };
    constexpr float Pi = 3.14159265358979323846f;
}

void sonic::Dsp::Configure(unsigned sampleRate, unsigned channels, const Profile& profile) noexcept
{
    channels_ = std::min(channels, 32u);
    for (std::size_t i = 0; i < filters_.size(); ++i)
    {
        auto& filter = filters_[i];
        filter = {};
        if (!profile.enabled || sampleRate == 0 || Frequencies[i] >= sampleRate * 0.48f)
            continue;

        const float reduction = std::clamp(profile.reductionPercent[i], 0.0f, 100.0f);
        const float gainDb = -48.0f * reduction / 100.0f;
        if (gainDb > -0.01f)
            continue;

        // RBJ constant-Q peaking filter. One filter per octave-wide comfort band;
        // unaffected bands retain unity gain rather than moving master volume.
        const float amplitude = std::pow(10.0f, gainDb / 40.0f);
        const float omega = 2.0f * Pi * Frequencies[i] / static_cast<float>(sampleRate);
        const float alpha = std::sin(omega) / (2.0f * 8.0f);
        const float a0 = 1.0f + alpha / amplitude;
        filter.b0 = (1.0f + alpha * amplitude) / a0;
        filter.b1 = (-2.0f * std::cos(omega)) / a0;
        filter.b2 = (1.0f - alpha * amplitude) / a0;
        filter.a1 = filter.b1;
        filter.a2 = (1.0f - alpha / amplitude) / a0;
    }
}

void sonic::Dsp::Reset() noexcept
{
    for (auto& filter : filters_)
    {
        filter.z1.fill(0);
        filter.z2.fill(0);
    }
}

void sonic::Dsp::Process(float* samples, std::size_t frameCount) noexcept
{
    if (samples == nullptr || channels_ == 0)
        return;

    for (std::size_t frame = 0; frame < frameCount; ++frame)
    {
        for (unsigned channel = 0; channel < channels_; ++channel)
        {
            float value = samples[frame * channels_ + channel];
            for (auto& filter : filters_)
            {
                const float output = filter.b0 * value + filter.z1[channel];
                filter.z1[channel] = filter.b1 * value - filter.a1 * output + filter.z2[channel];
                filter.z2[channel] = filter.b2 * value - filter.a2 * output;
                value = output;
            }
            samples[frame * channels_ + channel] = value;
        }
    }
}
