#include <windows.h>
#include <audioenginebaseapo.h>
#include <baseaudioprocessingobject.h>
#include <audiomediatype.h>
#include <ksmedia.h>
#include <atomic>
#include <cstring>
#include <new>

#include "../SonicShielding.Dsp/SonicDsp.h"

// {4DF8E93B-E1F7-4FC9-87D1-39F086704ECD}
static constexpr CLSID CLSID_SonicShieldingApo =
{ 0x4df8e93b, 0xe1f7, 0x4fc9, { 0x87, 0xd1, 0x39, 0xf0, 0x86, 0x70, 0x4e, 0xcd } };

static std::atomic<long> g_objects{ 0 };

class SonicApo final : public IAudioProcessingObject,
                       public IAudioProcessingObjectRT,
                       public IAudioProcessingObjectConfiguration
{
public:
    SonicApo() { ++g_objects; }

    ~SonicApo()
    {
        --g_objects;
    }

    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid, void** result) override
    {
        if (result == nullptr) return E_POINTER;
        *result = nullptr;
        if (iid == IID_IUnknown || iid == __uuidof(IAudioProcessingObject))
            *result = static_cast<IAudioProcessingObject*>(this);
        else if (iid == __uuidof(IAudioProcessingObjectRT))
            *result = static_cast<IAudioProcessingObjectRT*>(this);
        else if (iid == __uuidof(IAudioProcessingObjectConfiguration))
            *result = static_cast<IAudioProcessingObjectConfiguration*>(this);
        else
            return E_NOINTERFACE;
        AddRef();
        return S_OK;
    }

    ULONG STDMETHODCALLTYPE AddRef() override { return ++references_; }
    ULONG STDMETHODCALLTYPE Release() override
    {
        const ULONG remaining = --references_;
        if (remaining == 0) delete this;
        return remaining;
    }

    HRESULT STDMETHODCALLTYPE Initialize(UINT32 size, BYTE* data) override
    {
        if ((size == 0) != (data == nullptr)) return E_INVALIDARG;
        if (initialized_) return APOERR_ALREADY_INITIALIZED;
        initialized_ = true;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE Reset() override { dsp_.Reset(); return S_OK; }
    HRESULT STDMETHODCALLTYPE GetLatency(HNSTIME* latency) override
    {
        if (latency == nullptr) return E_POINTER;
        *latency = 0;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE GetRegistrationProperties(APO_REG_PROPERTIES** properties) override
    {
        if (properties == nullptr) return E_POINTER;
        *properties = static_cast<APO_REG_PROPERTIES*>(CoTaskMemAlloc(sizeof(registration_.m_Properties)));
        if (*properties == nullptr) return E_OUTOFMEMORY;
        std::memcpy(*properties, &registration_.m_Properties, sizeof(registration_.m_Properties));
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE IsInputFormatSupported(IAudioMediaType* opposite, IAudioMediaType* requested, IAudioMediaType** supported) override
    {
        return CheckFormat(opposite, requested, supported);
    }
    HRESULT STDMETHODCALLTYPE IsOutputFormatSupported(IAudioMediaType* opposite, IAudioMediaType* requested, IAudioMediaType** supported) override
    {
        return CheckFormat(opposite, requested, supported);
    }
    HRESULT STDMETHODCALLTYPE GetInputChannelCount(UINT32* channels) override
    {
        if (channels == nullptr) return E_POINTER;
        *channels = samplesPerFrame_;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE LockForProcess(
        UINT32 inputCount, APO_CONNECTION_DESCRIPTOR** inputs,
        UINT32 outputCount, APO_CONNECTION_DESCRIPTOR** outputs) override
    {
        if (inputCount != 1 || outputCount != 1 || inputs == nullptr || outputs == nullptr)
            return APOERR_NUM_CONNECTIONS_INVALID;
        if (inputs[0] == nullptr || outputs[0] == nullptr || inputs[0]->pFormat == nullptr)
            return E_POINTER;

        UNCOMPRESSEDAUDIOFORMAT format{};
        const HRESULT formatResult = inputs[0]->pFormat->GetUncompressedAudioFormat(&format);
        if (FAILED(formatResult))
        {
            return formatResult;
        }
        if (!IsFloatFormat(format)) return APOERR_FORMAT_NOT_SUPPORTED;
        samplesPerFrame_ = format.dwSamplesPerFrame;
        sonic::Profile profile;
        dsp_.Configure(static_cast<unsigned>(format.fFramesPerSecond), format.dwSamplesPerFrame, profile);
        locked_ = true;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE UnlockForProcess() override
    {
        dsp_.Reset();
        locked_ = false;
        return S_OK;
    }

    void STDMETHODCALLTYPE APOProcess(
        UINT32, APO_CONNECTION_PROPERTY** inputs,
        UINT32 outputCount, APO_CONNECTION_PROPERTY** outputs) override
    {
        auto* input = inputs[0];
        auto* output = outputs[0];
        const UINT32 frames = input->u32ValidFrameCount;

        if (input->u32BufferFlags == BUFFER_VALID)
        {
            const auto sampleCount = static_cast<std::size_t>(frames) * samplesPerFrame_;
            if (outputCount != 0 && output->pBuffer != input->pBuffer)
                std::memcpy(reinterpret_cast<void*>(output->pBuffer), reinterpret_cast<void*>(input->pBuffer), sampleCount * sizeof(float));
            dsp_.Process(reinterpret_cast<float*>(output->pBuffer), frames);
        }
        else if (input->u32BufferFlags == BUFFER_SILENT && outputCount != 0)
        {
            const auto sampleCount = static_cast<std::size_t>(frames) * samplesPerFrame_;
            std::memset(reinterpret_cast<void*>(output->pBuffer), 0, sampleCount * sizeof(float));
        }

        output->u32BufferFlags = input->u32BufferFlags;
        output->u32ValidFrameCount = frames;
    }

    static CRegAPOProperties<1> registration_;

    UINT32 STDMETHODCALLTYPE CalcInputFrames(UINT32 outputFrames) override { return outputFrames; }
    UINT32 STDMETHODCALLTYPE CalcOutputFrames(UINT32 inputFrames) override { return inputFrames; }

private:
    static bool IsFloatFormat(const UNCOMPRESSEDAUDIOFORMAT& format)
    {
        return format.guidFormatType == KSDATAFORMAT_SUBTYPE_IEEE_FLOAT &&
               format.dwBytesPerSampleContainer == sizeof(float) &&
               format.dwValidBitsPerSample == 32 &&
               format.dwSamplesPerFrame > 0 && format.dwSamplesPerFrame <= 32;
    }
    static HRESULT CheckFormat(IAudioMediaType* opposite, IAudioMediaType* requested, IAudioMediaType** supported)
    {
        if (supported != nullptr) *supported = nullptr;
        if (opposite == nullptr || requested == nullptr) return E_POINTER;
        UNCOMPRESSEDAUDIOFORMAT format{};
        const HRESULT result = requested->GetUncompressedAudioFormat(&format);
        if (FAILED(result)) return result;
        return IsFloatFormat(format) ? S_OK : APOERR_FORMAT_NOT_SUPPORTED;
    }
    std::atomic<ULONG> references_{ 1 };
    bool initialized_ = false;
    bool locked_ = false;
    UINT32 samplesPerFrame_ = 0;
    sonic::Dsp dsp_;
};

#pragma warning(push)
#pragma warning(disable: 4815)
CRegAPOProperties<1> SonicApo::registration_(
    CLSID_SonicShieldingApo,
    L"Sonic Shielding frequency filter",
    L"Copyright Sonic Shielding contributors",
    1, 0,
    __uuidof(IAudioProcessingObject),
    static_cast<APO_FLAG>(APO_FLAG_INPLACE | APO_FLAG_SAMPLESPERFRAME_MUST_MATCH |
                          APO_FLAG_FRAMESPERSECOND_MUST_MATCH | APO_FLAG_BITSPERSAMPLE_MUST_MATCH));
#pragma warning(pop)

class SonicClassFactory final : public IClassFactory
{
public:
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid, void** result) override
    {
        if (result == nullptr) return E_POINTER;
        *result = nullptr;
        if (iid != IID_IUnknown && iid != IID_IClassFactory) return E_NOINTERFACE;
        *result = static_cast<IClassFactory*>(this);
        AddRef();
        return S_OK;
    }
    ULONG STDMETHODCALLTYPE AddRef() override { return ++references_; }
    ULONG STDMETHODCALLTYPE Release() override
    {
        const ULONG remaining = --references_;
        if (remaining == 0) delete this;
        return remaining;
    }
    HRESULT STDMETHODCALLTYPE CreateInstance(IUnknown* outer, REFIID iid, void** result) override
    {
        if (outer != nullptr) return CLASS_E_NOAGGREGATION;
        auto* apo = new (std::nothrow) SonicApo();
        if (apo == nullptr) return E_OUTOFMEMORY;
        const HRESULT created = apo->QueryInterface(iid, result);
        apo->Release();
        return created;
    }
    HRESULT STDMETHODCALLTYPE LockServer(BOOL lock) override
    {
        g_objects += lock ? 1 : -1;
        return S_OK;
    }
private:
    std::atomic<ULONG> references_{ 1 };
};

extern "C" BOOL WINAPI DllMain(HINSTANCE, DWORD, void*) { return TRUE; }

extern "C" HRESULT __stdcall DllGetClassObject(REFCLSID clsid, REFIID iid, void** result)
{
    if (clsid != CLSID_SonicShieldingApo) return CLASS_E_CLASSNOTAVAILABLE;
    auto* factory = new (std::nothrow) SonicClassFactory();
    if (factory == nullptr) return E_OUTOFMEMORY;
    const HRESULT found = factory->QueryInterface(iid, result);
    factory->Release();
    return found;
}

extern "C" HRESULT __stdcall DllCanUnloadNow()
{
    return g_objects.load() == 0 ? S_OK : S_FALSE;
}
