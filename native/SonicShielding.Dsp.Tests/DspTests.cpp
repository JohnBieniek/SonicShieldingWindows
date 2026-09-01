#include "../SonicShielding.Dsp/SonicDsp.h"

#include <cmath>
#include <iostream>
#include <vector>

namespace
{
    constexpr float Pi = 3.14159265358979323846f;

    float MeasureGain(float frequency)
    {
        constexpr unsigned sampleRate = 48000;
        constexpr std::size_t frames = sampleRate * 2;
        std::vector<float> samples(frames);
        for (std::size_t i = 0; i < frames; ++i)
            samples[i] = std::sin(2.0f * Pi * frequency * static_cast<float>(i) / sampleRate);

        sonic::Dsp dsp;
        sonic::Profile profile;
        dsp.Configure(sampleRate, 1, profile);
        dsp.Process(samples.data(), samples.size());

        double squareSum = 0;
        for (std::size_t i = sampleRate; i < frames; ++i)
            squareSum += samples[i] * samples[i];
        return static_cast<float>(std::sqrt(squareSum / sampleRate) * std::sqrt(2.0));
    }
}

int main()
{
    const float low = MeasureGain(500);
    const float speech = MeasureGain(1500);
    const float high = MeasureGain(8000);
    const float veryHigh = MeasureGain(12000);

    std::cout << "500 Hz=" << low << " 1500 Hz=" << speech
              << " 8000 Hz=" << high << " 12000 Hz=" << veryHigh << '\n';

    if (low < 0.94f || speech < 0.78f || high > 0.08f || veryHigh > 0.03f)
    {
        std::cerr << "Frequency-selective attenuation is outside the required bounds.\n";
        return 1;
    }
    return 0;
}
